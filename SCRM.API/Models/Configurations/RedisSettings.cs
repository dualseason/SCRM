namespace SCRM.Models.Configurations
{
    public class RedisSettings
    {
        public string ConnectionString { get; set; } = "localhost:6379";
        public int Database { get; set; } = 0;
        public string InstanceName { get; set; } = "SCRM";
        public string KeyPrefix { get; set; } = "SCRM:";
        public int ConnectRetry { get; set; } = 3;
        public int ConnectTimeout { get; set; } = 5000;
        public int SyncTimeout { get; set; } = 5000;
        public bool AbortOnConnectFail { get; set; } = false;
        public bool Ssl { get; set; } = false;
        public bool AllowAdmin { get; set; } = true;
    }
}