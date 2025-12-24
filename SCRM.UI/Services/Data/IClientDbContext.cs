using SCRM.API.Models.Entities;

namespace SCRM.UI.Services.Data
{
    public interface IClientDbContext
    {
        IClientDataSet<Contact> Contacts { get; }
        IClientDataSet<WechatAccount> WechatAccounts { get; }
        IClientDataSet<Message> Messages { get; }
        IClientDataSet<Conversation> Conversations { get; }
        IClientDataSet<MomentsTimeline> MomentsTimelines { get; }
    }
}
