using CrmUpdateHandler;
using CrmUpdateHandler.Utility;
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
            var payload = new CanonicalContact("1234");
            Assert.Equal("001234", payload.contactId);
        }

        [Fact]
        public void New_Contact_Generates_FullNames_Correctly()
        {
            var contact = new CanonicalContact("1234")
            {
                firstName = "Jack",
                lastName = "Mack"
            };

            var newContactPayload = new NewContactPayload(contact);

            Assert.Equal("Jack.Mack", newContactPayload.fullNamePeriodSeparated);
            Assert.Equal("Jack Mack", newContactPayload.fullName);
        }


        [Fact]
        public void Updated_Contact_Generates_FullNames_Correctly()
        {
            var contact = new CanonicalContact("1234")
            {
                firstName = "Jack",
                lastName = "Mack"
            };

            Assert.Equal("Jack.Mack", contact.fullNamePeriodSeparated);
            Assert.Equal("Jack Mack", contact.fullName);

            contact.lastName = string.Empty;
            Assert.Equal("Jack", contact.fullNamePeriodSeparated);
            Assert.Equal("Jack", contact.fullName);

            contact.firstName = string.Empty;
            contact.lastName = "Foo";
            Assert.Equal("Foo", contact.fullNamePeriodSeparated);
            Assert.Equal("Foo", contact.fullName);
        }



    }
}
