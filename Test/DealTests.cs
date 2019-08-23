namespace Test
{
    using CrmUpdateHandler;
    using CrmUpdateHandler.Utility;
    using Test.TestFixtures;
    using Xunit;
    using System.Threading.Tasks;
    using Moq;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Net;

    /// <summary>
    /// Test class for Deal creation. A dummy contact is created in the HubSpot sandbox, and various
    /// deals are created against it.
    /// </summary>
    public class DealTests : IClassFixture<TestContactCreationFixture>, IClassFixture<EnvironmentSetupFixture>
    {
        private TestContactCreationFixture contactCreationFixture;
        private EnvironmentSetupFixture environmentSetupFixture;

        /// <summary>
        /// Constructor is called for every test. It is passed the fixture, which is instantiated only once, just
        /// before the first test is run, to set the environment variables used by the test
        /// </summary>
        /// <param name="contactCreationFixture"></param>
        public DealTests(TestContactCreationFixture contactCreationFixture, EnvironmentSetupFixture environmentSetupFixture)
        {
            this.contactCreationFixture = contactCreationFixture;
            this.environmentSetupFixture = environmentSetupFixture;
        }

        [Fact(Skip = "not using Deals any more")]
        public async Task Verify_Deal_Creation_Happy_Path()
        {
            var contact = await contactCreationFixture.CreateTestContact();
            var logger = new Mock<ILogger>();

            var adapter = new HubspotAdapter();

            // TODO: Update CreateHubSpotDealAsync so it discovers the pipeline ID and the stageID dynamically
            var dealResponse = await adapter.CreateHubSpotDealAsync(
                Convert.ToInt32(contact.contactId),
                "Automated Integration Test - Delete Me",
                "706439",   // Sales Pipeline 1 - you can see the ID in the URL
                "706470",   // Submitted Contract Information, 706470
                logger.Object, 
                isTest: true);

            Assert.NotNull(dealResponse);
            Assert.True(string.IsNullOrEmpty(dealResponse.ErrorMessage), dealResponse.ErrorMessage);
            Assert.Equal(HttpStatusCode.OK, dealResponse.StatusCode);
        }
    }
}
