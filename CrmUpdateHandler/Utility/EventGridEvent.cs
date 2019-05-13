using System;
using System.Collections.Generic;
using System.Text;

namespace CrmUpdateHandler.Utility
{
    /// <summary>
    /// A base class to encapsulate all the common properties of an Azure Event Grid event
    /// </summary>
    public class EventGridEvent
    {
        /// <summary>
        /// Gets the boilerplate Event Grid id property
        /// </summary>
        public string id { get; protected set; }

        /// <summary>
        /// Gets the boilerplate event type that can be used to route events
        /// </summary>
        public string eventType { get; protected set; }

        /// <summary>
        /// Gets the subject of this event
        /// </summary>
        public string subject { get; protected set; }

        /// <summary>
        /// Event time in ISO8601 format
        /// </summary>
        public DateTime eventTime { get; protected set; }

        public string dataVersion => "1.0";

    }
}
