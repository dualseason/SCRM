using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SCRM.API.Services.Data;
using SCRM.Services;
using SCRM.Services.Data;
using SCRM.SHARED.Models;
using SCRM.SHARED.Models.Dtos;
using Jubo.JuLiao.IM.Wx.Proto;

namespace SCRM.API.Services.Core
{
    public class ServerDeviceCommandService
    {
        private static readonly HashSet<string> KnownDeviceBoolConfigKeys = new(StringComparer.Ordinal)
        {
            "fastSend",
            "silentFunc",
            "silentAccept",
            "autoPic",
            "autoLogin",
            "addInWw",
            "lightscn",
            "forceRun",
            "disturb",
            "moreLog",
            "wx_show_alias",
            "wx_can_delete",
            "wx_can_block",
            "wx_can_exitGroup",
            "wx_del_conv",
            "wx_can_logout",
            "wx_can_changeAcnt",
            "wx_can_acntInfo",
            "wx_show_toast",
            "wx_can_sendcard"
        };

        private static readonly HashSet<string> KnownDeviceIntConfigKeys = new(StringComparer.Ordinal)
        {
            "keepWake",
            "port"
        };

        private static readonly HashSet<string> KnownDeviceStrConfigKeys = new(StringComparer.Ordinal)
        {
            "host",
            "fileUpUrl"
        };

        private readonly ClientTaskService _clientTaskService;
        private readonly ConnectionManager _connectionManager;
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
        private readonly ILogger<ServerDeviceCommandService> _logger;

        public ServerDeviceCommandService(
            ClientTaskService clientTaskService, 
            ConnectionManager connectionManager,
            IDbContextFactory<ApplicationDbContext> dbContextFactory,
            ILogger<ServerDeviceCommandService> logger)
        {
            _clientTaskService = clientTaskService;
            _connectionManager = connectionManager;
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        public async Task<TaskResult> SendMessageAsync(string deviceUuid, string recipientId, string content, int type = 1, string atIds = "")
        {
            var normalizedType = NormalizeTalkToFriendContentType(type, content);
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("SendMessageAsync: Device {DeviceUuid} offline.", deviceUuid);
                return TaskResult.Fail("设备未在线或连接不存在");
            }

            // MsgType: 1070 (TalkToFriendTask)
            _logger.LogInformation("SendMessageAsync: Dispatching Task to {RecipientId} with ContentType={Type} ({EnumName})", 
                recipientId, normalizedType, ((EnumContentType)normalizedType).ToString());
                
            var result = await _clientTaskService.SendTalkToFriendTaskAsync(connectionId, recipientId, content, (EnumContentType)normalizedType, atIds);
            return result;
        }

        /// <summary>
        /// 归一化聊天内容类型。
        /// <para>网页历史版本曾把视频等媒体按文本链下发，导致服务端日志显示 15 秒文本超时。
        /// 这里按 URL/JSON 文件载荷做兜底，不改变调用方明确传入的有效媒体类型。</para>
        /// </summary>
        private static int NormalizeTalkToFriendContentType(int type, string? content)
        {
            if (type != (int)EnumContentType.UnknownContent && type != (int)EnumContentType.Text)
            {
                return type;
            }

            var text = content?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return type;
            }

            var lower = text.Split('?', '#')[0].ToLowerInvariant();
            if (lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") || lower.EndsWith(".png")
                || lower.EndsWith(".gif") || lower.EndsWith(".webp") || lower.EndsWith(".bmp"))
            {
                return (int)EnumContentType.Picture;
            }

            if (lower.EndsWith(".mp4") || lower.EndsWith(".mov") || lower.EndsWith(".m4v")
                || lower.EndsWith(".3gp") || lower.EndsWith(".avi")
                || lower.EndsWith(".mkv") || lower.EndsWith(".webm"))
            {
                return (int)EnumContentType.Video;
            }

            if (text.StartsWith("{", StringComparison.Ordinal) && text.Contains("\"url\"", StringComparison.OrdinalIgnoreCase))
            {
                return (int)EnumContentType.File;
            }

            if (type == 8)
            {
                return (int)EnumContentType.File;
            }

            return type == (int)EnumContentType.UnknownContent ? (int)EnumContentType.Text : type;
        }
        
        public async Task<TaskResult> AcceptFriendRequestAsync(
            string deviceUuid,
            string friendId,
            string friendNick,
            string remark = "",
            string replyMsg = "",
            bool addWithWW = false,
            bool onlyWW = false,
            int permission = 0,
            AcceptFriendAddRequestTaskMessage.Types.EnumFriendAddOperation operation = AcceptFriendAddRequestTaskMessage.Types.EnumFriendAddOperation.Accept,
            string weChatId = "")
        {
             var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(AcceptFriendRequestAsync));
             if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("设备未在线或连接不存在");

             var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                 ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(AcceptFriendRequestAsync))
                 : weChatId.Trim();
             if (string.IsNullOrEmpty(ownerWxid)) return TaskResult.Fail("设备当前没有可用微信账号");
              
             // MsgType: 1075 (AcceptFriendAddRequestTask)
             var taskId = DateTime.UtcNow.Ticks;
             return await _clientTaskService.SendAcceptFriendAddRequestTaskAsync(
                 connectionId,
                 friendId,
                 friendNick,
                 taskId,
                 operation,
                 remark,
                  replyMsg,
                  addWithWW,
                  onlyWW,
                  permission,
                  ownerWxid);
        }


        public async Task<TaskResult> SendGroupMessageAsync(string deviceUuid, List<string> friendIds, string content, int contentType = 0, int duration = 0, bool original = false)
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("SendGroupMessageAsync: Device {DeviceUuid} offline.", deviceUuid);
                return TaskResult.Fail("设备未在线");
            }

            return await _clientTaskService.SendWeChatGroupSendTaskAsync(connectionId, friendIds, content, contentType, duration, original);
        }

        /// <summary>
        /// 请求客户端回传微信“群发助手”历史。
        /// <para>这是异步快照任务：下发成功只表示客户端已收到请求，真实历史由 GroupSendHistoryPushNotice 上报并落库。</para>
        /// </summary>
        public async Task<TaskResult> SyncMassSendHistoryAsync(string deviceUuid, long endTime = 0, string weChatId = "")
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("SyncMassSendHistoryAsync: Device {DeviceUuid} offline.", deviceUuid);
                return TaskResult.Fail("设备未在线");
            }

            var taskId = DateTime.UtcNow.Ticks;
            var queued = await _clientTaskService.SendGetGroupSendHistoryTaskAsync(connectionId, taskId, endTime, weChatId);
            return queued
                ? TaskResult.Ok(taskId, "群发历史同步指令已下发，等待客户端上报历史")
                : TaskResult.Fail(taskId, "群发历史同步指令下发失败");
        }

        /// <summary>
        /// 请求客户端拉取好友申请历史/补偿列表。
        /// </summary>
        public async Task<TaskResult> PullFriendAddReqListAsync(string deviceUuid, long startTime = 0, bool onlyNew = true, bool getAll = false, string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(PullFriendAddReqListAsync));
            if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("设备未在线或连接不存在");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(PullFriendAddReqListAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return TaskResult.Fail("设备当前没有可用微信账号");

            return await _clientTaskService.SendPullFriendAddReqListTaskAsync(connectionId, startTime, onlyNew, getAll, ownerWxid, DateTime.UtcNow.Ticks);
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


        public async Task<TaskResult> AddFriendInChatRoomAsync(string deviceUuid, string chatRoomId, string friendId, string message, string remark = "", int permission = 0)
        {
            _logger.LogInformation("AddFriendInChatRoomAsync: Device={DeviceUuid}, ChatRoom={ChatRoomId}, Friend={FriendId}, Permission={Permission}",
                deviceUuid, chatRoomId, friendId, permission);
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(AddFriendInChatRoomAsync));
            if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("设备未在线或连接不存在");

            var ownerWxid = await GetRequiredWeChatIdAsync(deviceUuid, nameof(AddFriendInChatRoomAsync));
            if (string.IsNullOrEmpty(ownerWxid)) return TaskResult.Fail("设备当前没有可用微信账号");

            return await _clientTaskService.SendAddFriendInChatRoomTaskAsync(connectionId, chatRoomId, friendId, message, remark, permission, weChatId: ownerWxid);
        }

        public async Task<TaskResult> JoinGroupByQrAsync(string deviceUuid, string qrUrl = "", string qrContent = "")
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("JoinGroupByQrAsync: Device {DeviceUuid} offline.", deviceUuid);
                return TaskResult.Fail("设备未在线或连接不存在");
            }

            return await _clientTaskService.SendJoinGroupByQrTaskAsync(connectionId, qrUrl, qrContent, string.Empty, DateTime.UtcNow.Ticks);
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> PullChatRoomQrCodeAsync(string deviceUuid, string chatRoomId)
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("PullChatRoomQrCodeAsync: Device {DeviceUuid} offline.", deviceUuid);
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");
            }

            return await _clientTaskService.SendPullChatRoomQrCodeTaskAsync(connectionId, chatRoomId, string.Empty, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 执行手机/微信进程侧设备操作，承接 PhoneActionTask(1223)。
        /// <para>PhoneCall 由 ClientTaskService 按“写出成功”返回，避免系统拨号无标准回包导致误超时。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> ExecutePhoneActionAsync(
            string deviceUuid,
            int action,
            string strParam = "",
            int intParam = 0,
            string weChatId = "",
            string imei = "")
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("ExecutePhoneActionAsync: Device {DeviceUuid} offline.", deviceUuid);
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备未在线或连接不存在");
            }

            if (!System.Enum.IsDefined(typeof(EnumPhoneAction), action))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail($"不支持的手机操作：{action}");
            }

            var phoneAction = (EnumPhoneAction)action;
            if (phoneAction == EnumPhoneAction.None)
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("不支持的手机操作：None");
            }

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await ResolveOptionalCurrentWechatIdAsync(deviceUuid)
                : weChatId.Trim();
            var deviceImei = string.IsNullOrWhiteSpace(imei)
                ? await GetDeviceImeiAsync(deviceUuid)
                : imei.Trim();

            return await _clientTaskService.SendPhoneActionTaskAsync(
                connectionId,
                phoneAction,
                DateTime.UtcNow.Ticks,
                strParam ?? string.Empty,
                intParam,
                ownerWxid,
                deviceImei);
        }

        /// <summary>
        /// 拉取当前微信个人二维码。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> PullWeChatQrCodeAsync(string deviceUuid, string weChatId = "")
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("PullWeChatQrCodeAsync: Device {DeviceUuid} offline.", deviceUuid);
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备未在线或连接不存在");
            }

            return await _clientTaskService.SendPullWeChatQrCodeTaskAsync(connectionId, weChatId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 拉取附近 POI 列表。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> GetPoiListAsync(string deviceUuid, double lat, double lng, string keyword = "", string weChatId = "")
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("GetPoiListAsync: Device {DeviceUuid} offline.", deviceUuid);
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备未在线或连接不存在");
            }

            return await _clientTaskService.SendGetPoiListTaskAsync(connectionId, lat, lng, keyword, weChatId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 拉取表情详细信息。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> PullEmojiInfoAsync(string deviceUuid, string md5, string weChatId = "")
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("PullEmojiInfoAsync: Device {DeviceUuid} offline.", deviceUuid);
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备未在线或连接不存在");
            }

            return await _clientTaskService.SendPullEmojiInfoTaskAsync(connectionId, md5, weChatId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 对指定聊天表情消息执行消息级补图。
        /// <para>服务端保存 MsgSvrId 上下文，1273 返回后自动下发 CDNDownloadFileTask(ChatMsgEmoji)。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> PullEmojiInfoForMessageAsync(
            string deviceUuid,
            string md5,
            long msgSvrId,
            string friendId = "",
            string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(PullEmojiInfoForMessageAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备未在线或连接不存在");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(PullEmojiInfoForMessageAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备当前没有可用微信账号");

            return await _clientTaskService.SendPullEmojiInfoForMessageTaskAsync(
                connectionId,
                ownerWxid,
                md5,
                msgSvrId,
                friendId,
                DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 搜索微信联系人。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> FindContactAsync(string deviceUuid, string content, string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(FindContactAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备未在线或连接不存在");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(FindContactAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备当前没有可用微信账号");

            return await _clientTaskService.SendFindContactTaskAsync(connectionId, content, ownerWxid, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 查询微信定位。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> GetWeChatLocationAsync(string deviceUuid, bool noCache = false, string weChatId = "")
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("GetWeChatLocationAsync: Device {DeviceUuid} offline.", deviceUuid);
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备未在线或连接不存在");
            }

            return await _clientTaskService.SendWeChatLocationTaskAsync(connectionId, noCache, weChatId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 查询微信零钱和银行卡摘要。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> GetWalletBalanceAsync(string deviceUuid, int flag = 0, string weChatId = "")
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("GetWalletBalanceAsync: Device {DeviceUuid} offline.", deviceUuid);
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备未在线或连接不存在");
            }

            return await _clientTaskService.SendWalletBalanceTaskAsync(connectionId, flag, weChatId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 查询手机电量、网络和存储状态。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> GetPhoneStateAsync(string deviceUuid, string imei = "", string weChatId = "")
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("GetPhoneStateAsync: Device {DeviceUuid} offline.", deviceUuid);
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备未在线或连接不存在");
            }

            return await _clientTaskService.SendPhoneStateTaskAsync(connectionId, imei, weChatId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 下发手机短信发送任务。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendSmsAsync(
            string deviceUuid,
            string number,
            string content,
            string weChatId = "",
            string imei = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(SendSmsAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备未在线或连接不存在");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await ResolveOptionalCurrentWechatIdAsync(deviceUuid)
                : weChatId.Trim();

            var finalImei = string.IsNullOrWhiteSpace(imei)
                ? await GetDeviceImeiAsync(deviceUuid)
                : imei.Trim();

            return await _clientTaskService.SendSmsTaskAsync(
                connectionId,
                ownerWxid,
                finalImei,
                number,
                content,
                DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 下发短信历史拉取任务。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> PullSmsAsync(
            string deviceUuid,
            long startTime,
            long endTime,
            string weChatId = "",
            string imei = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(PullSmsAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备未在线或连接不存在");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await ResolveOptionalCurrentWechatIdAsync(deviceUuid)
                : weChatId.Trim();

            var finalImei = string.IsNullOrWhiteSpace(imei)
                ? await GetDeviceImeiAsync(deviceUuid)
                : imei.Trim();

            return await _clientTaskService.SendPullSmsTaskAsync(
                connectionId,
                ownerWxid,
                finalImei,
                startTime,
                endTime,
                DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 下发通话记录拉取任务。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> PullCallLogsAsync(
            string deviceUuid,
            long startTime,
            long endTime,
            string weChatId = "",
            string imei = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(PullCallLogsAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备未在线或连接不存在");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await ResolveOptionalCurrentWechatIdAsync(deviceUuid)
                : weChatId.Trim();

            var finalImei = string.IsNullOrWhiteSpace(imei)
                ? await GetDeviceImeiAsync(deviceUuid)
                : imei.Trim();

            return await _clientTaskService.SendPullCallLogTaskAsync(
                connectionId,
                ownerWxid,
                finalImei,
                startTime,
                endTime,
                DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求 Android 同步企微用户列表。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SyncQwUsersAsync(string deviceUuid, string weChatId = "")
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("SyncQwUsersAsync: Device {DeviceUuid} offline.", deviceUuid);
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备未在线或连接不存在");
            }

            return await _clientTaskService.SendTriggerQwUserPushTaskAsync(connectionId, weChatId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求 Android 回传指定时间段内的聊天消息 MsgSvrId 快照。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SyncChatMsgIdsAsync(string deviceUuid, long startTime, long endTime, string weChatId = "")
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("SyncChatMsgIdsAsync: Device {DeviceUuid} offline.", deviceUuid);
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备未在线或连接不存在");
            }

            return await _clientTaskService.SendTriggerChatMsgIdsPushTaskAsync(connectionId, startTime, endTime, weChatId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端回传历史聊天消息。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SyncHistoryMessagesAsync(
            string deviceUuid,
            string friendId = "",
            long startTime = 0,
            long endTime = 0,
            int flag = 0,
            int count = 50,
            string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(SyncHistoryMessagesAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(SyncHistoryMessagesAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            return await _clientTaskService.SendTriggerHistoryMsgPushTaskAsync(connectionId, ownerWxid, friendId, startTime, endTime, flag, count, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端同步指定会话已读状态。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SyncMessageReadAsync(string deviceUuid, string friendId, string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(SyncMessageReadAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(SyncMessageReadAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            return await _clientTaskService.SendTriggerMessageReadTaskAsync(connectionId, ownerWxid, friendId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端回传未读会话列表。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SyncUnreadListAsync(string deviceUuid, string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(SyncUnreadListAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(SyncUnreadListAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            return await _clientTaskService.SendTriggerUnreadPushTaskAsync(connectionId, ownerWxid, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端同步单个会话未读状态。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SyncConversationUnreadAsync(string deviceUuid, string friendId, string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(SyncConversationUnreadAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(SyncConversationUnreadAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            return await _clientTaskService.SendTriggerUnReadTaskAsync(connectionId, ownerWxid, friendId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端异步同步当前微信好友列表。
        /// <para>
        /// 对齐 62203 的 SyncFriendListAsyncReq(3056)：Android 收到后复用本地联系人全量上报链，
        /// 主数据仍由 FriendPushNotice(2026) 进入 DbHelper.SaveContacts 持久化；这里不直接写联系人表。
        /// </para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SyncFriendListAsync(string deviceUuid, string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(SyncFriendListAsync));
            var taskId = DateTime.UtcNow.Ticks;
            if (string.IsNullOrEmpty(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "设备未在线或连接不存在");
            }

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await ResolveOptionalCurrentWechatIdAsync(deviceUuid)
                : weChatId.Trim();

            // 如果服务端刚启动还未拿到账号快照，仍允许 WeChatId 为空下发；62203/SmRun 会使用当前登录微信兜底。
            var sent = await _clientTaskService.SendSyncFriendListTaskAsync(connectionId, ownerWxid, taskId);
            _logger.LogInformation(
                "SyncFriendListAsync dispatched: Device={DeviceUuid}, ConnectionId={ConnectionId}, WeChatId={WeChatId}, TaskId={TaskId}, Sent={Sent}",
                deviceUuid,
                connectionId,
                ownerWxid,
                taskId,
                sent);

            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(taskId, "好友同步指令已下发，等待手机端回传 FriendPushNotice")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "好友同步指令下发失败");
        }

        /// <summary>
        /// 请求客户端回传当前微信账号快照。
        /// <para>对齐 62203 的 GetWeChatsReq(3050)，结果由 GetWeChatsRsp(3051) 异步上报并更新账号展示。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> RefreshWeChatAccountsAsync(string deviceUuid)
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(RefreshWeChatAccountsAsync));
            if (string.IsNullOrEmpty(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备未在线或连接不存在");
            }

            var sent = await _clientTaskService.SendGetWeChatsReqAsync(connectionId);
            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(0, "微信账号状态查询指令已下发，等待手机端回传 3051")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail("微信账号状态查询指令下发失败");
        }

        /// <summary>
        /// 请求客户端回传业务联系人列表。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SyncBizContactsAsync(string deviceUuid, string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(SyncBizContactsAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(SyncBizContactsAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            return await _clientTaskService.SendTriggerBizContactPushTaskAsync(connectionId, ownerWxid, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端回传企微会话列表。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SyncQwConversationsAsync(string deviceUuid, long startTime = 0, long endTime = 0, int limit = 100, int offset = 0, string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(SyncQwConversationsAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(SyncQwConversationsAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            return await _clientTaskService.SendTriggerQwConvPushTaskAsync(connectionId, ownerWxid, startTime, endTime, limit, offset, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端同步联系人标签列表。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SyncContactLabelsAsync(string deviceUuid, string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(SyncContactLabelsAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(SyncContactLabelsAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            return await _clientTaskService.SendTriggerLabelPushTaskAsync(connectionId, ownerWxid, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 创建或重命名联系人标签，也可通过 AddList/DelList 调整标签成员。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SaveContactLabelAsync(
            string deviceUuid,
            string labelName,
            int labelId = 0,
            string addList = "",
            string delList = "",
            string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(SaveContactLabelAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(SaveContactLabelAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            return await _clientTaskService.SendContactLabelTaskAsync(
                connectionId,
                ownerWxid,
                labelName,
                labelId,
                addList,
                delList,
                DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 删除联系人标签。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> DeleteContactLabelAsync(string deviceUuid, int labelId, string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(DeleteContactLabelAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(DeleteContactLabelAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            return await _clientTaskService.SendContactLabelDeleteTaskAsync(connectionId, ownerWxid, labelId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 设置单个好友的完整标签 ID 集合。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SetContactLabelsAsync(
            string deviceUuid,
            string friendId,
            IEnumerable<int>? labelIds,
            string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(SetContactLabelsAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(SetContactLabelsAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            return await _clientTaskService.SendContactSetLabelTaskAsync(
                connectionId,
                ownerWxid,
                friendId,
                labelIds,
                DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端按 MsgSvrId 补偿单条聊天消息。
        /// <para>下发成功后等待 RequestTalkMsgTaskResultNotice 异步回传并由 ChatMessageHandler 落库。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> RequestTalkMsgAsync(string deviceUuid, long msgSvrId, string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(RequestTalkMsgAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(RequestTalkMsgAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            return await _clientTaskService.SendRequestTalkMsgTaskAsync(connectionId, ownerWxid, msgSvrId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端按 MsgSvrId 补偿原始聊天正文/XML。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> RequestTalkContentAsync(string deviceUuid, long msgSvrId, string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(RequestTalkContentAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(RequestTalkContentAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            return await _clientTaskService.SendRequestTalkContentTaskAsync(connectionId, ownerWxid, msgSvrId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端补偿聊天消息详情。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> RequestTalkDetailAsync(
            string deviceUuid,
            string friendId,
            long msgId,
            string msgSvrId = "",
            string md5 = "",
            bool getOriginal = false,
            string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(RequestTalkDetailAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(RequestTalkDetailAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            return await _clientTaskService.SendRequestTalkDetailTaskAsync(
                connectionId,
                ownerWxid,
                friendId,
                msgId,
                msgSvrId,
                md5,
                getOriginal,
                DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端对指定语音消息执行语音转文字。
        /// <para>结果由通用 TaskResultNotice 返回，并由 TaskMessageHandler 写入 VoiceToTextLogs。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> VoiceTransTextAsync(
            string deviceUuid,
            string friendId,
            long msgSvrId,
            string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(VoiceTransTextAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(VoiceTransTextAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            return await _clientTaskService.SendVoiceTransTextTaskAsync(
                connectionId,
                ownerWxid,
                friendId,
                msgSvrId,
                DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端撤回指定聊天消息。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> RevokeMessageAsync(
            string deviceUuid,
            string friendId,
            long msgSvrId,
            string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(RevokeMessageAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(RevokeMessageAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            return await _clientTaskService.SendRevokeMessageTaskAsync(
                connectionId,
                ownerWxid,
                friendId,
                msgSvrId,
                DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端转发一条已有聊天消息。
        /// <para>talker 是原消息所在会话 wxid，friendIds 是目标接收人列表，多个目标用逗号分隔。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> ForwardMessageAsync(
            string deviceUuid,
            string talker,
            long msgSvrId,
            string friendIds,
            string extMsg = "",
            string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(ForwardMessageAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(ForwardMessageAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            return await _clientTaskService.SendForwardMessageTaskAsync(
                connectionId,
                ownerWxid,
                talker,
                msgSvrId,
                friendIds,
                extMsg,
                DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端转发多条已有聊天消息。
        /// <para>talker 是原消息所在会话，msgIds 是原消息服务器 ID 列表，friendIds 是目标接收人列表。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> ForwardMultiMessageAsync(
            string deviceUuid,
            string talker,
            IEnumerable<long>? msgIds,
            string friendIds,
            string extMsg = "",
            bool sendRecord = false,
            string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(ForwardMultiMessageAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(ForwardMultiMessageAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            return await _clientTaskService.SendForwardMultiMessageTaskAsync(
                connectionId,
                ownerWxid,
                talker,
                msgIds,
                friendIds,
                extMsg,
                sendRecord,
                DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端按原始内容转发消息。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> ForwardMessageByContentAsync(
            string deviceUuid,
            string friendIds,
            long msgSvrId,
            int msgType,
            string content,
            string thumb = "",
            string extMsg = "",
            string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(ForwardMessageByContentAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(ForwardMessageByContentAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            return await _clientTaskService.SendForwardMessageByContentTaskAsync(
                connectionId,
                ownerWxid,
                friendIds,
                msgSvrId,
                msgType,
                content,
                thumb,
                extMsg,
                DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端清空微信端聊天记录。
        /// <para>该操作不删除 SCRM 服务端消息库，等待微信端回包确认真实执行结果。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> ClearAllChatMsgAsync(
            string deviceUuid,
            int flag = 0,
            string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(ClearAllChatMsgAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(ClearAllChatMsgAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            return await _clientTaskService.SendClearAllChatMsgTaskAsync(
                connectionId,
                ownerWxid,
                flag,
                DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 请求客户端查询红包详情。
        /// <para>协议没有 TaskId，真实结果通过 QueryHbDetailTaskResultNotice 异步推送。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> QueryHbDetailAsync(
            string deviceUuid,
            string hbUrl,
            string weChatId = "")
        {
            if (string.IsNullOrWhiteSpace(hbUrl))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("红包链接为空");
            }

            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(QueryHbDetailAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(QueryHbDetailAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            var queued = await _clientTaskService.SendQueryHbDetailTaskAsync(connectionId, ownerWxid, hbUrl.Trim());
            return queued
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(0, "红包详情查询任务已下发，等待客户端异步结果")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail("红包详情查询任务下发失败");
        }

        /// <summary>
        /// 请求客户端查询红包状态。
        /// <para>协议没有 TaskId，真实结果通过 QueryHbStatusTaskResultNotice 异步推送。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> QueryHbStatusAsync(
            string deviceUuid,
            string hbUrl,
            string weChatId = "")
        {
            if (string.IsNullOrWhiteSpace(hbUrl))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("红包链接为空");
            }

            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(QueryHbStatusAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(QueryHbStatusAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            var queued = await _clientTaskService.SendQueryHbStatusTaskAsync(connectionId, ownerWxid, hbUrl.Trim());
            return queued
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(0, "红包状态查询任务已下发，等待客户端异步结果")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail("红包状态查询任务下发失败");
        }

        /// <summary>
        /// 请求客户端发送微信红包。
        /// <para>金额单位为分；支付密码只参与本次下发，不持久化、不记录日志。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendLuckyMoneyAsync(
            string deviceUuid,
            string friendId,
            int money,
            int number,
            string passwd,
            string wish = "",
            string weChatId = "")
        {
            if (string.IsNullOrWhiteSpace(friendId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("红包接收人为空");
            }

            if (money < 1 || money > 20000)
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("红包金额必须在 1 到 20000 分之间");
            }

            if (number < 1 || number > 100)
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("红包个数必须在 1 到 100 之间");
            }

            if (!IsValidPaymentPassword(passwd))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("支付密码必须为 6 位数字");
            }

            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(SendLuckyMoneyAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(SendLuckyMoneyAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            var taskId = DateTime.UtcNow.Ticks;
            return await _clientTaskService.SendLuckyMoneyTaskAsync(
                connectionId,
                ownerWxid,
                friendId.Trim(),
                money,
                number,
                passwd.Trim(),
                wish?.Trim() ?? string.Empty,
                taskId);
        }

        /// <summary>
        /// 请求客户端执行微信转账。
        /// <para>金额单位为分；群内转账时 roomId 为群会话 ID，friendId 为收款人 wxid。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> RemittanceAsync(
            string deviceUuid,
            string friendId,
            int money,
            string passwd,
            string memo = "",
            string roomId = "",
            string weChatId = "")
        {
            if (string.IsNullOrWhiteSpace(friendId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("转账收款人为空");
            }

            if (money < 1)
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("转账金额必须大于 0 分");
            }

            if (!IsValidPaymentPassword(passwd))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("支付密码必须为 6 位数字");
            }

            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(RemittanceAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(RemittanceAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            var taskId = DateTime.UtcNow.Ticks;
            return await _clientTaskService.SendRemittanceTaskAsync(
                connectionId,
                ownerWxid,
                friendId.Trim(),
                money,
                passwd.Trim(),
                memo?.Trim() ?? string.Empty,
                taskId,
                roomId?.Trim() ?? string.Empty);
        }

        /// <summary>
        /// 请求客户端执行微信账号登出。
        /// <para>下发成功后不立即强制服务端离线，等待 AccountLogoutNotice 或连接断开事件校准状态。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> WechatLogoutAsync(
            string deviceUuid,
            string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(WechatLogoutAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(WechatLogoutAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            var queued = await _clientTaskService.SendWechatLogoutTaskAsync(connectionId, ownerWxid);
            return queued
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(0, "微信登出任务已下发，等待客户端上报账号状态")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail("微信登出任务下发失败");
        }

        /// <summary>
        /// 请求客户端下载微信 CDN 媒体文件。
        /// <para>结果由 CDNDownloadResultNotice 异步回传并按 MsgSvrId 回填消息媒体 URL。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> DownloadCdnFileAsync(
            string deviceUuid,
            string cdnUrl,
            string cdnKey,
            int fileType,
            string fileId = "",
            string fileFmt = "",
            int fileSize = 0,
            long msgSvrId = 0,
            string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(DownloadCdnFileAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(DownloadCdnFileAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            var normalizedFileType = System.Enum.IsDefined(typeof(CDNFileType), fileType)
                ? (CDNFileType)fileType
                : CDNFileType.ChatMsgFile;

            return await _clientTaskService.SendCDNDownloadFileTaskAsync(
                connectionId,
                ownerWxid,
                cdnUrl,
                cdnKey,
                normalizedFileType,
                fileId,
                fileFmt,
                fileSize,
                msgSvrId,
                DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 启动好友检测/清粉任务。
        /// <para>OnlyCheck=true 表示只检测不删除，避免误触发删除好友。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> StartFriendDetectAsync(
            string deviceUuid,
            string message,
            bool onlyCheck = true,
            int skipHour = 24,
            int mode = 0,
            int max = 0,
            string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(StartFriendDetectAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(StartFriendDetectAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            return await _clientTaskService.SendPostFriendDetectTaskAsync(
                connectionId,
                ownerWxid,
                message,
                onlyCheck,
                skipHour,
                mode,
                max,
                DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 停止好友检测/清粉任务。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> StopFriendDetectAsync(string deviceUuid, long taskId = 0, string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(StopFriendDetectAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(StopFriendDetectAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            return await _clientTaskService.SendPostStopFriendDetectTaskAsync(connectionId, ownerWxid, taskId == 0 ? DateTime.UtcNow.Ticks : taskId);
        }

        /// <summary>
        /// 拉取好友检测/清粉最终结果。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> GetFriendDetectResultAsync(string deviceUuid, string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(GetFriendDetectResultAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(GetFriendDetectResultAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            return await _clientTaskService.SendGetFriendDetectResultTaskAsync(connectionId, ownerWxid, DateTime.UtcNow.Ticks);
        }

        public async Task<bool> ApproveChatRoomInviteAsync(string deviceUuid, long msgSvrId, string roomId = "", string msgContent = "", long msgId = 0)
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("ApproveChatRoomInviteAsync: Device {DeviceUuid} offline.", deviceUuid);
                return false;
            }

            var finalMsgId = msgId != 0 ? msgId : msgSvrId;
            var ownerWxid = await GetRequiredWeChatIdAsync(deviceUuid, nameof(ApproveChatRoomInviteAsync));
            var result = await _clientTaskService.SendChatRoomInviteApproveTaskAsync(connectionId, msgSvrId, roomId, msgContent, ownerWxid, finalMsgId, DateTime.UtcNow.Ticks);
            return result.success;
        }

        public async Task<TaskResult> SendJielongAsync(string deviceUuid, string chatRoomId, string content, string title = "", string sample = "", string memo = "", long msgSvrId = 0)
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("SendJielongAsync: Device {DeviceUuid} offline.", deviceUuid);
                return TaskResult.Fail("设备未在线或连接不存在");
            }

            return await _clientTaskService.SendJielongTaskAsync(connectionId, chatRoomId, content, title, sample, memo, msgSvrId, string.Empty, DateTime.UtcNow.Ticks);
        }

        public async Task<TaskResult> AddFriendWithSceneAsync(string deviceUuid, string friendWxId, string message, string remark = "", string label = "", int scene = 3, int permission = 0, string verificationImagePath = "")
        {
            _logger.LogInformation(
                "AddFriendWithSceneAsync: Device={DeviceUuid}, Friend={FriendWxId}, Scene={Scene}, Permission={Permission}, Message={Message}, Remark={Remark}, Label={Label}, HasImage={HasImage}",
                deviceUuid,
                friendWxId,
                scene,
                permission,
                message,
                remark,
                label,
                !string.IsNullOrWhiteSpace(verificationImagePath));
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(AddFriendWithSceneAsync));
            if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("设备未在线或连接不存在");

            var ownerWxid = await GetRequiredWeChatIdAsync(deviceUuid, nameof(AddFriendWithSceneAsync));
            if (string.IsNullOrEmpty(ownerWxid)) return TaskResult.Fail("设备当前没有可用微信账号");

            return await _clientTaskService.SendAddFriendWithSceneTaskAsync(connectionId, friendWxId, message, remark, label, scene, permission, verificationImagePath, ownerWxid);
        }

        /// <summary>
        /// 通过手机号添加好友，对齐 62203 AddFriendsTask。
        /// </summary>
        public async Task<TaskResult> AddFriendsByPhoneAsync(string deviceUuid, IEnumerable<string> phones, string message, string remark = "", string label = "", int permission = 0)
        {
            var normalizedPhones = (phones ?? Enumerable.Empty<string>())
                .Where(phone => !string.IsNullOrWhiteSpace(phone))
                .Select(phone => phone.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedPhones.Count == 0)
            {
                return TaskResult.Fail("手机号为空");
            }

            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(AddFriendsByPhoneAsync));
            if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("设备未在线或连接不存在");
            var ownerWxid = await GetRequiredWeChatIdAsync(deviceUuid, nameof(AddFriendsByPhoneAsync));
            if (string.IsNullOrEmpty(ownerWxid)) return TaskResult.Fail("设备当前没有可用微信账号");

            if (normalizedPhones.Count == 1)
            {
                return await _clientTaskService.SendAddFriendsTaskAsync(connectionId, normalizedPhones, message, remark, label, permission, DateTime.UtcNow.Ticks, ownerWxid);
            }

            // 62203 Android 执行层当前只消费 AddFriendsTask.Phones[0]；
            // 服务端在公开入口统一拆成多个 1072，避免批量手机号被客户端静默丢弃。
            var baseTaskId = DateTime.UtcNow.Ticks;
            var successCount = 0;
            var errors = new List<string>();

            for (var index = 0; index < normalizedPhones.Count; index++)
            {
                var phone = normalizedPhones[index];
                var taskId = baseTaskId + index;
                var result = await _clientTaskService.SendAddFriendsTaskAsync(
                    connectionId,
                    new[] { phone },
                    message,
                    remark,
                    label,
                    permission,
                    taskId,
                    ownerWxid);

                if (result.success)
                {
                    successCount++;
                    if (index < normalizedPhones.Count - 1)
                    {
                        await Task.Delay(500);
                    }
                }
                else
                {
                    errors.Add($"第{index + 1}个手机号 {phone} 下发失败：{result.message ?? "未知错误"}");
                }
            }

            if (successCount > 0)
            {
                if (errors.Count > 0)
                {
                    _logger.LogWarning(
                        "AddFriendsByPhoneAsync: Device={DeviceUuid} 部分手机号逐条下发失败，Success={SuccessCount}/{TotalCount}, Errors={Errors}",
                        deviceUuid,
                        successCount,
                        normalizedPhones.Count,
                        string.Join("；", errors));
                }

                return TaskResult.Ok(baseTaskId, $"手机号加好友已逐条下发 {successCount}/{normalizedPhones.Count}，等待客户端回执");
            }

            return TaskResult.Fail(baseTaskId, $"手机号加好友全部下发失败：{string.Join("；", errors)}");
        }

        /// <summary>
        /// 从通讯录添加好友，对齐 62203 AddFriendFromPhonebookTask。
        /// </summary>
        public async Task<TaskResult> AddFriendFromPhonebookAsync(string deviceUuid, string message, int count = 1, int index = 0, bool reset = false)
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(AddFriendFromPhonebookAsync));
            if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("设备未在线或连接不存在");
            var ownerWxid = await GetRequiredWeChatIdAsync(deviceUuid, nameof(AddFriendFromPhonebookAsync));
            if (string.IsNullOrEmpty(ownerWxid)) return TaskResult.Fail("未找到当前登录微信账号");
            return await _clientTaskService.SendAddFriendFromPhonebookTaskAsync(connectionId, message, count, index, DateTime.UtcNow.Ticks, reset, ownerWxid);
        }

        /// <summary>
        /// 通过名片消息添加好友，对齐 62203 AddFriendNameCardTask。
        /// </summary>
        public async Task<TaskResult> AddFriendNameCardAsync(string deviceUuid, long msgSvrId, string message, string remark = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(AddFriendNameCardAsync));
            if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("设备未在线或连接不存在");
            var ownerWxid = await GetRequiredWeChatIdAsync(deviceUuid, nameof(AddFriendNameCardAsync));
            if (string.IsNullOrEmpty(ownerWxid)) return TaskResult.Fail("未找到当前登录微信账号");
            return await _clientTaskService.SendAddFriendNameCardTaskAsync(connectionId, msgSvrId, message, remark, DateTime.UtcNow.Ticks, ownerWxid);
        }

        /// <summary>
        /// 重新发送好友验证，对齐 62203 SendFriendVerifyTask。
        /// </summary>
        public async Task<TaskResult> SendFriendVerifyAsync(string deviceUuid, string friendId, string message)
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(SendFriendVerifyAsync));
            if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("设备未在线或连接不存在");
            var ownerWxid = await GetRequiredWeChatIdAsync(deviceUuid, nameof(SendFriendVerifyAsync));
            if (string.IsNullOrEmpty(ownerWxid)) return TaskResult.Fail("未找到当前登录微信账号");
            return await _clientTaskService.SendFriendVerifyTaskAsync(connectionId, friendId, message, DateTime.UtcNow.Ticks, ownerWxid);
        }

        public async Task<TaskResult> ModifyFriendMemoAsync(string deviceUuid, string friendId, string memo, string desc = "", string phone = "", int delFlag = 0)
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("ModifyFriendMemoAsync: Device {DeviceUuid} offline.", deviceUuid);
                return TaskResult.Fail("设备未在线或连接不存在");
            }

            var taskId = DateTime.UtcNow.Ticks;
            return await _clientTaskService.SendModifyFriendMemoTaskAsync(connectionId, friendId, memo, desc, phone, delFlag, taskId);
        }

        /// <summary>
        /// 设置好友权限。
        /// </summary>
        /// <param name="deviceUuid">设备 UUID。</param>
        /// <param name="friendId">目标好友 wxid。</param>
        /// <param name="permissionMask">权限位掩码：8=仅聊天，2=不让他看我朋友圈，1=不看他朋友圈。</param>
        public async Task<TaskResult> SetFriendPermissionAsync(string deviceUuid, string friendId, int permissionMask)
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(SetFriendPermissionAsync));
            if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("设备未在线或连接不存在");

            var ownerWxid = await GetRequiredWeChatIdAsync(deviceUuid, nameof(SetFriendPermissionAsync));
            if (string.IsNullOrEmpty(ownerWxid)) return TaskResult.Fail("设备当前没有可用微信账号");

            var taskId = DateTime.UtcNow.Ticks;
            return await _clientTaskService.SendSetFriendPermissionTaskAsync(connectionId, friendId, permissionMask, taskId, ownerWxid);
        }

        public async Task<TaskResult> DeleteFriendAsync(string deviceUuid, string friendId)
        {
            _logger.LogInformation("DeleteFriendAsync: Device={DeviceUuid}, Friend={FriendId}", deviceUuid, friendId);
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(DeleteFriendAsync));
            if (string.IsNullOrEmpty(connectionId)) return TaskResult.Fail("设备未在线或连接不存在");

            var ownerWxid = await GetRequiredWeChatIdAsync(deviceUuid, nameof(DeleteFriendAsync));
            if (string.IsNullOrEmpty(ownerWxid)) return TaskResult.Fail("设备当前没有可用微信账号");

            var taskId = DateTime.UtcNow.Ticks;
            return await _clientTaskService.SendDeleteFriendTaskAsync(connectionId, friendId, taskId, ownerWxid);
        }

        public async Task<bool> SyncChatRoomsAsync(string deviceUuid, int flag = 0, string weChatId = "")
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("SyncChatRoomsAsync: Device {DeviceUuid} offline.", deviceUuid);
                return false;
            }

            var roomTaskId = DateTime.UtcNow.Ticks;
            var conversationTaskId = roomTaskId + 1;
            var roomSent = await _clientTaskService.SendTriggerChatRoomPushTaskAsync(connectionId, roomTaskId, flag, weChatId);
            var conversationSent = await _clientTaskService.SendTriggerConversationPushTaskAsync(
                connectionId,
                withName: true,
                limit: 100,
                taskId: conversationTaskId);

            if (roomSent || conversationSent)
            {
                _clientTaskService.ScheduleChatRoomRefreshAfterMutation(connectionId, roomTaskId, "SyncChatRooms");
            }

            _logger.LogInformation(
                "SyncChatRoomsAsync dispatched: Device={DeviceUuid}, ConnectionId={ConnectionId}, RoomTaskId={RoomTaskId}, RoomSent={RoomSent}, ConversationTaskId={ConversationTaskId}, ConversationSent={ConversationSent}",
                deviceUuid,
                connectionId,
                roomTaskId,
                roomSent,
                conversationTaskId,
                conversationSent);

            return roomSent || conversationSent;
        }


        /// <summary>
        /// 请求设备拉取群邀请列表。
        /// </summary>
        public async Task<bool> GetChatRoomInviteListAsync(string deviceUuid)
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("GetChatRoomInviteListAsync: Device {DeviceUuid} offline.", deviceUuid);
                return false;
            }

            return await _clientTaskService.SendGetChatRoomInviteListTaskAsync(connectionId, string.Empty, DateTime.UtcNow.Ticks);
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SyncMomentsAsync(
            string deviceUuid,
            long startTime = 0,
            IEnumerable<long>? circleIds = null,
            string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(SyncMomentsAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");
            if (string.IsNullOrWhiteSpace(weChatId))
            {
                weChatId = await GetRequiredWeChatIdAsync(deviceUuid, nameof(SyncMomentsAsync));
            }
            var taskId = DateTime.UtcNow.Ticks;
            var success = await _clientTaskService.SendTriggerCirclePushTaskAsync(connectionId, taskId, weChatId, startTime, circleIds);
            return success
                ? new SCRM.SHARED.Models.Dtos.TaskResult
                {
                    taskId = taskId,
                    success = true,
                    message = "朋友圈同步指令已下发，等待客户端回执"
                }
                : new SCRM.SHARED.Models.Dtos.TaskResult
                {
                    taskId = taskId,
                    success = false,
                    message = "朋友圈同步任务下发失败"
                };
        }

        public async Task<bool> AgreeJoinGroupAsync(string deviceUuid, string talker, long msgSvrId, string content)
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("AgreeJoinGroupAsync: Device {DeviceUuid} offline.", deviceUuid);
                return false;
            }

            var taskId = DateTime.UtcNow.Ticks;
            var result = await _clientTaskService.SendAgreeJoinChatRoomTaskAsync(connectionId, talker, msgSvrId, content, taskId);
            return result.success;
        }


        private async Task<string> GetRequiredConnectionIdAsync(string deviceUuid, string methodName)
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("{MethodName}: Device {DeviceUuid} offline.", methodName, deviceUuid);
                return string.Empty;
            }
            return connectionId;
        }

        /// <summary>
        /// 读取设备当前微信号。
        /// <para>命令下发可能被多个 UI 回调并发触发，这里使用独立 DbContext，避免共享 scoped DbContext 出现并发查询异常。</para>
        /// <para>只允许返回当前在线微信账号；离线历史账号和 3051 lastKnown 快照不能作为任务默认 wxid，避免任务下发到错误账号。</para>
        /// </summary>
        private async Task<string> GetRequiredWeChatIdAsync(string deviceUuid, string methodName)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var account = await db.WechatAccounts
                .AsNoTracking()
                .OrderByDescending(a => a.lastOnlineAt)
                .ThenByDescending(a => a.updatedAt)
                .FirstOrDefaultAsync(a => a.clientUuid == deviceUuid && !a.isDeleted && a.accountStatus == 1);

            if (account == null || string.IsNullOrWhiteSpace(account.wxid))
            {
                _logger.LogWarning(
                    "{MethodName}: Device {DeviceUuid} has no online WeChat account; offline history/lastKnown will not be used as default wxid.",
                    methodName,
                    deviceUuid);
                return string.Empty;
            }

            return account.wxid;
        }

        /// <summary>
        /// 读取当前在线微信号；没有在线账号时返回空字符串。
        /// <para>用于手机能力或 Android 可自行兜底当前登录态的任务。这里绝不返回离线历史账号/lastKnown，避免污染任务归属。</para>
        /// </summary>
        private async Task<string> ResolveOptionalCurrentWechatIdAsync(string deviceUuid)
        {
            // 绝不返回离线历史账号/lastKnown；没有在线账号时返回空字符串，让 Android 当前登录态自行兜底。
            if (string.IsNullOrWhiteSpace(deviceUuid))
            {
                return string.Empty;
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var normalizedDeviceUuid = deviceUuid.Trim();
            var account = await db.WechatAccounts
                .AsNoTracking()
                .OrderByDescending(a => a.lastOnlineAt)
                .ThenByDescending(a => a.updatedAt)
                .FirstOrDefaultAsync(a => a.clientUuid == normalizedDeviceUuid && !a.isDeleted && a.accountStatus == 1);

            return account?.wxid ?? string.Empty;
        }

        /// <summary>
        /// 获取设备 IMEI。
        /// <para>PostDeviceInfoNotice 中没有 IMEI 时用设备 UUID 兜底，保证短信/通话记录仍能按设备维度隔离。</para>
        /// </summary>
        private async Task<string> GetDeviceImeiAsync(string deviceUuid)
        {
            if (string.IsNullOrWhiteSpace(deviceUuid))
            {
                return string.Empty;
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var device = await db.GetSrClient(deviceUuid.Trim());
            return string.IsNullOrWhiteSpace(device?.device?.IMEI)
                ? deviceUuid.Trim()
                : device.device.IMEI.Trim();
        }

        private static bool IsValidPaymentPassword(string? passwd)
        {
            var value = passwd?.Trim() ?? string.Empty;
            return value.Length == 6 && value.All(char.IsDigit);
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SphGetMentionAsync(string deviceUuid, long lastLikeId = 0, long lastCommentId = 0, long lastFollowId = 0)
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(SphGetMentionAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");
            var success = await _clientTaskService.SendSphGetMentionTaskAsync(connectionId, lastLikeId, lastCommentId, lastFollowId, DateTime.UtcNow.Ticks);
            return success ? SCRM.SHARED.Models.Dtos.TaskResult.Ok() : SCRM.SHARED.Models.Dtos.TaskResult.Fail("视频号提及拉取任务下发失败");
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SphGetCommentAsync(string deviceUuid, long feedId, string nonceId, string feedAuth, long refCommentId = 0, long replyCommentId = 0, int sortType = 0)
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(SphGetCommentAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");
            var success = await _clientTaskService.SendSphGetCommentTaskAsync(connectionId, feedId, nonceId, feedAuth, refCommentId, replyCommentId, sortType, DateTime.UtcNow.Ticks);
            return success ? SCRM.SHARED.Models.Dtos.TaskResult.Ok() : SCRM.SHARED.Models.Dtos.TaskResult.Fail("视频号评论列表拉取任务下发失败");
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SphUserPageAsync(string deviceUuid, string sphUserName)
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(SphUserPageAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");
            if (string.IsNullOrWhiteSpace(sphUserName)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("视频号用户名不能为空");
            var success = await _clientTaskService.SendSphUserPageTaskAsync(connectionId, sphUserName, DateTime.UtcNow.Ticks);
            return success ? SCRM.SHARED.Models.Dtos.TaskResult.Ok() : SCRM.SHARED.Models.Dtos.TaskResult.Fail("视频号用户页拉取任务下发失败");
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SphPostAsync(string deviceUuid, string content, List<string> medias, int mediaType = 0, string cover = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(SphPostAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");
            return await _clientTaskService.SendSphPostTaskAsync(connectionId, content, medias, mediaType, cover, DateTime.UtcNow.Ticks);
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SphCommentAsync(string deviceUuid, long feedId, string nonceId, string feedAuth, int type, string content, string media = "", long replyCommentId = 0, string replyUsername = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(SphCommentAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");
            return await _clientTaskService.SendSphCommentTaskAsync(connectionId, feedId, nonceId, feedAuth, type, content, media, replyCommentId, replyUsername, DateTime.UtcNow.Ticks);
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SphLikeAsync(string deviceUuid, long feedId, int type = 1, bool isCancel = false)
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(SphLikeAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");
            return await _clientTaskService.SendSphLikeTaskAsync(connectionId, feedId, type, isCancel, DateTime.UtcNow.Ticks);
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SphDelCommentAsync(string deviceUuid, long feedId, long commentId)
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(SphDelCommentAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");
            return await _clientTaskService.SendSphDelCommentTaskAsync(connectionId, feedId, commentId, DateTime.UtcNow.Ticks);
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> PostMomentAsync(string deviceUuid, string content, List<string> imageUrls)
        {
            return await PostMomentAsync(deviceUuid, MomentPostRequestDto.FromLegacy(content, imageUrls));
        }

        /// <summary>
        /// 下发高级朋友圈发布任务。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> PostMomentAsync(string deviceUuid, MomentPostRequestDto request)
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(PostMomentAsync));
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("PostMomentAsync: Device {DeviceUuid} offline.", deviceUuid);
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");
            }

            var currentWeChatId = await GetRequiredWeChatIdAsync(deviceUuid, nameof(PostMomentAsync));
            if (string.IsNullOrEmpty(currentWeChatId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");
            }

            var validation = MomentPostRequestValidator.BindAndValidate(request, currentWeChatId);
            if (!validation.IsValid)
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(validation.ErrorMessage);
            }

            var taskId = DateTime.UtcNow.Ticks;
            return await _clientTaskService.SendPostSNSNewsTaskAsync(connectionId, validation.Request, taskId);
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> DeleteMomentAsync(string deviceUuid, long circleId)
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(DeleteMomentAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");
            var weChatId = await GetRequiredWeChatIdAsync(deviceUuid, nameof(DeleteMomentAsync));
            if (string.IsNullOrEmpty(weChatId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");
            return await _clientTaskService.SendDeleteSNSNewsTaskAsync(connectionId, weChatId, circleId, DateTime.UtcNow.Ticks);
        }

        /// <summary>
        /// 普通朋友圈点赞/取消点赞。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> LikeMomentAsync(string deviceUuid, long circleId, bool isCancel = false)
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(LikeMomentAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");
            var weChatId = await GetRequiredWeChatIdAsync(deviceUuid, nameof(LikeMomentAsync));
            if (string.IsNullOrEmpty(weChatId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");
            if (circleId == 0) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("朋友圈ID无效");
            return await _clientTaskService.SendCircleLikeTaskAsync(connectionId, weChatId, circleId, isCancel, DateTime.UtcNow.Ticks);
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> DeleteMomentCommentAsync(string deviceUuid, long circleId, long commentId, long publishTime)
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(DeleteMomentCommentAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");
            var weChatId = await GetRequiredWeChatIdAsync(deviceUuid, nameof(DeleteMomentCommentAsync));
            if (string.IsNullOrEmpty(weChatId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");
            return await _clientTaskService.SendCircleCommentDeleteTaskAsync(connectionId, weChatId, circleId, commentId, publishTime, DateTime.UtcNow.Ticks);
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> ReplyMomentCommentAsync(string deviceUuid, long circleId, string toWeChatId, string content, long replyCommentId, bool isResend = false)
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(ReplyMomentCommentAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");
            var weChatId = await GetRequiredWeChatIdAsync(deviceUuid, nameof(ReplyMomentCommentAsync));
            if (string.IsNullOrEmpty(weChatId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");
            return await _clientTaskService.SendCircleCommentReplyTaskAsync(connectionId, weChatId, circleId, toWeChatId, content, replyCommentId, isResend, DateTime.UtcNow.Ticks);
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> PullFriendMomentsAsync(string deviceUuid, string friendId, long refSnsId = 0, int count = 20, long startTime = 0, long refTime = 0)
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(PullFriendMomentsAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");
            var weChatId = await GetRequiredWeChatIdAsync(deviceUuid, nameof(PullFriendMomentsAsync));
            if (string.IsNullOrEmpty(weChatId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");
            if (string.IsNullOrWhiteSpace(friendId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("好友微信ID不能为空");
            var success = await _clientTaskService.SendPullFriendCircleTaskAsync(connectionId, weChatId, friendId, refSnsId, count, startTime, refTime, DateTime.UtcNow.Ticks);
            return success ? SCRM.SHARED.Models.Dtos.TaskResult.Ok() : SCRM.SHARED.Models.Dtos.TaskResult.Fail("好友朋友圈拉取任务下发失败");
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> PullMomentDetailAsync(string deviceUuid, long circleId, bool getBigMap = false)
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(PullMomentDetailAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");
            var weChatId = await GetRequiredWeChatIdAsync(deviceUuid, nameof(PullMomentDetailAsync));
            if (string.IsNullOrEmpty(weChatId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");
            // 微信 snsId 以 long 传输时可能为负数，只有 0 才表示无效。
            if (circleId == 0) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("朋友圈ID无效");
            var success = await _clientTaskService.SendPullCircleDetailTaskAsync(connectionId, weChatId, circleId, getBigMap);
            return success ? SCRM.SHARED.Models.Dtos.TaskResult.Ok() : SCRM.SHARED.Models.Dtos.TaskResult.Fail("朋友圈详情拉取任务下发失败");
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SyncMomentMessagesAsync(string deviceUuid, bool onlyComment = false, bool getAll = true)
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(SyncMomentMessagesAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");
            var weChatId = await GetRequiredWeChatIdAsync(deviceUuid, nameof(SyncMomentMessagesAsync));
            if (string.IsNullOrEmpty(weChatId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");
            var taskId = DateTime.UtcNow.Ticks;
            var success = await _clientTaskService.SendTriggerCircleMsgPushTaskAsync(connectionId, weChatId, onlyComment, getAll, taskId);
            return success
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(taskId, "朋友圈互动消息同步指令已下发，等待客户端回执")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "朋友圈互动消息同步任务下发失败");
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> MarkMomentMessageReadAsync(string deviceUuid, long circleId, int commentId = 0)
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(MarkMomentMessageReadAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");
            var weChatId = await GetRequiredWeChatIdAsync(deviceUuid, nameof(MarkMomentMessageReadAsync));
            if (string.IsNullOrEmpty(weChatId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");
            // 微信 snsId 以 long 传输时可能为负数，只有 0 才表示无效。
            if (circleId == 0) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("朋友圈ID无效");
            var success = await _clientTaskService.SendCircleMsgReadTaskAsync(connectionId, weChatId, circleId, commentId);
            return success ? SCRM.SHARED.Models.Dtos.TaskResult.Ok() : SCRM.SHARED.Models.Dtos.TaskResult.Fail("朋友圈互动消息已读任务下发失败");
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> ClearMomentMessageAsync(string deviceUuid, long circleId, int commentId = 0, bool isRead = true)
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(ClearMomentMessageAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");
            var weChatId = await GetRequiredWeChatIdAsync(deviceUuid, nameof(ClearMomentMessageAsync));
            if (string.IsNullOrEmpty(weChatId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");
            // 62203 中 CircleId=0 可能表示清空全部互动消息，因此这里不再拦截 0。
            var success = await _clientTaskService.SendCircleMsgClearTaskAsync(connectionId, weChatId, circleId, commentId, isRead);
            return success ? SCRM.SHARED.Models.Dtos.TaskResult.Ok() : SCRM.SHARED.Models.Dtos.TaskResult.Fail("朋友圈互动消息清理任务下发失败");
        }

        /// <summary>
        /// 朋友圈一键点赞。
        /// <para>62203 会读取 Rate/Num/EndTime/TimeOut；默认值保持旧行为。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> OneKeyLikeMomentsAsync(
            string deviceUuid,
            int rate = 100,
            int num = 0,
            int endTime = 0,
            int timeOut = 0)
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(OneKeyLikeMomentsAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");
            var weChatId = await GetRequiredWeChatIdAsync(deviceUuid, nameof(OneKeyLikeMomentsAsync));
            if (string.IsNullOrEmpty(weChatId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");
            var taskId = DateTime.UtcNow.Ticks;
            return await _clientTaskService.SendOneKeyLikeTaskAsync(connectionId, taskId, weChatId, rate, num, endTime, timeOut);
        }

        public async Task<bool> SendMultiPictureAsync(string deviceUuid, string friendWxId, List<string> imageUrls)
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("SendMultiPictureAsync: Device {DeviceUuid} offline.", deviceUuid);
                return false;
            }

            var normalizedUrls = imageUrls?
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(url => url.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            if (normalizedUrls.Count == 0)
            {
                return false;
            }

            var successCount = 0;
            foreach (var imageUrl in normalizedUrls)
            {
                var result = await _clientTaskService.SendTalkToFriendTaskAsync(
                    connectionId,
                    friendWxId,
                    imageUrl,
                    EnumContentType.Picture);
                if (result.success)
                {
                    successCount++;
                    await Task.Delay(350);
                }
            }

            _logger.LogInformation(
                "SendMultiPictureAsync fallback via TalkToFriendTask: Device={DeviceUuid}, Friend={FriendWxId}, Success={SuccessCount}/{Total}",
                deviceUuid,
                friendWxId,
                successCount,
                normalizedUrls.Count);
            return successCount == normalizedUrls.Count;
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> RequestScreenShotAsync(string deviceUuid)
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId)) 
            {
                _logger.LogError("RequestScreenShotAsync: Device {DeviceUuid} not connected.", deviceUuid);
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备未在线或连接不存在");
            }

            // MsgType: 1282 (ScreenShotTask)
            var taskId = DateTime.UtcNow.Ticks;
            var result = await _clientTaskService.SendScreenShotTaskAsync(connectionId, taskId);
             
            _logger.LogInformation(
                "Sent RequestScreenShot ({TaskId}) to {DeviceUuid} result: {Success}, Message={Message}",
                taskId,
                deviceUuid,
                result.success,
                result.message);

            if (!result.success)
            {
                result.taskId = result.taskId > 0 ? result.taskId : taskId;
                if (string.IsNullOrWhiteSpace(result.message))
                {
                    result.message = "截图失败";
                }

                return result;
            }

            return new SCRM.SHARED.Models.Dtos.TaskResult
            {
                taskId = result.taskId > 0 ? result.taskId : taskId,
                success = true,
                message = "截图任务已完成",
                data = result.message
            };
        }

        /// <summary>
        /// 请求客户端执行 GetA8Key。
        /// <para>成功时返回的 TaskResult.message 通常就是 A8Key 后 URL。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> GetA8KeyAsync(
            string deviceUuid,
            int type,
            string url,
            string userName = "",
            string msgSvrId = "",
            int reason = 0,
            string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(GetA8KeyAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");
            if (string.IsNullOrWhiteSpace(url)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Url 不能为空");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(GetA8KeyAsync))
                : weChatId.Trim();

            var taskId = DateTime.UtcNow.Ticks;
            return await _clientTaskService.SendGetA8KeyTaskAsync(
                connectionId,
                ownerWxid ?? string.Empty,
                type,
                url,
                userName,
                msgSvrId,
                reason,
                taskId);
        }

        /// <summary>
        /// 修改微信资料或隐私设置。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> UpdateWechatSettingAsync(
            string deviceUuid,
            int action,
            string content = "",
            int intParam = 0,
            string weChatId = "")
        {
            if (!Enum.IsDefined(typeof(EnumSettings), action))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail($"不支持的微信设置动作：{action}");
            }

            var enumAction = (EnumSettings)action;
            var validationError = ValidateWechatSettingTask(enumAction, content, intParam);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail(validationError);
            }

            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(UpdateWechatSettingAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(UpdateWechatSettingAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            var taskId = DateTime.UtcNow.Ticks;
            return await _clientTaskService.SendWechatSettingTaskAsync(
                connectionId,
                ownerWxid,
                enumAction,
                content ?? string.Empty,
                intParam,
                taskId);
        }

        private static string ValidateWechatSettingTask(EnumSettings action, string content, int intParam)
        {
            return action switch
            {
                EnumSettings.ChangeNickName when string.IsNullOrWhiteSpace(content) => "昵称不能为空",
                EnumSettings.ChangeAvatar when string.IsNullOrWhiteSpace(content) => "头像路径或 URL 不能为空",
                EnumSettings.ChangeZone when string.IsNullOrWhiteSpace(content) => "地区内容不能为空，建议格式：CN_省份_城市",
                EnumSettings.ChangeSignature when string.IsNullOrWhiteSpace(content) => "个性签名不能为空",
                EnumSettings.ChangeGender when intParam is not (1 or 2) => "性别参数只允许 1 或 2",
                _ => string.Empty
            };
        }

        /// <summary>
        /// 请求 Android 主动回传当前配置快照。
        /// <para>结果通过 ConfigPushNotice 或当前 SmRun 兼容口径 SetConfigTask 异步上报。</para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> TriggerConfigPushAsync(string deviceUuid)
        {
            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("TriggerConfigPushAsync: Device {DeviceUuid} offline.", deviceUuid);
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备未在线或连接不存在");
            }

            var taskId = DateTime.UtcNow.Ticks;
            var sent = await _clientTaskService.SendTriggerConfigPushTaskAsync(connectionId, taskId);
            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(taskId, "配置同步指令已下发，等待客户端上报配置快照")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail(taskId, "配置同步指令下发失败");
        }

        /// <summary>
        /// 下发 Android 设备级配置。
        /// <para>
        /// 该入口只处理 SmRun/62203 明确支持的 SetConfigTask 键：
        /// fastSend/silentFunc/silentAccept/.../host/fileUpUrl 等。
        /// 账号自动化设置仍由 WechatAccountSettings 服务端逻辑管理，二者不能混用。
        /// </para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SetDeviceConfigAsync(string deviceUuid, DeviceConfigDto config)
        {
            if (string.IsNullOrWhiteSpace(deviceUuid))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备 UUID 为空");
            }

            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(SetDeviceConfigAsync));
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备未在线或连接不存在");
            }

            if (config == null || config.IsEmpty)
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("没有选择任何需要下发的配置项");
            }

            var boolConfs = NormalizeDeviceConfigMap(config.BoolConfs, KnownDeviceBoolConfigKeys);
            var intConfs = NormalizeDeviceConfigMap(config.IntConfs, KnownDeviceIntConfigKeys);
            var strConfs = NormalizeDeviceConfigMap(config.StrConfs, KnownDeviceStrConfigKeys);

            if (boolConfs.Count == 0 && intConfs.Count == 0 && strConfs.Count == 0)
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("没有有效的 62203 配置键可下发");
            }

            if (intConfs.TryGetValue("port", out var port) && (port <= 0 || port > 65535))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("port 必须在 1 到 65535 之间");
            }

            if (intConfs.TryGetValue("keepWake", out var keepWake) && keepWake < 0)
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("keepWake 不能小于 0");
            }

            var sent = await _clientTaskService.SendSetConfigTaskAsync(connectionId, boolConfs, intConfs, strConfs);
            var count = boolConfs.Count + intConfs.Count + strConfs.Count;
            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(0, $"设备配置已下发：{count} 项")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备配置下发失败");
        }

        /// <summary>
        /// 下发微信违禁词列表。
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SetForbiddenWordAsync(string deviceUuid, IEnumerable<string>? words, string weChatId = "")
        {
            var connectionId = await GetRequiredConnectionIdAsync(deviceUuid, nameof(SetForbiddenWordAsync));
            if (string.IsNullOrEmpty(connectionId)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Device offline");

            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? await GetRequiredWeChatIdAsync(deviceUuid, nameof(SetForbiddenWordAsync))
                : weChatId.Trim();
            if (string.IsNullOrEmpty(ownerWxid)) return SCRM.SHARED.Models.Dtos.TaskResult.Fail("WeChat account not found");

            var normalizedWords = (words ?? Enumerable.Empty<string>())
                .Select(word => word?.Trim() ?? string.Empty)
                .Where(word => !string.IsNullOrWhiteSpace(word))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var sent = await _clientTaskService.SendSetForbiddenWordAsync(connectionId, ownerWxid, normalizedWords);
            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(0, $"违禁词列表已下发：Count={normalizedWords.Length}")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail("违禁词列表下发失败");
        }

        private static Dictionary<string, TValue> NormalizeDeviceConfigMap<TValue>(
            IDictionary<string, TValue>? source,
            ISet<string> allowList)
        {
            var result = new Dictionary<string, TValue>(StringComparer.Ordinal);
            if (source == null)
            {
                return result;
            }

            foreach (var item in source)
            {
                var key = item.Key?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(key) || !allowList.Contains(key))
                {
                    continue;
                }

                result[key] = item.Value;
            }

            return result;
        }

        /// <summary>
        /// 删除设备前通知在线 Android 端主动断开。
        /// <para>
        /// PostDeleteDeviceNotice(1097) 是设备管理通知，没有 TaskId 和结果回包；
        /// 因此这里只返回 TCP 写出是否成功。设备离线时返回失败，由删除流程继续做本地删除。
        /// </para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> NotifyDeviceDeleteAsync(string deviceUuid)
        {
            if (string.IsNullOrWhiteSpace(deviceUuid))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备 UUID 为空");
            }

            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备离线，未发送删除通知");
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var device = await db.SrClients.AsNoTracking().FirstOrDefaultAsync(item => item.uuid == deviceUuid);
            var imei = string.IsNullOrWhiteSpace(device?.device?.IMEI)
                ? deviceUuid
                : device.device.IMEI;

            var sent = await _clientTaskService.SendPostDeleteDeviceNoticeAsync(connectionId, imei);
            _logger.LogInformation(
                "PostDeleteDeviceNotice dispatched: DeviceUuid={DeviceUuid}, Imei={Imei}, ConnectionId={ConnectionId}, Sent={Sent}",
                deviceUuid,
                imei,
                connectionId,
                sent);

            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(0, "删除设备通知已下发，客户端将主动断开连接")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail("删除设备通知下发失败");
        }

        /// <summary>
        /// 下发设备 App 升级通知。
        /// <para>
        /// UpgradeDeviceAppNotice(1094) 无 TaskId/无结果回包；返回成功仅代表服务端已写入在线 TCP 通道。
        /// </para>
        /// </summary>
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> UpgradeDeviceAppAsync(
            string deviceUuid,
            string packageName,
            string version,
            int versionCode,
            string packageUrl,
            string weChatId = "")
        {
            if (string.IsNullOrWhiteSpace(deviceUuid))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备 UUID 为空");
            }

            var normalizedPackageName = string.IsNullOrWhiteSpace(packageName)
                ? "com.juliao.ty.imscrm"
                : packageName.Trim();
            var normalizedVersion = string.IsNullOrWhiteSpace(version)
                ? versionCode.ToString()
                : version.Trim();
            var normalizedPackageUrl = packageUrl?.Trim() ?? string.Empty;

            if (versionCode <= 0)
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("版本码 VerNumber 必须大于 0");
            }

            if (string.IsNullOrWhiteSpace(normalizedPackageUrl))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("升级包下载地址不能为空");
            }

            var connectionId = await _connectionManager.GetConnectionIdByDeviceUuidAsync(deviceUuid);
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("设备离线，无法下发升级通知");
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var device = await db.SrClients.AsNoTracking().FirstOrDefaultAsync(item => item.uuid == deviceUuid);
            var imei = string.IsNullOrWhiteSpace(device?.device?.IMEI)
                ? deviceUuid
                : device.device.IMEI;
            var ownerWxid = string.IsNullOrWhiteSpace(weChatId)
                ? device?.device?.WeChatId ?? string.Empty
                : weChatId.Trim();

            var sent = await _clientTaskService.SendUpgradeDeviceAppNoticeAsync(
                connectionId,
                ownerWxid,
                imei,
                normalizedPackageName,
                normalizedVersion,
                versionCode,
                normalizedPackageUrl);

            _logger.LogInformation(
                "UpgradeDeviceAppNotice dispatched: DeviceUuid={DeviceUuid}, Imei={Imei}, Package={PackageName}, Version={Version}, VersionCode={VersionCode}, Sent={Sent}",
                deviceUuid,
                imei,
                normalizedPackageName,
                normalizedVersion,
                versionCode,
                sent);

            return sent
                ? SCRM.SHARED.Models.Dtos.TaskResult.Ok(0, $"升级通知已下发：{normalizedPackageName} {normalizedVersion}({versionCode})")
                : SCRM.SHARED.Models.Dtos.TaskResult.Fail("升级通知下发失败");
        }

        public Task<bool> PushAccountSettingsAsync(string deviceUuid, WechatAccountSettings settings)
        {
            // WechatAccountSettings 是服务端账号自动化策略，不是 Android SetConfigTask 的设备配置。
            // AutoAcceptFriendRequest 控制 AutomationService 是否自动下发 1075；
            // silentAccept 才是 Android 侧静默通过/接受相关流程的 62203 配置键，应从 SetDeviceConfigAsync 下发。
            // 因此这里不再把 AutoAcceptFriendRequest/AutoAcceptLuckyMoney/AutoLikeMoments 写成未知 Android 配置 key。
            _logger.LogInformation(
                "PushAccountSettingsAsync skipped Android SetConfig for account automation settings. Device={DeviceUuid}, AutoAcceptFriendRequest={AutoAcceptFriendRequest}, AutoAcceptLuckyMoney={AutoAcceptLuckyMoney}, AutoLikeMoments={AutoLikeMoments}",
                deviceUuid,
                settings?.AutoAcceptFriendRequest,
                settings?.AutoAcceptLuckyMoney,
                settings?.AutoLikeMoments);

            return Task.FromResult(true);
        }
    }
}
