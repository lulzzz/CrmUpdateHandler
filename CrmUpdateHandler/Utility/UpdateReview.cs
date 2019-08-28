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
        public UpdateReview()
        {
            this.Changes = new List<UpdateReviewChange>();
        }

        public List<UpdateReviewChange> Changes { get; private set; }

        public void AddChange(string friendlyName, string oldValue, string newValue)
        {
            if (oldValue == newValue) return;

            this.Changes.Add(new UpdateReviewChange(friendlyName, oldValue, newValue));
        }
    }

    public class UpdateReviewChange
    {
        public UpdateReviewChange(string friendlyName, string currentValue, string newValue)
        {
            this.Fieldname = friendlyName;
            this.Current = currentValue;
            this.New = newValue;
        }

        /// <summary>
        /// Gets the nme of the field, as displayed to the approver in an email
        /// </summary>
        public string Fieldname { get; private set; }

        public string Current { get; private set; }
        public string New { get; private set; }
    }
}
