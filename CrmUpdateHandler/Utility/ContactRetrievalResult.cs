using System.Net;

namespace CrmUpdateHandler.Utility
{
    /// <summary>
    /// Contains the details of a Contact, as well as the status flags from the retrieval itself. 
    /// </summary>
    internal class ContactRetrievalResult
    {
        public ContactRetrievalResult(CanonicalContact contact)
        {
            this.Payload = contact;
            this.StatusCode = HttpStatusCode.OK;
        }

        public ContactRetrievalResult(HttpStatusCode code)
        {
            this.StatusCode = code;
        }
        public ContactRetrievalResult(HttpStatusCode code, string errorMessage)
        {
            this.StatusCode = code;
            this.ErrorMessage = errorMessage;
        }

        public CanonicalContact Payload { get; private set; }

        public HttpStatusCode StatusCode { get; set; }

        public string ErrorMessage { get; set; }

    }

}
