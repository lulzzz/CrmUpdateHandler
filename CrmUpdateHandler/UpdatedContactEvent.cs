using System;
using System.Collections.Generic;
using System.Text;

namespace CrmUpdateHandler
{
    /// <summary>
    /// This is the wrapper object that is sent to event grid
    /// </summary>
    public class UpdatedContactEvent
    {
        public string id { get; set; }

        public string eventType { get; set; }

        public string subject { get; set; }

        /// <summary>
        /// Event time in ISO8601 format
        /// </summary>
        public DateTime eventTime { get; set; }

        public UpdatedContactPayload data { get; set; }

        public string dataVersion => "1.0";
    }

    /// <summary>
    /// This is the representation of an updated Contact that is sent to event grid
    /// </summary>
    /// <remarks>Held separately from NewContactPayload, because we anticipate it may one day need to support new and old values</remarks>
    public class UpdatedContactPayload
    {
        public string contactId { get; set; }

        public string firstName { get; set; }

        public string lastName { get; set; }

        public string phone { get; set; }

        public string email { get; set; }

        public string customerNameOnBill { get; set; }

        public string restUri { get; set; }
    }
}
