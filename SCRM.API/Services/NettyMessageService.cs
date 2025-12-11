using System.Linq;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SCRM.Services.Netty;
using System;
using System.Threading;
using System.Threading.Tasks;
using SCRM.SHARED.Proto;
using Google.Protobuf.WellKnownTypes;

namespace SCRM.Services
{
    public class NettyMessageService : INettyService, IHostedService
    {
        private readonly Serilog.ILogger _logger = SCRM.Shared.Core.Utility.logger;
        private readonly NettyServer _nettyServer;
        private readonly ConnectionManager _connectionManager;

        public NettyMessageService(
            NettyServer nettyServer,
            ConnectionManager connectionManager)
        {
            _nettyServer = nettyServer;
            _connectionManager = connectionManager;
        }

        public async Task StartAsync()
        {
            try
            {
                if (!_nettyServer.IsRunning)
                {
                    await _nettyServer.StartAsync();
                    _logger.Information("Netty message service started successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to start Netty message service");
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
                    _logger.Information("Netty message service stopped successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to stop Netty message service");
            }
        }

        public async Task<bool> SendMessageToNettyAsync(object? message, string messageType = "", string targetId = "", string targetType = "", string topic = "", string tag = "")
        {
            try
            {
                if (!_nettyServer.IsRunning)
                {
                    _logger.Warning("Netty server is not running");
                    return false;
                }

                // 构造 TransportMessage
                var transportMessage = new TransportMessage
                {
                    Id = DateTime.UtcNow.Ticks, // 简单生成ID
                    AccessToken = "", // 需要时填充
                    MsgType = System.Enum.TryParse<EnumMsgType>(messageType, out var typeEnum) ? typeEnum : EnumMsgType.UnknownMsg,
                    RefMessageId = 0
                };

                if (message != null)
                {
                    transportMessage.Content = Any.Pack((Google.Protobuf.IMessage)message);
                }

                // 如果有特定的目标ID (ConnectionId)
                if (!string.IsNullOrEmpty(targetId))
                {
                    var channel = _connectionManager.GetChannel(targetId);

                    // Fallback 1: Try ID as User ID
                    if (channel == null)
                    {
                        channel = _connectionManager.GetChannelByUserId(targetId);
                    }

                    // Fallback 2: Try ID as Device UUID (DeviceInfo)
                    if (channel == null)
                    {
                        var allConns = await _connectionManager.GetAllConnectionsAsync();
                        var connInfo = allConns.FirstOrDefault(c => c.DeviceInfo == targetId);
                        if (connInfo != null)
                        {
                            channel = _connectionManager.GetChannel(connInfo.ConnectionId);
                        }
                    }

                    if (channel != null && channel.Active)
                    {
                        await channel.WriteAndFlushAsync(transportMessage);
                        _logger.Information("Message sent to client: {ClientId}", targetId);
                        return true;
                    }
                    else
                    {
                         _logger.Warning("Client not found or inactive: {ClientId}", targetId);
                         return false;
                    }
                }

                // 广播给所有连接 (示例逻辑，实际可能需要更精细的广播)
                var connections = await _connectionManager.GetAllConnectionsAsync();
                foreach (var conn in connections)
                {
                    var channel = _connectionManager.GetChannel(conn.ConnectionId);
                    if (channel != null && channel.Active)
                    {
                        await channel.WriteAndFlushAsync(transportMessage);
                    }
                }
                
                _logger.Information("Broadcast message sent");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error sending message via Netty");
                return false;
            }
        }

        public async Task<int> GetConnectedClientsCountAsync()
        {
            var stats = _connectionManager.GetStatistics();
            return await Task.FromResult(stats.TotalConnections);
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
    }

    public interface INettyService
    {
        Task StartAsync();
        Task StopAsync();
        bool IsRunning { get; }
        int Port { get; }
    }
}