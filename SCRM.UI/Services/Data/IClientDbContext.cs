using SCRM.API.Models.Entities;
using SCRM.SHARED.Models;

namespace SCRM.UI.Services.Data
{
    public interface IClientDbContext
    {
        IClientDataSet<Contact> Contacts { get; }
        IClientDataSet<Message> Messages { get; }
    }
}
