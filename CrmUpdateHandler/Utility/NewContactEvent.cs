using System;
using System.Collections.Generic;
using System.Text;

namespace CrmUpdateHandler
{
    /// <summary>
    /// This is the wrapper object around the NewContactPayload that is sent to EventGrid
    /// </summary>
    public class NewContactEvent
    {
        public string id { get; set; }

        public string eventType { get; set; }

        public string subject { get; set; }

        /// <summary>
        /// Event time in ISO8601 format
        /// </summary>
        public DateTime eventTime { get; set; }

        public NewContactPayload data { get; set; }

        public string dataVersion => "1.0";
    }

    /// <summary>
    /// This is the representation of a new Contact that is sent to Event Grid
    /// </summary>
    public class NewContactPayload
    {
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

        public string firstName { get; set; }

        public string lastName { get; set; }

        public string phone { get; set; }

        public string email { get; set; }

        public string customerNameOnBill { get; set; }

        public string synergyRrn { get; set; }

        

        public string restUri { get; set; }
    }
}
