using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using SCRM.SHARED.Proto;
using SCRM.Services;
using System;
using System.Threading.Tasks;

namespace SCRM.Services
{
    public class ClientTaskService
    {
        private readonly NettyMessageService _nettyMessageService;
        private readonly Serilog.ILogger _logger = SCRM.Shared.Core.Utility.logger;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<long, TaskCompletionSource<SCRM.SHARED.Models.TaskResult>> _pendingTasks = new();

        public ClientTaskService(NettyMessageService nettyMessageService)
        {
            _nettyMessageService = nettyMessageService;
        }

        public void CompleteTask(long taskId, bool success, string? message = null)
        {
            if (_pendingTasks.TryRemove(taskId, out var tcs))
            {
                tcs.TrySetResult(new SCRM.SHARED.Models.TaskResult { Success = success, Message = message });
            }
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
        public async Task<SCRM.SHARED.Models.TaskResult> SendTalkToFriendTaskAsync(string connectionId, string friendWxId, string content, EnumContentType contentType = EnumContentType.Text)
        {
            var taskId = DateTime.UtcNow.Ticks;
            var task = new TalkToFriendTaskMessage
            {
                FriendId = friendWxId,
                Content = ByteString.CopyFromUtf8(content),
                ContentType = contentType,
                MsgId = taskId,
                Immediate = true
            };

            var tcs = new TaskCompletionSource<SCRM.SHARED.Models.TaskResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingTasks.TryAdd(taskId, tcs);

            var sent = await _nettyMessageService.SendMessageToNettyAsync(
                task, 
                EnumMsgType.TalkToFriendTask.ToString(), 
                connectionId);

            if (!sent)
            {
                _pendingTasks.TryRemove(taskId, out _);
                return SCRM.SHARED.Models.TaskResult.Fail("Failed to send to Netty");
            }

            // Wait for result with timeout (e.g. 10 seconds)
            var timeoutTask = Task.Delay(10000);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _pendingTasks.TryRemove(taskId, out _);
                return SCRM.SHARED.Models.TaskResult.Fail("Timeout waiting for client response");
            }

            return await tcs.Task;
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
        /// <summary>
        /// 发送群聊操作任务（踢人、拉人、改名等）
        /// </summary>
        public async Task<bool> SendChatRoomActionTaskAsync(string connectionId, string chatRoomId, EnumChatRoomAction action, string content, int intValue, long taskId)
        {
            var task = new ChatRoomActionTaskMessage
            {
                ChatRoomId = chatRoomId,
                Action = action,
                Content = content,
                IntValue = intValue,
                TaskId = taskId
            };

            return await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.ChatRoomActionTask.ToString(),
                connectionId);
        }

        /// <summary>
        /// 发送同意入群任务
        /// </summary>
        public async Task<bool> SendAgreeJoinChatRoomTaskAsync(string connectionId, string talker, long msgSvrId, string msgContent, long taskId)
        {
            var task = new AgreeJoinChatRoomTaskMessage
            {
                Talker = talker,
                MsgSvrId = msgSvrId,
                MsgContent = msgContent,
                TaskId = taskId
            };

            return await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.AgreeJoinChatRoomTask.ToString(),
                connectionId);
        }
        /// <summary>
        /// 发送删除好友任务
        /// </summary>
        public async Task<bool> SendDeleteFriendTaskAsync(string connectionId, string friendId, long taskId)
        {
            var task = new DeleteFriendTaskMessage
            {
                FriendId = friendId,
                TaskId = taskId
            };

            return await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.DeleteFriendTask.ToString(),
                connectionId);
        }

        /// <summary>
        /// 发送接受好友请求任务（自动通过）
        /// </summary>
        public async Task<bool> SendAcceptFriendAddRequestTaskAsync(string connectionId, string friendId, string friendNick, long taskId)
        {
            var task = new AcceptFriendAddRequestTaskMessage
            {
                FriendId = friendId,
                FriendNick = friendNick,
                Operation = AcceptFriendAddRequestTaskMessage.Types.EnumFriendAddOperation.Accept,
                TaskId = taskId
            };

            return await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.AcceptFriendAddRequestTask.ToString(),
                connectionId);
        }
        /// <summary>
        /// 发送截屏任务
        /// </summary>
        public async Task<bool> SendScreenShotTaskAsync(string connectionId, long taskId)
        {
            var task = new ScreenShotTaskMessage
            {
                Type = 0, // Default type
                Param = "default", // Default param
                TaskId = taskId
            };

            return await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.ScreenShotTask.ToString(),
                connectionId);
        }

        /// <summary>
        /// 发送手机操作任务（重启、清理缓存等）
        /// </summary>
        public async Task<bool> SendPhoneActionTaskAsync(string connectionId, EnumPhoneAction action, long taskId)
        {
            var task = new PhoneActionTaskMessage
            {
                Action = action,
                TaskId = taskId
            };

            return await _nettyMessageService.SendMessageToNettyAsync(
                task,
                EnumMsgType.PhoneActionTask.ToString(),
                connectionId);
        }
    }
}
