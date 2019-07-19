using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CrmUpdateHandler.Utility
{
    /// <summary>
    /// This is necessary to move the Functions to take advantage of the new Dependency Injection features announced in May 2019
    /// </summary>
    public interface IHubSpotAdapter
    {
        Task<HubSpotAccessResult> RetrieveHubspotContactById(string contactId, bool fetchPreviousValues);

        Task<HubSpotAccessResult> RetrieveHubspotContactByEmailAddr(string email, bool fetchPreviousValues);

        Task<HubSpotAccessResult> CreateHubspotContactAsync(string email, string firstname, string lastname, string primaryPhone);
    }
}
