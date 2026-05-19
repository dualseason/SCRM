using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SCRM.Shared.Core;
using SCRM.Models;
using DotNetty.Transport.Channels;
using Jubo.JuLiao.IM.Wx.Proto;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;


namespace SCRM.API.Services.Core
{
    /// <summary>
    /// 连接管理器
    /// <para>核心单例服务，负责全站 WebSocket/TCP 连接的生命周期管理与索引。</para>
    /// <para>主要索引维度：</para>
    /// <list type="bullet">
    /// <item>ConnectionId -> ConnectionInfo (主索引)</item>
    /// <item>UserId -> Set of ConnectionIds (用户多端登录支持)</item>
    /// <item>DeviceType -> Set of ConnectionIds (设备类型广播支持)</item>
    /// <item>DeviceUuid -> ConnectionId (设备唯一标识反查)</item>
    /// </list>
    /// <para>同时维护 Netty Channel 实例的引用，用于实际数据发送。</para>
    /// </summary>
    public class ConnectionManager
    {
        private readonly ILogger<ConnectionManager> _logger;
        
        /// <summary>
        /// 主连接字典: ConnectionId -> ConnectionInfo
        /// </summary>
        private readonly ConcurrentDictionary<string, UserConnectionInfo> _connections = new ConcurrentDictionary<string, UserConnectionInfo>();
        
        /// <summary>
        /// 用户维度索引: UserId -> Set of ConnectionIds (支持多端登录)
        /// </summary>
        private readonly ConcurrentDictionary<string, HashSet<string>> _userConnections = new ConcurrentDictionary<string, HashSet<string>>();
        
        /// <summary>
        /// 设备类型索引: DeviceType -> Set of ConnectionIds
        /// </summary>
        private readonly ConcurrentDictionary<string, HashSet<string>> _deviceTypeConnections = new ConcurrentDictionary<string, HashSet<string>>();
        
        /// <summary>
        /// 设备UUID索引: DeviceUuid -> ConnectionId (假设每个设备UUID同时只有一个活跃连接)
        /// </summary>
        private readonly ConcurrentDictionary<string, string> _deviceUuidConnections = new ConcurrentDictionary<string, string>();
        
        /// <summary>
        /// Netty Channel 实例缓存: ConnectionId -> IChannel
        /// </summary>
        private readonly ConcurrentDictionary<string, IChannel> _activeChannels = new ConcurrentDictionary<string, IChannel>();

        /// <summary>
        /// 定时器：定期检查沉默客户端（已连接但未上报微信ID）
        /// </summary>
        private readonly System.Threading.Timer _silentClientCheckTimer;

        public ConnectionManager(ILogger<ConnectionManager> logger)
        {
            _logger = logger;
            // 初始化定时器：每30秒检查一次
            _silentClientCheckTimer = new System.Threading.Timer(CheckSilentClients, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// 注册新连接
        /// 在 DeviceAuth 或 连接初始化时调用
        /// </summary>
        public Task AddConnectionAsync(string userId, string connectionId, string deviceType, string deviceInfo = "")
        {
            var connectionInfo = new UserConnectionInfo
            {
                userId = userId,
                connectionId = connectionId,
                deviceType = deviceType,
                deviceInfo = deviceInfo,
                deviceUuid = deviceInfo, // In MessageRouter, we pass UUID as deviceInfo
                connectedAt = DateTime.UtcNow,
                lastActivityAt = DateTime.UtcNow
            };

            // 添加到连接字典
            _connections.TryAdd(connectionId, connectionInfo);

            // 添加到用户连接映射
            var userConns = _userConnections.GetOrAdd(userId, _ => new HashSet<string>());
            lock (userConns)
            {
                userConns.Add(connectionId);
            }

            // 添加到设备类型连接映射
            var deviceConns = _deviceTypeConnections.GetOrAdd(deviceType, _ => new HashSet<string>());
            lock (deviceConns)
            {
                deviceConns.Add(connectionId);
            }

            // 添加到设备UUID映射
            if (!string.IsNullOrEmpty(deviceInfo))
            {
                _deviceUuidConnections.AddOrUpdate(deviceInfo, connectionId, (key, oldVal) => connectionId);
            }

            _logger.LogInformation("连接已添加 - 用户ID: {UserId}, 连接ID: {ConnectionId}, 设备类型: {DeviceType}",
                userId, connectionId, deviceType);

            return Task.CompletedTask;
        }

        /// <summary>
        /// 检查连接是否有效
        /// </summary>
        public bool IsConnected(string connectionId)
        {
            return _connections.ContainsKey(connectionId);
        }

        /// <summary>
        /// 移除连接
        /// 在断开连接 (ChannelInactive) 时调用
        /// </summary>
        public Task RemoveConnectionAsync(string connectionId)
        {
            if (_connections.TryRemove(connectionId, out var connectionInfo))
            {
                // 从用户连接映射中移除
                if (_userConnections.TryGetValue(connectionInfo.userId, out var userConns))
                {
                    lock (userConns)
                    {
                        userConns.Remove(connectionId);
                        if (userConns.Count == 0)
                        {
                            _userConnections.TryRemove(connectionInfo.userId, out _);
                        }
                    }
                }

                // 从设备类型连接映射中移除
                if (_deviceTypeConnections.TryGetValue(connectionInfo.deviceType, out var deviceConns))
                {
                    lock (deviceConns)
                    {
                        deviceConns.Remove(connectionId);
                        if (deviceConns.Count == 0)
                        {
                            _deviceTypeConnections.TryRemove(connectionInfo.deviceType, out _);
                        }
                    }
                }

                // 从设备UUID映射中移除
                if (!string.IsNullOrEmpty(connectionInfo.deviceUuid))
                {
                    // Only remove if it points to THIS connectionId (handle race conditions)
                    if (_deviceUuidConnections.TryGetValue(connectionInfo.deviceUuid, out var currentConnId) && currentConnId == connectionId)
                    {
                        _deviceUuidConnections.TryRemove(connectionInfo.deviceUuid, out _);
                    }
                }

                _logger.LogInformation("连接已移除 - 用户ID: {UserId}, 连接ID: {ConnectionId}, 设备类型: {DeviceType}",
                    connectionInfo.userId, connectionId, connectionInfo.deviceType);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 注册 Netty 通道实例
        /// </summary>
        public void RegisterChannel(string connectionId, IChannel channel)
        {
            _activeChannels.AddOrUpdate(connectionId, channel, (key, oldValue) => channel);
        }

        /// <summary>
        /// 移除 Netty 通道实例
        /// </summary>
        public void RemoveChannel(string connectionId)
        {
            _activeChannels.TryRemove(connectionId, out _);
        }

        /// <summary>
        /// 获取指定连接的 Netty 通道
        /// </summary>
        public IChannel? GetChannel(string connectionId)
        {
            _activeChannels.TryGetValue(connectionId, out var channel);
            return channel;
        }

        /// <summary>
        /// 根据 UserId 获取任意一个活跃的 Netty 通道
        /// </summary>
        public IChannel? GetChannelByUserId(string userId)
        {
            if (_userConnections.TryGetValue(userId, out var connectionIds))
            {
                foreach (var connId in connectionIds)
                {
                    if (_activeChannels.TryGetValue(connId, out var channel) && channel.Active)
                    {
                        return channel;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 获取用户的所有连接信息
        /// </summary>
        public Task<IEnumerable<UserConnectionInfo>> GetConnectionsByUserAsync(string userId)
        {
            if (_userConnections.TryGetValue(userId, out var connectionIds))
            {
                var connections = connectionIds
                    .Select(connId => _connections.TryGetValue(connId, out var conn) ? conn : null!)
                    .Where(conn => conn != null)
                    .ToList()!;

                return Task.FromResult(connections.AsEnumerable());
            }

            return Task.FromResult(Enumerable.Empty<UserConnectionInfo>());
        }

        /// <summary>
        /// 获取特定设备类型的所有连接
        /// </summary>
        public Task<IEnumerable<UserConnectionInfo>> GetConnectionsByDeviceTypeAsync(string deviceType)
        {
            if (_deviceTypeConnections.TryGetValue(deviceType, out var connectionIds))
            {
                var connections = connectionIds
                    .Select(connId => _connections.TryGetValue(connId, out var conn) ? conn : null!)
                    .Where(conn => conn != null)
                    .ToList()!;

                return Task.FromResult(connections.AsEnumerable());
            }

            return Task.FromResult(Enumerable.Empty<UserConnectionInfo>());
        }

        /// <summary>
        /// 获取所有活跃连接
        /// </summary>
        public Task<IEnumerable<UserConnectionInfo>> GetAllConnectionsAsync()
        {
            return Task.FromResult(_connections.Values.AsEnumerable());
        }

        /// <summary>
        /// 根据 ConnectionId 获取连接详情
        /// </summary>
        public Task<UserConnectionInfo?> GetConnectionAsync(string connectionId)
        {
            _connections.TryGetValue(connectionId, out var connection);
            return Task.FromResult(connection);
        }

        /// <summary>
        /// 根据设备UUID 获取 ConnectionId
        /// </summary>
        public Task<string?> GetConnectionIdByDeviceUuidAsync(string deviceUuid)
        {
            if (string.IsNullOrWhiteSpace(deviceUuid))
            {
                return Task.FromResult<string?>(null);
            }

            if (_deviceUuidConnections.TryGetValue(deviceUuid, out var connectionId))
            {
                if (_connections.ContainsKey(connectionId)
                    && _activeChannels.TryGetValue(connectionId, out var channel)
                    && channel.Active)
                {
                    return Task.FromResult<string?>(connectionId);
                }

                // 旧连接断开后字典里可能残留 DeviceUuid -> ConnectionId。
                // 如果继续返回这个 ID，网页会对离线通道下发任务，表现为先失败再成功的双提示。
                _deviceUuidConnections.TryRemove(deviceUuid, out _);
                _logger.LogWarning(
                    "设备UUID映射指向非活跃连接，已清理: DeviceUuid={DeviceUuid}, ConnectionId={ConnectionId}",
                    deviceUuid,
                    connectionId);
            }

            var fallback = _connections.Values
                .Where(c => string.Equals(c.deviceUuid, deviceUuid, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.deviceInfo, deviceUuid, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.connectionId)
                .FirstOrDefault(connId => _activeChannels.TryGetValue(connId, out var channel) && channel.Active);

            if (!string.IsNullOrWhiteSpace(fallback))
            {
                _deviceUuidConnections[deviceUuid] = fallback;
                return Task.FromResult<string?>(fallback);
            }

            return Task.FromResult<string?>(null);
        }

        /// <summary>
        /// 判断指定设备 UUID 当前是否有活跃 Netty 通道。
        /// </summary>
        public Task<bool> IsDeviceUuidActiveAsync(string deviceUuid)
        {
            if (string.IsNullOrWhiteSpace(deviceUuid))
            {
                return Task.FromResult(false);
            }

            if (_deviceUuidConnections.TryGetValue(deviceUuid, out var connectionId)
                && _activeChannels.TryGetValue(connectionId, out var channel)
                && channel.Active
                && _connections.ContainsKey(connectionId))
            {
                return Task.FromResult(true);
            }

            var active = _connections.Values.Any(c =>
                (string.Equals(c.deviceUuid, deviceUuid, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.deviceInfo, deviceUuid, StringComparison.OrdinalIgnoreCase))
                && _activeChannels.TryGetValue(c.connectionId, out var ch)
                && ch.Active);

            return Task.FromResult(active);
        }

        /// <summary>
        /// 根据设备 UUID 获取当前活跃连接 ID。
        /// <para>保留给只需要取连接号的调用；内部会清理过期映射。</para>
        /// </summary>
        public async Task<string?> GetActiveConnectionIdByDeviceUuidAsync(string deviceUuid)
        {
            return await GetConnectionIdByDeviceUuidAsync(deviceUuid).ConfigureAwait(false);
        }

        /// <summary>
        /// 更新连接的最后活动时间
        /// </summary>
        public Task<bool> UpdateConnectionActivityAsync(string connectionId)
        {
            if (_connections.TryGetValue(connectionId, out var connection))
            {
                connection.lastActivityAt = DateTime.UtcNow;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        /// <summary>
        /// 更新连接绑定的 UserId (账号切换/合并时使用)
        /// </summary>
        public Task<bool> UpdateConnectionUserIdAsync(string connectionId, string newUserId)
        {
            if (_connections.TryGetValue(connectionId, out var connection))
            {
                var oldUserId = connection.userId;
                if (oldUserId == newUserId) return Task.FromResult(true);

                // 1. Remove from old User mapping
                if (!string.IsNullOrEmpty(oldUserId))
                {
                    if (_userConnections.TryGetValue(oldUserId, out var oldUserConns))
                    {
                        lock (oldUserConns)
                        {
                            oldUserConns.Remove(connectionId);
                            if (oldUserConns.Count == 0) _userConnections.TryRemove(oldUserId, out _);
                        }
                    }
                }

                // 2. Update Connection Info
                connection.userId = newUserId;

                // 3. Add to new User mapping
                var newUserConns = _userConnections.GetOrAdd(newUserId, _ => new HashSet<string>());
                lock (newUserConns)
                {
                    newUserConns.Add(connectionId);
                }

                _logger.LogInformation("Connection {ConnectionId} re-bound from User {OldUser} to {NewUser}", connectionId, oldUserId, newUserId);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        /// <summary>
        /// 更新连接的微信信息 (设备上报信息后调用)
        /// 如果传入 null，则不更新对应字段
        /// </summary>
        public Task<bool> UpdateConnectionWeChatInfoAsync(string connectionId, string wechatId, string? nickName=null)
        {
             if (_connections.TryGetValue(connectionId, out var connection))
             {
                 if (wechatId != null) connection.wechatId = wechatId;
                 if (nickName != null) connection.nickName = nickName;
                 return Task.FromResult(true);
             }
             return Task.FromResult(false);
        }

        /// <summary>
        /// 检查连接是否已认证 (关联了 UserId)
        /// </summary>
        public Task<bool> IsConnectionAuthenticatedAsync(string connectionId)
        {
            if (_connections.TryGetValue(connectionId, out var connection))
            {
                // Must have a valid UserId to be considered authenticated
                return Task.FromResult(!string.IsNullOrEmpty(connection.userId));
            }
            return Task.FromResult(false);
        }

        /// <summary>
        /// 检查用户是否在线
        /// </summary>
        public Task<bool> IsUserOnlineAsync(string userId)
        {
            if (_userConnections.TryGetValue(userId, out var connectionIds))
            {
                var hasActiveConnections = connectionIds
                    .Select(connId => _connections.TryGetValue(connId, out var conn) ? conn : null)
                    .Any(conn => conn != null && conn.isOnline);

                return Task.FromResult(hasActiveConnections);
            }

            return Task.FromResult(false);
        }

        /// <summary>
        /// 获取当前在线用户总数
        /// </summary>
        public Task<int> GetOnlineUserCountAsync()
        {
            var onlineUsers = _userConnections
                .Select(kvp => kvp.Value
                    .Select(connId => _connections.TryGetValue(connId, out var conn) ? conn : null)
                    .Any(conn => conn != null && conn.isOnline))
                .Count(isOnline => isOnline);

            return Task.FromResult(onlineUsers);
        }

        /// <summary>
        /// 获取连接统计概览
        /// </summary>
        public ConnectionStatistics GetStatistics()
        {
            var totalConnections = _connections.Count;
            var totalUsers = _userConnections.Count;
            var deviceTypeStats = _deviceTypeConnections
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count);
            var onlineUsers = _connections.Values.Count(conn => conn.isOnline);

            return new ConnectionStatistics
            {
                totalConnections = totalConnections,
                totalUsers = totalUsers,
                onlineUsers = onlineUsers,
                deviceTypeStatistics = deviceTypeStats,
                lastUpdated = DateTime.UtcNow
            };
        }


        /// <summary>
        /// 检查沉默客户端的任务
        /// <para>如果客户端连接超过40秒仍未上报WeChatId，则主动发送账号状态查询与 TriggerWechatPushTask 指令进行探测。</para>
        /// </summary>
        private void CheckSilentClients(object state)
        {
            try
            {
                var now = DateTime.UtcNow;
                foreach (var conn in _connections.Values)
                {
                    // 筛选条件：
                    // 1. WeChatId 为空 (说明还没上报)
                    // 2. 连接时长 > 40秒 (给足启动时间)
                    // 3. 最近 40 秒没有任何活动 (真正 silent，而不是“已在线但只是在收发心跳/消息”)
                    //
                    // 旧实现只看 connectedAt，不看 lastActivityAt。
                    // 结果是：连接建立超过 40 秒后，只要 wechatId 没同步进连接表，
                    // 即使客户端一直在发心跳、发消息、收消息，也会被误判成 silent。
                    // 这里把 “连接已建立足够久” 和 “最近确实没活动” 两个条件同时收紧。
                    if (string.IsNullOrEmpty(conn.wechatId)
                        && (now - conn.connectedAt).TotalSeconds > 40
                        && (now - conn.lastActivityAt).TotalSeconds > 40)
                    {
                        var channel = GetChannel(conn.connectionId);
                        if (channel != null && channel.Active)
                        {
                            _logger.LogWarning($"[SilentClientCheck] Client {conn.connectionId} (Dev:{conn.deviceType}) silent for >40s. Sending GetWeChatsReq + TriggerWechatPushTask...");

                            // 先发 3050 轻量账号状态查询。该链路不需要拉起微信 UI，
                            // 可以在服务端重启或微信状态已缓存时更快补齐连接表中的 wxid。
                            var getWeChatsMsg = new TransportMessage
                            {
                                Id = DateTime.UtcNow.Ticks,
                                MsgType = EnumMsgType.GetWeChatsReq,
                                Content = Any.Pack(new GetWeChatsReqMessage
                                {
                                    UnionId = 0,
                                    AccountType = EnumAccountType.Main
                                })
                            };
                            channel.WriteAndFlushAsync(getWeChatsMsg);

                            // 再构造 TriggerWechatPushTask 消息 (参考 AuthMessageHandler.cs)。
                            // 客户端会使用此空 ID 来触发自身状态上报，最终仍以 WeChatOnlineNotice 落库为准。
                            var syncMsg = new TransportMessage
                            {
                                Id = DateTime.UtcNow.Ticks,
                                MsgType = EnumMsgType.TriggerWechatPushTask,
                                Content = Any.Pack(new TriggerWechatPushTaskMessage
                                {
                                    WeChatId = "" // Empty ID force client to report self
                                })
                            };

                            // 发送指令
                            channel.WriteAndFlushAsync(syncMsg);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SilentClientCheck] Error occurred while checking silent clients.");
            }
        }
    }

    /// <summary>
    /// 连接统计数据模型
    /// </summary>
    public class ConnectionStatistics 
    {
        public int totalConnections { get; set; }
        public int totalUsers { get; set; }
        public int onlineUsers { get; set; }
        public Dictionary<string, int> deviceTypeStatistics { get; set; } = new Dictionary<string, int>();
        public DateTime lastUpdated { get; set; }
    }
}
