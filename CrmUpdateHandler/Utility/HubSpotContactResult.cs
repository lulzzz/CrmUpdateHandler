namespace CrmUpdateHandler.Utility
{
    using System.Net;

    /// <summary>
    /// Represents the result of accessing the HubSpot CRM to retrieve, update or create a contact. 
    /// Contains the details of a Contact, as well as the status flags from the CRUD operation itself. 
    /// </summary>
    public class HubSpotContactResult : HubSpotAccessResult
    {
        public HubSpotContactResult(CanonicalContact contact)
            :base(HttpStatusCode.OK)
        {
            this.Payload = contact;
        }

        public HubSpotContactResult(HttpStatusCode code) : base(code)
        {
        }

        public HubSpotContactResult(HttpStatusCode code, string errorMessage) : base(code, errorMessage)
        {
        }

        public CanonicalContact Payload { get; private set; }
    }
}
