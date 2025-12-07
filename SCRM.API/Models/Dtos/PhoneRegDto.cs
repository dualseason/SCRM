namespace SCRM.API.Models.DTOs
{
    public class PhoneRegDto
    {
        public string Imei { get; set; }
        public string RegCode { get; set; } // This might contain Email or legacy code
        public string Mac { get; set; }
        public string ReturnKey { get; set; }
        public string Hsman { get; set; }
        public string Hstype { get; set; }
        public string AndroidApi { get; set; }
        public string AndroidId { get; set; }
        public int VersionCode { get; set; }
        public string PackageName { get; set; }
        public string UserEmail { get; set; }
    }
}
