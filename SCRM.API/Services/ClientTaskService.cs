using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Jubo.JuLiao.IM.Wx.Proto;
using SCRM.Services;
using System;
using System.Threading.Tasks;

namespace SCRM.Services
{
    public class ClientTaskService
    {
        private readonly NettyMessageService _nettyMessageService;
        private readonly Serilog.ILogger _logger = SCRM.Shared.Core.Utility.logger;

        public ClientTaskService(NettyMessageService nettyMessageService)
        {
            _nettyMessageService = nettyMessageService;
        }

        /// <summary>
        /// 发送心跳请求
        /// </summary>
        public async Task<bool> SendHeartBeatAsync(string connectionId)
        {
            var task = new HeartBeatMessage();
            return await _nettyMessageService.SendMessageToNettyAsync(
                task, 
                EnumMsgType.HeartBeatReq.ToString(), 
                connectionId);
        }

        /// <summary>
        /// 发送给好友发消息任务
        /// </summary>
        public async Task<bool> SendTalkToFriendTaskAsync(string connectionId, string friendWxId, string content, EnumContentType contentType = EnumContentType.Text)
        {
            var task = new TalkToFriendTaskMessage
            {
                FriendId = friendWxId,
                Content = ByteString.CopyFromUtf8(content),
                ContentType = contentType,
                MsgId = DateTime.UtcNow.Ticks,
                Immediate = true
            };

            return await _nettyMessageService.SendMessageToNettyAsync(
                task, 
                EnumMsgType.TalkToFriendTask.ToString(), 
                connectionId);
        }

        /// <summary>
        /// 发送同步好友列表任务
        /// </summary>
        public async Task<bool> SendSyncFriendListTaskAsync(string connectionId)
        {
            return await _nettyMessageService.SendMessageToNettyAsync(
                null, 
                EnumMsgType.SyncFriendListAsyncReq.ToString(), 
                connectionId);
        }

        /// <summary>
        /// 发送添加好友任务
        /// </summary>
        public async Task<bool> SendAddFriendTaskAsync(string connectionId, string friendWxId, string message, int scene = 3)
        {
            var task = new AddFriendWithSceneTaskMessage
            {
                Friend = friendWxId,
                Message = message,
                Scene = scene,
                TaskId = DateTime.UtcNow.Ticks
            };

            return await _nettyMessageService.SendMessageToNettyAsync(
                task, 
                EnumMsgType.AddFriendWithSceneTask.ToString(), 
                connectionId);
        }

        /// <summary>
        /// 发送获取群发历史任务
        /// </summary>
        public async Task<bool> SendGetGroupSendHistoryTaskAsync(string connectionId)
        {
            var task = new GetGroupSendHistoryTaskMessage
            {
                TaskId = DateTime.UtcNow.Ticks
            };

            return await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.GetGroupSendHistoryTask.ToString(),
                connectionId);
        }
        /// <summary>
        /// 发送发布朋友圈任务
        /// </summary>
        public async Task<bool> SendPostSNSNewsTaskAsync(string connectionId, string content, List<string> attachments, long taskId)
        {
            var task = new PostSNSNewsTaskMessage
            {
                Content = content,
                TaskId = taskId
            };
            // Note: Attachment handling would go here, simplified for now
            
            return await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.PostSnsnewsTask.ToString(),
                connectionId);
        }

        /// <summary>
        /// 发送触发好友列表推送任务
        /// </summary>
        public async Task<bool> SendTriggerFriendPushTaskAsync(string connectionId, long taskId)
        {
            var task = new TriggerFriendPushTaskMessage
            {
                TaskId = taskId
            };

            return await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.TriggerFriendPushTask.ToString(),
                connectionId);
        }

        /// <summary>
        /// 发送触发群聊列表推送任务
        /// </summary>
        public async Task<bool> SendTriggerChatRoomPushTaskAsync(string connectionId, long taskId)
        {
            var task = new TriggerChatRoomPushTaskMessage
            {
                TaskId = taskId
            };

            return await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.TriggerChatroomPushTask.ToString(),
                connectionId);
        }

        /// <summary>
        /// 发送触发朋友圈推送任务
        /// </summary>
        public async Task<bool> SendTriggerCirclePushTaskAsync(string connectionId, long taskId)
        {
            var task = new TriggerCirclePushTaskMessage
            {
                TaskId = taskId
            };

            return await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.TriggerCirclePushTask.ToString(),
                connectionId);
        }

        /// <summary>
        /// 发送一键点赞任务
        /// </summary>
        public async Task<bool> SendOneKeyLikeTaskAsync(string connectionId, long taskId)
        {
            var task = new OneKeyLikeTaskMessage
            {
                TaskId = taskId
            };

            return await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.OneKeyLikeTask.ToString(),
                connectionId);
        }

        /// <summary>
        /// 发送消息撤回任务
        /// </summary>
        public async Task<bool> SendRevokeMessageTaskAsync(string connectionId, string friendId, long msgSvrId, long taskId)
        {
            var task = new RevokeMessageTaskMessage
            {
                FriendId = friendId,
                MsgId = msgSvrId,
                TaskId = taskId
            };

            return await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.RevokeMessageTask.ToString(),
                connectionId);
        }
    }
}
