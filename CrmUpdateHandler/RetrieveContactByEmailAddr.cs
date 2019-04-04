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

namespace CrmUpdateHandler
{
    /// <summary>
    /// Given an email address, look up that email address in the CRM and return the ID of the Contact
    /// This is part of a workflow triggered when an email lands in the approvals@plicoenergy.om.au inbox
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
    public static class RetrieveContactByEmailAddr
    {
        [FunctionName("RetrieveContactByEmailAddr")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("RetrieveContactByEmailAddr HTTP trigger");

            // Was this a sign-of-life request?
            string hello = req.Query["hello"];
            if (!string.IsNullOrEmpty(hello))
            {
                // We got a sign-of-life request. Just echo the hello string
                return new OkObjectResult(hello);
            }

            // Retrieve the Hubspot contact corresponding to this email address
            try
            {
                using (var client = new HttpClient())
                {

                }
            }
            catch (Exception ex)
            {

            }

            return  (ActionResult)new OkResult();
        }
    }
}
