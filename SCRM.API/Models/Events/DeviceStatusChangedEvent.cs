namespace SCRM.API.Models.Events
{
    public class DeviceStatusChangedEvent
    {
        public string DeviceUuid { get; set; }
        public bool IsOnline { get; set; }
        
        public DeviceStatusChangedEvent(string deviceUuid, bool isOnline)
        {
            DeviceUuid = deviceUuid;
            IsOnline = isOnline;
        }
    }
}
