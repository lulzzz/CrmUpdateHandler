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
        Task<CrmAccessResult> RetrieveHubspotContactById(string contactId, bool fetchPreviousValues);

        Task<CrmAccessResult> RetrieveHubspotContactByEmailAddr(string email, bool fetchPreviousValues);

        Task<CrmAccessResult> CreateHubspotContactAsync(string email, string firstname, string lastname, string primaryPhone);
    }
}
