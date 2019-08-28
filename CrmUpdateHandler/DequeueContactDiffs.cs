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

            // Typical 'diff' packet coming into us here. Note that the names are friendly names, so we have to resolve them
            // to internal names (this forces us to check the input too, which is an extra security hurdle for the bad guys)
            //
            // {
            //   "crmid": "012345",
            //   "changes":[
            //     {  
            //       "name":"First",
            //       "value":"Bill"
            //     },
            //     {  
            //       "name":"Last",
            //       "value":"McPherson"
            //     }
            //   ]
            // }

            string where = string.Empty;
            try
            {
                where = "deserialising message text";
                dynamic userdata = JsonConvert.DeserializeObject(diffJson);

                where = "accessing crmid";
                string crmid = userdata.crmid;

                if (string.IsNullOrEmpty(crmid))
                {
                    throw new CrmUpdateHandlerException("crmid not found in message");
                }

                where = "accessing changes";
                if (userdata.changes == null)
                {
                    log.LogWarning("No 'changes' found");
                    return;
                }

                where = "testing changes datatype";
                if (!(userdata.changes is JArray))
                {
                    throw new CrmUpdateHandlerException("'changes' property was not an array");
                }

                where = "extracting changes";
                var props = new HubSpotContactProperties();
                foreach (dynamic change in userdata.changes)
                {
                    string displayName = change.name;   // convert to string
                    string value = change.value;   // convert to string
                    string internalPropertyName = ResolveFriendlyNameToHubspotPropertyName(displayName);
                    if (string.IsNullOrEmpty(internalPropertyName))
                    {
                        //log.LogWarning($"Could not resolve '{displayName}' to a hubspot property name");
                    }
                    else
                    {
                        props.Add(internalPropertyName, value);
                    }
                }

                if (props.properties.Count == 0)
                {
                    // nothing to do
                    return;
                }

                where = "updating contact details";
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

        /// <summary>
        /// Returns the internal HubSpot property name for a given friendly name.
        /// </summary>
        /// <param name="displayName"></param>
        /// <returns></returns>
        private string ResolveFriendlyNameToHubspotPropertyName(string displayName)
        {
            switch(displayName.ToLower())
            {
                case "first":
                case "first name":
                    return "firstname";
                case "last":
                case "last name":
                    return "lastname";
                case "salutation":
                    return "salutation";
                case "lead status":
                    return "hs_lead_status";
                case "address":
                case "street address":
                    return "address";
                case "city":
                    return "city";
                case "state":
                    return "state";
                case "postcode":
                case "post code":
                case "zip":
                case "zip code":
                    return "zip";
                case "country":
                    return "country";
                case "email":
                    return "email";
                case "phone":
                case "phone number":
                    return "phone";
                case "mobile":
                case "mobile phone":
                case "mobile number":
                    return "mobilephone";
                case "installationrecordexists":
                    return "installationrecordexists";
                default:
                    return null;
            }
        }
    }
}
