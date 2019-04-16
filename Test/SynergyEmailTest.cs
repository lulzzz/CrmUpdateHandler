using CrmUpdateHandler;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace Test
{
    public class SynergyEmailTest : FunctionTest
    {
        [Fact]
        public async System.Threading.Tasks.Task Verify_We_Can_Extract_Fields_From_Email()
        {
            var query = new Dictionary<string, StringValues>(); // If we wanted to test query params we'd put them here.

            // Load in the body of an actual Synergy email
            var body = File.ReadAllText(@".\\TestData\\SynergyEmail.txt", Encoding.UTF8);
            var simulatedHttpRequest = this.HttpRequestSetup(query, body);
            var loggerMock = new Mock<ILogger>();

            var result = await HandleSynergyRenewableApplicationEmail.Run(simulatedHttpRequest, log: loggerMock.Object);
            var okObjectResult = (OkObjectResult)result;
            Assert.NotNull(okObjectResult);
            Assert.Equal("{\"name\": \"TIMOTHY ARMSTRONG\",\"rrn\": \"000303902873\",\"email\": \"tim_armstrong@internode.on.net\"}", okObjectResult.Value);
        }
    }
}
