using CrmUpdateHandler;
using CrmUpdateHandler.Utility;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Test.TestFixtures;
using Test.Utility;
using Xunit;

namespace Test
{
    /// <summary>
    /// Exercise and verify our ability to update the Contract-related proprties in HubSpot
    /// </summary>
    public class UpdateContractStatusTests : IClassFixture<EnvironmentSetupFixture>
    {
        private EnvironmentSetupFixture _environmentSetupFixture;

        /// <summary>
        /// Acts as the error queue
        /// </summary>
        private TestableAsyncCollector<string> _errorQueue;

        /// <summary>
        /// Stores log messages emitted by the code under test
        /// </summary>
        private ListLogger _logger;

        /// <summary>
        /// Constructor is called prior to every test
        /// </summary>
        public UpdateContractStatusTests(EnvironmentSetupFixture environmentSetupFixture)
        {
            this._environmentSetupFixture = environmentSetupFixture;

            this._errorQueue = new TestableAsyncCollector<string>();
            this._logger = new ListLogger();
        }

        // When Docusign tells us a contract has been sent, we want to make sure that we're invoking the correct method on the hubspot adapter
        [Fact]
        public void HandleContractSentNotification()
        {
            // Contract Sent, Testy Webhookssen, Id 124
            var filePath = @".\\TestData\\EventNotificationContractSent.json";
            var body = File.ReadAllText(filePath, Encoding.UTF8);
            var evt = JsonConvert.DeserializeObject<EventGridEvent>(body);

            var mockAdapter = new Mock<IHubSpotAdapter>();
            var successResult = new HubSpotContactResult(HttpStatusCode.OK);

            // Make sure that the call to the adapter is made as expected
            mockAdapter.Setup(p => p.UpdateContractStatusAsync(
                "testy.webhookssen@ksc.net.au",
                "Sent",
                It.IsAny<ILogger>(),
                It.IsAny<bool>())).ReturnsAsync(successResult);

            var func = new UpdateContractStatusHandler(mockAdapter.Object);
            func.Run(evt, _errorQueue, _logger);

            Assert.Empty(_errorQueue.Items);
        }


        [Fact]
        public void HandleContractSignedNotification()
        {
            // Contract Signed, Testy Webhookssen, Id 124
            var filePath = @".\\TestData\\EventNotificationContractSigned.json";
            var body = File.ReadAllText(filePath, Encoding.UTF8);
            var evt = JsonConvert.DeserializeObject<EventGridEvent>(body);

            var mockAdapter = new Mock<IHubSpotAdapter>();
            var successResult = new HubSpotContactResult(HttpStatusCode.OK);

            // Make sure that the call to the adapter is made as expected
            mockAdapter.Setup(p => p.UpdateContractStatusAsync(
                "testy.webhookssen@ksc.net.au",
                "Signed",
                It.IsAny<ILogger>(),
                It.IsAny<bool>())).ReturnsAsync(successResult);

            var func = new UpdateContractStatusHandler(mockAdapter.Object);
            func.Run(evt, _errorQueue, _logger);

            Assert.Empty(_errorQueue.Items);
        }

        [Fact]
        public void HandleContractRejectedNotification()
        {
            // Contract Signed, Testy Webhookssen, Id 124
            var filePath = @".\\TestData\\EventNotificationContractRejected.json";
            var body = File.ReadAllText(filePath, Encoding.UTF8);
            var evt = JsonConvert.DeserializeObject<EventGridEvent>(body);

            var mockAdapter = new Mock<IHubSpotAdapter>();
            var successResult = new HubSpotContactResult(HttpStatusCode.OK);

            // Make sure that the call to the adapter is made as expected
            mockAdapter.Setup(p => p.UpdateContractStatusAsync(
                "testy.webhookssen@ksc.net.au",
                "Rejected",
                It.IsAny<ILogger>(),
                It.IsAny<bool>())).ReturnsAsync(successResult);

            var func = new UpdateContractStatusHandler(mockAdapter.Object);
            func.Run(evt, _errorQueue, _logger);

            Assert.Empty(_errorQueue.Items);
        }

        [Fact]
        public void unknown_contract_state_is_handled_via_error_queue()
        {
            // Contract Signed, Testy Webhookssen, Id 124
            var filePath = @".\\TestData\\EventNotificationContractStateUnknown.json";
            var body = File.ReadAllText(filePath, Encoding.UTF8);
            var evt = JsonConvert.DeserializeObject<EventGridEvent>(body);

            var mockAdapter = new Mock<IHubSpotAdapter>();

            var func = new UpdateContractStatusHandler(mockAdapter.Object);
            func.Run(evt, _errorQueue, _logger);

            Assert.Single(_errorQueue.Items);
            Assert.Equal("CrmUpdateHandler.UpdateContractStatusHandler: Unknown contract state: 'Unknown' for installation 124", _errorQueue.Items[0]);
        }

    }
}
