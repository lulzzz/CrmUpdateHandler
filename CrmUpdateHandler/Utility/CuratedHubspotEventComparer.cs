using System;
using System.Collections.Generic;
using System.Text;

namespace CrmUpdateHandler.Utility
{
    internal class CuratedHubspotEventComparer : EqualityComparer<CuratedHubspotEvent>
    {
        public override bool Equals(CuratedHubspotEvent x, CuratedHubspotEvent y)
        {
            return (x.EventId == y.EventId) &&
                (x.IsNew == y.IsNew) &&
                (x.Vid == y.Vid);
        }

        public override int GetHashCode(CuratedHubspotEvent obj)
        {
            int result = 0;

            // Don't compute hash code on null objects.
            if (obj == null)
            {
                return 0;
            }

            return string.Format($"{obj.Vid}.{obj.EventId}.{obj.IsNew}").GetHashCode();
        }
    }
}
