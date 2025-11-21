using System.Collections.Generic;
using System.Threading.Tasks;

namespace SCRM.Services
{
    public interface IConnectionManager
    {
        Task AddConnectionAsync(string userId, string connectionId, string deviceType, string deviceInfo = "");
        Task RemoveConnectionAsync(string connectionId);
        Task<IEnumerable<UserConnectionInfo>> GetConnectionsByUserAsync(string userId);
        Task<IEnumerable<UserConnectionInfo>> GetConnectionsByDeviceTypeAsync(string deviceType);
        Task<IEnumerable<UserConnectionInfo>> GetAllConnectionsAsync();
        Task<UserConnectionInfo?> GetConnectionAsync(string connectionId);
        Task UpdateConnectionActivityAsync(string connectionId);
        Task<bool> IsUserOnlineAsync(string userId);
        Task<int> GetOnlineUserCountAsync();
    }

    public class UserConnectionInfo
    {
        public string UserId { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string DeviceInfo { get; set; } = string.Empty;
        public DateTime ConnectedAt { get; set; }
        public DateTime LastActivityAt { get; set; }
        public bool IsOnline => DateTime.UtcNow.Subtract(LastActivityAt).TotalMinutes < 5; // 5分钟内活跃
    }
}