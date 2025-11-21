using System.Threading.Tasks;

namespace SCRM.Services
{
    public interface ISignalRMessageService
    {
        Task SendMessageToUserAsync(string userId, object message, string messageType = "");
        Task SendMessageToDeviceTypeAsync(string deviceType, object message, string messageType = "");
        Task SendMessageToAllAsync(object message, string messageType = "");
        Task SendMessageToRoomAsync(string roomId, object message, string messageType = "");
        Task NotifyUserOnlineAsync(string userId, string deviceType);
        Task NotifyUserOfflineAsync(string userId, string deviceType);
        Task SendHeartbeatResponseAsync(string userId);
    }
}