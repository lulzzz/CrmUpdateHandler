using System.Net;

namespace CrmUpdateHandler.Utility
{
    /// <summary>
    /// Represents the result of accessing the Crm to retrieve, update or create a contact. 
    /// Contains the details of a Contact, as well as the status flags from the CRUD operation itself. 
    /// </summary>
    public class CrmAccessResult
    {
        public CrmAccessResult(CanonicalContact contact)
        {
            this.Payload = contact;
            this.StatusCode = HttpStatusCode.OK;
        }

        public CrmAccessResult(HttpStatusCode code)
        {
            this.StatusCode = code;
        }
        public CrmAccessResult(HttpStatusCode code, string errorMessage)
        {
            this.StatusCode = code;
            this.ErrorMessage = errorMessage;
        }

        public CanonicalContact Payload { get; private set; }

        public HttpStatusCode StatusCode { get; set; }

        public string ErrorMessage { get; set; }

    }

}
