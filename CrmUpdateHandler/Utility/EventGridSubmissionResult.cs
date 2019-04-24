using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;

namespace CrmUpdateHandler.Utility
{
    internal class EventGridSubmissionResult
    {
        public EventGridSubmissionResult(HttpResponseMessage response)
        {
            this.StatusCode = response.StatusCode;
        }

        public HttpStatusCode StatusCode { get; private set; }

        public string ErrorMessage { get; set;}
    }
}
