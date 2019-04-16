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
    internal static class RetrieveContactByEmailAddr
    {
        // Singleton instance - makes the Azure functions more scalable.
        private static readonly HttpClient httpClient;

        private static readonly string hapikey;

        static RetrieveContactByEmailAddr()
        {
            // See https://docs.microsoft.com/en-us/azure/architecture/antipatterns/improper-instantiation/
            // for an explanation as to why this is better than 'using (var httplient = new HttpClient()) {}"
            httpClient = new HttpClient();

            hapikey = Environment.GetEnvironmentVariable("hapikey", EnvironmentVariableTarget.Process);
        }


        [FunctionName("RetrieveContactByEmailAddr")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("RetrieveContactByEmailAddr HTTP trigger");

            // the email address is expected to come in as a queryparam.
            string email = req.Query["email"];


            // Retrieve the Hubspot contact corresponding to this email address
            try
            {
                var result = await RetrieveHubspotContactIdByEmailAdd(email);

                if (result.StatusCode == HttpStatusCode.OK)
                {
                    return new OkObjectResult(result.ContactId);
                }
                else if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    return (ActionResult)new NotFoundResult();
                }
                else
                {
                    log.LogError("Error: HTTP {0} {1} ", (int)result.StatusCode, result.ErrorMessage);
                    log.LogError("email: {0} ", email);
                    return new StatusCodeResult((int)result.StatusCode);
                }

            }
            catch (Exception ex)
            {
                return new StatusCodeResult(500);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="email"></param>
        /// <param name="hapikey"></param>
        /// <returns>A simple value type</returns>
        internal static async Task<ContactRetrievalResult> RetrieveHubspotContactIdByEmailAdd(string email)
        {
            var hapikey = Environment.GetEnvironmentVariable("hapikey", EnvironmentVariableTarget.Process);

            if (string.IsNullOrEmpty(hapikey))
            {
                return new ContactRetrievalResult(HttpStatusCode.InternalServerError, "hapi key not found");
            }

            // See https://developers.hubspot.com/docs/methods/contacts/get_contact_by_email
            var url = string.Format($"https://api.hubapi.com/contacts/v1/contact/email/{email}/profile?hapikey={hapikey}");
            //log.LogInformation("url: {0}", url);

            // Go get the contact from HubSpot


            HttpResponseMessage response = await httpClient.GetAsync(url);
            HttpContent content = response.Content;
            //log.LogInformation("Response StatusCode from contact retrieval: " + (int)response.StatusCode);
            //log.LogInformation("Hubspot Contact");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                // ... Read the string.
                string resultText = await content.ReadAsStringAsync();
                //log.LogInformation(resultText);

                dynamic contactJson = JsonConvert.DeserializeObject(resultText);

                var contactId = Convert.ToString(contactJson?.vid);

                return new ContactRetrievalResult(contactId);

            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new ContactRetrievalResult(response.StatusCode);
            }
            else
            {
                //log.LogError("Error: HTTP {0} {1} ", (int)response.StatusCode, response.StatusCode);
                //log.LogError("email: {0} ", email);
                string resultText = await content.ReadAsStringAsync();
                return new ContactRetrievalResult(response.StatusCode, resultText);
                //log.LogInformation(resultText);
            }
        }

        internal class ContactRetrievalResult
        {
            public ContactRetrievalResult(string contactId)
            {
                this.StatusCode = HttpStatusCode.OK;
                this.ContactId = contactId;
            }

            public ContactRetrievalResult(HttpStatusCode code)
            {
                this.StatusCode = code;
            }
            public ContactRetrievalResult(HttpStatusCode code, string errorMessage)
            {
                this.StatusCode = code;
                this.ErrorMessage = errorMessage;
            }

            public HttpStatusCode StatusCode { get; set; }

            public string ErrorMessage { get; set; }
            
            public string ContactId { get; set; }
        }
    }
}
