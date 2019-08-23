using CrmUpdateHandler.Utility;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrmUpdateHandler.Utility
{
    /// <summary>
    /// This is the wrapper object around an updated contact that is sent to event grid
    /// </summary>
    public class UpdatedContactEvent : EventGridEventBase
    {
        public UpdatedContactEvent(string id, CanonicalContact hubspotContact)
        {
            this.id = id;
            this.subject = "Starling.Crm.ContactUpdated";
            this.eventTime = DateTime.UtcNow;
            this.data = hubspotContact;

            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            this.eventType = "ContactProperties";

        }

        /// <summary>
        /// Gets the payload data object passed to the event grid
        /// </summary>
        public CanonicalContact data { get; private set; }
    }

}
