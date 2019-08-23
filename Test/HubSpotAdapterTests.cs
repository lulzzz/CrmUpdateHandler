using CrmUpdateHandler.Utility;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Test.TestFixtures;
using Test.Utility;
using Xunit;

namespace Test
{
    /// <summary>
    /// A class to explicitly test the HubSpot Adapter
    /// </summary>
    /// <remarks>I wonder if we're getting close to the HubSpot API rate limit when we run this suite of tests.</remarks>
    public class HubSpotAdapterTests: IClassFixture<TestContactCreationFixture>, IClassFixture<EnvironmentSetupFixture>
    {
        private TestContactCreationFixture _contactCreationFixture;
        private EnvironmentSetupFixture _environmentSetupFixture;

        /// <summary>
        /// Stores log messages emitted by the code under test
        /// </summary>
        private ListLogger _logger;

        public HubSpotAdapterTests(TestContactCreationFixture contactCreationFixture, EnvironmentSetupFixture environmentSetupFixture)
        {
            this._contactCreationFixture = contactCreationFixture;
            this._environmentSetupFixture = environmentSetupFixture;
            this._logger = new ListLogger();
        }


        [Fact]
        public async Task verify_happy_case_contract_sent_update()
        {
            var contact = await _contactCreationFixture.CreateTestContact();

            var adapter = new HubspotAdapter();
            var response = await adapter.UpdateContractStatusAsync(_contactCreationFixture.TestContactEmail, "Sent", _logger, isTest: true);

            Assert.True(string.IsNullOrEmpty(response.ErrorMessage));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Read the contact back and verify that the status is "Sent"
        }

        [Fact]
        public async Task verify_happy_case_contract_signed_update()
        {
            var contact = await _contactCreationFixture.CreateTestContact();

            var adapter = new HubspotAdapter();
            var response = await adapter.UpdateContractStatusAsync(_contactCreationFixture.TestContactEmail, "Signed", _logger, isTest: true);

            Assert.True(string.IsNullOrEmpty(response.ErrorMessage));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task verify_happy_case_contract_rejected_update()
        {
            var contact = await _contactCreationFixture.CreateTestContact();

            var adapter = new HubspotAdapter();
            var response = await adapter.UpdateContractStatusAsync(_contactCreationFixture.TestContactEmail, "Rejected", _logger, isTest: true);

            Assert.True(string.IsNullOrEmpty(response.ErrorMessage));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task verify_invalid_contract_status_handling()
        {
            var contact = await _contactCreationFixture.CreateTestContact();

            var adapter = new HubspotAdapter();

            Exception ex = await Assert.ThrowsAsync<CrmUpdateHandlerException>(() => adapter.UpdateContractStatusAsync(_contactCreationFixture.TestContactEmail, "Bogus", _logger, isTest: true));

            Assert.Equal("Unrecognised contract status: 'Bogus'", ex.Message);
        }

        [Fact]
        public async Task verify_contract_status_update_handles_invalid_email_addr()
        {
            var contact = await _contactCreationFixture.CreateTestContact();

            var adapter = new HubspotAdapter();
            var response = await adapter.UpdateContractStatusAsync("bogus_email@example.com", "Sent", _logger, isTest: true);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
