using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrmUpdateHandler.Utility
{
    /// <summary>
    /// A class the serialises and deserialises to the data packet coming from the OnContractStatusChanged event
    /// </summary>
    internal class ContractStatusNotification
    {
        [JsonProperty(PropertyName = "contractState")]
        internal string ContractState { get; set; }

        [JsonProperty(PropertyName = "installationId")]
        internal int InstallationId { get; set; }

        [JsonProperty(PropertyName = "customeremail")]
        internal string CustomerEmail { get; set; }


        [JsonProperty(PropertyName = "senddate")]
        internal DateTime SendDate { get; set; }


        [JsonProperty(PropertyName = "signingdate")]
        internal DateTime? SigningDate { get; set; }


        [JsonProperty(PropertyName = "rejectionreason")]
        internal string RejectionReason { get; set; }

    }
}
