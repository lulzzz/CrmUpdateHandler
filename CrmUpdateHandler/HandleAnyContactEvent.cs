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
using CrmUpdateHandler.Utility;

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

            // Was this a sign-of-life request? (i.e. somebody just passing the 'hello' param to see if the function is deployed and available)
            string hello = req.Query["hello"];
            if (!string.IsNullOrEmpty(hello))
            {
                log.LogInformation("HandleAnyContactEvent - sign-of-life check");
                // We got a sign-of-life request. Just echo the hello string and exit
                return new OkObjectResult(hello);
            }

            log.LogInformation("HandleAnyContactEvent trigger by HTTP");

            // Parse the request body as JSON. It should be an array containing one or more contact-related events from Hubspot
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
                    log.LogInformation("Attempt number {0} for contact {1}: {2}", attemptNumber, objectId, subscriptionType);

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
                foreach (var contactEvent in curatedEvents)
                {
                    string objectId = contactEvent.Vid;

                    var contactResult = await HubspotAdapter.RetrieveHubspotContactById(objectId, fetchPreviousValues: true);

                    NewContactEvent newContactEvent = null;
                    UpdatedContactEvent updatedContactEvent = null;

                    log.LogInformation("Response StatusCode from contact retrieval: " + (int)contactResult.StatusCode);

                    // Check Status Code. If we got the Contact OK, then raise the appropriate event.
                    if (contactResult.StatusCode == HttpStatusCode.OK)
                    {
                        //log.LogInformation(resultText);

                        if (contactEvent.IsNew)
                        {
                            // Extract some details of the contact, to send to Event Grid
                            newContactEvent = new NewContactEvent(contactEvent.EventId, contactResult.Payload);
                            newContacts.Add(newContactEvent);
                            log.LogInformation("New Contact: " + contactResult.Payload.email);
                        }
                        else
                        {
                            updatedContactEvent = new UpdatedContactEvent(contactEvent.EventId, contactEvent.PropertyName, contactResult.Payload);

                            updatedContacts.Add(updatedContactEvent);
                            log.LogInformation("Updating " + contactEvent.PropertyName + " for " + contactResult.Payload.email);

                        }
                    }
                    else
                    {
                        log.LogError("Error: HTTP {0} {1} ", (int)contactResult.StatusCode, contactResult.StatusCode);
                        log.LogError("Contact ID: {0} ", objectId);
                        log.LogInformation(contactResult.ErrorMessage);
                        log.LogInformation("Original Request Body:");
                        log.LogInformation(requestBody);
                    }
                }


                // With the filtering done, now we raise separate events for each new and updated contact.
                // See https://docs.microsoft.com/en-us/azure/event-grid/post-to-custom-topic
                // and https://docs.microsoft.com/en-us/azure/event-grid/monitor-event-delivery

                await EventGridAdapter.RaiseUpdatedContactEventsAsync(updatedContacts);

                await EventGridAdapter.RaiseNewContactEventsAsync(newContacts);

                
            }
            catch (Exception ex)
            {
                log.LogError("Request failed: {0}", ex.Message);
                return new StatusCodeResult(500);
            }
            
            return new OkResult();
        }
    }
}
