namespace SCRM.Configurations
{
    public class RocketMQSettings
    {
        public string NameServer { get; set; } = "localhost:9876";
        public string ProducerGroup { get; set; } = "SCRM_Producer_Group";
        public string ConsumerGroup { get; set; } = "SCRM_Consumer_Group";
        public List<string> Topics { get; set; } = new List<string>();
    }
}