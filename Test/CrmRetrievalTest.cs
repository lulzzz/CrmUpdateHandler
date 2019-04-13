using CrmUpdateHandler;
using System;
using Xunit;

namespace Test
{
    public class CrmRetrievalTest
    {
        /// <summary>
        /// This test relies on an environment variable called 'hapikey' being correctly set. Note that after setting this, Visual Studio must be restarted
        /// in order to see it. 
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async System.Threading.Tasks.Task Janine_Pittaway_Is_Retrieved_SuccessfullyAsync()
        {
            var retrievalResult = await RetrieveContactByEmailAddr.RetrieveHubspotContactIdByEmailAdd("jpittaway@brightcommunications.com.au");
            Assert.True(string.IsNullOrEmpty(retrievalResult.ErrorMessage), retrievalResult.ErrorMessage);
            Assert.Equal("1551", retrievalResult.ContactId);
        }
    }
}
