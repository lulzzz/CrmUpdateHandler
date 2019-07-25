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

        public CanonicalContact(
            string vid, 
            string firstName,
            string lastName,
            string preferredName,
            string phone,
            string email,
            string customerAddress,
            //string jobTitle,
            string leadStatus,
            bool installationRecordExists)
            :this(vid)
        {
            this.firstName = firstName;

            this.lastName = lastName;

            this.preferredName = preferredName;

            this.phone = phone;

            this.email = email;

            this.customerAddress = customerAddress;

            //this.jobTitle = jobTitle;

            this.leadStatus = leadStatus;

            this.installationRecordExists = installationRecordExists;
        }

        public string contactId { get; set; }
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


        public string preferredName { get; set; }
        public string oldPreferredName { get; set; }

        public string phone { get; set; }
        public string oldPhone { get; set; }

        public string email { get; set; }
        public string oldEmail { get; set; }

        public string customerAddress { get; set; }
        public string oldcustomerAddress { get; set; }

        public string jobTitle { get; set; }
        public string oldJobTitle { get; set; }

        /// <summary>
        /// This governs whether an Installations record is created or not.
        /// </summary>
        public string leadStatus { get; set; }
        public string oldLeadStatus { get; set; }

        /// <summary>
        /// An app can set the InstallationRecordExists custom property to suppress the creation of an Installation record that 
        /// would normally happen when a customer record is created in the Ready To Engage state
        /// </summary>
        public bool installationRecordExists { get; set; }

        // Evidence of a crappy design: If adding a new field here, be sure to update NewContactPayload as well. 
        // TODO: Look at refactoring things so that NewContactPayload is a parent of this class, to eliminate duplicated declarations.

    }
}
