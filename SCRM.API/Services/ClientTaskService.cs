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
    }
}
