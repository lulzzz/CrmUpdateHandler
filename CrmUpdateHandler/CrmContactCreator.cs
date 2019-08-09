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
using System.Net.Http;
using System.Text;

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
    ///             The HubSpot contact lead status should be whatever was passed
    ///             No Installation Record is created
    /// </remarks>
    public class CrmContactCreator
    {
        // Singleton instance - makes the Azure functions more scalable.
        private static readonly HttpClient azureFunctionHttpClient;

        static CrmContactCreator()
        {
            azureFunctionHttpClient = new HttpClient();

            var newInstallationAzureFunctionKey = Environment.GetEnvironmentVariable("CreateNewInstallationAzureFunctionKey", EnvironmentVariableTarget.Process);
            
            // TODO: Find this out.
            azureFunctionHttpClient.DefaultRequestHeaders.Add("x-functions-key", newInstallationAzureFunctionKey);
        }

        /// <summary>
        /// The world-facing endpoint that creates a HubSpot Contact and an Installation all in one.
        /// </summary>
        /// <param name="req"></param>
        /// <param name="log"></param>
        /// <returns>Returns 200 OK if all goes well.
        /// This function can also enqueue error messages and requests for update reviews</returns>
        /// <remarks>See <see cref="https://crmupdatehandler.azurewebsites.net/api/CreateNewCrmContact"/>
        /// This function creates a contact with "InstallationRecordExists = true", which means that the 
        /// HubSpot contact-creation webhook doesn't create an Installation record (it's inhibited by
        /// a condition placed on the relevant subscriber to the Event Grid's 'NewCrmContact' topic)</remarks>
        [FunctionName("CreateNewCrmContact")]
        public async Task<IActionResult> CreateNewContact(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            [Queue("error-notification", Connection = "AzureWebJobsStorage")] IAsyncCollector<string> errorQueue,
            [Queue("existing-contact-update-review", Connection = "AzureWebJobsStorage")] IAsyncCollector<string> updateReviewQueue,
            ILogger log)
        {
            log.LogInformation("CreateNewCrmContact triggered");

            // Test mode can be turned on by passing ?test, or by running from localhost
            // It will use the Hubspot sandbox via a hapikey override, and return a Contact with a hard-coded crmid=2001
            var test = req.Query["test"];
            var isTest = test.Count > 0;

            if (req.Host.Host == "localhost")
            {
                isTest = true;
            }

                       
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            if (string.IsNullOrEmpty(requestBody))
            {
                await errorQueue.AddAsync(nameof(CrmContactCreator) + ": Request body is not empty");
                return new BadRequestObjectResult("Empty Request Body");
            }

            // For a while, it will be handy to keep a record of the original JSON in the log files, in 
            // case we need to track and fix systemic failures that lay undetected for a while. 
            log.LogInformation(requestBody);


            dynamic userdata = JsonConvert.DeserializeObject(requestBody);

            // Test
            //userdata.contact.crmid = 99;
            //var dbg = new StringContent(userdata.ToString());


            // If body is empty or not JSON, just return 
            if (userdata == null)
            {
                log.LogWarning("Contact information was empty or not JSON");
                await errorQueue.AddAsync(nameof(CrmContactCreator) + ": Contact information was empty or not JSON");
                return new OkResult();
            }

            string leadStatus = userdata?.leadStatus;

            string firstname = userdata?.contact?.firstname;
            string lastname = userdata?.contact?.lastname;
            string preferredName = userdata?.contact?.firstname;   // initialise the preferred name as the first name
            string email = userdata?.contact?.email;
            string phone = userdata?.contact?.phone;

            string installStreetAddress1 = userdata?.installAddress?.street;
            string installStreetAddress2 = userdata?.installAddress?.unit;
            string installCity = userdata?.installAddress?.suburb;
            string installState = userdata?.installAddress?.state;
            string installPostcode = userdata?.installAddress?.postcode;

            string propertyOwnership = userdata?.property?.propertyOwnership; // "owner" or 
            string propertyType = userdata?.property?.propertyType;   // "business" or 
            string abn = userdata?.property?.abn;

            string customerStreetAddress1 = userdata?.signatories?.signer1?.address?.street;
            string customerStreetAddress2 = userdata?.signatories?.signer1?.address?.unit;
            string customerCity = userdata?.signatories?.signer1?.address?.suburb;
            string customerState = userdata?.signatories?.signer1?.address?.state;
            string customerPostcode = userdata?.signatories?.signer1?.address?.postcode;
            
           
            string mortgageStatus = userdata?.mortgage?.mortgageStatus;   // "yes" / "no"
            string bankName = userdata?.mortgage?.bankName;
            string bankBranch = userdata?.mortgage?.bankBranch;

            const bool installationRecordExists = true; // inhibit the creation of an Installation record.

            log.LogInformation($"Creating {firstname} {lastname} as {email} {(isTest ? "in test database":string.Empty)}");

            var crmAccessResult = await HubspotAdapter.CreateHubspotContactAsync(
                email, 
                firstname, 
                lastname, 
                preferredName, 
                phone,
                customerStreetAddress1,
                customerStreetAddress2,
                customerCity,
                customerState,
                customerPostcode,
                leadStatus,
                installationRecordExists,
                log,
                isTest);

            // Some failures aren't really failures
            if (crmAccessResult.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                // This is not unexpected. We can't blindly overwrite existing contact details when we don't know anything about the intentions
                // of the caller. So we add the whole packet to a queue for reivew and approval by a human (using Approvals in Flow)
                await updateReviewQueue.AddAsync(requestBody);
                return (ActionResult)new OkResult();

            }
            else if (crmAccessResult.StatusCode != System.Net.HttpStatusCode.OK)
            {
                // This is a real failure. We cannot continue.
                log.LogError($"Error {crmAccessResult.StatusCode} creating HubSpot contact: {crmAccessResult.ErrorMessage}");
                await errorQueue.AddAsync(nameof(CrmContactCreator) + ": Error "+ crmAccessResult.StatusCode + " creating HubSpot contact: " + crmAccessResult.ErrorMessage);
                return new BadRequestObjectResult(crmAccessResult.ErrorMessage);
            }

            log.LogInformation($"{firstname} {lastname} ({email}) created as {crmAccessResult.Payload.contactId}");


            // Now we must create an Installations record
            // The structure we must post to the 'CreateInstallation' endpoint is the same structure
            // as we receive in this method, with the addition of 
            //      (1) the contract.crmid property
            //      (2) a 'sendContract' flag that tells the Installation-creator to send a contract when the Installation record is created
            userdata.contact.crmid = crmAccessResult.Payload.contactId;
            userdata.sendContract = true;

            // To create a record in the Installations table we post to an Azure function https://plicoinstallationhandler.azurewebsites.net/api/CreateInstallation
            var createInstallationEndpoint = Environment.GetEnvironmentVariable("CreateNewInstallationAzureFunctionEndpoint", EnvironmentVariableTarget.Process);

            if (string.IsNullOrEmpty(createInstallationEndpoint))
            {
                await errorQueue.AddAsync(nameof(CrmContactCreator) + ": CreateNewInstallationAzureFunctionEndpoint was not configured");
                return new BadRequestObjectResult("CreateNewInstallationAzureFunctionEndpoint was not configured");
            }

            // Pass on the test flag to the installation creator
            if (isTest)
            {
                createInstallationEndpoint += "?test";
            }

            log.LogInformation($"Creating installation record via {createInstallationEndpoint}");
            string installationData = userdata.ToString();
            var newInstallationRequestBody = new StringContent(installationData, Encoding.UTF8, "application/json");    // Sets Content-Type header
            if (isTest)
            {
                // Just log the installation information 
                log.LogInformation("Test mode: Not proceeding with Installation-creation. Installation data follows");
                log.LogInformation(installationData);

                // TODO: Could we do more? Pass a 'test' flag to the next stages? Yes, see above
            }
            else
            {
                // We're doing this for real. Proceed with the creation of a Installation and the sending of a contract
                var response = await azureFunctionHttpClient.PostAsync(createInstallationEndpoint, newInstallationRequestBody);

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    log.LogError($"Error {response.StatusCode} invoking installation-creator");
                    await errorQueue.AddAsync(nameof(CrmContactCreator) + ": Error " + response.StatusCode + " invoking installation-creator");
                    string errmsg = await response.Content.ReadAsStringAsync();
                    return new BadRequestObjectResult(errmsg);
                }                
            }

            return (ActionResult)new OkObjectResult(crmAccessResult.Payload);
        }
    }
}
