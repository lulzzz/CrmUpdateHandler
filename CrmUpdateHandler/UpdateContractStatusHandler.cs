// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using CrmUpdateHandler.Utility;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace CrmUpdateHandler
{
    /// <summary>
    /// A class to hold functions that update the Contract Status in HubSpot, potentially via various entry points
    /// </summary>
    /// <see cref="https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-event-grid"/>
    public class UpdateContractStatusHandler
    {
        private IHubSpotAdapter _hubSpotAdapter;

        /// <summary>
        /// Constructor obtains a HubSpot Adaptor by dependency injection from the framework, or from a unit test.
        /// </summary>
        /// <param name="injectedHubSpotAdapter"></param>
        public UpdateContractStatusHandler(IHubSpotAdapter injectedHubSpotAdapter)
        {
            this._hubSpotAdapter = injectedHubSpotAdapter;
        }

        /// <summary>
        /// An Event-triggered function that subscribes to the OnContractStatusChanged topic. It updates the Contract Status fields in HubSpot for the given email
        /// </summary>
        /// <param name="eventGridEvent"></param>
        /// <param name="errors"></param>
        /// <param name="log"></param>
        /// <remarks>Event Grid always sends an array and may send more than one event in the array. 
        /// The runtime invokes this function once for each array element.
        /// To test from Postman, post the body to cref="http://localhost:7071/runtime/webhooks/eventgrid?functionName=UpdateContractStatusHandler"
        /// with 
        ///    Content-Type: application/json
        ///    aeg-event-type: Notification
        /// 
        /// To wire up as an event subscriber, set the webhook as
        /// https://CrmUpdateHandler.azurewebsites.net/runtime/webhooks/eventgrid?functionName=UpdateHubSpotContractStatus&code={systemkey}
        /// There was a host key named "eventgrid_extension" there, I used that. 
        /// </remarks>
        [StorageAccount("AzureWebJobsStorage")]
        [FunctionName("UpdateHubSpotContractStatus")]
        public async Task Run([EventGridTrigger]EventGridEvent eventGridEvent,
            [Queue("error-notification")] IAsyncCollector<string> errors,
            ILogger log)
        {
            log.LogInformation(eventGridEvent.Data.ToString());

            // Instantiate our convenient wrapper for the error-log queue
            var errQ = new ErrorQueueLogger(errors, "CrmUpdateHandler", nameof(UpdateContractStatusHandler));

            // The shape of the data looks much like the following
            // [
            // {
            //    "contractstate":"Sent",
            //    "eventtype":"ContractSent",
            //    "installationId":"124",
            //    "customeremail":"testy.webhookssen@ksc.net.au",
            //    "senddate":"2019-07-26T01:54:30.85Z",
            //    "signingdate":null,
            //    "rejectionreason":null
            // }
            // ]

            string where = string.Empty;
            try
            {
                where = "deserializing Event Grid notification structure";
                var contractStatusNotification = JsonConvert.DeserializeObject<CustomerContractData>(eventGridEvent.Data.ToString());

                if (contractStatusNotification == null)
                {
                    log.LogError("Event deserialisation error\n" + eventGridEvent.Data.ToString());
                    errQ.LogError("Event deserialisation error\n" + eventGridEvent.Data.ToString());
                    return;
                }

                // OK, we got enough info to proceed. Do some checks
                where = "checking Event Grid package for consistency";
                switch (contractStatusNotification.ContractState)
                {
                    case "Sent":
                        break;
                    case "Signed":
                        // If the contract was signed, the signing date must be present
                        if (!contractStatusNotification.SigningDate.HasValue)
                        {
                            log.LogError("Signing Date missing for installation " + contractStatusNotification.InstallationId);
                            errQ.LogError("Signing Date missing for installation " + contractStatusNotification.InstallationId);
                            return;
                        }
                        break;
                    case "Rejected":
                        // If the contract was rejected, the signing date must be present (in this case, it's the rejection date)
                        if (!contractStatusNotification.SigningDate.HasValue)
                        {
                            log.LogError("Signing Date missing for rejected installation " + contractStatusNotification.InstallationId);
                            errQ.LogError("Signing Date missing for rejected installation " + contractStatusNotification.InstallationId);
                            return;
                        }

                        // We can't force the user to fill out a rejection reason. But we'll log it
                        if (string.IsNullOrEmpty(contractStatusNotification.RejectionReason))
                        {
                            log.LogWarning("Rejection reason was missing for installation " + contractStatusNotification.InstallationId);
                        }
                        break;
                    case "ContractForwarded":
                        // TODO: Handle this
                        break;
                    default:
                        // If somebody introduces a new contract state, we'll know about it soon enough.
                        log.LogError("Unknown contract state: '" + contractStatusNotification.ContractState + "' for installation " + contractStatusNotification.InstallationId);
                        errQ.LogError("Unknown contract state: '" + contractStatusNotification.ContractState + "' for installation " + contractStatusNotification.InstallationId);
                        return;
                }

                // Now we can update the indicated installation in HubSpot
                where = "patching '" + contractStatusNotification.CustomerEmail + "' in hubspot";
                var contactUpdateResult = await _hubSpotAdapter.UpdateContractStatusAsync(contractStatusNotification.CustomerEmail, contractStatusNotification.ContractState, log, isTest: false);
                if (contactUpdateResult.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    log.LogError("Error updating contract state for '" + contractStatusNotification.CustomerEmail + "': " + contactUpdateResult.ErrorMessage);
                    errQ.LogError($"Error updating contract state for {contractStatusNotification.CustomerEmail}' (installation {contractStatusNotification.InstallationId}): {contactUpdateResult.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                log.LogError($"Exception {where}: {ex.Message}");
                errQ.LogError($"Exception {where}: {ex.Message}");
                return;
            }
        }
    }
}
