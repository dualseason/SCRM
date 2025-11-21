using System.Threading.Tasks;

namespace SCRM.Services
{
    public interface IRocketMQProducerService
    {
        Task<bool> SendMessageAsync(string topic, string message);
        Task<bool> SendMessageAsync(string topic, string message, string tag);
        Task<bool> SendDelayedMessageAsync(string topic, string message, int delayLevel);
        Task<bool> SendOrderlyMessageAsync(string topic, string message, string hashKey);
        Task StartAsync();
        Task StopAsync();
    }
}