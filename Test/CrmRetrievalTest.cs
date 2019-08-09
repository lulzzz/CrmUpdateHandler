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

namespace Test
{
    public class CrmRetrievalTest : AzureFunctionTestBase, IClassFixture<TestContactCreationFixture>, IClassFixture<EnvironmentSetupFixture>
    {
        private TestContactCreationFixture contactCreationFixture;
        private EnvironmentSetupFixture environmentSetupFixture;

        /// <summary>
        /// Constructor is called for every test. It is passed the fixture, which is instantiated only once, just
        /// before the first test is run, to set the environment variables used by the test
        /// </summary>
        /// <param name="contactCreationFixture"></param>
        public CrmRetrievalTest(TestContactCreationFixture contactCreationFixture, EnvironmentSetupFixture environmentSetupFixture)
        {
            this.contactCreationFixture = contactCreationFixture;
            this.environmentSetupFixture = environmentSetupFixture;
        }


        // Wait on Microsoft to fix their bugs and allow Azure Functions as instantiable classes. Then we
        // can maybe do dependency injection to enable testing. 
        // THIS TEST WILL ACTUALLY CREATE A CONTACT IN THE DB. THATS WHY WE NEED TO UNDERSTAND DI BETTER
        public async Task Verify_Function_Calls_HubSpot_Adapter()
        {
            var logger = new Mock<ILogger>();
            var errorQ = new Mock<IAsyncCollector<string>>();
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
                    It.IsAny<string>()))
                .ReturnsAsync(desiredResult);

            // Load in the JSON body of a typical create-crm request
            var filePath = @".\\TestData\\NewContactBody.txt";
            var body = File.ReadAllText(filePath, Encoding.UTF8);
            var query = new Dictionary<string, StringValues>(); // If we want to test query params, put them here.
            //query.Add("messageId", "ABC123");
            var simulatedHttpRequest = this.HttpRequestSetup(query, body);

            var contactCreator = new CrmContactCreator();

            // Create the contact, with a mock error queue
            var result = await contactCreator.CreateNewContact(simulatedHttpRequest, errorQ.Object, updateReviewQ.Object, logger.Object);

            // TODO: Much better to mock the HttpClient used by the HubSpotAdapter. Then we can verify the actual 
            // request being sent to the HttpClient.

            Assert.IsType<OkObjectResult>(result);
        }


        /// <summary>
        /// This test relies on an environment variable called 'hapikey' being correctly set. It's in 
        /// Properties\launchSettings.json which is not committed to github
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Test_Contact_Is_Retrieved_From_HubSpot_By_Email()
        {
            var logger = new Mock<ILogger>();
            var contact = await contactCreationFixture.CreateTestContact();

            var contactRetrievalResult = await HubspotAdapter.RetrieveHubspotContactByEmailAddr(
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
        public async Task Test_User_Is_Retrieved_From_HubSpot_By_Id()
        {
            var logger = new Mock<ILogger>();
            var contact = await contactCreationFixture.CreateTestContact();

            var contactRetrievalResult = await HubspotAdapter.RetrieveHubspotContactById(
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

        [Fact]
        public async Task newContact_Tester()
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
