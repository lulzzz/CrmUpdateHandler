using System;
using System.Collections.Generic;
using System.Text;

namespace CrmUpdateHandler.Utility
{
    /// <summary>
    /// Representation of a HubSpot Deal, as returned from the API
    /// </summary>
    public class HubSpotDeal
    {
        public int dealId { get; set; }
        public int portal { get; set; }

        public bool isDeleted { get; set; }
    }
}
