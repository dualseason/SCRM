using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SCRM.Netty;
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SCRM.Services
{
    public class NettyMessageService : INettyService, INettyMessageService, IHostedService
    {
        private readonly ILogger<NettyMessageService> _logger;
        private readonly INettyServer _nettyServer;
        private readonly ConcurrentDictionary<string, TcpClient> _tcpClients = new ConcurrentDictionary<string, TcpClient>();

        public NettyMessageService(
            ILogger<NettyMessageService> logger,
            INettyServer nettyServer)
        {
            _logger = logger;
            _nettyServer = nettyServer;
        }

        public async Task StartAsync()
        {
            try
            {
                if (!_nettyServer.IsRunning)
                {
                    await _nettyServer.StartAsync();
                    _logger.LogInformation("Netty message service started successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Netty message service");
                throw;
            }
        }

        public async Task StopAsync()
        {
            try
            {
                if (_nettyServer.IsRunning)
                {
                    await _nettyServer.StopAsync();
                    _logger.LogInformation("Netty message service stopped successfully");
                }

                // 关闭所有TCP客户端连接
                foreach (var client in _tcpClients.Values)
                {
                    try
                    {
                        client.Close();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error closing TCP client");
                    }
                }
                _tcpClients.Clear();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop Netty message service");
            }
        }

        public async Task<bool> SendMessageToNettyAsync(object message, string messageType = "", string targetId = "", string targetType = "", string topic = "", string tag = "")
        {
            try
            {
                if (!_nettyServer.IsRunning)
                {
                    _logger.LogWarning("Netty server is not running");
                    return false;
                }

                var nettyMessage = new NettyNetMessage
                {
                    Type = messageType,
                    TargetType = targetType,
                    TargetId = targetId,
                    Content = message,
                    Topic = topic,
                    Tag = tag,
                    Timestamp = DateTime.UtcNow,
                    Source = "SCRM_API"
                };

                string jsonMessage = JsonSerializer.Serialize(nettyMessage);
                byte[] messageBytes = Encoding.UTF8.GetBytes(jsonMessage);

                // 如果有特定的TCP客户端，则直接发送
                if (!string.IsNullOrEmpty(targetId) && _tcpClients.TryGetValue(targetId, out var client))
                {
                    try
                    {
                        var stream = client.GetStream();
                        await stream.WriteAsync(messageBytes, 0, messageBytes.Length);
                        _logger.LogInformation("Message sent to TCP client: {ClientId}", targetId);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending message to TCP client: {ClientId}", targetId);
                        _tcpClients.TryRemove(targetId, out _);
                    }
                }

                // 广播给所有TCP客户端
                var sendTasks = new List<Task>();
                foreach (var clientInfo in _tcpClients.ToList())
                {
                    sendTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var stream = clientInfo.Value.GetStream();
                            await stream.WriteAsync(messageBytes, 0, messageBytes.Length);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error sending message to client: {ClientId}", clientInfo.Key);
                            _tcpClients.TryRemove(clientInfo.Key, out _);
                        }
                    }));
                }

                await Task.WhenAll(sendTasks);
                _logger.LogInformation("Broadcast message sent to {Count} TCP clients", _tcpClients.Count);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message via Netty");
                return false;
            }
        }

        public async Task<int> GetConnectedClientsCountAsync()
        {
            return await Task.FromResult(_tcpClients.Count);
        }

        public async Task<bool> IsNettyServerRunningAsync()
        {
            return await Task.FromResult(_nettyServer?.IsRunning ?? false);
        }

        public bool IsRunning => _nettyServer?.IsRunning ?? false;
        public int Port => _nettyServer?.Port ?? 0;

        // IHostedService implementation
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return StartAsync();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return StopAsync();
        }

        // 内部消息类
        public class NettyNetMessage
        {
            public string Type { get; set; } = string.Empty;
            public string TargetType { get; set; } = string.Empty;
            public string TargetId { get; set; } = string.Empty;
            public object Content { get; set; } = new { };
            public string? Topic { get; set; }
            public string? Tag { get; set; }
            public DateTime Timestamp { get; set; } = DateTime.UtcNow;
            public string? Source { get; set; }
        }
    }

    // 让NettyMessageService实现INettyService接口
    public interface INettyService
    {
        Task StartAsync();
        Task StopAsync();
        bool IsRunning { get; }
        int Port { get; }
    }
}