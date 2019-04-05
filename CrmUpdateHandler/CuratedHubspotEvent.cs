using System;
using System.Collections.Generic;
using System.Text;

namespace CrmUpdateHandler
{
    /// <summary>
    /// This class is used in the logic that filters out Hubspot "Update" events if they're made redundant
    /// by the presence of a "New" event
    /// </summary>
    internal class CuratedHubspotEvent
    {
        public CuratedHubspotEvent(string objectId, string eventId, bool isnew)
        {
            this.Vid = objectId;
            this.EventId = eventId;
            this.IsNew = isnew;
        }

        public string Vid { get; set; }

        /// <summary>
        /// Event Id given to the event by Hubspot. We re-use it for the event Id we give to Event Grid
        /// </summary>
        public string EventId { get; set; }

        public bool IsNew { get; set; }
    }
}
