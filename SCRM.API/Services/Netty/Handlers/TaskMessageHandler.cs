using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Google.Protobuf;
using DotNetty.Transport.Channels;
using Jubo.JuLiao.IM.Wx.Proto;
using SCRM.API.Hubs;
using SCRM.Services;
using SCRM.Services.Data;
using SCRM.Services.Events;
using SCRM.API.Models.Entities;
using SCRM.API.Models.Events;

namespace SCRM.API.Services.Netty.Handlers
{
    /// <summary>
    /// 任务消息处理器
    /// 负责处理服务端发起任务的执行结果（如发消息结果、截图结果、红包查询结果等）
    /// Scoped Service
    /// </summary>
    public class TaskMessageHandler
    {
        private readonly ILogger<TaskMessageHandler> _logger;
        private readonly ClientTaskService _clientTaskService;
        private readonly ConnectionManager _connectionManager;
        private readonly IEventBus _eventBus;
        private readonly IHubContext<ClientHub> _hubContext;
        private readonly ApplicationDbContext _dbContext;

        public TaskMessageHandler(
            ILogger<TaskMessageHandler> logger,
            ClientTaskService clientTaskService,
            ConnectionManager connectionManager,
            IEventBus eventBus,
            IHubContext<ClientHub> hubContext,
            ApplicationDbContext dbContext)
        {
            _logger = logger;
            _clientTaskService = clientTaskService;
            _connectionManager = connectionManager;
            _eventBus = eventBus;
            _hubContext = hubContext;
            _dbContext = dbContext;
        }

        /// <summary>
        /// 处理给好友发消息任务结果 (4.1)
        /// </summary>
        public async Task HandleTalkToFriendTaskResult(TransportMessage message, IChannelHandlerContext context)
        {
            var result = message.Content.Unpack<TalkToFriendTaskResultNoticeMessage>();
            _logger.LogInformation("给好友发消息任务结果：成功={Success}, 代码={Code}, 消息={ErrMsg}", 
                result.Success, result.Code, result.ErrMsg);
            
             // 标记任务完成
             long correlationId = result.MsgId;
             if (correlationId == 0 && message.RefMessageId != 0) correlationId = message.RefMessageId; 
             _clientTaskService.CompleteTask(correlationId, result.Success, result.ErrMsg);

            // 发布事件给 UI
            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            await _eventBus.PublishAsync(new TaskResultReceivedEvent(correlationId, result.Success, result.ErrMsg, connectionId, connectionInfo?.deviceInfo ?? connectionId));

            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理截屏任务结果 (4.8)
        /// </summary>
        public async Task HandleScreenShotTaskResultNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var result = message.Content.Unpack<ScreenShotTaskResultNoticeMessage>();
            _logger.LogInformation("截屏结果：成功={Success}, Url={Url}", result.Success, result.Url);
            
            _clientTaskService.CompleteTask(result.TaskId, result.Success, result.Url);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            
            if (connectionInfo != null)
            {
                 // 兼容旧版 SignalR 通知
                 await _hubContext.Clients.Group(connectionInfo.deviceInfo ?? connectionId).SendAsync("ScreenShotReceived", result.Url);
                 
                 // 发布响应式事件
                 await _eventBus.PublishAsync(new TaskResultReceivedEvent(result.TaskId, result.Success, result.Url, connectionId, connectionInfo.deviceInfo ?? connectionId));
            }
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理通用任务结果通知 (4.9)
        /// </summary>
        public async Task HandleTaskResultNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var result = message.Content.Unpack<TaskResultNoticeMessage>();

            long correlationId = result.TaskId;
            if (correlationId == 0 && message.RefMessageId != 0) correlationId = message.RefMessageId; 
            _logger.LogInformation("任务结果：成功={Success}, 任务Id={TaskId}, 关联Id={RefId}, 消息={ErrMsg}", result.Success, result.TaskId, message.RefMessageId, result.ErrMsg);
            _clientTaskService.CompleteTask(correlationId, result.Success, result.ErrMsg);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            await _eventBus.PublishAsync(new TaskResultReceivedEvent(correlationId, result.Success, result.ErrMsg, connectionId, connectionInfo?.deviceInfo ?? connectionId));

            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理红包详情结果 (4.25 结果)
        /// 1. 记录红包信息 (RedPacket)
        /// 2. 记录领取详情 (RedPacketRecord)
        /// 3. 通知前端
        /// </summary>
        public async Task HandleQueryHbDetailTaskResultNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<QueryHbDetailTaskResultNoticeMessage>();
            _logger.LogInformation("红包详情：{WeChatId} - {HbUrl} (发送者：{Sender}, 金额：{TotalAmount})", 
                notice.WeChatId, notice.HbUrl, notice.Sender, notice.TotalAmount);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);

            if (connectionInfo == null || !long.TryParse(connectionInfo.userId, out long accountId))
            {
                return;
            }

            // 1. 创建红包记录
            var rp = new RedPacket
            {
                wechatAccountId = (int)accountId,
                senderWxid = notice.Sender ?? "Unknown", 
                totalAmount = notice.TotalAmount / 100m, // 分 -> 元
                totalCount = notice.TotalNum,
                redPacketMessage = notice.Wishing ?? "", 
                targetWxid = notice.HbUrl, // Hack: Store Url
                redPacketStatus = notice.HbStatus,
                receivedCount = notice.RecNum,
                receivedAmount = notice.RecAmount / 100m,
                sendTime = DateTime.UtcNow, 
                createdAt = DateTime.UtcNow,
                isDeleted = false
            };

            _dbContext.RedPackets.Add(rp);
            await _dbContext.SaveChangesAsync(); 

            // 2. 存取领取记录
            if (notice.Records != null)
            {
                foreach (var rec in notice.Records)
                {
                    var record = new RedPacketRecord
                    {
                        redPacketId = rp.id,
                        receiverWxid = rec.UserName,
                        receivedAmount = rec.Amount / 100m,
                        receiveTime = DateTime.TryParse(rec.Time, out var t) ? t : DateTime.UtcNow,
                        receiveStatus = 1,
                        createdAt = DateTime.UtcNow
                    };
                    _dbContext.RedPacketRecords.Add(record);
                }
                await _dbContext.SaveChangesAsync();
            }

            // 3. 前端通知
            await _hubContext.Clients.Group(connectionInfo.deviceInfo ?? connectionId).SendAsync("RedPacketReceived", rp.senderWxid, rp.totalAmount);
            
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理红包状态结果 (4.25 结果)
        /// 通知前端变更
        /// </summary>
        public async Task HandleQueryHbStatusTaskResultNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<QueryHbStatusTaskResultNoticeMessage>();
            _logger.LogInformation("红包状态：{WeChatId} - {HbUrl} (状态：{Status}, 消息：{Msg})", 
                notice.WeChatId, notice.HbUrl, notice.HbStatus, notice.StatusMsg);
            
            // 简单通知前端
            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            
            if (connectionInfo != null)
            {
                await _hubContext.Clients.Group(connectionInfo.deviceInfo ?? connectionId).SendAsync("RedPacketStatusChanged", notice.HbUrl, notice.HbStatus);
            }
            await SendAckAsync(message, context);
        }

        private async Task SendAckAsync(TransportMessage message, IChannelHandlerContext context)
        {
            var response = new TransportMessage
            {
                Id = 0,
                MsgType = EnumMsgType.MsgReceivedAck,
                RefMessageId = message.Id
            };

            if (!string.IsNullOrEmpty(message.AccessToken))
            {
                response.AccessToken = message.AccessToken;
            }

            await context.WriteAndFlushAsync(response);
        }
    }
}
