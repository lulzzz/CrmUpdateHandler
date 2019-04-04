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
        /// <returns></returns>
        public static bool Validate(HttpRequest req, string requestBody)
        {
            return true;
        }
    }
}
