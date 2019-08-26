using CrmUpdateHandler;
using CrmUpdateHandler.Utility;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Test.TestFixtures;
using Test.Utility;
using Xunit;

namespace Test
{
    public class CrmCreationTests : AzureFunctionTestBase, IClassFixture<TestContactCreationFixture>, IClassFixture<EnvironmentSetupFixture>
    {
        private TestContactCreationFixture contactCreationFixture;
        private EnvironmentSetupFixture environmentSetupFixture;

        /// <summary>
        /// Acts as the error queue
        /// </summary>
        private TestableAsyncCollector<string> _errorQueue;

        /// <summary>
        /// Acts as the installation queue
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
        public CrmCreationTests(TestContactCreationFixture contactCreationFixture, EnvironmentSetupFixture environmentSetupFixture)
        {
            this.contactCreationFixture = contactCreationFixture;
            this.environmentSetupFixture = environmentSetupFixture;

            this._errorQueue = new TestableAsyncCollector<string>();
            this._installationQueue = new TestableAsyncCollector<string>();
            this._logger = new ListLogger();
        }

        [Fact]
        public async Task verify_that_contact_creation_inserts_installation_on_queue()
        {
            var updateReviewQ = new Mock<IAsyncCollector<string>>();

            var hubspotAdapter = new Mock<IHubSpotAdapter>();   // See note below; I'd rather mock the HttpClient and use a real HubSpotAdapter here. 

            var data = new CanonicalContact("012345")
            {
                email = "aa.Postman@ksc.net.au",
                firstName = "aa",
                lastName = "Postman",
                phone = "867 5309"
            };

            var desiredResult = new HubSpotContactResult(data);

            // Set up a retval that the mock HubSpotAdapter might return
            hubspotAdapter.Setup(p => p.CreateHubspotContactAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<ILogger>(),
                    It.IsAny<bool>()))
                .ReturnsAsync(desiredResult);

            // Load in the JSON body of a typical create-crm request
            var filePath = @".\\TestData\\NewContactBody.txt";
            var body = File.ReadAllText(filePath, Encoding.UTF8);
            var query = new Dictionary<string, StringValues>(); // If we want to test query params, put them here.
            //query.Add("messageId", "ABC123");
            var simulatedHttpRequest = this.HttpRequestSetup(query, body);

            var contactCreator = new CrmContactCreator(hubspotAdapter.Object);

            // Create the contact, with a mock error queue
            var result = await contactCreator.CreateNewContact(simulatedHttpRequest, _errorQueue, updateReviewQ.Object, _installationQueue, _logger);

            // TODO: Review these tests in the light of the new dependency-injection capabilities. 

            Assert.IsType<OkObjectResult>(result);
            Assert.Single(_installationQueue.Items);
        }

        [Fact]
        public async Task verify_that_new_contact_with_no_mortgage_is_stored_correctly()
        {
            var updateReviewQ = new Mock<IAsyncCollector<string>>();

            var hubspotAdapter = new Mock<IHubSpotAdapter>();   // See note below; I'd rather mock the HttpClient and use a real HubSpotAdapter here. 

            var data = new CanonicalContact("012345")
            {
                email = "aa.Postman@ksc.net.au",
                firstName = "aa",
                lastName = "Postman",
                phone = "867 5309"
            };

            var desiredResult = new HubSpotContactResult(data);

            // Set up a retval that the mock HubSpotAdapter might return
            hubspotAdapter.Setup(p => p.CreateHubspotContactAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<ILogger>(),
                    It.IsAny<bool>()))
                .ReturnsAsync(desiredResult);

            // Load in the JSON body of a typical create-crm request
            var filePath = @".\\TestData\\NewContactNoMortgage.json";
            var body = File.ReadAllText(filePath, Encoding.UTF8);
            var query = new Dictionary<string, StringValues>(); // If we want to test query params, put them here.
            //query.Add("messageId", "ABC123");
            var simulatedHttpRequest = this.HttpRequestSetup(query, body);

            var contactCreator = new CrmContactCreator(hubspotAdapter.Object);

            // Create the contact, with a mock error queue
            var result = await contactCreator.CreateNewContact(simulatedHttpRequest, _errorQueue, updateReviewQ.Object, _installationQueue, _logger);

            // TODO: Review these tests in the light of the new dependency-injection capabilities. 

            Assert.IsType<OkObjectResult>(result);
            Assert.Single(_installationQueue.Items);

            // TODO: more assertions...
        }
    }
}
