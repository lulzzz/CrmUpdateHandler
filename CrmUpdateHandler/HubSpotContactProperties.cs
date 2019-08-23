using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CrmUpdateHandler
{
    /// <summary>
    /// A structure that serialises into a request body suitable to create a Contact via the HubSpot API
    /// </summary>
    public class HubSpotContactProperties
    {
        /// <summary>
        /// Creates a new instance of the ContactProperties object
        /// </summary>
        public HubSpotContactProperties()
        {
            this.properties = new List<PropertyValuePair>();
        }

        /// <summary>
        /// Gets a reference to a collection of contact properties that serialises with the name "properties"
        /// </summary>
        public List<PropertyValuePair> properties { get; private set; }

        /// <summary>
        /// Adds a new Contact property to the "properties" collection
        /// </summary>
        /// <param name="property"></param>
        /// <param name="value"></param>
        public void Add(string property, string value)
        {
            this.properties.Add(new PropertyValuePair(property, value));
        }
        /// <summary>
        /// The class that encapsulates the 'property' and the 'value' is internal
        /// </summary>
        [DebuggerDisplay("{property} = {value}")]
        public class PropertyValuePair
        {
            public PropertyValuePair(string property, string value)
            {
                this.property = property;
                this.value = value;
            }
            public string property { get; set; }
            public string value { get; set; }
        }

    }

}
