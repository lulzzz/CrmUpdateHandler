﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CrmUpdateHandler.Utility
{
    /// <summary>
    /// Centralises functionalities and resources for reading and writing against the Hubspot CRM.
    /// </summary>
    internal static partial class HubspotAdapter
    {
        // Singleton instance - this makes the Azure functions more scalable.
        private static readonly HttpClient httpClient;

        private static readonly string hapikey;

        /// <summary>
        /// An array of these properties will serialise to the correct form to send to Hubspot
        /// </summary>
        class NewContactProperty
        {
            public NewContactProperty(string property, string value)
            {
                this.property = property;
                this.value = value;
            }
            string property { get; set; }
            string value { get; set; }
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
        }

        /// <summary>
        /// Retrieve a Contact from Hubspot using the unique contact ID
        /// </summary>
        /// <param name="contactId">The Hubspot Contact Id</param>
        /// <param name="fetchPreviousValues">A value to indicate whether to populate the oldXXX properties from the Hubspot 'versions' array</param>
        /// <returns></returns>
        internal static async Task<CrmAccessResult> RetrieveHubspotContactById(string contactId, bool fetchPreviousValues)
        {
            // Formulate the url:
            // GET /contacts/v1/contact/vid/:vid/profile
            var url = string.Format($"https://api.hubapi.com/contacts/v1/contact/vid/{contactId}/profile?hapikey={hapikey}");
            //log.LogInformation("url: {0}", url);

            return await RetrieveHubspotContactWithUrl(url, fetchPreviousValues);
        }

        /// <summary>
        /// Retrieve a Contact from Hubspot using an email address
        /// </summary>
        /// <param name="email">The email address that uniquely identifies a Hubspot contact</param>
        /// <param name="fetchPreviousValues">A value to indicate whether to populate the oldXXX properties from the Hubspot 'versions' array</param>
        /// <returns>A Type containing the Contact</returns>
        /// <remarks>Sometimes we will want the old properties and the new (which Hubspot gives us). Other times, we will
        /// just want the current properties, as we will override them with an externally-sourced value</remarks>
        internal static async Task<CrmAccessResult> RetrieveHubspotContactByEmailAddr(string email, bool fetchPreviousValues)
        {
            // See https://developers.hubspot.com/docs/methods/contacts/get_contact_by_email
            var url = string.Format($"https://api.hubapi.com/contacts/v1/contact/email/{email}/profile?hapikey={hapikey}");
            //log.LogInformation("url: {0}", url);

            return await RetrieveHubspotContactWithUrl(url, fetchPreviousValues);
        }

        /// <summary>
        /// Create a contact in Hubspot
        /// </summary>
        /// <param name="email"></param>
        /// <param name="firstname"></param>
        /// <param name="lastname"></param>
        /// <returns></returns>
        /// <see cref="https://developers.hubspot.com/docs/methods/contacts/create_contact"/>
        internal static async Task<CrmAccessResult> CreateHubspotContactAsync(string email, string firstname, string lastname, string primaryPhone)
        {
            // Check that the Hubspot API key was correctly retrieved in the static constructor
            if (string.IsNullOrEmpty(hapikey))
            {
                return new CrmAccessResult(HttpStatusCode.InternalServerError, "hapi key not found");
            }

            var properties = new List<NewContactProperty>();

            // Need to sanitise the properties received here. 
            if (new EmailAddressAttribute().IsValid("email"))
            {
                properties.Add(new NewContactProperty("email", email));
            }
            else
            {
                return new CrmAccessResult(HttpStatusCode.InternalServerError, "New Contact email address not supplied");
            }

            if (UserInputIsValid(firstname))
            {
                properties.Add(new NewContactProperty("firstname", firstname));
            }

            if (UserInputIsValid(lastname))
            {
                properties.Add(new NewContactProperty("lastname", lastname));
            }

            if (UserInputIsValid(primaryPhone))
            {
                properties.Add(new NewContactProperty("phne", primaryPhone));
            }

            var url = string.Format($"https://api.hubapi.com/contacts/v1/contact/?hapikey={hapikey}");

            // Need to POST to Hubspot to create a contact
            HttpResponseMessage response = await httpClient.PostAsJsonAsync(url, properties);

            HttpContent content = response.Content;

            // Check Status Code. 
            if (response.StatusCode == HttpStatusCode.OK)
            {
                // All good. Read the string out of the body
                string resultText = await content.ReadAsStringAsync();

                var newContactResponse = ConvertHubspotJsonToCanonicalContact(resultText, fetchPreviousValues: false);

                var retval = new CrmAccessResult(newContactResponse);
                return retval;
            }
            else
            {
                // Some other error
                string resultText = await content.ReadAsStringAsync();
                return new CrmAccessResult(response.StatusCode, resultText);
            }
        }

        /// <summary>
        /// An extensible, centralised place to put validations for user input. Not sure it's adding value, but it's here to spark thought. Wish there was a library to do this.
        /// </summary>
        /// <param name="stringUnderTest"></param>
        /// <returns></returns>
        private static bool UserInputIsValid(string stringUnderTest)
        {
            if (string.IsNullOrEmpty(stringUnderTest)) return false;
            if (stringUnderTest.Length > 255) return false;
            if (stringUnderTest.Contains("<script")) return false;

            return true;
        }

        /// <summary>
        /// Utility method that retrieves a Hubspot contact from the API and extracts the most important parameters into a structure.
        /// </summary>
        /// <param name="url">A valid GET url for retrieving a Hubspot contact</param>
        /// <param name="fetchPreviousValues"></param>
        /// <returns></returns>
        private static async Task<CrmAccessResult> RetrieveHubspotContactWithUrl(string url, bool fetchPreviousValues)
        {
            // Check that the Hubspot API key was correctly retrieved in the static constructor
            if (string.IsNullOrEmpty(hapikey))
            {
                return new CrmAccessResult(HttpStatusCode.InternalServerError, "hapi key not found");
            }

            // Go get the contact from HubSpot using the supplied url
            HttpResponseMessage response = await httpClient.GetAsync(url);
            HttpContent content = response.Content;

            // Check Status Code. If we got the Contact OK, then raise the appropriate event.
            if (response.StatusCode == HttpStatusCode.OK)
            {
                // All good. Read the string out of the body
                string resultText = await content.ReadAsStringAsync();
                //log.LogInformation(resultText);
                var canonicalContact = ConvertHubspotJsonToCanonicalContact(resultText, fetchPreviousValues);

                var retval = new CrmAccessResult(canonicalContact);
                return retval;
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // NotFound error
                return new CrmAccessResult(response.StatusCode);
            }
            else
            {
                // Some other error
                string resultText = await content.ReadAsStringAsync();
                return new CrmAccessResult(response.StatusCode, resultText);
            }

        }

        /// <summary>
        /// Converts a raw Hubspot API response into a generic Contact structure with the properties that we care about
        /// </summary>
        /// <param name="wholeContactText">The raw response body from the API call</param>
        /// <param name="fetchPreviousValues"></param>
        /// <returns></returns>
        private static CanonicalContact ConvertHubspotJsonToCanonicalContact(string wholeContactText, bool fetchPreviousValues)
        {
            dynamic contact = ConvertStringToJson(wholeContactText);

            var firstName = contact.properties.firstname?.value;
            var lastName = contact.properties.lastname?.value;
            var phone = contact.properties.phone?.value;
            var email = contact.properties.email?.value;
            var customerNameOnBill = contact.properties.customer_name_on_bill?.value;
            var meterNumber = contact.properties.meter_number?.value;
            var synergyAccountNumber = contact.properties.synergy_account_no?.value;
            var synergyRrn = contact.properties.retailer_reference_number?.value;
            var supplyAddress = contact.properties.supply_address?.value;
            var jobTitle = contact.properties.jobtitle?.value;
            var cep = contact.properties.cep?.value;
            var contractStatus = contact.properties.contract_status?.value;

            var oldFirstName = firstName;
            var oldLastName = lastName;
            var oldPhone = phone;
            var oldEmail = email;
            var oldCustomerNameOnBill = customerNameOnBill;
            var oldMeterNumber = meterNumber;
            var oldSynergyAccountNumber = synergyAccountNumber;
            var oldSynergyRrn = synergyRrn;
            var oldSupplyAddress = supplyAddress;
            var oldJobTitle = jobTitle;
            var oldCep = cep;
            var oldContractStatus = contractStatus;

            var restUri = contact["profile-url"].ToString();

            if (fetchPreviousValues)
            {
                oldFirstName = GetPreviousValue(contact.properties.firstname);
                oldLastName = GetPreviousValue(contact.properties.lastname);
                oldPhone = GetPreviousValue(contact.properties.phone);
                oldEmail = GetPreviousValue(contact.properties.email);
                oldCustomerNameOnBill = GetPreviousValue(contact.properties.customer_name_on_bill);
                oldMeterNumber = GetPreviousValue(contact.properties.meter_number);
                oldSynergyAccountNumber = GetPreviousValue(contact.properties.synergy_account_no);
                oldSynergyRrn = GetPreviousValue(contact.properties.retailer_reference_number);
                oldSupplyAddress = GetPreviousValue(contact.properties.supply_address);
                oldJobTitle = GetPreviousValue(contact.properties.jobtitle);
                oldCep = GetPreviousValue(contact.properties.cep);
                oldContractStatus = GetPreviousValue(contact.properties.contract_status);
            }

            var canonicalContact = new CanonicalContact(Convert.ToString(contact.vid))
            {
                firstName = firstName,
                oldFirstName = oldFirstName,

                lastName = lastName,
                oldLastName = oldLastName,

                phone = phone,
                oldPhone = oldPhone,

                email = email,
                oldEmail = oldEmail,

                customerNameOnBill = customerNameOnBill,
                oldCustomerNameOnBill = oldCustomerNameOnBill,

                meterNumber = meterNumber,
                oldMeterNumber = oldMeterNumber,

                synergyAccountNumber = synergyAccountNumber,
                oldSynergyAccountNumber = oldSynergyAccountNumber,

                synergyRrn = synergyRrn,
                oldSynergyRrn = oldSynergyRrn,

                supplyAddress = supplyAddress,
                oldSupplyAddress = oldSupplyAddress,

                jobTitle = jobTitle,
                oldJobTitle = oldJobTitle,

                cep = cep,
                oldCep = oldCep,

                contractStatus = contractStatus,
                oldContractStatus = oldContractStatus,

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

        internal static dynamic ConvertStringToJson(string text)
        {
            dynamic json = JsonConvert.DeserializeObject(text);
            return json;
        }

    }
}