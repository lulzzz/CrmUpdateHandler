﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CrmUpdateHandler.Utility
{
    /// <summary>
    /// Encapsulates the functionalities required to raise events through the Event Grid in a mockable adapter class
    /// </summary>
    internal class EventGridAdapter
    {
        // Singleton instances - makes the Azure functions more scalable.
        private static readonly HttpClient newContactHttpClient;
        private static readonly HttpClient updatedContactHttpClient;
        static EventGridAdapter()
        {
            // See https://docs.microsoft.com/en-us/azure/architecture/antipatterns/improper-instantiation/
            // for an explanation as to why tis is better than 'using (var httplient = new HttpClient()) {}"
            newContactHttpClient = new HttpClient();
            updatedContactHttpClient = new HttpClient();

            // Set up the HTTP header required to invoke an EventGrid Topic - exactly once, for the lifetime of the Azure Function
            var newCrmContactTopicKey = Environment.GetEnvironmentVariable("NewCrmContactTopicKey", EnvironmentVariableTarget.Process);
            var updatedCrmContactTopicKey = Environment.GetEnvironmentVariable("UpdatedCrmContactTopicKey", EnvironmentVariableTarget.Process);

            newContactHttpClient.DefaultRequestHeaders.Add("aeg-sas-key", newCrmContactTopicKey);
            updatedContactHttpClient.DefaultRequestHeaders.Add("aeg-sas-key", updatedCrmContactTopicKey);

        }

        /// <summary>
        /// Pass updated contacts to the UpdatedCrmContact topic of the Event Grid
        /// </summary>
        /// <param name="updatedContacts"></param>
        internal static async Task<EventGridSubmissionResult> RaiseUpdatedContactEventsAsync(List<UpdatedContactEvent> updatedContacts)
        {
            EventGridSubmissionResult retval = null;

            if (updatedContacts.Count > 0)
            {
                //log.LogInformation("We got {0} updated contacts to pass to the event grid", updatedContacts.Count);
                var eventGridTopicEndpoint = Environment.GetEnvironmentVariable("UpdatedCrmContactTopicEndpoint", EnvironmentVariableTarget.Process);

                //log.LogInformation(eventGridTopicEndpoint);
                //log.LogInformation("Contact {0} ({1} {2}) updated {3}", updatedContacts[0].data.contactId, updatedContacts[0].data.firstName, updatedContacts[0].data.lastName, updatedContacts[0].eventType);
                //log.LogInformation(JsonConvert.SerializeObject(updatedContacts));

                var response = await updatedContactHttpClient.PostAsJsonAsync<List<UpdatedContactEvent>>(eventGridTopicEndpoint, updatedContacts);
                //log.LogInformation(eventGridTopicEndpoint);
                retval = new EventGridSubmissionResult(response);
                
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    //log.LogError("Error: HTTP {0} {1} ", (int)response.StatusCode, response.StatusCode);
                    retval.ErrorMessage = await response.Content.ReadAsStringAsync();
                    //log.LogInformation(resultText);
                }
            }

            return retval;
        }

        /// <summary>
        /// Pass new contacts to the NewCrmContact topic of the Event Grid
        /// </summary>
        /// <param name="newContacts"></param>
        internal static async Task<EventGridSubmissionResult> RaiseNewContactEventsAsync(List<NewContactEvent> newContacts)
        {
            EventGridSubmissionResult retval = null;

            if (newContacts.Count > 0)
            {
                //log.LogInformation("We got {0} new contacts to pass to the event grid", newContacts.Count);
                var eventGridTopicEndpoint = Environment.GetEnvironmentVariable("NewCrmContactTopicEndpoint", EnvironmentVariableTarget.Process);

                //log.LogInformation(eventGridTopicEndpoint);
                //log.LogInformation("Sending contact {0}: {1} {2}", newContacts[0].data.contactId, newContacts[0].data.firstName, newContacts[0].data.lastName);
                //log.LogInformation(JsonConvert.SerializeObject(newContacts));

                var response = await newContactHttpClient.PostAsJsonAsync<List<NewContactEvent>>(eventGridTopicEndpoint, newContacts);
                retval = new EventGridSubmissionResult(response);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    //log.LogError("Error: HTTP {0} {1} ", (int)response.StatusCode, response.StatusCode);
                    retval.ErrorMessage = await response.Content.ReadAsStringAsync();
                    //log.LogInformation(resultText);
                }
            }

            return retval;
        }
    }
}
