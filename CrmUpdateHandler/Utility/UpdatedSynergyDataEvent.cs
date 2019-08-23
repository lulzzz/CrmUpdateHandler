using System;
using System.Collections.Generic;
using System.Text;

namespace CrmUpdateHandler.Utility
{
    /// <summary>
    /// This is the wrapper object around an updated Installation data that is sent to event grid
    /// </summary>
    public class UpdatedSynergyDataEvent : EventGridEventBase
    {
        public UpdatedSynergyDataEvent(string id, string eventType, SynergyAccountData installation)
        {
            this.id = id;
            this.subject = "Starling.Installations.InstallationUpdated";
            this.eventTime = DateTime.UtcNow;
            this.eventType = eventType;
            this.data = installation;
        }

        /// <summary>
        /// Gets the payload data object passed to the event grid
        /// </summary>
        public SynergyAccountData data { get; private set; }
    }
}
