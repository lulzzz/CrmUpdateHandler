using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;

namespace CrmUpdateHandler
{
    /// <summary>
    /// This is the handler for the Hubspot webhook raised whenever a new Contact is created.
    /// Gathers all the details for a new contact and raises an event to EventGrid where multiple parties can subscribe to it.
    /// </summary>
    /// <remarks>https://crmupdatehandler.azurewebsites.net/api/RaiseNewContactEvent?hello=there
    /// </remarks>
    public static class RaiseNewContactEvent
    {

        [FunctionName("RaiseNewContactEvent")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("RaiseNewContactEvent trigger by HTTP");

            // Was this a sign-of-life request?
            string hello = req.Query["hello"];
            if (!string.IsNullOrEmpty(hello))
            {
                // We got a sign-of-life request. Just echo the hello string
                return new OkObjectResult(hello);
            }

            // Parse the request body as JSON. It should be an array containing one contact
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            // Verify that the request really came from Hubspot
            if (!HubspotRequestValidator.Validate(req, requestBody))
            {
                return new UnauthorizedResult();
            }


            dynamic data = JsonConvert.DeserializeObject(requestBody);

            // If body is empty or not JSON, just return 
            if (data == null)
            {
                return new OkResult();
            }

            // If JSON in the request body is not an array, just return
            Type t = data.GetType();
            if (!t.IsArray)
            {
                return new OkResult();
            }

            var firstItem = data[0];
            string objectId = firstItem?.objectId;

            // Now we have to reach back into Hubspot to retrieve the rest of the Contact details
            try
            {
                using (var client = new HttpClient())
                {

                }
            }
            catch (Exception ex)
            {

            }


            return objectId != null
                ? (ActionResult)new OkObjectResult($"Hello, {objectId}")
                : new BadRequestObjectResult("Please pass a name on the query string or in the request body");
        }
    }
}
