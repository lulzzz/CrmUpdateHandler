using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using CrmUpdateHandler.Utility;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CrmUpdateHandler
{
    public static class DequeueAnyContactEvent
    {
        /// <summary>
        /// A Queue-triggered function that handles the raw change packets from HubSpot
        /// Error messages are sent to the 'error-notification' queue. This is created 
        /// on demand, if it doesn't exist already, in the storage resource specified by
        /// the AzureWebJobsStorage appSetting
        /// </summary>
        /// <param name="myQueueItem"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        /// <remarks>To prevent parallel operation, the batchSize is set to 1. We had to reluctantly do this,
        /// because with 16 (actually, batchSize + newBatchThreshold = 24) instances of this function were tripping
        /// HubSpot's "too many calls" alarm, and calls were failing during bulk imports.
        /// TODO: Get the queue names from appSettings</remarks>
        [FunctionName("DequeueAnyContactEvent")]
        public static async Task Run(
            [QueueTrigger("raw-hubspot-change-notifications", Connection = "AzureWebJobsStorage")]string requestBody,
            [Queue("error-notification", Connection = "AzureWebJobsStorage")] IAsyncCollector<string> errors,
            ILogger log)
        {
            log.LogInformation($"DequeueAnyContactEvent trigger function processed");
            var where = string.Empty;

            try
            {
                // deserialisation-as-JSON can throw an exception, which is why it's in a try..catch
                where = "deserialising request body";
                dynamic contactEvents = JsonConvert.DeserializeObject(requestBody);

                // If body is empty or not JSON, just return 
                if (contactEvents == null)
                {
                    log.LogWarning("Contact information was empty or not JSON");
                    await errors.AddAsync(nameof(DequeueAnyContactEvent) + ": Contact information was empty or not JSON");
                    return;
                }

                // If JSON in the request body is not an array, just return
                var gotExpectedType = contactEvents is Newtonsoft.Json.Linq.JArray;
                if (!gotExpectedType)
                {
                    log.LogWarning("Contact information not an array of JSON objects");
                    await errors.AddAsync(nameof(DequeueAnyContactEvent) + ": Contact information not an array of JSON objects");
                    return;
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

                // Declare a couple of lists to contain the actual objects that we will pass to Event Grid
                var newContacts = new List<NewContactEvent>();
                var updatedContacts = new List<UpdatedContactEvent>();

                // Now we have to reach back into Hubspot to retrieve the rest of the Contact details
                // When a new contact is created in Hubspot, we might get many events - usually a bunch of property-change events
                // followed by a contract-creation event. To avoid duplicate notifications, we need to filter out any "update" 
                // events that are rendered redundant by the presence of a "new" event
                var curatedEvents = new HashSet<CuratedHubspotEvent>(new CuratedHubspotEventComparer());
                var newContactIds = new List<string>();

                where = "accessing contact event properties";
                foreach (var contactEvent in contactEvents)
                {
                    string objectId = contactEvent?.objectId;
                    string eventId = contactEvent?.eventId;
                    string subscriptionType = contactEvent?.subscriptionType;
                    string attemptNumber = contactEvent?.attemptNumber;
                    string changePropertyName = contactEvent?.propertyName;

                    // Temporary hack till our Flow can update folder names for us
                    //  - didn't work well. On new contact, you get 4 update events + a create event, so the code below sent 4 emails
                    //if (changePropertyName == "firstname" || changePropertyName == "lastname")
                    //{
                    //    if (subscriptionType != "contact.creation")
                    //    {
                    //        await errors.AddAsync(nameof(DequeueAnyContactEvent) + ": Name change for contact " + objectId + ". Check the Houses folder");
                    //    }
                    //}

                    log.LogInformation("Attempt number {0} for contact {1}: {2}", attemptNumber, objectId, subscriptionType);

                    switch (subscriptionType)
                    {
                        case "contact.creation":
                            newContactIds.Add(objectId);
                            curatedEvents.Add(new CuratedHubspotEvent(objectId, eventId, isNew: true));
                            break;
                        case "contact.propertyChange":
                            curatedEvents.Add(new CuratedHubspotEvent(objectId, eventId, isNew: false));
                            break;
                        default:
                            log.LogWarning("Unexpected subscriptionType from HubSpot: " + subscriptionType);
                            await errors.AddAsync(nameof(DequeueAnyContactEvent) + ": Unexpected subscriptionType from HubSpot: " + subscriptionType);
                            break;
                    }
                }

                where = "removing update messages that duplicate new messages";
                foreach (var newId in newContactIds)
                {
                    curatedEvents.RemoveWhere(c => c.Vid == newId && c.IsNew == false);
                }

                // Now we've tidied things up and the events are now unique, we can reach back into HubSpot to fetch the details of the contacts
                foreach (var contactEvent in curatedEvents)
                {
                    string objectId = contactEvent.Vid;

                    where = "retrieving contact " + objectId;
                    var contactResult = await HubspotAdapter.RetrieveHubspotContactById(objectId, fetchPreviousValues: true, log: log);

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
                            log.LogInformation("New Contact: {0}", contactResult.Payload.email);
                        }
                        else
                        {
                            updatedContactEvent = new UpdatedContactEvent(contactEvent.EventId, contactResult.Payload);

                            updatedContacts.Add(updatedContactEvent);
                            log.LogInformation("Updating " + contactResult.Payload.email);

                        }
                    }
                    else
                    {
                        log.LogError("Error: HTTP {0} {1} ", (int)contactResult.StatusCode, contactResult.StatusCode);
                        log.LogError("Contact ID: {0} ", objectId);
                        log.LogInformation(contactResult.ErrorMessage);
                        log.LogInformation("Original Request Body:");
                        log.LogInformation(requestBody);

                        await errors.AddAsync(nameof(DequeueAnyContactEvent) + ": Failed to retrieve contact " + objectId + " from HubSpot. " + contactResult.ErrorMessage);
                    }
                }


                // With the filtering done, now we raise separate events for each new and updated contact.
                // See https://docs.microsoft.com/en-us/azure/event-grid/post-to-custom-topic
                // and https://docs.microsoft.com/en-us/azure/event-grid/monitor-event-delivery

                where = "raising UpdatedContact events";
                await EventGridAdapter.RaiseUpdatedContactEventsAsync(updatedContacts);

                where = "raising NewContact events";
                //log.LogInformation(JsonConvert.SerializeObject(newContacts));
                await EventGridAdapter.RaiseNewContactEventsAsync(newContacts);
            }
            catch (Exception ex)
            {
                log.LogError("Request failed: {0}", ex.Message);
                await errors.AddAsync(nameof(DequeueAnyContactEvent) + ": Request failed " + where + ": " + ex.Message);
                //return new StatusCodeResult(500);
            }

        }
    }
}
