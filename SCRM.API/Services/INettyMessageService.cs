using System.Threading.Tasks;

namespace SCRM.Services
{
    public interface INettyMessageService
    {
        Task<bool> SendMessageToNettyAsync(object message, string messageType = "", string targetId = "", string targetType = "", string topic = "", string tag = "");
        Task<int> GetConnectedClientsCountAsync();
        Task<bool> IsNettyServerRunningAsync();
    }
}