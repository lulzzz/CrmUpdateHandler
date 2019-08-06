namespace CrmUpdateHandler
{
    using System.Threading.Tasks;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using System.IO;
    using CrmUpdateHandler.Utility;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    /// A queue-oriented handler for HubSpot events. It just grabs the change-packet sent from HubSpot and
    /// returns as quickly as possible to prevent HubSpot from seeing a timeout. 
    /// </summary>
    /// <remarks>
    /// QueueAttribute needs the Microsoft.Azure.WebJobs.Extensions.Storage package from NuGet
    /// Dependency Injection became available in May 2019: https://docs.microsoft.com/en-us/azure/azure-functions/functions-dotnet-dependency-injection
    /// </remarks>
    public static class HandleAnyContactEventQ
    {
        [StorageAccount("AzureWebJobsStorage")]
        [FunctionName("HandleAnyContactEventQ")]
        [return: Queue("raw-hubspot-change-notifications")]
        public static async Task<string> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("HandleAnyContactEventQ triggered by HTTP request");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            // TODO: Verify that the request really came from Hubspot
            if (!HubspotRequestValidator.Validate(req, requestBody))
            {
                throw new CrmUpdateHandlerException("Invalid request");
            }

            // TODO: Make this log message subject to a switch
            //log.LogInformation(requestBody);

            // Just dump the request body into the queue and return.
            return requestBody;
        }
    }
}
