using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using AngleSharp;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace CrmUpdateHandler.Utility
{
    /// <summary>
    /// Encapsulate all the email parsing capability into one class, for testability
    /// </summary>
    public static class SynergyEmailParser
    {
        public static async Task<SynergyAccountData> Parse(string emailBody, ILogger log)
        {
            // We want to extract name, email and RRN
            string customerName = string.Empty;
            string synergyAcountNumber = string.Empty;
            string supplyAddress = string.Empty;
            string meterNumber = string.Empty;
            var rrn = string.Empty;
            var email = string.Empty;

            var success = true;

            // Parse it with a regex. Use one regex for each property 

            // The Synergy emails are so terribly variable that we can't trust their internal HTML structure from one email to the next.
            // But they look the same. So we use some safe(ish) Regex's to get chunks of text that are guaranteed to contain the test we 
            // want, and then we use the the AngleSharp library to extract the target text from the resultant shifting mess.

            // We search for our target data one item at a time so that when things break, our logging can tell us which part broke

            var context = BrowsingContext.New(Configuration.Default);   // The AngleSharp context object used by everything else.
            // Look for customer name

            var reCustomerName = @"Customer name.*?</td>.*?(<td.*?</td>)";

            var match = Regex.Match(emailBody, reCustomerName, RegexOptions.Singleline);    // NB Singleline changes the interpretation of . so it matches every character (instead of 'every character except \n')
            if (match.Success)
            {
                var myCapture = match.Groups[1].Value;
                var document = await context.OpenAsync(r => r.Content(myCapture));
                // could be <td><p><span>payload</p></span></td>, or <td><p>payload</p></td>, or ...
                // So far, every emnail we've seen at least has a <p>aragraph element 
                var p = document.QuerySelector("p");
                customerName = p.TextContent.Trim().Replace((char)160, ' ');    // we'll sometimes see leading \n characters here; we want to tidy things up with Trim()
                                                                                // Also, this (char)160 seems to arise from a &nbsp; character
                log.LogInformation("The email was for {0}", customerName);
            }
            else
            {
                log.LogError("Customer name not found in Synergy email");
                success = false;
            }

            // Look for "Retailer reference number"
            var reRRN = @"<strong>.*?Retailer reference number.*?</strong>.*?</p>.*?</td>.*?(<td.*?</td>)";

            match = Regex.Match(emailBody, reRRN, RegexOptions.Singleline);
            if (match.Success)
            {
                var myCapture = match.Groups[1].Value;
                var document = await context.OpenAsync(r => r.Content(myCapture));
                var p = document.QuerySelector("p");    // See remarks on previous capture block
                rrn = p.TextContent.Trim(); 
                log.LogInformation("The RRN was {0}", rrn);
            }
            else
            {
                log.LogError("RRN not found in Synergy email");
                success = false;
                // TODO: Trigger an error flow.
            }

            // Look for "Synergy Account number"
            var reSynergyAccountNumber = @"Synergy Account number.*?</td>.*?(<td.*?</td>)";

            match = Regex.Match(emailBody, reSynergyAccountNumber, RegexOptions.Singleline);
            if (match.Success)
            {
                var myCapture = match.Groups[1].Value;
                var document = await context.OpenAsync(r => r.Content(myCapture));
                var p = document.QuerySelector("p");    // See remarks on previous capture block
                synergyAcountNumber = p.TextContent.Trim();
                log.LogInformation("The Synergy Account number was {0}", synergyAcountNumber);
            }
            else
            {
                log.LogError("Synergy Account number not found in Synergy email");
                success = false;
                // TODO: Trigger an error flow.
            }


            // Look for "Supply Address". Crucial here is the use of the non-greedy match expression: .*?
            var reSupplyAddress = @"Supply address.*?</td>.*?(<td.*?</td>)";
            match = Regex.Match(emailBody, reSupplyAddress, RegexOptions.Singleline);
            if (match.Success)
            {
                var myCapture = match.Groups[1].Value;
                var document = await context.OpenAsync(r => r.Content(myCapture));
                var p = document.QuerySelector("p");    // See remarks on previous capture block
                supplyAddress = p.TextContent.Trim();
                log.LogInformation("The Synergy Account number was {0}", supplyAddress);
            }
            else
            {
                log.LogError("Supply Address not found in Synergy email");
                success = false;
                // TODO: Trigger an error flow.
            }

            // Look for "Meter Number" 
            var reMeterNumber = @"Meter number.*?</td>.*?(<td.*?</td>)";

            match = Regex.Match(emailBody, reMeterNumber, RegexOptions.Singleline);
            if (match.Success)
            {
                var myCapture = match.Groups[1].Value;
                var document = await context.OpenAsync(r => r.Content(myCapture));
                var p = document.QuerySelector("p");    // See remarks on previous capture block
                meterNumber = p.TextContent.Trim();
                log.LogInformation("The Meter Number was {0}", meterNumber);
            }
            else
            {
                log.LogError("Meter Number not found in Synergy email");
                success = false;
                // TODO: Trigger an error flow.
            }

            // Look for the customer's email address
            var reEmail = @"Customer.s email address.*?</td>.*?(<td.*?</td>)";
            match = Regex.Match(emailBody, reEmail, RegexOptions.Singleline);
            if (match.Success)
            {
                var myCapture = match.Groups[1].Value;
                var document = await context.OpenAsync(r => r.Content(myCapture));
                var p = document.QuerySelector("p");    // See remarks on previous capture block
                email = p.TextContent.Trim();
                log.LogInformation("The email address was {0}", email);
            }
            else
            {
                log.LogError("Customer email address not found in Synergy email");
                success = false;
                // TODO: Trigger an error flow.
            }

            // Otherwise we got everything we need. Create an object encapsulating all the properties extracted from the email
            var extractedSynergyFields = new SynergyAccountData
            {
                customername = customerName,
                account = synergyAcountNumber,
                supplyaddress = supplyAddress,
                meter = meterNumber,
                customeremail = email,
                rrn = rrn
            };

            return success ? extractedSynergyFields : null;
        }
    }
}
