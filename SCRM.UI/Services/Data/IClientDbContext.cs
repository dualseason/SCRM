using SCRM.API.Models.Entities;

namespace SCRM.UI.Services.Data
{
    public interface IClientDbContext
    {
        IClientDataSet<Contact> Contacts { get; }
        IClientDataSet<Message> Messages { get; }
        IClientDataSet<MomentsTimeline> MomentsTimelines { get; }
    }
}
