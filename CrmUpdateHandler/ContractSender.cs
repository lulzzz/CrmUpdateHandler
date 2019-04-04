using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;

/// <remarks>
/// Resource Group: HubspotCRM
/// App Service: CrmUpdateHandler
/// </remarks>

namespace CrmUpdateHandler
{
    /// <summary>
    /// Send a contract to a person.
    /// First, check if they exist in the CRM, and create them if necessary.
    /// Them check whether they have been sent a contract already. If not, send one
    /// out via Docusign and update the ContractSent flag in the CRM.
    /// </summary>
    /// <remarks>
    /// This function will be invoked by the 'www.starling.energy' back-end, or from a console app.
    /// How to secure it? Set AuthorisationLevel = Function and pass a function app key via the 'x-functions-key' header
    /// </remarks>
    public static class ContractSender
    {
        [FunctionName("SendAContractToAPerson")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("SendAContractToAPerson was triggered by HTTP request.");

            // How to secure this function?


            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            var requiredFieldsArePresent = false;
            var missingFields = new List<string>();

            var email = data?.email.ToString(); // How to validate email?
            if (string.IsNullOrEmpty(email))
                missingFields.Add("email");

            var firstname = data?.firstname.ToString();
            if (string.IsNullOrEmpty(firstname))
                missingFields.Add("firstname");

            var lastname = data?.lastname.ToString();
            if (string.IsNullOrEmpty(lastname))
                missingFields.Add("lastname");

            // Did the caller pass the correct object in the request body?
            if (missingFields.Count == 0)
            {
                requiredFieldsArePresent = true;
            }
            else
            {
                return new BadRequestObjectResult("Missing fields in the request body: " + string.Join(",", missingFields));
            }

           

            return name != null
                ? (ActionResult)new OkObjectResult($"Hello, {name}")
                : new BadRequestObjectResult("Please pass a name on the query string or in the request body");
        }
    }
}
