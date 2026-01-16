namespace SCRM.API.Models.Events
{
    public class ScreenShotUploadedEvent
    {
        public string Url { get; set; }
        public string DeviceUuid { get; set; }
        
        public ScreenShotUploadedEvent(string url, string deviceUuid = null)
        {
            Url = url;
            DeviceUuid = deviceUuid;
        }
    }
}
