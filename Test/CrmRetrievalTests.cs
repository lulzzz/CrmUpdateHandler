using CrmUpdateHandler;
using CrmUpdateHandler.Utility;
using System;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Microsoft.Extensions.Primitives;
using System.Net.Http;
using Newtonsoft.Json;
using Test.TestFixtures;
using Microsoft.Azure.WebJobs;
using Test.Utility;

namespace Test
{
    public class CrmRetrievalTests : AzureFunctionTestBase, IClassFixture<TestContactCreationFixture>, IClassFixture<EnvironmentSetupFixture>
    {
        private TestContactCreationFixture contactCreationFixture;
        private EnvironmentSetupFixture environmentSetupFixture;

        /// <summary>
        /// Acts as the error queue
        /// </summary>
        private TestableAsyncCollector<string> _errorQueue;
        /// <summary>
        /// Acts as the error queue
        /// </summary>
        private TestableAsyncCollector<string> _installationQueue;

        /// <summary>
        /// Stores log messages emitted by the code under test
        /// </summary>
        private ListLogger _logger;

        /// <summary>
        /// Constructor is called for every test. It is passed the fixtures, which are instantiated only once, just
        /// before the first test is run, to set the environment variables used by the test
        /// </summary>
        /// <param name="contactCreationFixture"></param>
        public CrmRetrievalTests(TestContactCreationFixture contactCreationFixture, EnvironmentSetupFixture environmentSetupFixture)
        {
            this.contactCreationFixture = contactCreationFixture;
            this.environmentSetupFixture = environmentSetupFixture;

            this._errorQueue = new TestableAsyncCollector<string>();
            this._installationQueue = new TestableAsyncCollector<string>();
            this._logger = new ListLogger();
        }




        /// <summary>
        /// This test relies on an environment variable called 'hapikey' being correctly set. It's in 
        /// Properties\launchSettings.json which is not committed to github
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task verify_the_test_contact_is_retrieved_from_hubspot_by_email_addr()
        {
            var logger = new Mock<ILogger>();
            var contact = await contactCreationFixture.CreateTestContact();

            var adapter = new HubspotAdapter();

            var contactRetrievalResult = await adapter.RetrieveHubspotContactByEmailAddr(
                this.contactCreationFixture.TestContactEmailAddress, 
                fetchPreviousValues: true, 
                log: logger.Object,
                isTest: true);
            Assert.True(string.IsNullOrEmpty(contactRetrievalResult.ErrorMessage), contactRetrievalResult.ErrorMessage);
            Assert.Equal(200, (int)contactRetrievalResult.StatusCode);
            //Assert.Equal("001551", contactRetrievalResult.Payload.contactId);
        }

        /// <summary>
        /// This test relies on an environment variable called 'hapikey' being correctly set. Note that after setting this, Visual Studio must be restarted
        /// in order to see it. 
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task verify_the_test_contact_is_retrieved_from_hubspot_by_id()
        {
            var logger = new Mock<ILogger>();
            var contact = await contactCreationFixture.CreateTestContact();

            var adapter = new HubspotAdapter();

            var contactRetrievalResult = await adapter.RetrieveHubspotContactById(
                contact.contactId, 
                fetchPreviousValues: true, 
                log: logger.Object,
                isTest: true
                );
            Assert.True(string.IsNullOrEmpty(contactRetrievalResult.ErrorMessage), contactRetrievalResult.ErrorMessage);
            Assert.Equal(200, (int)contactRetrievalResult.StatusCode);
            Assert.Equal(this.contactCreationFixture.TestContactEmailAddress, contactRetrievalResult.Payload.email);
            Assert.Equal("Autocreated TestUser", contactRetrievalResult.Payload.fullName);
            Assert.Equal("Autocreated.TestUser", contactRetrievalResult.Payload.fullNamePeriodSeparated);
            Assert.Equal("Unit Test 1, CrmUpdateHandler St\nTest City\nWA 6000", contactRetrievalResult.Payload.customerAddress);
        }



        [Fact(Skip ="Not a real test")]
        public void newContact_Tester()
        {
            var newContactProperties = HubspotAdapter.AssembleContactProperties(
                "email@example.com", 
                "firstname", 
                "lastname", 
                "preferredName",
                "08 97561234",
                "123 example St",
                "Apt 5",
                "Bedrock",
                "WA",
                "9943",
                "Unsure",
                true);

            var json = JsonConvert.SerializeObject(newContactProperties);

            Assert.NotNull(json);

        }
    }
}
