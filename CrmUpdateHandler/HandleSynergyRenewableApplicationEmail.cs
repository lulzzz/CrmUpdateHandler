using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace CrmUpdateHandler
{
    public static class HandleSynergyRenewableApplicationEmail
    {
        /// <summary>
        /// Handler function triggered by a Flow when a Synergy email lands in the Approvals@plicoenergy.com mailbox
        /// It contains the Synergy RRN, which we want to pick up, to update the CRM.
        /// </summary>
        /// <param name="req"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        [FunctionName("HandleSynergyRenewableApplicationEmail")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            // Was this a sign-of-life request? (i.e. somebody just passing the 'hello' param to see if the function is available)
            string hello = req.Query["hello"];
            if (!string.IsNullOrEmpty(hello))
            {
                // We got a sign-of-life request. Just echo the hello string and exit
                return new OkObjectResult(hello);
            }

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            //log.LogInformation(requestBody);

            // We want to extract name, email and RRN
            string name = string.Empty;
            var rrn = string.Empty;
            var email = string.Empty;


            // Parse it with a regex. Do in 3 steps, so when things break, our logging can tell us which part broke
            //var reNameInCaps = @"you\shave\ssuccessfully\scompleted\sthe\srenewable\senergy\ssystem\sapplication\sfor\s(.*)\.\sPlease\sfind\syour\sretailer\sreference\snumber\sbelow";
            var reNameInCaps = @"you\shave\ssuccessfully\scompleted\sthe\srenewable\senergy.*system\sapplication\sfor\s(.*)\.\sPlease\sfind\syour\sretailer\sreference\snumber\sbelow";

            //var reRRN = @"<p.*<strong><span.*Retailer reference number</span></strong>.*</p>\s</td.*>\s<p.*><span.*>(.*)</span></p>";
                  var reRRN = @"<strong><span.*Retailer reference number</span></strong><span.*></span></p>\r?.</td>.*(\d{12})</span>";

            //var reEmail = @"Customer's email address</span></p>\s</td>\s<td.*<a href=.mailto:(.*).>(.*)</a></span></p>";
            var reEmail = @"<a href=.mailto:(.*).>(.*)</a></span></p>";

            var proceedToRetrieveCrmContact = true;

            var match = Regex.Match(requestBody, reNameInCaps);
            if (match.Success)
            {
                name = match.Groups[1].Value;
                log.LogInformation("The email was for {0}", name);
            }
            else
            {
                log.LogError("Name not found in Synergy email");
                proceedToRetrieveCrmContact = false;
                // TODO: Trigger an error flow.
            }

            match = Regex.Match(requestBody, reRRN,RegexOptions.Singleline);    // NB Singleline changes the interpretation of . so it matches every character (instead of 'every character except \n')
            if (match.Success)
            {
                rrn = match.Groups[1].Value;
                log.LogInformation("The RRN was {0}", rrn);
            }
            else
            {
                log.LogError("RRN not found in Synergy email");
                proceedToRetrieveCrmContact = false;
                // TODO: Trigger an error flow.
            }

            match = Regex.Match(requestBody, reEmail);
            if (match.Success)
            {
                email = match.Groups[1].Value;
                log.LogInformation("The email address was {0}", email);
            }
            else
            {
                log.LogError("Customer email address not found in Synergy email");
                proceedToRetrieveCrmContact = false;
                // TODO: Trigger an error flow.
            }

            if (proceedToRetrieveCrmContact)
            {
                // TODO: Refactor RetrieveContactByEmailAddr to return the whole contact, and construct an UpdatedContact object
                // to pass to event grid. 
                // Then construct two Flow handlers, to update the CRM contact, and to update the Sharepoint table, too
            }

            var retval = string.Format("{{\"name\": \"{0}\",\"rrn\": \"{1}\",\"email\": \"{2}\"}}",name,rrn,email);
            return new OkObjectResult(retval);





        }
    }
}
