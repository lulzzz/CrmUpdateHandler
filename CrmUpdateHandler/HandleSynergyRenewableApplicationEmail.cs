using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using CrmUpdateHandler.Utility;
using System.Net;
using System.Collections.Generic;

namespace CrmUpdateHandler
{
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>This function now has no contact with the CRM. It has been copied into the PlicoInstallationHandler project.
    /// Or maybe this is not a useful distinction to make. Time will tell.</remarks>
    public static class HandleSynergyRenewableApplicationEmail
    {
        /// <summary>
        /// Handler function triggered by a Flow when a Synergy email lands in the Approvals@plicoenergy.com mailbox
        /// It contains the Synergy RRN, which we want to pick up, to update the CRM.
        /// </summary>
        /// <param name="req"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        /// <remarks>https://crmupdatehandler.azurewebsites.net/api/HandleSynergyRenewableApplicationEmail?hello=there&code=</remarks>
        [FunctionName("HandleSynergyRenewableApplicationEmail")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {

            // Was this a sign-of-life request? (i.e. somebody just passing the 'hello' param to see if the function is available)
            string hello = req.Query["hello"];
            if (!string.IsNullOrEmpty(hello))
            {
                log.LogInformation("HandleSynergyRenewableApplicationEmail hello");
                // We got a sign-of-life request. Just echo the hello string and exit
                return new OkObjectResult(hello);
            }

            log.LogInformation("Function triggered by receipt of Synergy email");

            // The Flow that calls us passes the messageId as a querystring parameter. This gives us a chance to pass it through
            string messageId = req.Query["messageId"];
            log.LogInformation("Original messageId: " + messageId);

            // Extract the original email body from the body of the POST request.
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            //log.LogInformation(requestBody);  // maybe turn this on from application parameter?

            // Hand off the email parsing to a specialist parser.
            var extractedSynergyFields = await SynergyEmailParser.Parse(requestBody, log);

            // If there was a failure parsing the email, go no further. Return an error status which should cause the Flow to fail
            if (extractedSynergyFields == null)
            {
                // Error extracting info from the email
                return new BadRequestObjectResult("critical properties not found in email body");
            }

            // Append the id of the original email so that it can be moved or flagged later.
            extractedSynergyFields.messageId = messageId;

            // Now wrap up the synergy fields object so it's suitable for submission to EventGrid
            var uniqueEventId = "SynergyData" + DateTime.Now.Ticks;
            var eventType = "SynergyEmail";
            var updatedSynergyDataEvent = new UpdatedSynergyDataEvent(uniqueEventId, eventType, extractedSynergyFields);

            // Send that event to Event Grid
            var updatedSynergyDataPackets = new List<UpdatedSynergyDataEvent>();
            updatedSynergyDataPackets.Add(updatedSynergyDataEvent);
            var eventGridResponse = await EventGridAdapter.RaiseUpdatedSynergyDataEventsAsync(updatedSynergyDataPackets);

            if (eventGridResponse.StatusCode != HttpStatusCode.OK)
            {
                log.LogError("Error {0} sending updated synergy data to Event Grid: {1}", (int)eventGridResponse.StatusCode, eventGridResponse.ErrorMessage);
            }
            else
            {
                log.LogInformation("Successfully invoked {0} event", updatedSynergyDataEvent.eventType);
            }

            // Return a 200 OK with all the extracted information for confirmation. This is the same data object that is the payload sent to Event Grid

            return new OkObjectResult(extractedSynergyFields);
        }
    }
}
