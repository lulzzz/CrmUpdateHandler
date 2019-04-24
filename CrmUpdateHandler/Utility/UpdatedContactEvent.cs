using CrmUpdateHandler.Utility;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrmUpdateHandler.Utility
{
    /// <summary>
    /// This is the wrapper object around an updated contact that is sent to event grid
    /// </summary>
    public class UpdatedContactEvent
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
                case "customer_name_on_bill":
                    eventTypeName = "CustomerNameOnBill";
                    break;
                case "meter_number":
                    eventTypeName = "MeterNumber";
                    break;
                case "synergy_account_no":
                    eventTypeName = "SynergyAccountNo";
                    break;
                case "supply_address":
                    eventTypeName = "SupplyAddress";
                    break;
                case "jobtitle":
                    eventTypeName = "JobTitle";
                    break;
                case "retailer_reference_number":
                    eventTypeName = "RetailerReferenceNumber";
                    break;
                default:
                    break;
            }

            this.eventType = eventTypeName;

        }

        public string id { get; private set; }

        public string eventType { get; private set; }

        public string subject { get; private set; }

        /// <summary>
        /// Event time in ISO8601 format
        /// </summary>
        public DateTime eventTime { get; private set; }

        public CanonicalContact data { get; private set; }

        public string dataVersion => "1.0";
    }

}
