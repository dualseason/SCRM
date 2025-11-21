using DotNetty.Transport.Channels;
using System.Threading.Tasks;

namespace SCRM.Netty
{
    public interface INettyServer
    {
        Task StartAsync();
        Task StopAsync();
        bool IsRunning { get; }
        int Port { get; }
    }
}