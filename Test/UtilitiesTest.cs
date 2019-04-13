using CrmUpdateHandler;
using System;
using Xunit;

namespace Test
{
    public class UtilitiesTest
    {
        [Fact]
        public void NewContactPayload_ZeroPads_Vid()
        {
            var payload = new NewContactPayload("1234");
            Assert.Equal("001234", payload.contactId);
        }

        [Fact]
        public void UpdatedContactPayload_ZeroPads_Vid()
        {
            var payload = new UpdatedContactPayload("1234");
            Assert.Equal("001234", payload.contactId);
        }
    }
}
