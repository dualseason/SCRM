namespace SCRM.Models.Configurations
{
    public class JwtSettings
    {
        public const string SectionName = "JwtSettings";

        public string SecretKey { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        // ExpiryMinutes removed - moved to SystemConfig (Database)
        public int RefreshTokenExpiryDays { get; set; } = 7;
    }
}