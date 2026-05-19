using DotNetty.Transport.Channels;
using Google.Protobuf;
using Jubo.JuLiao.IM.Wx.Proto;
using Microsoft.Extensions.Logging;
using SCRM.API.Models.Entities;
using SCRM.API.Services.Core;
using SCRM.API.Services.Data;
using SCRM.API.Services.Netty.Handlers.Abstractions;
using SCRM.Models;
using SCRM.Services.Data;
using SCRM.Services.Events;
using SCRM.SHARED.Models.Events;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SCRM.API.Services.Netty.Handlers
{
    /// <summary>
    /// 手机短信与通话记录消息处理器。
    /// <para>处理 1300~1307 这组 Android 系统短信/通话 Notice，并统一通过 DbHelper 落库。</para>
    /// </summary>
    public class PhoneMessageHandler : MessageHandlerBase
    {
        private readonly ILogger<PhoneMessageHandler> _logger;
        private readonly ApplicationDbContext _db;
        private readonly ConnectionManager _connectionManager;
        private readonly IEventBus _eventBus;
        private readonly ClientTaskService _clientTaskService;

        public PhoneMessageHandler(
            ILogger<PhoneMessageHandler> logger,
            ApplicationDbContext db,
            ConnectionManager connectionManager,
            IEventBus eventBus,
            ClientTaskService clientTaskService) : base(logger)
        {
            _logger = logger;
            _db = db;
            _connectionManager = connectionManager;
            _eventBus = eventBus;
            _clientTaskService = clientTaskService;
        }

        public async Task HandleMessage(TransportMessage message, IChannelHandlerContext context)
        {
            await SendAckAsync(message, context);

            try
            {
                switch (message.MsgType)
                {
                    case EnumMsgType.SmsPushNotice:
                        await HandleSmsPush(message.Content.Unpack<SmsPushNoticeMessage>(), context);
                        break;
                    case EnumMsgType.SmsReadNotice:
                        await HandleSmsRead(message.Content.Unpack<SmsReadNoticeMessage>(), context);
                        break;
                    case EnumMsgType.SmsSentNotice:
                        await HandleSmsSent(message.Content.Unpack<SmsSentNoticeMessage>(), context);
                        break;
                    case EnumMsgType.PullSmsTaskResultNotice:
                        await HandlePullSmsResult(message.Content.Unpack<PullSmsTaskResultNoticeMessage>(), context);
                        break;
                    case EnumMsgType.CallLogPushNotice:
                        await HandleCallLogPush(message.Content.Unpack<CallLogPushNoticeMessage>(), context);
                        break;
                    case EnumMsgType.PullCallLogTaskResultNotice:
                        await HandlePullCallLogResult(message.Content.Unpack<PullCallLogTaskResultNoticeMessage>(), context);
                        break;
                    default:
                        _logger.LogWarning("Unhandled Phone Message Type: {Type} ({Id})", message.MsgType, (int)message.MsgType);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling phone message: {Type}", message.MsgType);
            }
        }

        /// <summary>
        /// 处理实时短信推送。
        /// </summary>
        private async Task HandleSmsPush(SmsPushNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId) || msg.Messages == null)
            {
                _logger.LogWarning("SmsPushNotice ignored because required fields are empty. WeChatId={WeChatId}", msg.WeChatId);
                return;
            }

            var sms = msg.Messages;
            var saved = await _db.UpsertSmsRecord(
                msg.WeChatId,
                msg.IMEI,
                sms.Id,
                sms.ThreadId,
                sms.Number,
                sms.Type,
                sms.Date,
                sms.Content,
                sms.Read,
                sms.SimId,
                sms.BlockType,
                "Push");

            _logger.LogInformation(
                "SmsPushNotice handled: WeChatId={WeChatId}, Imei={Imei}, SmsId={SmsId}, ThreadId={ThreadId}, Saved={Saved}",
                msg.WeChatId,
                msg.IMEI,
                sms.Id,
                sms.ThreadId,
                saved != null);

            await PublishSmsUpdatedAsync(context, msg.WeChatId);
        }

        /// <summary>
        /// 处理短信已读通知。
        /// </summary>
        private async Task HandleSmsRead(SmsReadNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId))
            {
                _logger.LogWarning("SmsReadNotice ignored because WeChatId is empty. SmsId={SmsId}, ThreadId={ThreadId}", msg.SmsId, msg.ThreadId);
                return;
            }

            var saved = await _db.MarkSmsRead(msg.WeChatId, msg.IMEI, msg.SmsId, msg.ThreadId);
            _logger.LogInformation(
                "SmsReadNotice handled: WeChatId={WeChatId}, Imei={Imei}, SmsId={SmsId}, ThreadId={ThreadId}, Updated={Updated}",
                msg.WeChatId,
                msg.IMEI,
                msg.SmsId,
                msg.ThreadId,
                saved != null);

            await PublishSmsUpdatedAsync(context, msg.WeChatId);
        }

        /// <summary>
        /// 处理短信发送状态通知。
        /// </summary>
        private async Task HandleSmsSent(SmsSentNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId))
            {
                _logger.LogWarning("SmsSentNotice ignored because WeChatId is empty. SmsId={SmsId}, Type={Type}", msg.SmsId, msg.Type);
                return;
            }

            var saved = await _db.MarkSmsSent(msg.WeChatId, msg.IMEI, msg.SmsId, msg.Type);
            _logger.LogInformation(
                "SmsSentNotice handled: WeChatId={WeChatId}, Imei={Imei}, SmsId={SmsId}, Type={Type}, Updated={Updated}",
                msg.WeChatId,
                msg.IMEI,
                msg.SmsId,
                msg.Type,
                saved != null);

            await PublishSmsUpdatedAsync(context, msg.WeChatId);
        }

        /// <summary>
        /// 处理短信历史拉取结果。
        /// </summary>
        private async Task HandlePullSmsResult(PullSmsTaskResultNoticeMessage msg, IChannelHandlerContext context)
        {
            var records = msg.Messages.Select(item => new SmsRecord
            {
                smsId = item.Id,
                threadId = item.ThreadId,
                number = item.Number ?? string.Empty,
                type = item.Type,
                rawDate = item.Date,
                content = item.Content ?? string.Empty,
                isRead = item.Read,
                simId = item.SimId,
                blockType = item.BlockType
            }).ToList();

            var saved = await _db.UpsertSmsRecords(msg.WeChatId, msg.IMEI, records, "Pull");
            var success = msg.Success && string.IsNullOrWhiteSpace(msg.ErrMsg);
            var resultMessage = success
                ? $"短信历史拉取完成：{saved.Count}/{msg.Messages.Count} 条"
                : $"短信历史拉取异常：{(string.IsNullOrWhiteSpace(msg.ErrMsg) ? "客户端返回失败" : msg.ErrMsg)}；已落库 {saved.Count}/{msg.Messages.Count} 条";

            _logger.LogInformation(
                "PullSmsTaskResultNotice handled: WeChatId={WeChatId}, Imei={Imei}, TaskId={TaskId}, Success={Success}, Count={Count}, Saved={Saved}, ErrMsg={ErrMsg}",
                msg.WeChatId,
                msg.IMEI,
                msg.TaskId,
                msg.Success,
                msg.Messages.Count,
                saved.Count,
                msg.ErrMsg);

            await CompleteAndPublishTaskResultAsync(context, msg.TaskId, success, resultMessage);
            await PublishSmsUpdatedAsync(context, msg.WeChatId);
        }

        /// <summary>
        /// 处理实时通话记录推送。
        /// </summary>
        private async Task HandleCallLogPush(CallLogPushNoticeMessage msg, IChannelHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(msg.WeChatId) || msg.Messages == null)
            {
                _logger.LogWarning("CallLogPushNotice ignored because required fields are empty. WeChatId={WeChatId}", msg.WeChatId);
                return;
            }

            var call = msg.Messages;
            var saved = await _db.UpsertCallLogRecord(
                msg.WeChatId,
                msg.IMEI,
                call.Id,
                call.Number,
                call.Type,
                call.Date,
                call.Duration,
                call.Record,
                call.SimId,
                call.BlockType,
                "Push");

            _logger.LogInformation(
                "CallLogPushNotice handled: WeChatId={WeChatId}, Imei={Imei}, CallLogId={CallLogId}, Number={Number}, Saved={Saved}",
                msg.WeChatId,
                msg.IMEI,
                call.Id,
                call.Number,
                saved != null);

            await PublishCallLogUpdatedAsync(context, msg.WeChatId);
        }

        /// <summary>
        /// 处理通话记录历史拉取结果。
        /// </summary>
        private async Task HandlePullCallLogResult(PullCallLogTaskResultNoticeMessage msg, IChannelHandlerContext context)
        {
            var records = msg.Messages.Select(item => new CallLogRecord
            {
                callLogId = item.Id,
                number = item.Number ?? string.Empty,
                type = item.Type,
                rawDate = item.Date,
                durationSeconds = item.Duration,
                recordUrl = item.Record ?? string.Empty,
                simId = item.SimId,
                blockType = item.BlockType
            }).ToList();

            var saved = await _db.UpsertCallLogRecords(msg.WeChatId, msg.IMEI, records, "Pull");
            // 兼容旧端错误分支可能出现 Success=true + ErrMsg：只要 ErrMsg 非空，前端仍按异常可见处理。
            var success = msg.Success && string.IsNullOrWhiteSpace(msg.ErrMsg);
            var resultMessage = success
                ? $"通话记录拉取完成：{saved.Count}/{msg.Messages.Count} 条"
                : $"通话记录拉取异常：{(string.IsNullOrWhiteSpace(msg.ErrMsg) ? "客户端返回失败" : msg.ErrMsg)}；已落库 {saved.Count}/{msg.Messages.Count} 条";

            _logger.LogInformation(
                "PullCallLogTaskResultNotice handled: WeChatId={WeChatId}, Imei={Imei}, TaskId={TaskId}, Success={Success}, Count={Count}, Saved={Saved}, ErrMsg={ErrMsg}",
                msg.WeChatId,
                msg.IMEI,
                msg.TaskId,
                msg.Success,
                msg.Messages.Count,
                saved.Count,
                msg.ErrMsg);

            await CompleteAndPublishTaskResultAsync(context, msg.TaskId, success, resultMessage);
            await PublishCallLogUpdatedAsync(context, msg.WeChatId);
        }

        /// <summary>
        /// 完成等待任务并发布页面可见任务结果。
        /// </summary>
        private async Task CompleteAndPublishTaskResultAsync(IChannelHandlerContext context, long taskId, bool success, string message)
        {
            if (taskId <= 0)
            {
                return;
            }

            _clientTaskService.CompleteTask(taskId, success, message);
            await PublishTaskResultAsync(context, taskId, success, message);
        }

        /// <summary>
        /// 发布短信列表刷新事件。
        /// </summary>
        private async Task PublishSmsUpdatedAsync(IChannelHandlerContext context, string accountId)
        {
            var connInfo = await GetConnectionInfoAsync(context);
            if (connInfo == null || string.IsNullOrWhiteSpace(accountId))
            {
                return;
            }

            await _eventBus.PublishAsync(new SmsRecordsUpdatedEvent(connInfo.deviceUuid, accountId, connInfo.userId));
        }

        /// <summary>
        /// 发布通话记录刷新事件。
        /// </summary>
        private async Task PublishCallLogUpdatedAsync(IChannelHandlerContext context, string accountId)
        {
            var connInfo = await GetConnectionInfoAsync(context);
            if (connInfo == null || string.IsNullOrWhiteSpace(accountId))
            {
                return;
            }

            await _eventBus.PublishAsync(new CallLogRecordsUpdatedEvent(connInfo.deviceUuid, accountId, connInfo.userId));
        }

        /// <summary>
        /// 发布异步任务结果。
        /// </summary>
        private async Task PublishTaskResultAsync(IChannelHandlerContext context, long taskId, bool success, string message)
        {
            var connId = context.Channel.Id.AsLongText();
            var connInfo = await GetConnectionInfoAsync(context);
            if (connInfo == null)
            {
                return;
            }

            await _eventBus.PublishAsync(new TaskResultReceivedEvent(taskId, success, message, connId, connInfo.deviceUuid));
        }

        /// <summary>
        /// 获取连接上下文。
        /// </summary>
        private async Task<UserConnectionInfo?> GetConnectionInfoAsync(IChannelHandlerContext context)
        {
            var connId = context.Channel.Id.AsLongText();
            return await _connectionManager.GetConnectionAsync(connId);
        }
    }
}
