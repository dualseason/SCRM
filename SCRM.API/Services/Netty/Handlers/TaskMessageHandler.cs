using DotNetty.Transport.Channels;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Jubo.JuLiao.IM.Wx.Proto;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SCRM.API.Hubs;
using SCRM.SHARED.Models.Events;
using SCRM.API.Services.Core;
using SCRM.Services.Events;
using SCRM.Services.Data;
using System;
using System.Threading.Tasks;

using SCRM.API.Services.Netty.Handlers.Abstractions;

namespace SCRM.API.Services.Netty.Handlers
{
    /// <summary>
    /// 任务结果消息处理器
    /// <para>负责接收并处理客户端对服务端下发任务的执行结果。</para>
    /// <para>核心功能：</para>
    /// <list type="bullet">
    /// <item>处理通用任务结果 (TaskResultNotice - 1200)</item>
    /// <item>处理截图任务结果 (ScreenShotTaskResultNotice - 1282)</item>
    /// <item>处理给好友发消息任务结果 (TalkToFriendTaskResultNotice)</item>
    /// <item>更新 ClientTaskService 挂起任务状态 (CompleteTask)</item>
    /// <item>发布 TaskResultReceivedEvent 事件</item>
    /// </list>
    /// </summary>
    public class TaskMessageHandler : MessageHandlerBase
    {
        private readonly ILogger<TaskMessageHandler> _logger;
        private readonly ClientTaskService _clientTaskService;
        private readonly ConnectionManager _connectionManager;
        private readonly IEventBus _eventBus;
        private readonly ApplicationDbContext _db;

        public TaskMessageHandler(
            ILogger<TaskMessageHandler> logger,
            ClientTaskService clientTaskService,
            ConnectionManager connectionManager,
            IEventBus eventBus,
            ApplicationDbContext db) : base(logger)
        {
            _logger = logger;
            _clientTaskService = clientTaskService;
            _connectionManager = connectionManager;
            _eventBus = eventBus;
            _db = db;
        }

        public async Task HandleTaskResult(TransportMessage message, IChannelHandlerContext context)
        {
            // 1. 发送 ACK
            await SendAckAsync(message, context);

            // 2. 解析通用任务结果 (TaskResultNotice)
            // 大多数任务结果都是 TaskResultNotice 类型，除了特定的几个
            long taskIdRequest = 0;
            bool success = false;
            string errMsg = string.Empty;

            try
            {
                var msgType = message.MsgType;
                switch (msgType)
                {
                    case EnumMsgType.ScreenShotTaskResultNotice: // 1282
                        var ssMsg = message.Content.Unpack<ScreenShotTaskResultNoticeMessage>();
                        success = ssMsg.Success;
                        errMsg = ""; // ssMsg.ErrorMessage; 
                        taskIdRequest = ssMsg.TaskId;

                        if (success && !string.IsNullOrEmpty(ssMsg.Url))
                        {
                            var connId = context.Channel.Id.AsLongText();
                            var connInfo = await _connectionManager.GetConnectionAsync(connId);
                            var deviceUuid = connInfo?.deviceUuid;
                            _eventBus.PublishAsync(new ScreenShotUploadedEvent(ssMsg.Url) { DeviceUuid = deviceUuid });
                        }
                        break;

                    case EnumMsgType.TalkToFriendTaskResultNotice:
                        var talkMsg = message.Content.Unpack<TalkToFriendTaskResultNoticeMessage>();
                        success = talkMsg.Success;
                        errMsg = ""; 
                        taskIdRequest = 0; 
                        break;

                    case EnumMsgType.TaskResultNotice:
                        var taskMsg = message.Content.Unpack<TaskResultNoticeMessage>();
                        success = taskMsg.Success;
                        errMsg = ""; 
                        taskIdRequest = taskMsg.TaskId;
                        break;

                    case EnumMsgType.QueryHbDetailTaskResultNotice:
                    case EnumMsgType.QueryHbStatusTaskResultNotice:
                         break;

                    default:
                        break;
                }

                _clientTaskService.CompleteTask(taskIdRequest, success, errMsg);

                if (taskIdRequest > 0 || success)
                {
                    var connId = context.Channel.Id.AsLongText();
                    var connInfo = await _connectionManager.GetConnectionAsync(connId);
                    var deviceUuid = connInfo?.deviceUuid;
                    _eventBus.PublishAsync(new TaskResultReceivedEvent(taskIdRequest, success, errMsg, "", deviceUuid));
                }

                _logger.LogInformation("Task Result Handled: Id={TaskId}, Success={Success}", taskIdRequest, success);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling TaskResult message");
            }
        }
    }
}
