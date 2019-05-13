using System;
using System.Collections.Generic;
using System.Text;

namespace CrmUpdateHandler.Utility
{
    public class SynergyAccountData
    {
        /// <summary>
        /// Gets or sets the Customer name, in caps
        /// </summary>
        public string customername { get; set; }

        /// <summary>
        /// gets or sets the Synergy Retail Reference Number, required for the Western Power application
        /// </summary>
        public string rrn { get; set; }

        // Gets or sets the synergy account number
        public string account { get; set; }

        /// <summary>
        /// Gets or sets the supply address which must match exactly
        /// </summary>
        public string supplyaddress { get; set; }

        /// <summary>
        /// Gets or sets the meter number for this installation
        /// </summary>
        public string meter { get; set; }

        /// <summary>
        /// Gets or sets the customer email address
        /// </summary>
        public string customeremail { get; set; }

        /// <summary>
        /// Gets or sets the message Id of the original email that triggered this process
        /// </summary>
        public string messageId { get;set;}

    }
}
