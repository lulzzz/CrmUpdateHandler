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

namespace CrmUpdateHandler
{
    /// <summary>
    /// Entry point that allows external applications to create a new contact in the CRM
    /// </summary>
    /// <remarks>
    ///     Integration Tests: (invoke this Azure function from Postman, or actually create a new contact in HubSpot)
    ///         ALWAYS creates a Contact in HubSpot
    ///         ALWAYS creates a new record in CrmContacts
    ///         ALWAYS creates a folder in SharePoint
    ///         IF leadStatus == 'Ready to Engage'
    ///             The HubSpot contact lead status should = 'Ready to Engage'
    ///             An Installation record should be created
    ///         ELSE            
    ///             The HubSpot contact lead status should = whatever was passed
    ///             No Installation Record is created
    /// </remarks>
    public class CrmContactCreator
    {

        [FunctionName("CreateNewCrmContact")]
        public async Task<IActionResult> CreateNewContact(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("CreateNewCrmContact triggered");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();


            dynamic newContact = JsonConvert.DeserializeObject(requestBody);

            // If body is empty or not JSON, just return 
            if (newContact == null)
            {
                log.LogWarning("Contact information was empty or not JSON");
                return new OkResult();
            }

            string email = newContact?.email;
            string firstname = newContact?.firstname;
            string lastname = newContact?.lastname;
            string phone = newContact?.phone;
            string leadStatus = newContact?.leadStatus;

            log.LogInformation($"Creating {firstname} {lastname} as {email}");

            var crmAccessResult = await HubspotAdapter.CreateHubspotContactAsync(email, firstname, lastname, phone, leadStatus);

            return crmAccessResult.StatusCode == System.Net.HttpStatusCode.OK
                ? (ActionResult)new OkObjectResult(crmAccessResult.Payload)
                : new BadRequestObjectResult(crmAccessResult.ErrorMessage);
        }
    }
}
