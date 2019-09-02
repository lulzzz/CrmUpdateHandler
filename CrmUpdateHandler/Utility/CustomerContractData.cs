using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrmUpdateHandler.Utility
{
    /// <summary>
    /// A class the serialises and deserialises to the data packet coming from the OnContractStatusChanged event
    /// </summary>
    /// <remarks>PlicoInstallationHandler uses this class too, to raise and sink contract-status-change events</remarks>
    internal class CustomerContractData
    {
        /// <summary>
        /// Gets or sets a value indicating the state of the contract.
        /// </summary>
        [JsonProperty(PropertyName = "contractState")]
        public string ContractState { get; set; }

        /// <summary>
        /// We can selectively invoke handlers based on this event type
        ///  </summary>
        /// <remarks>Duplicates the event type passed to the event grid topic</remarks>
        [JsonProperty(PropertyName = "eventtype")]
        public string EventType { get; set; }

        /// <summary>
        /// The installation Id is what uniquely identifies an installation record
        /// </summary>
        [JsonProperty(PropertyName = "installationId")]
        public string InstallationId { get; set; }

        /// <summary>
        /// The Customer Name is convenient to have, e.g. for internal natifications
        /// </summary>
        [JsonProperty(PropertyName = "customerName")]
        public string CustomerName { get; set; }

        /// <summary>
        /// The customer email should always be present.
        /// </summary>
        [JsonProperty(PropertyName = "customeremail")]
        public string CustomerEmail { get; set; }

        [JsonProperty(PropertyName = "senddate")]
        public DateTime SendDate { get; set; }

        [JsonProperty(PropertyName = "signingdate")]
        public DateTime? SigningDate { get; set; }

        [JsonProperty(PropertyName = "forwardedFrom")]
        public string ForwardedFrom { get; set; }

        /// <summary>
        /// If the contract state is "Declined" (Rejected) then we capture the user-entered reason
        /// </summary>
        [JsonProperty(PropertyName = "rejectionreason")]
        public string RejectionReason { get; set; }
    }
}
