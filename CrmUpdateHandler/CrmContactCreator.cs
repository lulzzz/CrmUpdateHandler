

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
    using System.Collections.Generic;

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
        private IHubSpotAdapter _hubSpotAdapter;

        /// <summary>
        /// Constructor is an entry point for the dependency-injection defined in Startup.cs
        /// </summary>
        /// <param name="hubSpotAdapter"></param>
        public CrmContactCreator(IHubSpotAdapter hubSpotAdapter)
        {
            this._hubSpotAdapter = hubSpotAdapter;
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
            [Queue("error-notification")] IAsyncCollector<string> errors,
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

            // Instantiate our convenient wrapper for the error-log queue
            var errQ = new ErrorQueueLogger(errors, "CrmUpdateHandler", nameof(CrmContactCreator));

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            if (string.IsNullOrEmpty(requestBody))
            {
                errQ.LogError("Request body is not empty");
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
                errQ.LogError("Contact information was empty or not JSON");
                return new OkResult();
            }

            string leadStatus = userdata?.leadStatus;

            string firstname = userdata?.contact?.firstname;
            string lastname = userdata?.contact?.lastname;
            string preferredName = userdata?.contact?.firstname;   // initialise the preferred name as the first name
            string email = userdata?.contact?.email;
            string phone = userdata?.contact?.phone;

            if (string.IsNullOrEmpty(phone))
            {
                phone = userdata?.contact?.mobilephone;
            }

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

            var crmAccessResult = await this._hubSpotAdapter.CreateHubspotContactAsync(
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
                // A Conflict is not unexpected. However, we can't blindly overwrite existing contact details when we don't know anything 
                // about the intentions of the caller. The changes must be Approved by a human.
                // To facilitate the Approval process, we take the original data packet (which has enough information for both a HubSpot
                // contact and an Installation record) and supplement it with a list of changes, which the Approval flow can use to present
                // a nice(ish) UI to the approver. After approval, the packet can then continue to flow (via queues) to process that create
                // installations and effect changes to hubspot contacts

                var orig = crmAccessResult.Payload;
                //var changeList = new List<UpdateReviewChange>();

                // The changelist must serialise to 'nice' JSON. No nulls, else Flow can't parse it.
                var updateReview = new UpdateReview();
                updateReview.AddChange("First", orig.firstName??"", firstname??"");
                updateReview.AddChange("Last", orig.lastName??"", lastname??"");
                updateReview.AddChange("Phone", orig.phone??"", phone ?? "");
                updateReview.AddChange("Street Address", orig.streetAddress ?? "", (customerStreetAddress1 + " " + customerStreetAddress2).Trim());
                updateReview.AddChange("City", orig.city ?? "", customerCity ?? "");
                updateReview.AddChange("State", orig.state ?? "", customerState ?? "");
                updateReview.AddChange("Postcode", orig.postcode ?? "", customerPostcode ?? "");

                var newLeadStatus = HubspotAdapter.ResolveLeadStatus(leadStatus);
                updateReview.AddChange("Lead status", orig.leadStatus ?? "", newLeadStatus ??"");

                // Mimic the installation-inhib logic in the original contact-creation code.
                if (newLeadStatus == "READY_TO_ENGAGE")
                {
                    if (orig.leadStatus == "INTERESTED")
                    {
                        // We need to inhibit the creation of an Installation record if this approval goes ahead, to prevent a race condition
                        updateReview.AddChange("installationrecordexists", "", "true");
                    }
                }

                // TODO: Call an installation-details web-service to get these details

                var installAddress = HubspotAdapter.AssembleCustomerAddress(
                    (installStreetAddress1 + " " + installStreetAddress2).Trim(),
                    installCity,
                    installState,
                    installPostcode);

                // TODO: more...including Installation fields...
                updateReview.AddChange("Install Address", "", installAddress ?? "");  // TODO
                updateReview.AddChange("propertyOwnership", "", propertyOwnership ?? "");  // TODO
                updateReview.AddChange("propertyType", "", propertyType ?? "");  // TODO
                updateReview.AddChange("ABN", "", abn ?? "");  // TODO

                updateReview.AddChange("mortgageStatus", "", mortgageStatus ?? "");  // TODO

                var newBankName = (bankName == "Other") ? bankOther : bankName;
                updateReview.AddChange("bankName", "", newBankName ?? "");  // TODO

                userdata.changes = Newtonsoft.Json.Linq.JToken.FromObject(updateReview.Changes);

                // Prepare the original 'Join' data packet for re-use as an Installation if a 
                // human approves the changes. And set an 'updatePermitted' flag that signals
                // to the receiving process that the data has been through a human review, and
                // it's OK to update the Installation if it exists already.
                userdata.contact.crmid = crmAccessResult.Payload.contactId;
                userdata.sendContract = true;
                userdata.updatePermitted = true;    // if it passes human approval, then it's OK to update the Installation

                string updateReviewPackage = JsonConvert.SerializeObject(userdata);
                log.LogInformation("For the 'existing-contact-update-review' queue:\n" + updateReviewPackage);

                // Queue the submission for human approval
                await updateReviewQueue.AddAsync(updateReviewPackage);

                // Control now passes to the Flow called on contact-update-review message, where the changes are approved or rejected
                return (ActionResult)new OkResult();

            }
            else if (crmAccessResult.StatusCode != System.Net.HttpStatusCode.OK)
            {
                // This is a real failure. We cannot continue.
                log.LogError($"Error {crmAccessResult.StatusCode} creating HubSpot contact: {crmAccessResult.ErrorMessage}");
                errQ.LogError("Error " + crmAccessResult.StatusCode + " creating HubSpot contact: " + crmAccessResult.ErrorMessage);
                return new BadRequestObjectResult(crmAccessResult.ErrorMessage);
            }

            log.LogInformation($"{firstname} {lastname} ({email}) created as {crmAccessResult.Payload.contactId}");


            // Now we must create an Installations record by placing a job on the 'installations-to-be-created' queue
            // The structure we place on this queue is the same structure as we receive in this method, with the addition of 
            //      (1) the contract.crmid property
            //      (2) a 'sendContract' flag that tells the Installation-creator to send a contract when the Installation record is created
            // NB: 'updatePermitted' is NOT set, because it would be a big surprise worthy of a big error to find an existing 
            // Installation in the absence of a HubSpot contact record
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
