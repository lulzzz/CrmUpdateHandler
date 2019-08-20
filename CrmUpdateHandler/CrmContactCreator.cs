

namespace CrmUpdateHandler
{
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
    using CrmUpdateHandler.Utility;

    /// <summary>
    /// Entry point that allows external applications to create a new contact in the CRM, and an Installation, and send a contract
    /// </summary>
    /// <remarks>
    ///     Integration Tests: (invoke this Azure function from Postman, or join up via the Plico website)
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

        static CrmContactCreator()
        {
            // TODO: Lose this line below, remove the key from local.settings.json and also from application settings online. 
            var newInstallationAzureFunctionKey = Environment.GetEnvironmentVariable("CreateNewInstallationAzureFunctionKey", EnvironmentVariableTarget.Process);
        }

        /// <summary>
        /// The world-facing endpoint that creates a HubSpot Contact and queues an Installation for creation and a contract to be sent
        /// </summary>
        /// <param name="req"></param>
        /// <param name="log"></param>
        /// <returns>Returns 200 OK if all goes well.
        /// This function can also enqueue error messages and requests for update reviews</returns>
        /// <remarks>See <see cref="https://crmupdatehandler.azurewebsites.net/api/CreateNewCrmContact"/>
        /// This function creates a contact with "InstallationRecordExists = true", which means that the 
        /// HubSpot contact-creation webhook doesn't create an Installation record (it's inhibited by
        /// a condition placed on the relevant subscriber to the Event Grid's 'NewCrmContact' topic)</remarks>
        [StorageAccount("AzureWebJobsStorage")]
        [FunctionName("CreateNewCrmContact")]
        public async Task<IActionResult> CreateNewContact(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            [Queue("error-notification")] IAsyncCollector<string> errorQueue,
            [Queue("existing-contact-update-review")] IAsyncCollector<string> updateReviewQueue,
            [Queue("installations-to-be-created")] IAsyncCollector<string> installationsAwaitingCreationQueue,
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
            string bankName = userdata?.mortgage?.bankName;     // user-selected bank from the dropdown. Drawn from a Blob which in turns is generated by the Flow called "When BankContactDetails change => update bank-dropdown JSON"
            string bankOther = userdata?.mortgage?.bankOther;   // if user selected "Other" from the dropdown, this is what they entered
            //string bankBranch = userdata?.mortgage?.bankBranch;   // we don't collect bank branch. That comes from BankContactDetails

            const bool installationRecordExists = true; // inhibit the creation of an Installation record.

            log.LogInformation($"Creating {firstname} {lastname} as {email} {(isTest ? "in test database" : string.Empty)}");

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
                // of the caller. So we add the whole packet to a queue for review and approval by a human (using Approvals in Flow)
                var orig = crmAccessResult.Payload;
                var updateReview = new UpdateReview(email, requestBody);
                updateReview.AddChange("First", orig.firstName, firstname);
                updateReview.AddChange("Last", orig.lastName, lastname);
                updateReview.AddChange("Phone", orig.phone, phone);
                // TODO: more...
                await updateReviewQueue.AddAsync(JsonConvert.SerializeObject(updateReview));
                return (ActionResult)new OkResult();

            }
            else if (crmAccessResult.StatusCode != System.Net.HttpStatusCode.OK)
            {
                // This is a real failure. We cannot continue.
                log.LogError($"Error {crmAccessResult.StatusCode} creating HubSpot contact: {crmAccessResult.ErrorMessage}");
                await errorQueue.AddAsync(nameof(CrmContactCreator) + ": Error " + crmAccessResult.StatusCode + " creating HubSpot contact: " + crmAccessResult.ErrorMessage);
                return new BadRequestObjectResult(crmAccessResult.ErrorMessage);
            }

            log.LogInformation($"{firstname} {lastname} ({email}) created as {crmAccessResult.Payload.contactId}");


            // Now we must create an Installations record by placing a job on the 'installations-to-be-created' queue
            // The structure we place on this queue is the same structure as we receive in this method, with the addition of 
            //      (1) the contract.crmid property
            //      (2) a 'sendContract' flag that tells the Installation-creator to send a contract when the Installation record is created
            userdata.contact.crmid = crmAccessResult.Payload.contactId;
            userdata.sendContract = true;

            // Place the augmented join-up data-packet on the queue. It will be picked up by the 'DequeueInstallationsForCreation' function in the 
            // PlicoInstallationsHandler solution.
            var msg = JsonConvert.SerializeObject(userdata);
            await installationsAwaitingCreationQueue.AddAsync(msg);

            return (ActionResult)new OkObjectResult(crmAccessResult.Payload);

        }
    }
}
