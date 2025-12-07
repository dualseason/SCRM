using System.Threading.Tasks;

namespace SCRM.UI.Services
{
    public interface IClientTaskService
    {
        Task<bool> SendTalkToFriendAsync(string connectionId, string friendWxId, string content);
    }
}
