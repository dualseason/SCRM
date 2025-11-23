namespace SCRM.Models
{
    public class UserConnectionInfo
    {
        public string UserId { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string DeviceInfo { get; set; } = string.Empty;
        public DateTime ConnectedAt { get; set; }
        public bool IsOnline { get; set; } = true;
        public DateTime LastActivityAt { get; set; }
    }
}
