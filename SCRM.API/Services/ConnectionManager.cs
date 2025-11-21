using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SCRM.Services
{
    public class ConnectionManager : IConnectionManager
    {
        private readonly ILogger<ConnectionManager> _logger;
        private readonly ConcurrentDictionary<string, UserConnectionInfo> _connections = new ConcurrentDictionary<string, UserConnectionInfo>();
        private readonly ConcurrentDictionary<string, HashSet<string>> _userConnections = new ConcurrentDictionary<string, HashSet<string>>();
        private readonly ConcurrentDictionary<string, HashSet<string>> _deviceTypeConnections = new ConcurrentDictionary<string, HashSet<string>>();

        public ConnectionManager(ILogger<ConnectionManager> logger)
        {
            _logger = logger;
        }

        public Task AddConnectionAsync(string userId, string connectionId, string deviceType, string deviceInfo = "")
        {
            var connectionInfo = new UserConnectionInfo
            {
                UserId = userId,
                ConnectionId = connectionId,
                DeviceType = deviceType,
                DeviceInfo = deviceInfo,
                ConnectedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
            };

            // 添加到连接字典
            _connections.TryAdd(connectionId, connectionInfo);

            // 添加到用户连接映射
            var userConns = _userConnections.GetOrAdd(userId, _ => new HashSet<string>());
            userConns.Add(connectionId);

            // 添加到设备类型连接映射
            var deviceConns = _deviceTypeConnections.GetOrAdd(deviceType, _ => new HashSet<string>());
            deviceConns.Add(connectionId);

            _logger.LogInformation("连接已添加 - 用户ID: {UserId}, 连接ID: {ConnectionId}, 设备类型: {DeviceType}",
                userId, connectionId, deviceType);

            return Task.CompletedTask;
        }

        public Task RemoveConnectionAsync(string connectionId)
        {
            if (_connections.TryRemove(connectionId, out var connectionInfo))
            {
                // 从用户连接映射中移除
                if (_userConnections.TryGetValue(connectionInfo.UserId, out var userConns))
                {
                    userConns.Remove(connectionId);
                    if (userConns.Count == 0)
                    {
                        _userConnections.TryRemove(connectionInfo.UserId, out _);
                    }
                }

                // 从设备类型连接映射中移除
                if (_deviceTypeConnections.TryGetValue(connectionInfo.DeviceType, out var deviceConns))
                {
                    deviceConns.Remove(connectionId);
                    if (deviceConns.Count == 0)
                    {
                        _deviceTypeConnections.TryRemove(connectionInfo.DeviceType, out _);
                    }
                }

                _logger.LogInformation("连接已移除 - 用户ID: {UserId}, 连接ID: {ConnectionId}, 设备类型: {DeviceType}",
                    connectionInfo.UserId, connectionId, connectionInfo.DeviceType);
            }

            return Task.CompletedTask;
        }

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

        public Task<IEnumerable<UserConnectionInfo>> GetAllConnectionsAsync()
        {
            return Task.FromResult(_connections.Values.AsEnumerable());
        }

        public Task<UserConnectionInfo?> GetConnectionAsync(string connectionId)
        {
            _connections.TryGetValue(connectionId, out var connection);
            return Task.FromResult(connection);
        }

        public Task UpdateConnectionActivityAsync(string connectionId)
        {
            if (_connections.TryGetValue(connectionId, out var connection))
            {
                connection.LastActivityAt = DateTime.UtcNow;
            }
            return Task.CompletedTask;
        }

        public Task<bool> IsUserOnlineAsync(string userId)
        {
            if (_userConnections.TryGetValue(userId, out var connectionIds))
            {
                var hasActiveConnections = connectionIds
                    .Select(connId => _connections.TryGetValue(connId, out var conn) ? conn : null)
                    .Any(conn => conn != null && conn.IsOnline);

                return Task.FromResult(hasActiveConnections);
            }

            return Task.FromResult(false);
        }

        public Task<int> GetOnlineUserCountAsync()
        {
            var onlineUsers = _userConnections
                .Select(kvp => kvp.Value
                    .Select(connId => _connections.TryGetValue(connId, out var conn) ? conn : null)
                    .Any(conn => conn != null && conn.IsOnline))
                .Count(isOnline => isOnline);

            return Task.FromResult(onlineUsers);
        }

        // 获取统计信息
        public ConnectionStatistics GetStatistics()
        {
            var totalConnections = _connections.Count;
            var totalUsers = _userConnections.Count;
            var deviceTypeStats = _deviceTypeConnections
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count);
            var onlineUsers = _connections.Values.Count(conn => conn.IsOnline);

            return new ConnectionStatistics
            {
                TotalConnections = totalConnections,
                TotalUsers = totalUsers,
                OnlineUsers = onlineUsers,
                DeviceTypeStatistics = deviceTypeStats,
                LastUpdated = DateTime.UtcNow
            };
        }
    }

    public class ConnectionStatistics
    {
        public int TotalConnections { get; set; }
        public int TotalUsers { get; set; }
        public int OnlineUsers { get; set; }
        public Dictionary<string, int> DeviceTypeStatistics { get; set; } = new Dictionary<string, int>();
        public DateTime LastUpdated { get; set; }
    }
}