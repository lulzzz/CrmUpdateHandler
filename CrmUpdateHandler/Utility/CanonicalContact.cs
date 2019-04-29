using System;
using System.Collections.Generic;
using System.Text;

namespace CrmUpdateHandler.Utility
{
    public class CanonicalContact
    {
        /// <summary>
        /// Only internal subclasses can use this constructor
        /// </summary>
        internal CanonicalContact()
        {

        }

        /// <summary>
        /// This is the representation of a generic Contact, as it might be retrieved from Hubspot
        /// </summary>
        /// <param name="vid"></param>
        public CanonicalContact(string vid)
        {
            this.contactId = vid.PadLeft(6, '0');
        }

        public string contactId { get; private set; }
        public string restUri { get; set; }


        public string firstName { get; set; }
        public string oldFirstName { get; set; }

        public string lastName { get; set; }
        public string oldLastName { get; set; }

        /// <summary>
        /// Gets a full name of the form 'First', 'Last' or 'First.Last'
        /// </summary>
        public string fullNamePeriodSeparated => (this.firstName + "." + this.lastName).Trim('.');
        public string oldFullNamePeriodSeparated => (this.oldFirstName + "." + this.oldLastName).Trim('.');


        /// <summary>
        /// Gets a full name, space-separated.
        /// </summary>
        public string fullName => (this.firstName + " " + this.lastName).Trim(' ');
        public string oldFullName => (this.oldFirstName + " " + this.oldLastName).Trim(' ');


        public string phone { get; set; }
        public string oldPhone { get; set; }

        public string email { get; set; }
        public string oldEmail { get; set; }

        public string customerNameOnBill { get; set; }
        public string oldCustomerNameOnBill { get; set; }

        public string meterNumber { get; set; }
        public string oldMeterNumber { get; set; }

        public string synergyAccountNumber { get; set; }
        public string oldSynergyAccountNumber { get; set; }

        public string synergyRrn { get; set; }
        public string oldSynergyRrn { get; set; }

        public string supplyAddress { get; set; }
        public string oldSupplyAddress { get; set; }

        public string jobTitle { get; set; }
        public string oldJobTitle { get; set; }

        public string cep { get; set; }
        public string oldCep { get; set; }

        public string contractStatus { get; set; }
        public string oldContractStatus { get; set; }

    }
}
