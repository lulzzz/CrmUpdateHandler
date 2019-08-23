
using System.Collections.Generic;
using System.Text;

namespace Test.TestFixtures
{
    using CrmUpdateHandler.Utility;
    using System;
    using System.Threading.Tasks;
    using Moq;
    using Microsoft.Extensions.Logging;
    using System.Threading;

    /// <summary>
    /// This fixture will create a unit test contact in the HubSpot sandbox and delete it when done.
    /// </summary>
    public class TestContactCreationFixture : IDisposable
    {
        private CanonicalContact contact;

        //Instantiate a Singleton of the Semaphore with a value of 1. This means that only 1 thread can be granted access at a time.
        static SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1); 


        public TestContactCreationFixture()
        {
            
        }

        public string TestContactEmailAddress => "testcontactcreationfixture@example.com";

        /// <summary>
        /// Gets the vid of the temporary test contact
        /// </summary>
        public string TestContactId
        {
            get
            {
                if (this.contact == null)
                {
                    throw new CrmUpdateHandlerException("You must call TestContactCreationFixture.CreateTestContact() early in your test");
                }

                return this.contact.contactId;
            }
        }

        /// <summary>
        /// Gets the email address of the temporary test contact
        /// </summary>
        public string TestContactEmail
        {
            get
            {
                if (this.contact == null)
                {
                    throw new CrmUpdateHandlerException("You must call TestContactCreationFixture.CreateTestContact() early in your test");
                }

                return this.contact.email;
            }
        }

        /// <summary>
        /// Creates the test contact in the HubSpot sandbox if necessary
        /// </summary>
        /// <returns></returns>
        public async Task<CanonicalContact> CreateTestContact()
        {
            var logger = new Mock<ILogger>();
            var log = logger.Object;

            // Ensure only one thread can enter this code at a time. 
            await semaphoreSlim.WaitAsync();
            try
            {
                if (this.contact == null)
                {
                    const bool installationRecordexists = true;
                    var adapter = new HubspotAdapter();

                    var contactResult = await adapter.CreateHubspotContactAsync(
                        this.TestContactEmailAddress,
                        "Autocreated",
                        "TestUser",
                        "Auto",
                        "08 123456789",
                        "Unit Test 1",
                        "CrmUpdateHandler St",
                        "Test City",
                        "WA",
                        "6000",
                        "Ready To Engage",
                        installationRecordexists,
                        log,
                        true);

                    if (contactResult.StatusCode == System.Net.HttpStatusCode.Conflict)
                    {
                        // Contact already exists - so just use that one
                        contactResult = await adapter.RetrieveHubspotContactByEmailAddr(this.TestContactEmailAddress, false, log, isTest: true);
                    }

                    if (contactResult.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        log.LogError($"Error {contactResult.StatusCode} creating HubSpot contact: {contactResult.ErrorMessage}");
                        throw new Exception(contactResult.ErrorMessage);
                    }

                    this.contact = contactResult.Payload;
                }

                return this.contact;
            }
            finally
            {
                //When the task is ready, release the semaphore. It is vital to ALWAYS release the semaphore when we are ready, or else we will end up with a Semaphore that is forever locked.
                //This is why it is important to do the Release within a try...finally clause; program execution may crash or take a different path, this way you are guaranteed execution
                semaphoreSlim.Release();
            }

        }

        public void Dispose()
        {
            
        }
    }
}
