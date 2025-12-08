using System.Collections.Concurrent;
using SCRM.API.Models.Entities;

namespace SCRM.Services.Data
{
    public static class GlobleCache
    {
        /// <summary>
        /// Cache for SrClients (Devices), Key: UUID
        /// </summary>
        public static ConcurrentDictionary<string, SrClient> SrClients { get; } = new();

        /// <summary>
        /// Cache for WechatAccounts (Contacts/Accounts), Key: AccountId (long)
        /// </summary>
        public static ConcurrentDictionary<long, WechatAccount> WechatAccounts { get; } = new();

        /// <summary>
        /// Cache for Contacts (Friends), Key: WechatAccountId (long), Value: List of Contacts
        /// </summary>
        public static ConcurrentDictionary<long, List<Contact>> Contacts { get; } = new();
    }
}
