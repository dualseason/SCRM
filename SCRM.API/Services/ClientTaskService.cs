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
        private readonly Microsoft.Extensions.Logging.ILogger<ClientTaskService> _logger;
        
        /// <summary>
        /// 挂起的任务字典: TaskId -> TaskCompletionSource (用于等待从 Netty 返回的异步结果)
        /// key: TaskId (long)
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<long, TaskCompletionSource<SCRM.SHARED.Models.Dtos.TaskResult>> _pendingTasks = new();

        public ClientTaskService(NettyMessageService nettyMessageService, Microsoft.Extensions.Logging.ILogger<ClientTaskService> logger)
        {
            _nettyMessageService = nettyMessageService;
            _logger = logger;
        }

        /// <summary>
        /// 完成挂起的任务
        /// 当 MessageRouter 收到 TaskResultNotice 时调用
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <param name="success">是否成功</param>
        /// <param name="message">错误信息或结果描述</param>
        public void CompleteTask(long taskId, bool success, string? message = null)
        {
            if (_pendingTasks.TryRemove(taskId, out var tcs))
            {
                tcs.TrySetResult(new SCRM.SHARED.Models.Dtos.TaskResult { Success = success, Message = message });
            }
        }

        /// <summary>
        /// 发送心跳请求 (1001)
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
        /// 发送给好友发消息任务 (1.1)
        /// 此任务需要等待客户端的 TaskResultNotice 返回结果
        /// </summary>
        /// <param name="connectionId">连接ID</param>
        /// <param name="friendWxId">好友微信号</param>
        /// <param name="content">消息内容</param>
        /// <param name="contentType">消息类型</param>
        /// <returns>任务执行结果</returns>
        /// <summary>
        /// 通用任务发送并等待结果帮助方法
        /// </summary>
        private async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendTaskAndWaitAsync(
            Google.Protobuf.IMessage taskMessage, 
            string msgType, 
            string connectionId, 
            long taskId, 
            int timeoutMs = 15000)
        {
            var tcs = new TaskCompletionSource<SCRM.SHARED.Models.Dtos.TaskResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingTasks.TryAdd(taskId, tcs);

            var sent = await _nettyMessageService.SendMessageToNettyAsync(taskMessage, msgType, connectionId, customMessageId: taskId);

            if (!sent)
            {
                _pendingTasks.TryRemove(taskId, out _);
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Failed to send to Netty");
            }

            var timeoutTask = Task.Delay(timeoutMs);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _pendingTasks.TryRemove(taskId, out _);
                return SCRM.SHARED.Models.Dtos.TaskResult.Fail("Timeout waiting for client response");
            }

            return await tcs.Task;
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendTalkToFriendTaskAsync(string connectionId, string friendWxId, string content, EnumContentType contentType = EnumContentType.Text)
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

            return await SendTaskAndWaitAsync(task, EnumMsgType.TalkToFriendTask.ToString(), connectionId, taskId);
        }

        public async Task<bool> SendSyncFriendListTaskAsync(string connectionId)
        {
            // Sync task usually results in FriendPushNotice, not a direct TaskResult
            // So we keep it fire-and-forget or await ACK (1002)? 
            // Current design keeps it simple.
            return await _nettyMessageService.SendMessageToNettyAsync(
                null, 
                EnumMsgType.SyncFriendListAsyncReq.ToString(), 
                connectionId);
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendAddFriendTaskAsync(string connectionId, string friendWxId, string message, int scene = 3)
        {
            var taskId = DateTime.UtcNow.Ticks;
            var task = new AddFriendWithSceneTaskMessage
            {
                Friend = friendWxId,
                Message = message,
                Scene = scene,
                TaskId = taskId
            };

            return await SendTaskAndWaitAsync(task, EnumMsgType.AddFriendWithSceneTask.ToString(), connectionId, taskId);
        }

        public async Task<bool> SendGetGroupSendHistoryTaskAsync(string connectionId)
        {
             // Usually returns a list notice, not generic result
             var task = new GetGroupSendHistoryTaskMessage { TaskId = DateTime.UtcNow.Ticks };
             return await _nettyMessageService.SendMessageToNettyAsync(task, EnumMsgType.GetGroupSendHistoryTask.ToString(), connectionId);
        }
        
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendPostSNSNewsTaskAsync(string connectionId, string content, List<string> attachments, long taskId)
        {
            var task = new PostSNSNewsTaskMessage
            {
                Content = content,
                TaskId = taskId
            };
            
            return await SendTaskAndWaitAsync(task, EnumMsgType.PostSnsnewsTask.ToString(), connectionId, taskId);
        }

        public async Task<bool> SendTriggerFriendPushTaskAsync(string connectionId, long taskId)
        {
            // Init task, fire-and-forget likely preferred unless we want to wait for "Start Push" Ack
            var task = new TriggerFriendPushTaskMessage { TaskId = taskId };
            return await _nettyMessageService.SendMessageToNettyAsync(task, EnumMsgType.TriggerFriendPushTask.ToString(), connectionId);
        }

        public async Task<bool> SendTriggerChatRoomPushTaskAsync(string connectionId, long taskId)
        {
            var task = new TriggerChatRoomPushTaskMessage { TaskId = taskId };
            return await _nettyMessageService.SendMessageToNettyAsync(task, EnumMsgType.TriggerChatroomPushTask.ToString(), connectionId);
        }

        public async Task<bool> SendTriggerCirclePushTaskAsync(string connectionId, long taskId)
        {
             var task = new TriggerCirclePushTaskMessage { TaskId = taskId };
             return await _nettyMessageService.SendMessageToNettyAsync(task, EnumMsgType.TriggerCirclePushTask.ToString(), connectionId);
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendOneKeyLikeTaskAsync(string connectionId, long taskId)
        {
            var task = new OneKeyLikeTaskMessage { TaskId = taskId };
            return await SendTaskAndWaitAsync(task, EnumMsgType.OneKeyLikeTask.ToString(), connectionId, taskId); // Check if this returns TaskResult or OneKeyLikeTaskResult
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendRevokeMessageTaskAsync(string connectionId, string friendId, long msgSvrId, long taskId)
        {
            var task = new RevokeMessageTaskMessage
            {
                FriendId = friendId,
                MsgId = msgSvrId,
                TaskId = taskId
            };
            return await SendTaskAndWaitAsync(task, EnumMsgType.RevokeMessageTask.ToString(), connectionId, taskId);
        }
        
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendChatRoomActionTaskAsync(string connectionId, string chatRoomId, EnumChatRoomAction action, string content, int intValue, long taskId)
        {
            var task = new ChatRoomActionTaskMessage
            {
                ChatRoomId = chatRoomId,
                Action = action,
                Content = content,
                IntValue = intValue,
                TaskId = taskId
            };

            return await SendTaskAndWaitAsync(task, EnumMsgType.ChatRoomActionTask.ToString(), connectionId, taskId);
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendAgreeJoinChatRoomTaskAsync(string connectionId, string talker, long msgSvrId, string msgContent, long taskId)
        {
            var task = new AgreeJoinChatRoomTaskMessage
            {
                Talker = talker,
                MsgSvrId = msgSvrId,
                MsgContent = msgContent,
                TaskId = taskId
            };
            return await SendTaskAndWaitAsync(task, EnumMsgType.AgreeJoinChatRoomTask.ToString(), connectionId, taskId, 30000); // 30s timeout for join
        }
        
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendDeleteFriendTaskAsync(string connectionId, string friendId, long taskId)
        {
            var task = new DeleteFriendTaskMessage
            {
                FriendId = friendId,
                TaskId = taskId
            };
            return await SendTaskAndWaitAsync(task, EnumMsgType.DeleteFriendTask.ToString(), connectionId, taskId);
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendAcceptFriendAddRequestTaskAsync(string connectionId, string friendId, string friendNick, long taskId)
        {
            var task = new AcceptFriendAddRequestTaskMessage
            {
                FriendId = friendId,
                FriendNick = friendNick,
                Operation = AcceptFriendAddRequestTaskMessage.Types.EnumFriendAddOperation.Accept,
                TaskId = taskId
            };
            return await SendTaskAndWaitAsync(task, EnumMsgType.AcceptFriendAddRequestTask.ToString(), connectionId, taskId, 30000);
        }
        
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendScreenShotTaskAsync(string connectionId, long taskId)
        {
            var task = new ScreenShotTaskMessage
            {
                Type = 0, 
                Param = "", 
                TaskId = taskId
            };
            return await SendTaskAndWaitAsync(task, EnumMsgType.ScreenShotTask.ToString(), connectionId, taskId, 20000); // 20s for screenshot upload
        }

        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendPhoneActionTaskAsync(string connectionId, EnumPhoneAction action, long taskId)
        {
            var task = new PhoneActionTaskMessage
            {
                Action = action,
                TaskId = taskId
            };
            // Note: PhoneAction might not return TaskResultNotice properly in all versions, 
            // but if Audit says verified, we use it.
            return await SendTaskAndWaitAsync(task, EnumMsgType.PhoneActionTask.ToString(), connectionId, taskId);
        }

        public async Task<bool> SendTriggerConfigPushTaskAsync(string connectionId, long taskId)
        {
            var task = new TriggerConfigPushMessage { };
            return await _nettyMessageService.SendMessageToNettyAsync(task, EnumMsgType.TriggerConfigPush.ToString(), connectionId);
        }

        public async Task<bool> SendSetConfigTaskAsync(string connectionId, Dictionary<string, bool> boolConfs, Dictionary<string, int> intConfs, Dictionary<string, string> strConfs)
        {
            var task = new SetConfigTaskMessage();
            if (boolConfs != null)
                foreach (var kvp in boolConfs) task.BoolConfs.Add(new BoolConfigMessage { Key = kvp.Key, Value = kvp.Value });
            if (intConfs != null)
                foreach (var kvp in intConfs) task.IntConfs.Add(new IntConfigMessage { Key = kvp.Key, Value = kvp.Value });
            if (strConfs != null)
                foreach (var kvp in strConfs) task.StrConfs.Add(new StrConfigMessage { Key = kvp.Key, Value = kvp.Value });

            return await _nettyMessageService.SendMessageToNettyAsync(task, EnumMsgType.SetConfigTask.ToString(), connectionId);
        }
        
        public async Task<bool> SendTakeLuckyMoneyTaskAsync(string connectionId, string weChatId, string friendId, long msgSvrId, string key)
        {
            var task = new TakeLuckyMoneyTaskMessage
            {
                WeChatId = weChatId,
                FriendId = friendId,
                MsgSvrId = msgSvrId,
                MsgKey = key, 
                TaskId = DateTime.UtcNow.Ticks
            };
            return await _nettyMessageService.SendMessageToNettyAsync(task, EnumMsgType.TakeLuckyMoneyTask.ToString(), connectionId);
        }

        public async Task<bool> SendQueryHbDetailTaskAsync(string connectionId, string weChatId, string nativeUrl)
        {
            var task = new QueryHbDetailTaskMessage
            {
                WeChatId = weChatId,
                HbUrl = nativeUrl 
            };
            return await _nettyMessageService.SendMessageToNettyAsync(task, EnumMsgType.QueryHbDetailTask.ToString(), connectionId);
        }
        
        public async Task<SCRM.SHARED.Models.Dtos.TaskResult> SendCircleLikeTaskAsync(string connectionId, string weChatId, long circleId, bool isCancel, long taskId)
        {
            var task = new CircleLikeTaskMessage
            {
                WeChatId = weChatId,
                CircleId = circleId,
                IsCancel = isCancel,
                TaskId = taskId
            };
            return await SendTaskAndWaitAsync(task, EnumMsgType.CircleLikeTask.ToString(), connectionId, taskId);
        }
    }
}

