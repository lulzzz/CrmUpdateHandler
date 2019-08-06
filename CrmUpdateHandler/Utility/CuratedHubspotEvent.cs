using System;
using System.Collections.Generic;
using System.Text;

namespace CrmUpdateHandler.Utility
{
    /// <summary>
    /// This class is used in the logic that filters out Hubspot "Update" events if they're made redundant
    /// by the presence of a "New" event
    /// </summary>
    internal class CuratedHubspotEvent
    {

        /// <summary>
        /// Constructs an instance of a CuratedHubspotEvent for the occasions where a Contact is updated.
        /// </summary>
        /// <param name="objectId"></param>
        /// <param name="eventId"></param>
        public CuratedHubspotEvent(string objectId, string eventId, bool isNew)
        {
            this.Vid = objectId;
            this.EventId = eventId;
            this.IsNew = isNew;
        }

        /// <summary>
        /// Gets or sets the Contact Id
        /// </summary>
        public string Vid { get; set; }

        /// <summary>
        /// Gets or sets the event Id given to the event by Hubspot. We re-use it for the event Id we give to Event Grid
        /// </summary>
        public string EventId { get; set; }

        public bool IsNew { get; set; }
    }
}
