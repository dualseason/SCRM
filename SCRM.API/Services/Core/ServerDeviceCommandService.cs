using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SCRM.Services;
using SCRM.SHARED.Models;
using Jubo.JuLiao.IM.Wx.Proto;

namespace SCRM.API.Services
{
    public class ServerDeviceCommandService
    {
        private readonly ClientTaskService _clientTaskService;
        private readonly ConnectionManager _connectionManager;
        private readonly ILogger<ServerDeviceCommandService> _logger;

        public ServerDeviceCommandService(
            ClientTaskService clientTaskService, 
            ConnectionManager connectionManager,
            ILogger<ServerDeviceCommandService> logger)
        {
            _clientTaskService = clientTaskService;
            _connectionManager = connectionManager;
            _logger = logger;
        }

        public async Task<bool> SendMessageAsync(string deviceUuid, string recipientId, string content)
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("SendMessageAsync: Device {DeviceUuid} offline.", deviceUuid);
                return false;
            }

            // MsgType: 1070 (TalkToFriendTask)
            var result = await _clientTaskService.SendTalkToFriendTaskAsync(connectionId, recipientId, content);
            return result.success;
        }
        
        public async Task<bool> AcceptFriendRequestAsync(string deviceUuid, string friendId, string friendNick)
        {
             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId))
             {
                 _logger.LogWarning("AcceptFriendRequestAsync: Device {DeviceUuid} offline.", deviceUuid);
                 return false;
             }
             
             // MsgType: 1075 (AcceptFriendAddRequestTask)
             var taskId = DateTime.UtcNow.Ticks;
             var result = await _clientTaskService.SendAcceptFriendAddRequestTaskAsync(connectionId, friendId, friendNick, taskId);
             return result.success;
        }

        public async Task<bool> ExecuteGroupActionAsync(string deviceUuid, string chatRoomId, int action, string content, int intValue)
        {
             var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
             if (string.IsNullOrEmpty(connectionId))
             {
                 _logger.LogWarning("ExecuteGroupActionAsync: Device {DeviceUuid} offline.", deviceUuid);
                 return false;
             }
             
             // MsgType: 1213 (ChatRoomActionTask)
             var taskId = DateTime.UtcNow.Ticks;
             var result = await _clientTaskService.SendChatRoomActionTaskAsync(connectionId, chatRoomId, (EnumChatRoomAction)action, content, intValue, taskId);
             return result.success;
        }

        public async Task<bool> RequestScreenShotAsync(string deviceUuid)
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId)) 
            {
                _logger.LogError("RequestScreenShotAsync: Device {DeviceUuid} not connected.", deviceUuid);
                return false;
            }

            // MsgType: 1282 (ScreenShotTask)
            var taskId = DateTime.UtcNow.Ticks;
            var result = await _clientTaskService.SendScreenShotTaskAsync(connectionId, taskId);
            
            _logger.LogInformation("Sent RequestScreenShot ({TaskId}) to {DeviceUuid} result: {Success}", taskId, deviceUuid, result.success);
            return result.success;
        }

        public async Task<bool> PushAccountSettingsAsync(string deviceUuid, WechatAccountSettings settings)
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId)) return false;
            
            // MsgType: 1382 (SetConfigTask)
            // Note: Currently ClientTaskService.SendSetConfigTaskAsync takes Dictionaries.
            // We need to map WechatAccountSettings properties to the dictionary format expected by SetConfigTask.
            // For now, if the original intention was just to push "AutoAcceptLuckyMoney", we map that.
            
            var boolConfs = new System.Collections.Generic.Dictionary<string, bool>();
            // AutoAcceptLuckyMoney is bool, assuming we always push it or checking logic
            boolConfs.Add("AutoAcceptLuckyMoney", settings.AutoAcceptLuckyMoney);

            // Add other settings if needed
            boolConfs.Add("AutoAcceptFriendRequest", settings.AutoAcceptFriendRequest);
            boolConfs.Add("AutoLikeMoments", settings.AutoLikeMoments);

            var result = await _clientTaskService.SendSetConfigTaskAsync(connectionId, boolConfs, null, null); 
            return result;
        }
    }
}
