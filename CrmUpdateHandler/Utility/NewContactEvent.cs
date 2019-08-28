using System;
using System.Collections.Generic;
using System.Text;

namespace CrmUpdateHandler.Utility
{
    /// <summary>
    /// This is the wrapper object around the NewContactPayload that is sent to EventGrid
    /// </summary>
    public class NewContactEvent
    {
        /// <summary>
        /// Creates an event structure containing a CanonicalContact, suitable for passing to an EventGrid topic
        /// </summary>
        public NewContactEvent(string eventId, CanonicalContact hubSpotContact)
        {
            this.id = eventId;
            this.eventType = "NewContact";
            subject = "Starling.Crm.ContactCreated";
            this.eventTime = DateTime.UtcNow;
            data = new NewContactPayload(hubSpotContact);

        }

        public string id { get; private set; }

        public string eventType { get; private set; }

        public string subject { get; private set; }

        /// <summary>
        /// Event time in ISO8601 format
        /// </summary>
        public DateTime eventTime { get; private set; }

        public NewContactPayload data { get; private set; }

        public string dataVersion => "1.0";
    }

    /// <summary>
    /// This is the representation of a new Contact that is sent to Event Grid
    /// </summary>
    /// <remarks>TODO: Try and refactor this class so it's a parent of <see cref="CanonicalContact"/> to eliminate the repeated fields.  </remarks>
    public class NewContactPayload
    {
        public NewContactPayload(CanonicalContact hubspotContact)
        {
            this.contactId = hubspotContact.contactId;
            this.firstName = hubspotContact.firstName;
            this.lastName = hubspotContact.lastName;
            this.preferredName = hubspotContact.preferredName;
            this.phone = hubspotContact.phone;
            this.email = hubspotContact.email;
            this.streetAddress = hubspotContact.streetAddress;
            this.city = hubspotContact.city;
            this.state = hubspotContact.state;
            this.postcode = hubspotContact.postcode;
            this.customerAddress = hubspotContact.customerAddress;
            this.leadStatus = hubspotContact.leadStatus;

            this.installationRecordExists = hubspotContact.installationRecordExists;
            this.restUri = hubspotContact.restUri;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="vid">the 4-digit ID from Hubspot</param>
        public NewContactPayload(string vid)
        {
            // Pad the contact id with leading 0s
            this.contactId = vid.PadLeft(6, '0');
        }

        public string contactId { get; private set; }

        public string firstName { get; private set; }

        public string lastName { get; private set; }

        public string fullNamePeriodSeparated => (this.firstName + "." + this.lastName).Trim('.');

        public string fullName => (this.firstName + " " + this.lastName).Trim(' ');

        public string preferredName { get; private set; }

        public string phone { get; private set; }

        public string customerAddress { get; private set; }

        public string email { get; private set; }

        public string streetAddress { get; private set; }

        public string city { get; private set; }

        public string state { get; private set; }

        public string postcode { get; private set; }

        public string leadStatus { get; set; }

        public string restUri { get; private set; }

        /// <summary>
        /// An app can set the InstallationRecordExists custom property to suppress the creation of an Installation record that 
        /// would normally happen when a customer record is created in the Ready To Engage state
        /// </summary>
        public bool installationRecordExists { get; set; }
    }
}
