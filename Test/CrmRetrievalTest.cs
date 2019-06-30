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

namespace Test
{
    public class CrmRetrievalTest : AzureFunctionTestBase
    {
        // Wait on Microsoft to fix their bugs and allow Azure Functions as instantiable classes. Then we
        // can maybe do dependency injection to enable testing. 
        // THIS TEST WILL ACTUALLY CREATE A CONTACT IN THE DB. THATS WHY WE NEED TO UNDERSTAND DI BETTER
        public async Task Verify_Function_Calls_HubSpot_Adapter()
        {
            var logger = new Mock<ILogger>();
            var hubspotAdapter = new Mock<IHubSpotAdapter>();   // See note below; I'd rather mock the HttpClient and use a real HubSpotAdapter here. 

            var data = new CanonicalContact("012345")
            {
                email = "aa.Postman@ksc.net.au",
                firstName = "aa",
                lastName = "Postman",
                phone = "867 5309"
            };

            var desiredResult = new CrmAccessResult(data);

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

            // Simulate the event being triggered
            var result = await contactCreator.CreateNewContact(simulatedHttpRequest, logger.Object);

            // TODO: Much better to mock the HttpClient used by the HubSpotAdapter. Then we can verify the actual 
            // request being sent to the HttpClient.

            Assert.IsType<OkObjectResult>(result);
        }


        /// <summary>
        /// This test relies on an environment variable called 'hapikey' being correctly set. Note that after setting this, Visual Studio must be restarted
        /// in order to see it. 
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Janine_Pittaway_Is_Retrieved_From_HubSpot_By_Email()
        {
            var logger = new Mock<ILogger>();
            var contactRetrievalResult = await HubspotAdapter.RetrieveHubspotContactByEmailAddr("jpittaway@brightcommunications.com.au", fetchPreviousValues: true, log: logger.Object);
            Assert.True(string.IsNullOrEmpty(contactRetrievalResult.ErrorMessage), contactRetrievalResult.ErrorMessage);
            Assert.Equal(200, (int)contactRetrievalResult.StatusCode);
            Assert.Equal("001551", contactRetrievalResult.Payload.contactId);
        }

        /// <summary>
        /// This test relies on an environment variable called 'hapikey' being correctly set. Note that after setting this, Visual Studio must be restarted
        /// in order to see it. 
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Janine_Pittaway_Is_Retrieved_From_HubSpot_By_Id()
        {
            var logger = new Mock<ILogger>();
            var contactRetrievalResult = await HubspotAdapter.RetrieveHubspotContactById("1551", fetchPreviousValues: true, log: logger.Object);
            Assert.True(string.IsNullOrEmpty(contactRetrievalResult.ErrorMessage), contactRetrievalResult.ErrorMessage);
            Assert.Equal(200, (int)contactRetrievalResult.StatusCode);
            Assert.Equal("jpittaway@brightcommunications.com.au", contactRetrievalResult.Payload.email);
            Assert.Equal("Janine Pittaway", contactRetrievalResult.Payload.fullName);
            Assert.Equal("Janine.Pittaway", contactRetrievalResult.Payload.fullNamePeriodSeparated);
            Assert.Equal("189 Sheoak Drive\nYallingup\nWA 6282", contactRetrievalResult.Payload.customerAddress);
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
                "Unsure");

            var json = JsonConvert.SerializeObject(newContactProperties);

            Assert.NotNull(json);

        }
    }
}
