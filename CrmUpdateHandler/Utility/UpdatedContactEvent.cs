using CrmUpdateHandler.Utility;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrmUpdateHandler.Utility
{
    /// <summary>
    /// This is the wrapper object around an updated contact that is sent to event grid
    /// </summary>
    public class UpdatedContactEvent : EventGridEvent
    {
        public UpdatedContactEvent(string id, string propertyName, CanonicalContact hubspotContact)
        {
            this.id = id;
            this.subject = "Starling.Crm.ContactUpdated";
            this.eventTime = DateTime.UtcNow;
            this.data = hubspotContact;

            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentNullException(nameof(propertyName));
            }

            // The Event Type can be used in the Azure Subscription-configuration UI to trigger only on certain types of changes
            string eventTypeName = "Other";
            switch (propertyName)
            {
                case "firstname":
                    eventTypeName = "Name";
                    break;
                case "lastname":
                    eventTypeName = "Name";
                    break;
                case "phone":
                    eventTypeName = "Phone";
                    break;
                case "email":
                    eventTypeName = "Email";
                    break;
                case "preferred_name":
                    eventTypeName = "PreferredName";
                    break;
                case "address":
                case "city":
                case "state":
                case "zip":
                    eventTypeName = "CustomerAddress";
                    break;
                case "jobtitle":
                    eventTypeName = "JobTitle";
                    break;
                default:
                    break;
            }

            this.eventType = eventTypeName;

        }

        /// <summary>
        /// Gets the payload data object passed to the event grid
        /// </summary>
        public CanonicalContact data { get; private set; }
    }

}
