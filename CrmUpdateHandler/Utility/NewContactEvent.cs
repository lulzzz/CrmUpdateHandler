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
        /// We get a complex structure back from Hubspot when we retrieve a Contact. We 
        /// only need a bit of it.
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
    public class NewContactPayload
    {
        public NewContactPayload(CanonicalContact hubspotContact)
        {
            this.contactId = hubspotContact.contactId;
            this.firstName = hubspotContact.firstName;
            this.lastName = hubspotContact.lastName;
            this.phone = hubspotContact.phone;
            this.email = hubspotContact.email;
            this.customerNameOnBill = hubspotContact.customerNameOnBill;
            this.meterNumber = hubspotContact.meterNumber;
            this.synergyAccountNumber = hubspotContact.synergyAccountNumber;
            this.synergyRrn = hubspotContact.synergyRrn;
            this.supplyAddress = hubspotContact.supplyAddress;
            this.jobTitle = hubspotContact.jobTitle;
            this.cep = hubspotContact.cep;
            this.contractStatus = hubspotContact.contractStatus;

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

        public string phone { get; private set; }

        public string email { get; private set; }

        public string customerNameOnBill { get; private set; }

        public string meterNumber { get; set; }

        public string synergyAccountNumber { get; set; }

        public string synergyRrn { get; private set; }

        public string supplyAddress { get; set; }

        public string jobTitle { get; set; }

        public string cep { get; set; }

        public string contractStatus { get; set; }



        public string restUri { get; private set; }
    }
}
