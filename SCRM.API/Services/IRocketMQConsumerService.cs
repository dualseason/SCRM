using System.Threading.Tasks;

namespace SCRM.Services
{
    public interface IRocketMQConsumerService
    {
        Task<bool> SubscribeAsync(string topic, string tag);
        Task<bool> SubscribeAsync(string topic);
        Task StartAsync();
        Task StopAsync();
    }
}