using Microsoft.Extensions.Logging;
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
        Task<HubSpotContactResult> RetrieveHubspotContactById(string contactId, bool fetchPreviousValues, ILogger log, bool isTest);

        Task<HubSpotContactResult> RetrieveHubspotContactByEmailAddr(string email, bool fetchPreviousValues, ILogger log, bool isTest);

        Task<HubSpotContactResult> CreateHubspotContactAsync(
            string email,
            string firstname,
            string lastname,
            string preferredName,
            string primaryPhone,
            string streetAddress1,
            string streetAddress2,
            string city,
            string state,
            string postcode,
            string leadStatus,
            bool installationRecordExists,
            ILogger log,
            bool isTest);

        /// <summary>
        /// Updates the relevant fields of a HubSpot contact according to the given contract status.
        /// </summary>
        /// <param name="email"></param>
        /// <param name="status"></param>
        /// <param name="log"></param>
        /// <param name="isTest"></param>
        /// <returns></returns>
        Task<HubSpotContactResult> UpdateContractStatusAsync(
            string email,
            string contractStatus,
            ILogger log,
            bool isTest);

        Task<HubSpotContactResult> UpdateContactDetailsAsync(
            string vid,
            HubSpotContactProperties props,
            ILogger log,
            bool isTest);
    }
}
