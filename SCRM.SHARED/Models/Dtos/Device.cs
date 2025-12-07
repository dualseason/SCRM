namespace SCRM.API.Models.DTOs
{
    public class Device
    {
        public string regCode { get; set; }
        public string imei { get; set; }
        public string mac { get; set; }
        public string androidId { get; set; }
        public string packageName { get; set; }
        public string returnKey { get; set; }
        public string hsman { get; set; }
        public string hstype { get; set; }
        public string androidApi { get; set; }
        public int versionCode { get; set; }
    }

    public class BasicDeviceInfo : Device
    {
        // Inherits all properties from Device
    }

    public class ExtendedDeviceInfo : Device
    {
        public string imsi { get; set; }
        public string fingerprint { get; set; }
        public string userData { get; set; }
    }
}
