namespace SCRM.Models.Configurations
{
    public class NettySettings
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 8647;
        public int HttpPort { get; set; } = 42718;
    }
}
