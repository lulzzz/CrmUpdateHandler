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
    /// <summary>
    /// This is the handler for the Hubspot webhooks raised whenever a new Contact is created or changed.
    /// It gathers all the details for the new or updated contact(s) and raises an event to EventGrid from
    /// where multiple parties can subscribe to it.
    /// </summary>
    /// <remarks>Sign-of-life test: https://crmupdatehandler.azurewebsites.net/api/HandleAnyContactEvent?hello=there
    /// </remarks>
    public static class HandleAnyContactEvent
    {

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

            var validationHeader = req.Headers["X-HubSpot-Signature"];
            log.LogInformation("validationHeader: {0}", validationHeader);

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
                    string subscriptionType = contactEvent?.subscriptionType;

                    switch (subscriptionType)
                    {
                        case "contact.creation":
                            newContactIds.Add(objectId);
                            curatedEvents.Add(new CuratedHubspotEvent(objectId, eventId, true));
                            break;
                        case "contact.propertyChange":
                            curatedEvents.Add(new CuratedHubspotEvent(objectId, eventId, false));
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
                using (var client = new HttpClient())
                {
                    foreach (var contactEvent in curatedEvents)
                    {
                        string objectId = contactEvent.Vid;

                        // GET /contacts/v1/contact/vid/:vid/profile
                        var url = string.Format($"https://api.hubapi.com/contacts/v1/contact/vid/{objectId}/profile?hapikey={hapikey}");
                        log.LogInformation("url: {0}", url);

                        NewContactEvent newContactEvent = null;
                        UpdatedContactEvent updatedContactEvent = null;

                        // Go get the contact from HubSpot, including all line items
                        HttpResponseMessage response = await client.GetAsync(url);
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
                            } else
                            {
                                updatedContactEvent = ConvertJsonToUpdatedContactInfo(resultText, contactEvent.EventId);
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
                }


                // With the filtering done, now we raise separate events for each new and updated contact.
                // See https://docs.microsoft.com/en-us/azure/event-grid/post-to-custom-topic
                // and https://docs.microsoft.com/en-us/azure/event-grid/monitor-event-delivery

                // Pass updated contacts to the UpdatedCrmContact topic of the Event Grid
                if (updatedContacts.Count > 0)
                {
                    log.LogInformation("We got {0} updated contact to pass to the event grid", updatedContacts.Count);
                    var eventGridHeaderValue = Environment.GetEnvironmentVariable("UpdatedCrmContactNameTopicKey", EnvironmentVariableTarget.Process);
                    var eventGridTopicEndpoint = Environment.GetEnvironmentVariable("UpdatedCrmContactNameTopicEndpoint", EnvironmentVariableTarget.Process);

                    using (var eventGridclient = new HttpClient())
                    {
                        eventGridclient.DefaultRequestHeaders.Add("aeg-sas-key", eventGridHeaderValue);
                        var response = await eventGridclient.PostAsJsonAsync<List<UpdatedContactEvent>>(eventGridTopicEndpoint, updatedContacts);
                        HttpContent content = response.Content;

                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            log.LogError("Error: HTTP {0} {1} ", (int)response.StatusCode, response.StatusCode);
                            string resultText = await content.ReadAsStringAsync();
                            log.LogInformation(resultText);
                        }
                    }
                }                
                
                // Pass new contacts to the NewCrmContact topic of the Event Grid
                if (newContacts.Count > 0)
                {
                    log.LogInformation("We got {0} new contacts to pass to the event grid", newContacts.Count);
                    var eventGridHeaderValue = Environment.GetEnvironmentVariable("NewCrmContactTopicKey", EnvironmentVariableTarget.Process);
                    var eventGridTopicEndpoint = Environment.GetEnvironmentVariable("NewCrmContactNameTopicEndpoint", EnvironmentVariableTarget.Process);

                    using (var eventGridclient = new HttpClient())
                    {
                        eventGridclient.DefaultRequestHeaders.Add("aeg-sas-key", eventGridHeaderValue);
                        var response = await eventGridclient.PostAsJsonAsync<List<NewContactEvent>>(eventGridTopicEndpoint, newContacts);
                        HttpContent content = response.Content;
                        string debugText = await content.ReadAsStringAsync();
                        log.LogInformation(eventGridTopicEndpoint);
                        log.LogInformation(debugText);

                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            log.LogError("Error: HTTP {0} {1} ", (int)response.StatusCode, response.StatusCode);
                            string resultText = await content.ReadAsStringAsync();
                            log.LogInformation(resultText);
                        }
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
            var newContactPayload = new NewContactPayload
            {
                
                contactId = contact.vid,
                firstName = contact.properties.firstname.value,
                lastName = contact.properties.lastname.value,
                phone = contact.properties.phone.value,
                email = contact.properties.email.value,
                customerNameOnBill = contact.properties.customer_name_on_bill.value,
                restUri = contact["profile-url"].ToString()
            };

            var retval = new NewContactEvent
            {
                id = eventId,
                eventType = "Starling.Crm.ContactCreated",
                subject = "Contacts",
                eventTime = DateTime.UtcNow,
                data = newContactPayload
            };

            return retval;
        }

        /// <summary>
        /// Convert the Contact structure that we get from Hubspot into the format that we use for Event Grid
        /// </summary>
        /// <param name="text"></param>
        /// <param name="eventId"></param>
        /// <returns></returns>
        public static UpdatedContactEvent ConvertJsonToUpdatedContactInfo(string text, string eventId)
        {
            dynamic contact = ConvertStringToJson(text);
            var updatedContactPayload = new UpdatedContactPayload
            {
                contactId = contact.vid,
                firstName = contact.properties.firstname.value,
                lastName = contact.properties.lastname.value,
                phone = contact.properties.phone.value,
                email = contact.properties.email.value,
                customerNameOnBill = contact.properties.customer_name_on_bill.value,
                restUri = contact["profile-url"].ToString()
            };


            var retval = new UpdatedContactEvent
            {
                id = eventId,
                eventType = "Starling.Crm.ContactUpdated",
                subject = "Contacts",
                eventTime = DateTime.UtcNow,
                data = updatedContactPayload
            };

            return retval;
        }


        public static dynamic ConvertStringToJson(string text)
        {
            dynamic orderJson = JsonConvert.DeserializeObject(text);
            return orderJson;
        }
    }
}
