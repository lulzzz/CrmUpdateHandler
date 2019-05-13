using CrmUpdateHandler;
using CrmUpdateHandler.Utility;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using CrmUpdateHandler.Utility;

namespace Test
{
    public class SynergyEmailTest : FunctionTest
    {
        /// <summary>
        /// This test verifies that we can extract text from the first variation of the Synergy email that we encountered.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async System.Threading.Tasks.Task Verify_We_Can_Extract_Fields_From_Email()
        {
            //var query = new Dictionary<string, StringValues>(); // If we want to test query params, put them here.
            //query.Add("messageId", "ABC123");

            /* Notes:
             * 
             * This style of email body has no line breaks and uses an href for the customer email.
             */

            // Load in the body of an actual Synergy email
            var body = File.ReadAllText(@".\\TestData\\SynergyEmail.txt", Encoding.UTF8);


            //var simulatedHttpRequest = this.HttpRequestSetup(query, body);
            var loggerMock = new Mock<ILogger>();

            var retval = await SynergyEmailParser.Parse(body, loggerMock.Object);
            Assert.NotNull(retval);

            Assert.Equal("000303902873", retval.rrn);
            Assert.Equal("TIMOTHY ARMSTRONG", retval.customername);
            Assert.Equal("292569720", retval.account);
            Assert.Equal("Lot 89 13 CABARITA RD ABBEY 6280 WA AU", retval.supplyaddress);
            Assert.Equal("0520016974", retval.meter);
            Assert.Equal("tim_armstrong@internode.on.net", retval.customeremail);
            //Assert.Equal("ABC123", retval.messageId); // When we can mock the EventGrid comment this back in
        }

        /// <summary>
        /// This test verifies that we can extract text from a different variant of the Synergy email.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async System.Threading.Tasks.Task Verify_We_Can_Extract_Fields_From_DoubleLineEmail()
        {
            //var query = new Dictionary<string, StringValues>(); // If we want to test query params, put them here.
            //query.Add("messageId", "ABC123");
            /* Notes:
             * 
             * This style of email body has line breaks and the customer email is inside a <p> element
             */

            var body = File.ReadAllText(@".\\TestData\\SynergyEmailDoubleLine.txt", Encoding.UTF8);
            var loggerMock = new Mock<ILogger>();

            var retval = await SynergyEmailParser.Parse(body, loggerMock.Object);
            Assert.NotNull(retval);

            Assert.Equal("000303925627", retval.rrn);
            Assert.Equal("Russell Bradshaw", retval.customername);
            Assert.Equal("141221770", retval.account);
            Assert.Equal("Lot 866 18 CARNEGIE DR DUNSBOROUGH 6281 WA AU", retval.supplyaddress);
            Assert.Equal("15D103218", retval.meter);
            Assert.Equal("russell.bradshaw@bigpond.com", retval.customeremail);
            //Assert.Equal("ABC123", retval.messageId); // When we can mock the EventGrid comment this back in
        }
    }
}
