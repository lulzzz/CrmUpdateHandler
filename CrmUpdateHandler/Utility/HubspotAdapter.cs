using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CrmUpdateHandler.Utility
{
    /// <summary>
    /// Centralises functionalities and resources for reading and writing against the Hubspot CRM.
    /// </summary>
    internal static class HubspotAdapter
    {
        // Singleton instance - this makes the Azure functions more scalable.
        private static readonly HttpClient httpClient;

        private static readonly string hapikey;
        private static readonly string sandbox_hapikey;

        /// <summary>
        /// A structure that serialises into a request body suitable to create a Contact via the HubSpot API
        /// </summary>
        public class ContactProperties
        {
            /// <summary>
            /// Creates a new instance of the ContactProperties object
            /// </summary>
            public ContactProperties()
            {
                this.properties = new List<PropertyValuePair>();
            }

            /// <summary>
            /// Gets a reference to a collection of contact properties that serialises with the name "properties"
            /// </summary>
            public List<PropertyValuePair> properties { get; private set; }

            /// <summary>
            /// Adds a new Contact property to the "properties" collection
            /// </summary>
            /// <param name="property"></param>
            /// <param name="value"></param>
            public void Add(string property, string value)
            {
                this.properties.Add(new PropertyValuePair(property, value));
            }
            /// <summary>
            /// The class that encapsulates the 'property' and the 'value' is internal
            /// </summary>
            [DebuggerDisplay("{property} = {value}")]
            public class PropertyValuePair
            {
                public PropertyValuePair(string property, string value)
                {
                    this.property = property;
                    this.value = value;
                }
                public string property { get; set; }
                public string value { get; set; }
            }

        }

        /// <summary>
        /// Static constructor performs a one-time initialisation of the httpClient and hubspot API key
        /// </summary>
        static HubspotAdapter()
        {
            // See https://docs.microsoft.com/en-us/azure/architecture/antipatterns/improper-instantiation/
            // for an explanation as to why this is better than 'using (var httplient = new HttpClient()) {}"
            httpClient = new HttpClient();

            hapikey = Environment.GetEnvironmentVariable("hapikey", EnvironmentVariableTarget.Process);
            sandbox_hapikey = Environment.GetEnvironmentVariable("sandbox_hapikey", EnvironmentVariableTarget.Process);
        }

        /// <summary>
        /// Retrieve a Contact from Hubspot using the unique contact ID
        /// </summary>
        /// <param name="contactId">The Hubspot Contact Id</param>
        /// <param name="fetchPreviousValues">A value to indicate whether to populate the oldXXX properties from the Hubspot 'versions' array</param>
        /// <returns></returns>
        internal static async Task<HubSpotContactResult> RetrieveHubspotContactById(string contactId, bool fetchPreviousValues, ILogger log, bool isTest = false)
        {
            // Check that the appropriate Hubspot API key was correctly retrieved in the static constructor
            var activeHapiKey = isTest ? sandbox_hapikey : hapikey;

            // Formulate the url:
            // GET /contacts/v1/contact/vid/:vid/profile
            var url = string.Format($"https://api.hubapi.com/contacts/v1/contact/vid/{contactId}/profile?hapikey={activeHapiKey}");
            //log.LogInformation("url: {0}", url);

            return await RetrieveHubspotContactWithUrl(url, fetchPreviousValues, log);
        }

        /// <summary>
        /// Retrieve a Contact from Hubspot using an email address
        /// </summary>
        /// <param name="email">The email address that uniquely identifies a Hubspot contact</param>
        /// <param name="fetchPreviousValues">A value to indicate whether to populate the oldXXX properties from the Hubspot 'versions' array</param>
        /// <returns>A Type containing the Contact</returns>
        /// <remarks>Sometimes we will want the old properties and the new (which Hubspot gives us). Other times, we will
        /// just want the current properties, as we will override them with an externally-sourced value</remarks>
        internal static async Task<HubSpotContactResult> RetrieveHubspotContactByEmailAddr(string email, bool fetchPreviousValues, ILogger log, bool isTest=false)
        {
            // Check that the appropriate Hubspot API key was correctly retrieved in the static constructor
            var activeHapiKey = isTest ? sandbox_hapikey : hapikey;

            // See https://developers.hubspot.com/docs/methods/contacts/get_contact_by_email
            var url = string.Format($"https://api.hubapi.com/contacts/v1/contact/email/{email}/profile?hapikey={activeHapiKey}");
            //log.LogInformation("url: {0}", url);

            return await RetrieveHubspotContactWithUrl(url, fetchPreviousValues, log);
        }

        /// <summary>
        /// Create a contact in Hubspot
        /// </summary>
        /// <param name="email"></param>
        /// <param name="firstname"></param>
        /// <param name="lastname"></param>
        /// <returns></returns>
        /// <see cref="https://developers.hubspot.com/docs/methods/contacts/create_contact"/>
        internal static async Task<HubSpotContactResult> CreateHubspotContactAsync(
            string email, 
            string firstname, 
            string lastname, 
            string preferredName, 
            string primaryPhone,
            string streetAddress1,
            string streetAddress2,
            string city,
            string state,
            string postcode,
            string leadStatus,
            bool installationRecordExists,
            ILogger log,
            bool isTest)
        {
            // Check that the appropriate Hubspot API key was correctly retrieved in the static constructor
            var activeHapiKey = isTest ? sandbox_hapikey : hapikey;

            // Check that the Hubspot API key was correctly retrieved in the static constructor
            if (string.IsNullOrEmpty(activeHapiKey))
            {
                return new HubSpotContactResult(HttpStatusCode.InternalServerError, "Hubspot API key not found");
            }

            if (!(new EmailAddressAttribute().IsValid(email)))
            {
                return new HubSpotContactResult(HttpStatusCode.InternalServerError, "New Contact email address not supplied");
            }

            // To send a Contact to Hubspot via the API we need an object that will serialise into a bunch of {property, value} pairs
            ContactProperties newContactProperties = AssembleContactProperties(
                email, 
                firstname, 
                lastname, 
                preferredName, 
                primaryPhone,
                streetAddress1,
                streetAddress2,
                city,
                state,
                postcode,
                leadStatus,
                installationRecordExists);

            if (newContactProperties == null)
            {
                return new HubSpotContactResult(HttpStatusCode.InternalServerError, "unhandled error assembling new contact command");
            }

            var url = string.Format($"https://api.hubapi.com/contacts/v1/contact/?hapikey={activeHapiKey}");

            //var dbg = JsonConvert.SerializeObject(newContactProperties);

            // Need to POST to Hubspot to create a contact
            log.LogInformation($"Posting to HubSpot to create {firstname} {lastname}");
            HttpResponseMessage response = await httpClient.PostAsJsonAsync(url, newContactProperties);

            HttpContent content = response.Content;

            // Check Status Code. 
            if (response.StatusCode == HttpStatusCode.OK)
            {
                log.LogInformation("Hubspot creation OK");
                // All good. Read the string out of the body
                string resultText = await content.ReadAsStringAsync();

                var newContactResponse = ConvertHubspotJsonToCanonicalContact(resultText, fetchPreviousValues: false, log: log);

                log.LogInformation("New Contact identifier is {0}", newContactResponse.contactId);

                var retval = new HubSpotContactResult(newContactResponse);
                return retval;
            }
            else if (response.StatusCode == HttpStatusCode.Conflict)
            {
                // The contact already exists. That's OK - we need to direct this contact to a human for review. 
                log.LogWarning("Contact already exists: {email}");

                // Let's read the whole contact back, then.
                var actualContact = await RetrieveHubspotContactByEmailAddr(email, false, log, isTest);

                var retval = new HubSpotContactResult(actualContact.Payload);
                retval.StatusCode = response.StatusCode;
                return retval;
            }
            else
            {
                // Some other error - return the status code and body to the caller of the function
                string resultText = await content.ReadAsStringAsync();
                log.LogError("Hubspot creation failed: {0}: {1}", response.StatusCode, resultText);
                return new HubSpotContactResult(response.StatusCode, resultText);
            }
        }

        /// <summary>
        /// Create a Deal in Hubspot
        /// </summary>
        /// <returns></returns>
        internal static async Task<HubSpotDealResult> CreateHubSpotDealAsync(
            int vid,
            string dealname,
            string salesPipeline,
            string initialStage,
            ILogger log,
            bool isTest)
        {
            // Check that the appropriate Hubspot API key was correctly retrieved in the static constructor
            var activeHapiKey = isTest ? sandbox_hapikey : hapikey;

            if (string.IsNullOrEmpty(activeHapiKey))
            {
                return new HubSpotDealResult(HttpStatusCode.InternalServerError, "Hubspot API key not found");
            }

            // TODO: Invoke  https://api.hubapi.com/crm-pipelines/v1/pipelines/deals?hapikey={activeHapiKey}
            // to retrieve all pipelines. Select the one with the label matching SalesPipeline, and get its pipelineId. 
            // Then select the stage with the label matching initialStage and get its stageId
            // log a bug with HubSpot, tell them that their error message on a "stage not found" is wrong - they use states from the default pipeline, not the nominated pipeline

            var newHubspotDeal = new HubSpotDealCreationStructure(vid, dealname);
            newHubspotDeal.Add("pipeline", salesPipeline);
            newHubspotDeal.Add("dealstage", initialStage);

            var url = string.Format($"https://api.hubapi.com/deals/v1/deal/?hapikey={activeHapiKey}");
            var dbg = JsonConvert.SerializeObject(newHubspotDeal);

            log.LogInformation($"Posting to HubSpot to create a Deal for {vid}");
            HttpResponseMessage response = await httpClient.PostAsJsonAsync(url, newHubspotDeal);

            HttpContent content = response.Content;

            // Check Status Code. 
            if (response.StatusCode == HttpStatusCode.OK)
            {
                log.LogInformation("Hubspot creation OK");

                // All good. Deserialise the response body as a specially-shaped HubSpotDeal object

                // Can we deserialise the content as a HubSpotDeal?
                string contentString = await content.ReadAsStringAsync();   // debug: But this is what we must deserialise into a hubspot deal.
                var hubspotDeal = await content.ReadAsAsync<HubSpotDeal>();

                if (isTest)
                {
                    //newContactResponse.contactId = "002001";    // Always testy webhookssen
                }

                log.LogInformation("New Deal identifier is {0}", hubspotDeal.dealId);

                var retval = new HubSpotDealResult(hubspotDeal);
                return retval;
            }
            else
            {
                // Some other error - return the status code and body to the caller of the function
                string resultText = await content.ReadAsStringAsync();
                log.LogError("Hubspot deal creation failed: {0}: {1}", response.StatusCode, resultText);
                return new HubSpotDealResult(response.StatusCode, resultText);
            }
        }


        /// <summary>
        /// Convert a bunch of properties to the name-value structure required by the HubSpot 'create' API
        /// </summary>
        /// <param name="email"></param>
        /// <param name="firstname"></param>
        /// <param name="lastname"></param>
        /// <param name="preferredName"></param>
        /// <param name="primaryPhone"></param>
        /// <param name="streetAddress"></param>
        /// <param name="city"></param>
        /// <param name="state"></param>
        /// <param name="postcode"></param>
        /// <param name="leadStatus"></param>
        /// <returns></returns>
        internal static ContactProperties AssembleContactProperties(
            string email, 
            string firstname, 
            string lastname, 
            string preferredName, 
            string primaryPhone,
            string streetAddress1,
            string streetAddress2,
            string city,
            string state,
            string postcode,
            string leadStatus,
            bool installationRecordExists)
        {
            var newContactProperties = new ContactProperties();

            // Need to sanitise the properties received here. 
            if (new EmailAddressAttribute().IsValid(email))
            {
                newContactProperties.Add("email", email);
            }
            else
            {
                // This should not occur, as it will be filtered out by the caller.
                return null;
            }

            if (UserInputIsValid(firstname))
            {
                newContactProperties.Add("firstname", firstname);
            }

            if (UserInputIsValid(lastname))
            {
                newContactProperties.Add("lastname", lastname);
            }

            if (UserInputIsValid(preferredName))
            {
                newContactProperties.Add("preferred_name", preferredName);
            }

            if (UserInputIsValid(primaryPhone))
            {
                newContactProperties.Add("phone", primaryPhone);
            }

            if (UserInputIsValid(streetAddress1, false) || UserInputIsValid(streetAddress2, false))
            {
                var comma = string.Empty;
                if (!string.IsNullOrEmpty(streetAddress1) && !string.IsNullOrEmpty(streetAddress2))
                {
                    comma = ", ";
                }
                var address = streetAddress1 + comma + streetAddress2;
                newContactProperties.Add("address", address); // Hubspot expects street, unit, apartment, etc to be concatenated.
            }

            if (UserInputIsValid(city))
            {
                newContactProperties.Add("city", city);
            }

            if (UserInputIsValid(state))
            {
                newContactProperties.Add("state", state);
            }

            if (UserInputIsValid(postcode))
            {
                newContactProperties.Add("zip", postcode);
            }

            newContactProperties.Add("hs_lead_status", ResolveLeadStatus(leadStatus));
            newContactProperties.Add("lifecyclestage", "lead"); // They are not just a subscriber
            newContactProperties.Add("installationrecordexists", installationRecordExists?"true":"false"); 

            return newContactProperties;
        }

        /// <summary>
        /// An extensible, centralised place to put validations for user input. Not sure it's adding value, but it's here to spark thought. Wish there was a library to do this.
        /// </summary>
        /// <param name="stringUnderTest"></param>
        /// <returns></returns>
        private static bool UserInputIsValid(string stringUnderTest, bool mustBePresent = true)
        {
            if (string.IsNullOrEmpty(stringUnderTest) && mustBePresent) return false;
            if (stringUnderTest.Length > 255) return false;
            if (stringUnderTest.Contains("<script")) return false;

            return true;
        }

        /// <summary>
        /// A special validator for the "lead status" enumeration, to ensure that the values for the hs_lead_status property
        /// match the internal values configured in HubSpot
        /// </summary>
        /// <param name="leadStatus"></param>
        /// <returns></returns>
        private static string ResolveLeadStatus(string leadStatus)
        {
            // Make sure we don't barf on a null
            if (string.IsNullOrEmpty(leadStatus))
            {
                leadStatus = "INTERESTED";
            }

            string retval = string.Empty;

            switch (leadStatus.ToUpper())
            {
                case "INTERESTED":
                    retval = "INTERESTED";
                    break;
                case "NOT INTERESTED":
                case "NOT_INTERESTED":
                    retval = "NOT_INTERESTED";
                    break;
                case "READY TO ENGAGE":
                case "READY_TO_ENGAGE":
                    retval = "READY_TO_ENGAGE";
                    break;
                case "INSTALLED":
                    retval = "INSTALLED";
                    break;
                case "WANTS ANOTHER SYSTEM":
                case "WANTS_ANOTHER_SYSTEM":
                    retval = "WANTS_ANOTHER_SYSTEM";
                    break;
                default:
                    break;
            }

            return retval;
        }

        /// <summary>
        /// Utility method that retrieves a Hubspot contact from the API and extracts the most important parameters into a structure.
        /// </summary>
        /// <param name="url">A valid GET url for retrieving a Hubspot contact. It's expected to have the hapikey on the url</param>
        /// <param name="fetchPreviousValues"></param>
        /// <returns></returns>
        private static async Task<HubSpotContactResult> RetrieveHubspotContactWithUrl(string url, bool fetchPreviousValues, ILogger log)
        {
            // Go get the contact from HubSpot using the supplied url
            HttpResponseMessage response = await httpClient.GetAsync(url);
            HttpContent content = response.Content;

            // Check Status Code. If we got the Contact OK, then raise the appropriate event.
            if (response.StatusCode == HttpStatusCode.OK)
            {
                // All good. Read the string out of the body
                string resultText = await content.ReadAsStringAsync();
                //log.LogInformation(resultText);
                var canonicalContact = ConvertHubspotJsonToCanonicalContact(resultText, fetchPreviousValues, log);

                var retval = new HubSpotContactResult(canonicalContact);
                return retval;
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // NotFound error
                return new HubSpotContactResult(response.StatusCode);
            }
            else
            {
                // Some other error
                string resultText = await content.ReadAsStringAsync();
                return new HubSpotContactResult(response.StatusCode, resultText);
            }

        }

        /// <summary>
        /// Converts a raw Hubspot API response into a generic Contact structure with the properties that we care about
        /// </summary>
        /// <param name="wholeContactText">The raw response body from the API call</param>
        /// <param name="fetchPreviousValues"></param>
        /// <returns></returns>
        private static CanonicalContact ConvertHubspotJsonToCanonicalContact(
            string wholeContactText, 
            bool fetchPreviousValues,
            ILogger log)
        {
            dynamic contact = ConvertStringToJson(wholeContactText);

            string vid = contact.vid;

            string firstName = contact.properties.firstname?.value;
            string lastName = contact.properties.lastname?.value;
            string preferredName = contact.properties.preferred_name?.value;
            string phone = contact.properties.phone?.value;
            string email = contact.properties.email?.value;
            string customerAddress = AssembleCustomerAddress(contact.properties);
            string jobTitle = contact.properties.jobtitle?.value;
            string leadStatus = contact.properties.hs_lead_status?.value;
            string installationrecordString = contact.properties.installationrecordexists?.value ?? "false";   // raw values are null, "false", "true" and ""
            bool installationrecordExists = (installationrecordString == "true");

            var oldFirstName = firstName;
            var oldLastName = lastName;
            var oldPreferredName = preferredName;
            var oldPhone = phone;
            var oldEmail = email;
            var oldCustomerAddress = customerAddress;
            var oldJobTitle = jobTitle;
            var oldLeadStatus = leadStatus;


            var restUri = contact["profile-url"].ToString();

            if (fetchPreviousValues)
            {
                oldFirstName = GetPreviousValue(contact.properties.firstname);
                oldLastName = GetPreviousValue(contact.properties.lastname);
                oldPreferredName = GetPreviousValue(contact.properties.preferred_name);
                oldPhone = GetPreviousValue(contact.properties.phone);
                oldEmail = GetPreviousValue(contact.properties.email);
                oldCustomerAddress = AssembleCustomerAddress(contact.properties, true);
                oldJobTitle = GetPreviousValue(contact.properties.jobtitle);
                oldLeadStatus = GetPreviousValue(contact.properties.hs_lead_status);
            }


            log.LogInformation($"CanonicalContact({contact.vid},{firstName},{lastName},{preferredName},{phone},{email},{customerAddress},{leadStatus})");
            var canonicalContact = new CanonicalContact(
                vid,
                firstName,
                lastName,
                preferredName,
                phone,
                email,
                customerAddress,
                leadStatus, 
                installationrecordExists)
            {
                oldFirstName = oldFirstName,
                oldLastName = oldLastName,
                oldPreferredName = oldPreferredName,
                oldPhone = oldPhone,
                oldEmail = oldEmail,
                oldcustomerAddress = oldCustomerAddress,

                jobTitle = jobTitle,
                oldJobTitle = oldJobTitle,

                oldLeadStatus = oldLeadStatus,

                restUri = restUri
            };

            return canonicalContact;
        }


        internal static string GetPreviousValue(dynamic prop)
        {
            var versions = prop?.versions;

            if (versions == null)
                return string.Empty;

            if (versions.Count < 2)
                return string.Empty;

            return versions[1]?.value;
        }

        /// <summary>
        /// Calculates a customer address from separate street, city, postcoe, etc fields
        /// </summary>
        /// <param name="properties"></param>
        /// <returns></returns>
        /// <remarks>This may need to become more sophisticated overtime</remarks>
        internal static string AssembleCustomerAddress(dynamic properties, bool usePrevious = false)
        {
            if (properties == null)
            {
                return string.Empty;
            }

            var dstreetAddress = usePrevious ? GetPreviousValue(properties.address) : properties.address?.value;
            var dcity = usePrevious ? GetPreviousValue(properties.city) : properties.city?.value;
            var dstate = usePrevious ? GetPreviousValue(properties.state) : properties.state?.value;
            var dpostcode = usePrevious ? GetPreviousValue(properties.zip) : properties.zip?.value;


            string streetAddress = dstreetAddress==null ? string.Empty : string.Format("{0}", dstreetAddress);
            string city = dcity==null ? string.Empty : string.Format("{0}", dcity);
            string state = dstate==null ? string.Empty : string.Format("{0}", dstate);
            string postcode = dpostcode == null ? string.Empty : string.Format("{0}", dpostcode);

            return AssembleCustomerAddress(streetAddress, city, state, postcode);
        }

        /// <summary>
        /// Assembles a singe-string address from multiple componenents
        /// </summary>
        /// <param name="streetAddress"></param>
        /// <param name="city"></param>
        /// <param name="state"></param>
        /// <param name="postcode"></param>
        /// <returns></returns>
        internal static string AssembleCustomerAddress(
            string streetAddress,
            string city,
            string state,
            string postcode)
        {
            // Build up bit by bit
            StringBuilder sb = new StringBuilder();

            // We take care that if components are missing, the result still makes sense, i.e. 
            // no random '\n' or spaces characters laying about if they are not needed.

            if (!string.IsNullOrEmpty(streetAddress))
            {
                sb.Append(streetAddress);
                sb.Append("\n");
            }


            if (!string.IsNullOrEmpty(city))
            {
                sb.Append(city);
                sb.Append("\n");
            }

            sb.Append(state);

            if (!string.IsNullOrEmpty(state) && !string.IsNullOrEmpty(postcode))
            {
                sb.Append(" ");
            }

            sb.Append(postcode);

            string address = sb.ToString();

            return address;
        }

        internal static dynamic ConvertStringToJson(string text)
        {
            dynamic json = JsonConvert.DeserializeObject(text);
            return json;
        }

    }
}
