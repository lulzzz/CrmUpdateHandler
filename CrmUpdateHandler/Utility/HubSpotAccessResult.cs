using System.Net;

namespace CrmUpdateHandler.Utility
{
    /// <summary>
    /// Base class for types returned from HubSpot access events.
    /// </summary>
    public class HubSpotAccessResult
    {
        public HubSpotAccessResult(HttpStatusCode code)
        {
            this.StatusCode = code;
        }
        public HubSpotAccessResult(HttpStatusCode code, string errorMessage)
        {
            this.StatusCode = code;
            this.ErrorMessage = errorMessage;
        }

        public HttpStatusCode StatusCode { get; set; }

        public string ErrorMessage { get; set; }

    }

}
