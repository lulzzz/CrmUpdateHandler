using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net;
using System.Collections.Generic;

namespace CrmUpdateHandler
{
    /*
     * HAIL, PERSON FROM THE FUTURE
     * 
     * This is some of the first code written for Starling Energy in its earliest days. There is no budget, no time. So whatever
     * tests are here are urgent necessities, whatever unchecked variables and stub functions you find are just technical debt
     * deliberately and (mostly) consciously incurred to get functionality out the door. There were no resources to improve test
     * coverage, set up automated deployment, etc. So please be forgiving and leave it better than you found it.
     * 
     * Mike Wiese, April 2019
     * 
     */


    /// <summary>
    /// This is the handler for the Hubspot webhooks raised whenever a new Contact is created or changed.
    /// It gathers all the details for the new or updated contact(s) and raises an event to EventGrid from
    /// where multiple parties can subscribe to it.
    /// </summary>
    /// <remarks>Sign-of-life test: https://crmupdatehandler.azurewebsites.net/api/HandleAnyContactEvent?hello=there
    /// </remarks>
    public static class HandleAnyContactEvent
    {
        // Singleton instances - makes the Azure functions more scalable.
        private static readonly HttpClient newContactHttpClient;
        private static readonly HttpClient updatedContactHttpClient;
        private static readonly HttpClient hubspotHttpClient;

        static HandleAnyContactEvent()
        {
            // See https://docs.microsoft.com/en-us/azure/architecture/antipatterns/improper-instantiation/
            // for an explanation as to why tis is better than 'using (var httplient = new HttpClient()) {}"
            newContactHttpClient = new HttpClient();
            updatedContactHttpClient = new HttpClient();
            hubspotHttpClient = new HttpClient();

            // Set up the header required to invoke an EventGrid Topic - exactly once, for the lifetime of the Azure Function
            var newCrmContactTopicKey = Environment.GetEnvironmentVariable("NewCrmContactTopicKey", EnvironmentVariableTarget.Process);
            var updatedCrmContactTopicKey = Environment.GetEnvironmentVariable("UpdatedCrmContactTopicKey", EnvironmentVariableTarget.Process);

            newContactHttpClient.DefaultRequestHeaders.Add("aeg-sas-key", newCrmContactTopicKey);
            updatedContactHttpClient.DefaultRequestHeaders.Add("aeg-sas-key", updatedCrmContactTopicKey);

        }


        /// <summary>
        /// The entry point into the function
        /// </summary>
        /// <param name="req"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        [FunctionName("HandleAnyContactEvent")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("HandleAnyContactEvent trigger by HTTP");

            // Was this a sign-of-life request? (i.e. somebody just passing the 'hello' param to see if the function is available)
            string hello = req.Query["hello"];
            if (!string.IsNullOrEmpty(hello))
            {
                // We got a sign-of-life request. Just echo the hello string and exit
                return new OkObjectResult(hello);
            }

            // Parse the request body as JSON. It should be an array containing one or more contact-related events
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            // Comment in to debug
            //log.LogInformation("requestBody");
            //log.LogInformation(requestBody);


            // TODO: Verify that the request really came from Hubspot
            if (!HubspotRequestValidator.Validate(req, requestBody))
            {
                return new UnauthorizedResult();
            }


            dynamic contactEvents = JsonConvert.DeserializeObject(requestBody);

            // If body is empty or not JSON, just return 
            if (contactEvents == null)
            {
                log.LogWarning("Contact information was empty or not JSON");
                return new OkResult();
            }

            // If JSON in the request body is not an array, just return
            var gotExpectedType = contactEvents is Newtonsoft.Json.Linq.JArray;
            if (!gotExpectedType)
            {
                log.LogWarning("Contact information not an array of JSON objects");
                return new OkResult();
            }

            // Reference info: When the phone number for contact 451 was changed, we got:
            //[
            //{ "objectId":451,
            //"propertyName":"phone",
            //"propertyValue":"12345",
            //"changeSource":"CRM_UI",
            //"eventId":1040856604,
            //"subscriptionId":111557,
            //"portalId":5618470,       <- that's Starling
            //"appId":191749,
            //"occurredAt":1554383495704,
            //"subscriptionType":"contact.propertyChange",
            //"attemptNumber":0}
            //]

            Microsoft.Extensions.Primitives.StringValues validationHeader;
            if (req.Headers.TryGetValue("X-HubSpot-Signature", out validationHeader))
            {
                log.LogInformation("validationHeader: {0}", validationHeader);
            }
            else
            {
                log.LogWarning("X-HubSpot-Signature not present");
            }


            var hapikey = Environment.GetEnvironmentVariable("hapikey", EnvironmentVariableTarget.Process);

            // Declare a couple of lists to contain the actual objects that we will pass to Event Grid
            var newContacts = new List<NewContactEvent>();
            var updatedContacts = new List<UpdatedContactEvent>();

            // Now we have to reach back into Hubspot to retrieve the rest of the Contact details
            try
            {
                // When a new contact is created in Hubspot, we might get many events - usually a bunch of property-change events
                // followed by a contract-creation event. To avoid duplicate notifications, we need to filter out any "update" 
                // events that are rendered redundant by the presence of a "new" event
                var curatedEvents = new List<CuratedHubspotEvent>();
                var newContactIds = new List<string>();

                foreach (var contactEvent in contactEvents)
                {
                    string objectId = contactEvent?.objectId;
                    string eventId = contactEvent?.eventId;
                    string propertyName = contactEvent?.propertyName;
                    string propertyValue = contactEvent?.propertyValue;
                    string subscriptionType = contactEvent?.subscriptionType;
                    string attemptNumber = contactEvent?.attemptNumber;
                    log.LogInformation("Attempt number {0}", attemptNumber);

                    switch (subscriptionType)
                    {
                        case "contact.creation":
                            newContactIds.Add(objectId);
                            curatedEvents.Add(new CuratedHubspotEvent(objectId, eventId));
                            break;
                        case "contact.propertyChange":
                            curatedEvents.Add(new CuratedHubspotEvent(objectId, eventId, propertyName, propertyValue));
                            break;
                        default:
                            break;
                    }
                }

                foreach (var newId in newContactIds)
                {
                    curatedEvents.RemoveAll(c => c.Vid == newId && c.IsNew == false);
                }

                // Now we've tidied things up and the contacts are now unique, we can reach back into HubSpot to fetch the details of the contacts
                // TODO: Look into sharing a static HttpClient instance across all functions, as per https://docs.microsoft.com/en-us/azure/azure-functions/manage-connections
                // The below is an antipattern, it can be fixed by making HttpClient a singleton as per https://docs.microsoft.com/en-us/azure/architecture/antipatterns/improper-instantiation/
                foreach (var contactEvent in curatedEvents)
                {
                    string objectId = contactEvent.Vid;

                    // GET /contacts/v1/contact/vid/:vid/profile
                    var url = string.Format($"https://api.hubapi.com/contacts/v1/contact/vid/{objectId}/profile?hapikey={hapikey}");
                    log.LogInformation("url: {0}", url);

                    NewContactEvent newContactEvent = null;
                    UpdatedContactEvent updatedContactEvent = null;

                    // Go get the contact from HubSpot
                    HttpResponseMessage response = await hubspotHttpClient.GetAsync(url);
                    HttpContent content = response.Content;

                    log.LogInformation("Response StatusCode from contact retrieval: " + (int)response.StatusCode);

                    // Check Status Code. If we got the Contact OK, then raise the appropriate event.
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        // ... Read the string.
                        string resultText = await content.ReadAsStringAsync();
                        //log.LogInformation(resultText);

                        if (contactEvent.IsNew)
                        {
                            // Extract some details of the contact, to send to Event Grid
                            newContactEvent = ConvertJsonToNewContactInfo(resultText, contactEvent.EventId);
                            newContacts.Add(newContactEvent);
                        }
                        else
                        {
                            updatedContactEvent = ConvertJsonToUpdatedContactInfo(resultText, contactEvent.EventId, contactEvent.PropertyName, contactEvent.PropertyValue);
                            updatedContacts.Add(updatedContactEvent);
                        }
                    }
                    else
                    {
                        log.LogError("Error: HTTP {0} {1} ", (int)response.StatusCode, response.StatusCode);
                        log.LogError("Contact ID: {0} ", objectId);
                        string resultText = await content.ReadAsStringAsync();
                        log.LogInformation(resultText);
                        log.LogInformation("Original Request Body:");
                        log.LogInformation(requestBody);
                    }
                }



                // With the filtering done, now we raise separate events for each new and updated contact.
                // See https://docs.microsoft.com/en-us/azure/event-grid/post-to-custom-topic
                // and https://docs.microsoft.com/en-us/azure/event-grid/monitor-event-delivery

                // Pass updated contacts to the UpdatedCrmContact topic of the Event Grid
                if (updatedContacts.Count > 0)
                {
                    log.LogInformation("We got {0} updated contacts to pass to the event grid", updatedContacts.Count);
                    var eventGridTopicEndpoint = Environment.GetEnvironmentVariable("UpdatedCrmContactTopicEndpoint", EnvironmentVariableTarget.Process);

                    log.LogInformation(eventGridTopicEndpoint);
                    log.LogInformation("Contact {0} ({1} {2}) updated {3}", updatedContacts[0].data.contactId, updatedContacts[0].data.firstName, updatedContacts[0].data.lastName, updatedContacts[0].eventType);
                    log.LogInformation(JsonConvert.SerializeObject(updatedContacts));

                    var response = await updatedContactHttpClient.PostAsJsonAsync<List<UpdatedContactEvent>>(eventGridTopicEndpoint, updatedContacts);
                    HttpContent content = response.Content;
                    log.LogInformation(eventGridTopicEndpoint);

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        log.LogError("Error: HTTP {0} {1} ", (int)response.StatusCode, response.StatusCode);
                        string resultText = await content.ReadAsStringAsync();
                        log.LogInformation(resultText);
                    }
                }                
                
                // Pass new contacts to the NewCrmContact topic of the Event Grid
                if (newContacts.Count > 0)
                {
                    log.LogInformation("We got {0} new contacts to pass to the event grid", newContacts.Count);
                    var eventGridTopicEndpoint = Environment.GetEnvironmentVariable("NewCrmContactTopicEndpoint", EnvironmentVariableTarget.Process);

                    log.LogInformation(eventGridTopicEndpoint);
                    log.LogInformation("Sending contact {0}: {1} {2}", newContacts[0].data.contactId, newContacts[0].data.firstName, newContacts[0].data.lastName);
                    log.LogInformation(JsonConvert.SerializeObject(newContacts));

                    var response = await newContactHttpClient.PostAsJsonAsync<List<NewContactEvent>>(eventGridTopicEndpoint, newContacts);
                    HttpContent content = response.Content;
                    
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        log.LogError("Error: HTTP {0} {1} ", (int)response.StatusCode, response.StatusCode);
                        string resultText = await content.ReadAsStringAsync();
                        log.LogInformation(resultText);
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogError("Request failed: {0}", ex.Message);
                return new StatusCodeResult(500);
            }
            
            return new OkResult();
        }

        /// <summary>
        /// We get a complex structure back from Hubspot when we retrieve a Contact. We 
        /// only need a bit of it.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="eventId"></param>
        /// <returns></returns>
        public static NewContactEvent ConvertJsonToNewContactInfo(string text, string eventId)
        {
            dynamic contact = ConvertStringToJson(text);

            var firstName = contact.properties.firstname?.value;
            var lastName = contact.properties.lastname?.value;
            var phone = contact.properties.phone?.value;
            var email = contact.properties.email?.value;
            var customerNameOnBill = contact.properties.customer_name_on_bill?.value;
            var meterNumber = contact.properties.meter_number?.value;
            var synergyAccountNumber = contact.properties.synergy_account_no?.value;
            var supplyAddress = contact.properties.supply_address?.value;
            var jobTitle = contact.properties.jobtitle?.value;
            var restUri = contact["profile-url"].ToString();

            var newContactPayload = new NewContactPayload(Convert.ToString(contact.vid))
            {
                firstName = firstName,
                lastName = lastName,
                phone = phone,
                email = email,
                customerNameOnBill = customerNameOnBill,
                restUri = restUri
            };

            var retval = new NewContactEvent
            {
                id = eventId,
                eventType = "NewContact",
                subject = "Starling.Crm.ContactCreated",
                eventTime = DateTime.UtcNow,
                data = newContactPayload
            };

            return retval;
        }

        /// <summary>
        /// Convert the Contact structure that we get from Hubspot into the format that we use for Event Grid
        /// </summary>
        /// <param name="wholeContactText"></param>
        /// <param name="eventId"></param>
        /// <returns></returns>
        public static UpdatedContactEvent ConvertJsonToUpdatedContactInfo(string wholeContactText, string eventId, string propertyName, string newValue)
        {
            dynamic contact = ConvertStringToJson(wholeContactText);

            var firstName = contact.properties.firstname?.value;
            var lastName = contact.properties.lastname?.value;
            var phone = contact.properties.phone?.value;
            var email = contact.properties.email?.value;
            var customerNameOnBill = contact.properties.customer_name_on_bill?.value;
            var meterNumber = contact.properties.meter_number?.value;
            var synergyAccountNumber = contact.properties.synergy_account_no?.value;
            var supplyAddress = contact.properties.supply_address?.value;
            var jobTitle = contact.properties.jobtitle?.value;
            var restUri = contact["profile-url"].ToString();

            var oldFirstName = firstName;
            var oldLastName = lastName;
            var oldPhone = phone;
            var oldEmail = email;
            var oldCustomerNameOnBill = customerNameOnBill;
            var oldMeterNumber = meterNumber;
            var oldSynergyAccountNumber = contact.properties.synergy_account_no?.versions[0].value;
            var oldSupplyAddress = contact.properties.supply_address?.versions[0].value;
            var oldJobTitle = contact.properties.jobtitle?.versions[0].value;

            if (string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            // The Event Type can be used in the Azure Subscription-configuration UI to trigger only on certain types of changes
            string eventTypeName = "Other";
            switch (propertyName)
            {
                case "firstname":
                    oldFirstName = GetPreviousValue(contact.properties.firstname);
                    eventTypeName = "Name";
                    break;
                case "lastname":
                    oldLastName = GetPreviousValue(contact.properties.lastname);
                    eventTypeName = "Name";
                    break;
                case "phone":
                    eventTypeName = "Phone";
                    oldPhone = GetPreviousValue(contact.properties.phone);
                    break;
                case "email":
                    eventTypeName = "Email";
                    oldEmail = GetPreviousValue(contact.properties.email);
                    break;
                case "customer_name_on_bill":
                    eventTypeName = "CustomerNameOnBill";
                    oldCustomerNameOnBill = GetPreviousValue(contact.properties.customer_name_on_bill);
                    break;
                case "meter_number":
                    eventTypeName = "MeterNumber";
                    oldMeterNumber = GetPreviousValue(contact.properties.meter_number);
                    break;
                case "synergy_account_no":
                    eventTypeName = "SynergyAccountNo";
                    oldSynergyAccountNumber = GetPreviousValue(contact.properties.synergy_account_no);
                    break;
                case "supply_address":
                    eventTypeName = "SupplyAddress";
                    oldSupplyAddress = GetPreviousValue(contact.properties.supply_address);
                    break;
                case "jobtitle":
                    eventTypeName = "JobTitle";
                    oldJobTitle = GetPreviousValue(contact.properties.jobtitle);
                    break;
                default:
                    break;
            }

            var updatedContactPayload = new UpdatedContactPayload(Convert.ToString(contact.vid))
            {
                firstName = firstName,
                oldFirstName = oldFirstName,

                lastName = lastName,
                oldLastName = oldLastName,

                phone = phone,
                oldPhone = oldPhone,

                email = email,
                oldEmail = oldEmail,

                customerNameOnBill = customerNameOnBill,
                oldCustomerNameOnBill = oldCustomerNameOnBill,

                meterNumber = meterNumber,
                oldMeterNumber = oldMeterNumber,

                synergyAccountNumber = synergyAccountNumber,
                oldSynergyAccountNumber = oldSynergyAccountNumber,

                supplyAddress = supplyAddress,
                oldSupplyAddress = oldSupplyAddress,

                jobTitle = jobTitle,
                oldJobTitle = oldJobTitle,

                restUri = restUri
            };


            var retval = new UpdatedContactEvent
            {
                id = eventId,
                eventType = eventTypeName,
                subject = "Starling.Crm.ContactUpdated",
                eventTime = DateTime.UtcNow,
                data = updatedContactPayload
            };

            return retval;
        }


        internal static dynamic ConvertStringToJson(string text)
        {
            dynamic json = JsonConvert.DeserializeObject(text);
            return json;
        }

        internal static string GetPreviousValue(dynamic prop)
        {
            var versions = prop?.versions;

            if (versions == null)
                return string.Empty;

            if (versions.Count < 2)
                return string.Empty;

            return versions[1]?.value;
        }
    }
}
