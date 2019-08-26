using System;
using System.Threading.Tasks;
using CrmUpdateHandler.Utility;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CrmUpdateHandler
{
    public class DequeueContactDiffs
    {
        private IHubSpotAdapter _hubSpotAdapter;

        /// <summary>
        /// Constructor is an entry point for the dependency-injection defined in Startup.cs
        /// </summary>
        /// <param name="hubSpotAdapter"></param>
        public DequeueContactDiffs(IHubSpotAdapter hubSpotAdapter)
        {
            this._hubSpotAdapter = hubSpotAdapter;
        }

        /// <summary>
        /// Takes a description of diffs to a contact and effects them against the API
        /// </summary>
        /// <param name="diffJson"></param>
        /// <param name="log"></param>
        [StorageAccount("AzureWebJobsStorage")]
        [FunctionName("DequeueContactDiffs")]
        public async Task Run([QueueTrigger("hubspot-contacts-needing-updates")]string diffJson,
            [Queue("error-notification")] IAsyncCollector<string> errors,
            ILogger log)
        {
            log.LogInformation($"DequeueContactDiffs processed: {diffJson}");

            // Instantiate our convenient wrapper for the error-log queue
            var errQ = new ErrorQueueLogger(errors, "CrmUpdateHandler", nameof(DequeueContactDiffs));

            // {
            //   "crmid": "012345",
            //   "changes":[
            //     {  
            //       "name":"prop1",
            //       "value":"val1"
            //     },
            //     {  
            //       "name":"prop2",
            //       "value":"val2"
            //     }
            //   ]
            // }

            string where = string.Empty;
            try
            {
                where = "deserialising message text";
                dynamic userdata = JsonConvert.DeserializeObject(diffJson);

                string crmid = userdata.crmid;

                if (string.IsNullOrEmpty(crmid))
                {
                    throw new CrmUpdateHandlerException("crmid not found in message");
                }

                if (userdata.changes == null)
                {
                    log.LogWarning("No 'changes' found");
                    return;
                }

                if (!(userdata.changes is JArray))
                {
                    throw new CrmUpdateHandlerException("'changes' property was not an array");
                }

                var props = new HubSpotContactProperties();
                foreach (dynamic change in userdata.changes)
                {
                    props.Add(change.name, change.value);
                }

                if (props.properties.Count == 0)
                {
                    // nothing to do
                    return;
                }

                var crmAccessResult = await _hubSpotAdapter.UpdateContactDetailsAsync(crmid, props, log, isTest: false);

                if (crmAccessResult.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    log.LogError($"Error {crmAccessResult.StatusCode} updating HubSpot contact {crmid}: {crmAccessResult.ErrorMessage}");
                    errQ.LogError("Error " + crmAccessResult.StatusCode + " updating HubSpot contact {crmid}: " + crmAccessResult.ErrorMessage);
                }
            }
            catch (Exception ex)    
            {
                errQ.LogError($"Exception {where}: {ex.Message}");
            }
        }
    }
}
