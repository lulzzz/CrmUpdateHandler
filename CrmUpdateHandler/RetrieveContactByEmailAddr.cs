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
using System.Runtime.CompilerServices;
using CrmUpdateHandler.Utility;

[assembly: InternalsVisibleTo("Test")]
namespace CrmUpdateHandler
{
    /// <summary>
    /// Given an email address, look up that email address in the CRM and return the ID of the Contact
    /// This is potentially part of a workflow triggered when an email lands in the approvals@plicoenergy.om.au inbox
    /// We have to invoke the Hubspot API and search for a contact by email.
    /// [
    ///  {
    ///    "eventId": 1,
    ///    "subscriptionId": 111448,
    ///    "portalId": 5684115,
    ///    "occurredAt": 1554364476289,
    ///    "subscriptionType": "contact.creation",
    ///    "attemptNumber": 0,
    ///    "objectId": 123,
    ///    "changeSource": "CRM",
    ///    "changeFlag": "NEW",
    ///    "appId": 191749
    ///  }
    /// ]

    /// </summary>
    /// <remarks>https://crmupdatehandler.azurewebsites.net/api/RetrieveContactByEmailAddr?hello=x
    /// https://github.com/projectkudu/kudu/wiki/Deploying-from-a-zip-file-or-url
    /// https://social.msdn.microsoft.com/Forums/en-US/520a8488-d1a9-4843-be01-effdba936bd3/azure-function-publish-from-vs2017-fails-with-requesttimeout-and-0x80070002-on?forum=AzureFunctions
    /// </remarks>
    internal class RetrieveContactByEmailAddr
    {
        private IHubSpotAdapter _hubSpotAdapter;

        /// <summary>
        /// Returns a Contact definition from the CRM, given an email address.
        /// </summary>
        /// <param name="req"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        [FunctionName("RetrieveContactByEmailAddr")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            [Queue("error-notification")] IAsyncCollector<string> errors,
            ILogger log)
        {
            log.LogInformation("RetrieveContactByEmailAddr HTTP trigger");

            // the email address is expected to come in as a queryparam.
            string email = req.Query["email"];

            // Instantiate our convenient wrapper for the error-log queue
            var errQ = new ErrorQueueLogger(errors, "CrmUpdateHandler", nameof(RetrieveContactByEmailAddr));


            // Retrieve the Hubspot contact corresponding to this email address
            try
            {
                var contactResult = await this._hubSpotAdapter.RetrieveHubspotContactByEmailAddr(email, fetchPreviousValues: false, log: log, isTest: false);

                if (contactResult.StatusCode == HttpStatusCode.OK)
                {
                    return new OkObjectResult(contactResult.Payload);
                }
                else if (contactResult.StatusCode == HttpStatusCode.NotFound)
                {
                    return (ActionResult)new NotFoundResult();
                }
                else
                {
                    log.LogError($"Error: HTTP {contactResult.StatusCode} {contactResult.ErrorMessage} for '{email}'");
                    errQ.LogError($"Error: HTTP {contactResult.StatusCode} {contactResult.ErrorMessage} for '{email}'");
                    return new StatusCodeResult((int)contactResult.StatusCode);
                }

            }
            catch (Exception ex)
            {
                log.LogError($"Exception: {ex.Message} retrieving contact '{email}'");
                errQ.LogError($"Exception: {ex.Message} retrieving contact '{email}'");
                return new StatusCodeResult(500);
            }
        }
    }
}
