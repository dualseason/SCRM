namespace SCRM.Models.Configurations
{
    public class Settings
    {
        public string DbPath { get; set; } = "GamePlatform.db";
        public int Port { get; set; } = 8080;
        public string UserConfigDir { get; set; } = "Users";
        
        // Add other settings as needed based on usage
        public string ToJsonString()
        {
            return System.Text.Json.JsonSerializer.Serialize(this);
        }
    }
}
