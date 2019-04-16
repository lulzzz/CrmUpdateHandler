using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrmUpdateHandler
{
    internal static class HubspotRequestValidator
    {
        /// <summary>
        /// To validate that a request really came from Hubspot, concatenate the body 
        /// of the request with the application secret, and compare the result to the 'X-HubSpot-Signature' header
        /// </summary>
        /// <param name="req"></param>
        /// <param name="requestBody"></param>
        /// <see cref="https://developers.hubspot.com/docs/methods/webhooks/webhooks-overview"/>
        /// <returns></returns>
        public static bool Validate(HttpRequest req, string requestBody)
        {
            // TODO
            // From the doco: To verify this signature, concatenate the app secret of your application and the un-parsed
            // request body of the request you're handling, and get a SHA-256 hash of the result. Compare the resulting 
            // hash with the value of the X-HubSpot-Signature. If these values match, then this verifies that this request came from HubSpot.
            var validationHeader = req.Headers["X-HubSpot-Signature"];
            return true;
        }
    }
}
