namespace SCRM.Models
{
    public class UserConnectionInfo
    {
        public string userId { get; set; } = string.Empty;
        public string connectionId { get; set; } = string.Empty;
        public string deviceType { get; set; } = string.Empty;
        public string deviceInfo { get; set; } = string.Empty;
        public string deviceUuid { get; set; } = string.Empty;
        public DateTime connectedAt { get; set; }
        public bool isOnline { get; set; } = true;
        public DateTime lastActivityAt { get; set; }
    }
}
