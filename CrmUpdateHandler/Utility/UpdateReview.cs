using System;
using System.Collections.Generic;
using System.Text;

namespace CrmUpdateHandler.Utility
{
    /// <summary>
    /// Serialises as a JSON package to be interpreted by a Flow into a Flow Approval
    /// </summary>
    public class UpdateReview
    {
        public UpdateReview(string email,
            string requestBody)
        {
            this.Email = email;
            this.RequestBody = requestBody;
            this.Changes = new List<UpdateReviewChange>();
        }

        public string Email { get; private set; }

        public List<UpdateReviewChange> Changes { get; private set; }

        public string RequestBody { get; private set; }

        public void AddChange(string field, string oldValue, string newValue)
        {
            this.Changes.Add(new UpdateReviewChange(field, oldValue, newValue));
        }
    }

    public class UpdateReviewChange
    {
        public UpdateReviewChange(string fieldName, string currentValue, string newValue)
        {
            this.Fieldname = fieldName;
            this.Current = currentValue;
            this.New = newValue;
        }

        public string Fieldname { get; private set; }
        public string Current { get; private set; }
        public string New { get; private set; }
    }
}
