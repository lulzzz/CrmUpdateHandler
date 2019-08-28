using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Moq;
using Test.TestFixtures;
using Test.Utility;
using System.Threading.Tasks;
using CrmUpdateHandler.Utility;
using CrmUpdateHandler;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Test
{
    public class CrmUpdateTests : AzureFunctionTestBase, IClassFixture<TestContactCreationFixture>, IClassFixture<EnvironmentSetupFixture>
    {
        private TestContactCreationFixture contactCreationFixture;
        private EnvironmentSetupFixture environmentSetupFixture;

        /// <summary>
        /// Acts as the error queue
        /// </summary>
        private TestableAsyncCollector<string> _errorQueue;

        /// <summary>
        /// Stores log messages emitted by the code under test
        /// </summary>
        private ListLogger _logger;


        /// <summary>
        /// Constructor is called for every test. It is passed the fixtures, which are instantiated only once, just
        /// before the first test is run, to set the environment variables used by the test
        /// </summary>
        /// <param name="contactCreationFixture"></param>
        public CrmUpdateTests(TestContactCreationFixture contactCreationFixture, EnvironmentSetupFixture environmentSetupFixture)
        {
            this.contactCreationFixture = contactCreationFixture;
            this.environmentSetupFixture = environmentSetupFixture;

            this._errorQueue = new TestableAsyncCollector<string>();
            this._logger = new ListLogger();
        }


        [Fact]
        public async Task verify_that_we_handle_a_non_json_message()
        {
            var mockAdapter = new Mock<IHubSpotAdapter>();

            var notJsonAtAll = "I am just a string";

            var func = new DequeueContactDiffs(mockAdapter.Object);
            await func.Run(notJsonAtAll, _errorQueue, _logger);

            // We expect an exception to be thrown
            Assert.Single(_errorQueue.Items);

            var errMsg = _errorQueue.Items[0];

            Assert.Contains("Exception", errMsg);
            Assert.Contains("deserialising message text", errMsg);

        }

        [Fact]
        public async Task verify_that_we_handle_no_crmid()
        {
            var mockAdapter = new Mock<IHubSpotAdapter>();

            var json_no_crmid = "{ \"msg\": \"no crmid here\" }";

            var func = new DequeueContactDiffs(mockAdapter.Object);
            await func.Run(json_no_crmid, _errorQueue, _logger);

            // We expect an exception to be thrown
            Assert.Single(_errorQueue.Items);

            var errMsg = _errorQueue.Items[0];

            Assert.Contains("Exception", errMsg);
            Assert.Contains("crmid not found", errMsg);
        }

        [Fact]
        public async Task verify_that_we_handle_a_missing_change_object()
        {
            var mockAdapter = new Mock<IHubSpotAdapter>();

            var json_no_crmid = "{ \"crmid\": \"012345\" }";

            var func = new DequeueContactDiffs(mockAdapter.Object);
            await func.Run(json_no_crmid, _errorQueue, _logger);

            // We can handle a missing changes object without fuss
            Assert.Empty(_errorQueue.Items);
        }

        [Fact]
        public async Task verify_that_we_handle_invalid_change_object()
        {
            var mockAdapter = new Mock<IHubSpotAdapter>();

            var json_no_crmid = "{ \"crmid\": \"012345\", \"changes\": 100 }";  // changes is not an array

            var func = new DequeueContactDiffs(mockAdapter.Object);
            await func.Run(json_no_crmid, _errorQueue, _logger);

            // We expect an exception to be thrown
            Assert.Single(_errorQueue.Items);

            var errMsg = _errorQueue.Items[0];

            Assert.Contains("Exception", errMsg);
            Assert.Contains("not an array", errMsg);
        }

        [Fact]
        public async Task verify_that_we_handle_empty_change_object()
        {
            var mockAdapter = new Mock<IHubSpotAdapter>();

            var json_no_crmid = "{ \"crmid\": \"012345\", \"changes\": [] }";  // changes is empty

            var func = new DequeueContactDiffs(mockAdapter.Object);
            await func.Run(json_no_crmid, _errorQueue, _logger);

            // We can handle a missing changes object without fuss
            Assert.Empty(_errorQueue.Items);
        }

        [Fact]
        public async Task verify_that_we_handle_some_changes()
        {
            var mockAdapter = new Mock<IHubSpotAdapter>();

            var desiredResult = new HubSpotContactResult(HttpStatusCode.OK);

            mockAdapter.Setup(p => p.UpdateContactDetailsAsync(
                "012345", 
                It.IsAny<HubSpotContactProperties>(),                    
                It.IsAny<ILogger>(),                    
                It.IsAny<bool>())).ReturnsAsync(desiredResult);

            var json_no_crmid = "{ \"crmid\": \"012345\", \"changes\": [{\"name\": \"Street Address\", \"value\": \"18 Example Place\"},{\"name\": \"Phone\", \"value\": \"0451443455\"}] }";

            var func = new DequeueContactDiffs(mockAdapter.Object);
            await func.Run(json_no_crmid, _errorQueue, _logger);

            // We can handle a missing changes object without fuss
            Assert.Empty(_errorQueue.Items);
        }
    }
}
