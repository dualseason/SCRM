using System.Linq;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SCRM.Services.Netty;
using SCRM.API.Services.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using Jubo.JuLiao.IM.Wx.Proto;
using Google.Protobuf.WellKnownTypes;


namespace SCRM.Services
{
    /// <summary>
    /// Netty 消息服务
    /// 提供对 Netty 服务器的生命周期管理和消息发送功能
    /// </summary>
    public class NettyMessageService : INettyService, IHostedService
    {
        private readonly Microsoft.Extensions.Logging.ILogger<NettyMessageService> _logger;
        private readonly NettyServer _nettyServer;
        private readonly ConnectionManager _connectionManager;

        public NettyMessageService(
            NettyServer nettyServer,
            ConnectionManager connectionManager,
            Microsoft.Extensions.Logging.ILogger<NettyMessageService> logger)
        {
            _nettyServer = nettyServer;
            _connectionManager = connectionManager;
            _logger = logger;
        }

        /// <summary>
        /// 启动 Netty 服务器
        /// </summary>
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

        /// <summary>
        /// 停止 Netty 服务器
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                if (_nettyServer.IsRunning)
                {
                    await _nettyServer.StopAsync();
                    _logger.LogInformation("Netty message service stopped successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop Netty message service");
            }
        }

        /// <summary>
        /// 发送消息到 Netty 客户端
        /// </summary>
        /// <param name="message">消息内容 (Protobuf 对象，可选)</param>
        /// <param name="messageType">消息类型 (EnumMsgType)</param>
        /// <param name="targetId">目标ID (ConnectionId / UserId / UUID)</param>
        /// <param name="customMessageId">自定义消息ID (用于Trace/ACK)</param>
        public async Task<bool> SendMessageToNettyAsync(object? message, string messageType = "", string targetId = "", string targetType = "", string topic = "", string tag = "", long? customMessageId = null)
        {
            try
            {
                if (!_nettyServer.IsRunning)
                {
                    _logger.LogWarning("Netty server is not running");
                    return false;
                }

                var transportMessage = new TransportMessage
                {
                    Id = customMessageId ?? DateTime.UtcNow.Ticks, // 使用自定义ID或生成新ID
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

                    // 策略 1: 尝试将 ID 作为 UserId 查找
                    if (channel == null)
                    {
                        channel = _connectionManager.GetChannelByUserId(targetId);
                    }

                    // 策略 2: 尝试将 ID 作为 Device UUID (DeviceInfo) 查找
                    if (channel == null)
                    {
                        var allConns = await _connectionManager.GetAllConnectionsAsync();
                        var connInfo = allConns.FirstOrDefault(c => c.deviceInfo == targetId);
                        if (connInfo != null)
                        {
                            channel = _connectionManager.GetChannel(connInfo.connectionId);
                        }
                    }

                    if (channel != null && channel.Active)
                    {
                        await channel.WriteAndFlushAsync(transportMessage);
                        _logger.LogInformation("向{ClientId}客户端发送{MsgType}指令", targetId, messageType);
                        return true;
                    }
                    else
                    {
                         _logger.LogWarning("Client not found or inactive: {ClientId}", targetId);
                         return false;
                    }
                }

                // 广播给所有连接 (示例逻辑，默认仅在 targetId 为空时触发，慎用)
                var connections = await _connectionManager.GetAllConnectionsAsync();
                foreach (var conn in connections)
                {
                    var channel = _connectionManager.GetChannel(conn.connectionId);
                    if (channel != null && channel.Active)
                    {
                        await channel.WriteAndFlushAsync(transportMessage);
                    }
                }
                
                _logger.LogInformation("Broadcast message sent");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message via Netty");
                return false;
            }
        }

        /// <summary>
        /// 获取当前连接的客户端数量
        /// </summary>
        public async Task<int> GetConnectedClientsCountAsync()
        {
            var stats = _connectionManager.GetStatistics();
            return await Task.FromResult(stats.totalConnections);
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
        Task<bool> SendMessageToNettyAsync(object? message, string messageType = "", string targetId = "", string targetType = "", string topic = "", string tag = "", long? customMessageId = null);
        Task<int> GetConnectedClientsCountAsync();
        Task<bool> IsNettyServerRunningAsync();
        bool IsRunning { get; }
        int Port { get; }
    }
}