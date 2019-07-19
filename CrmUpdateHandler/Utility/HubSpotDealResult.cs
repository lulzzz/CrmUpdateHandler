namespace CrmUpdateHandler.Utility
{
    using System.Net;

    /// <summary>
    /// Represents the result of HubSpot API calls, including the status code, error messages and payload
    /// </summary>
    class HubSpotDealResult : HubSpotAccessResult
    {
        public HubSpotDealResult(HubSpotDeal deal)
            : base(HttpStatusCode.OK)
        {
            this.Payload = deal;
        }

        public HubSpotDealResult(HttpStatusCode code) : base(code)
        {
        }

        public HubSpotDealResult(HttpStatusCode code, string errorMessage) : base(code, errorMessage)
        {
        }

        public HubSpotDeal Payload { get; private set; }
    }
}
