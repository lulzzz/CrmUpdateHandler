using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CrmUpdateHandler.Utility
{
    /// <summary>
    /// Representation of a HubSpot Deal, for the purposes of writing to HubSpot.
    /// </summary>
    internal class HubSpotDealCreationStructure
    {
        /// <summary>
        /// Create a HubSpotDeal for the given contact ID
        /// </summary>
        /// <param name="vid"></param>
        public HubSpotDealCreationStructure(
            int vid,
            string dealName)
        {
            this.associations = new DealAssociations();
            this.properties = new List<NameValuePair>();

            this.associations.associatedVids.Add(vid);

            this.properties.Add(new NameValuePair("dealname", dealName));
        }

        public DealAssociations associations { get; private set; }

        /// <summary>
        /// Gets a reference to a collection of contact properties that serialises with the name "properties"
        /// </summary>
        public List<NameValuePair> properties { get; private set; }

        /// <summary>
        /// Adds a new name-value pair to the properties collection of the Deal
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void Add(string name, string value)
        {
            this.properties.Add(new NameValuePair(name, value));
        }

        /// <summary>
        /// The class that encapsulates the 'name' and the 'value' is internal
        /// </summary>
        [DebuggerDisplay("{name} = {value}")]
        public class NameValuePair
        {
            public NameValuePair(string name, string value)
            {
                this.name = name;
                this.value = value;
            }
            public string name { get; set; }
            public string value { get; set; }
        }

        /// <summary>
        /// A DealAssociation serialises as an object with 'associatedCompanyIds' and 'associatedVids' collections 
        /// </summary>
        public class DealAssociations
        {
            public DealAssociations()
            {
                this.associatedCompanyIds = new List<int>();
                this.associatedVids = new List<int>();
            }
            public List<int> associatedCompanyIds { get; private set; }
            public List<int> associatedVids { get; private set; }
        }
    }
}
