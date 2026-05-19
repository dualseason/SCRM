using DotNetty.Transport.Channels;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Jubo.JuLiao.IM.Wx.Proto;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SCRM.API.Hubs;
using SCRM.API.Models.Entities;
using SCRM.API.Services.Core;
using SCRM.API.Services.Data;
using SCRM.API.Services.Netty.Handlers.Abstractions;
using SCRM.API.Services.Netty.Parsing;
using SCRM.Services.Data;
using SCRM.Services.Events;
using SCRM.SHARED.Models.Dtos;
using SCRM.SHARED.Models.Events;
using SCRM.SHARED.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SCRM.API.Services.Netty.Handlers
{
    /// <summary>
    /// 任务结果消息处理器
    /// <para>负责接收并处理客户端对服务端下发任务的执行结果。</para>
    /// <para>核心功能：</para>
    /// <list type="bullet">
    /// <item>处理通用任务结果 (TaskResultNotice - 1200)</item>
    /// <item>处理截图任务结果 (ScreenShotTaskResultNotice - 1283)</item>
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
        private readonly IHubContext<ClientHub> _hubContext;

        public TaskMessageHandler(
            ILogger<TaskMessageHandler> logger,
            ClientTaskService clientTaskService,
            ConnectionManager connectionManager,
            IEventBus eventBus,
            ApplicationDbContext db,
            IHubContext<ClientHub> hubContext) : base(logger)
        {
            _logger = logger;
            _clientTaskService = clientTaskService;
            _connectionManager = connectionManager;
            _eventBus = eventBus;
            _db = db;
            _hubContext = hubContext;
        }

        public async Task HandleTaskResult(TransportMessage message, IChannelHandlerContext context)
        {
            // 1. 发送 ACK
            await SendAckAsync(message, context);

            long taskIdRequest = 0;
            bool success = false;
            string resultMessage = string.Empty;
            object? resultData = null;
            TaskResultNoticeMessage? genericTaskResult = null;
            string contactLabelsUpdatedAccountId = string.Empty;

            try
            {
                switch (message.MsgType)
                {
                    case EnumMsgType.ScreenShotTaskResultNotice:
                    {
                        var ssMsg = message.Content.Unpack<ScreenShotTaskResultNoticeMessage>();
                        var screenshotUrl = ssMsg.Url ?? string.Empty;
                        success = ssMsg.Success && !string.IsNullOrWhiteSpace(screenshotUrl);
                        taskIdRequest = ssMsg.TaskId;
                        var connId = context.Channel.Id.AsLongText();
                        var connInfo = await _connectionManager.GetConnectionAsync(connId);
                        var deviceUuid = connInfo?.deviceUuid ?? string.Empty;
                        resultMessage = success
                            ? $"截图成功：{screenshotUrl}"
                            : NormalizeResultMessage(ssMsg.ErrMsg, string.IsNullOrWhiteSpace(screenshotUrl) ? "截图失败：未返回图片地址" : "截图失败", success);
                        resultData = success ? screenshotUrl : null;

                        _logger.LogInformation(
                            "收到截图结果: TaskId={TaskId}, Success={Success}, DeviceUuid={DeviceUuid}, Url={Url}, ErrMsg={ErrMsg}",
                            taskIdRequest,
                            success,
                            deviceUuid,
                            screenshotUrl,
                            ssMsg.ErrMsg);

                        if (success)
                        {
                            await _eventBus.PublishAsync(new ScreenShotUploadedEvent(screenshotUrl, deviceUuid));
                        }
                        break;
                    }
                    case EnumMsgType.TalkToFriendTaskResultNotice:
                    {
                        var talkMsg = message.Content.Unpack<TalkToFriendTaskResultNoticeMessage>();
                        success = talkMsg.Success;
                        taskIdRequest = talkMsg.MsgId;
                        resultMessage = NormalizeResultMessage(talkMsg.ErrMsg, talkMsg.Code.ToString(), success);
                        if (success && string.IsNullOrWhiteSpace(resultMessage))
                        {
                            resultMessage = "聊天消息任务已完成";
                        }

                        var connId = context.Channel.Id.AsLongText();
                        var connInfo = await _connectionManager.GetConnectionAsync(connId);
                        var hasTalkContext = _clientTaskService.TryTakeTalkToFriendTaskContext(taskIdRequest, out var talkContext)
                            && talkContext != null;
                        var effectiveWeChatId = FirstNonBlank(talkMsg.WeChatId, talkContext?.WeChatId, connInfo?.wechatId);
                        var effectiveFriendId = FirstNonBlank(
                            talkContext?.FriendId,
                            IsReliableTalkResultFriendId(talkMsg.FriendId) ? talkMsg.FriendId : string.Empty);

                        resultData = new
                        {
                            talkMsg.WeChatId,
                            RawFriendId = talkMsg.FriendId,
                            EffectiveWeChatId = effectiveWeChatId,
                            EffectiveFriendId = effectiveFriendId,
                            MsgId = talkMsg.MsgId,
                            MsgSvrId = talkMsg.MsgSvrId,
                            talkMsg.CreateTime,
                            Code = talkMsg.Code.ToString(),
                            talkMsg.ErrMsg,
                            HasPendingContext = hasTalkContext,
                            PendingContentType = talkContext?.ContentType.ToString() ?? string.Empty
                        };

                        if (hasTalkContext && talkContext != null)
                        {
                            await ApplyTalkToFriendTaskResultAsync(
                                talkContext,
                                talkMsg,
                                effectiveWeChatId,
                                effectiveFriendId,
                                context);
                        }
                        else
                        {
                            _logger.LogInformation(
                                "收到聊天发送 1028 但没有 pending context，仅完成任务不补插消息: TaskId={TaskId}, WeChatId={WeChatId}, FriendId={FriendId}, Success={Success}, MsgSvrId={MsgSvrId}, CreateTime={CreateTime}",
                                taskIdRequest,
                                talkMsg.WeChatId,
                                talkMsg.FriendId,
                                success,
                                talkMsg.MsgSvrId,
                                talkMsg.CreateTime);
                        }

                        if (_clientTaskService.TryTakeFireAndForgetTaskContext(
                            taskIdRequest,
                            out var batchTaskId,
                            out var taskScene,
                            out var targetId,
                            out var failureSummary,
                            out var successSummary))
                        {
                            _logger.LogInformation(
                                "收到无等待聊天任务结果: TaskId={TaskId}, BatchTaskId={BatchTaskId}, TaskScene={TaskScene}, TargetId={TargetId}, Success={Success}, Code={Code}, ErrMsg={ErrMsg}",
                                taskIdRequest,
                                batchTaskId,
                                taskScene,
                                targetId,
                                success,
                                talkMsg.Code,
                                talkMsg.ErrMsg);

                            taskIdRequest = batchTaskId > 0 ? batchTaskId : taskIdRequest;

                            if (success)
                            {
                                var prefix = string.IsNullOrWhiteSpace(successSummary)
                                    ? "无等待聊天任务执行成功"
                                    : successSummary.Trim();
                                resultMessage = string.IsNullOrWhiteSpace(targetId)
                                    ? prefix
                                    : $"{prefix}：{targetId}";
                            }
                            else
                            {
                                var detail = string.IsNullOrWhiteSpace(talkMsg.ErrMsg)
                                    ? $"Code={talkMsg.Code}"
                                    : talkMsg.ErrMsg.Trim();
                                var prefix = string.IsNullOrWhiteSpace(failureSummary)
                                    ? "无等待聊天任务执行失败"
                                    : failureSummary.Trim();
                                resultMessage = string.IsNullOrWhiteSpace(targetId)
                                    ? $"{prefix}：{detail}"
                                    : $"{prefix}：{targetId}；{detail}";
                            }
                        }
                        break;
                    }
                    case EnumMsgType.TaskResultNotice:
                    {
                        var taskMsg = message.Content.Unpack<TaskResultNoticeMessage>();
                        genericTaskResult = taskMsg;
                        success = taskMsg.Success;
                        taskIdRequest = taskMsg.TaskId;
                        resultMessage = NormalizeGenericTaskResultMessage(taskMsg);
                        break;
                    }
                    case (EnumMsgType)1073: // PostSNSNewsTaskResultNotice
                    case EnumMsgType.SphPostTaskResultNotice:
                    {
                        var postMsg = message.Content.Unpack<PostSNSNewsTaskResultNoticeMessage>();
                        success = postMsg.Success;
                        taskIdRequest = postMsg.TaskId;
                        if (message.MsgType == EnumMsgType.SphPostTaskResultNotice)
                        {
                            resultMessage = NormalizeFinderPostMessage(postMsg);
                        }
                        else
                        {
                            var wasPending = taskIdRequest > 0 && _clientTaskService.IsTaskPending(taskIdRequest);
                            _clientTaskService.TryGetMomentPostTaskContext(taskIdRequest, out var momentPostContext);
                            var momentPostResult = BuildMomentPostResultDto(postMsg, momentPostContext, isLate: !wasPending);
                            success = momentPostResult.success;
                            resultData = momentPostResult;
                            resultMessage = NormalizePostMomentMessage(postMsg, momentPostResult);
                        }
                        break;
                    }
                    case EnumMsgType.CircleCommentDeleteTaskResultNotice:
                    {
                        var deleteMsg = message.Content.Unpack<CircleCommentDeleteTaskResultNoticeMessage>();
                        success = deleteMsg.Success;
                        taskIdRequest = deleteMsg.TaskId;
                        resultMessage = success
                            ? $"CircleId={deleteMsg.CircleId}; CommentId={deleteMsg.CommentId}"
                            : NormalizeResultMessage(deleteMsg.ErrMsg, deleteMsg.Code.ToString(), success);
                        break;
                    }
                    case EnumMsgType.CircleCommentReplyTaskResultNotice:
                    {
                        var replyMsg = message.Content.Unpack<CircleCommentReplyTaskResultNoticeMessage>();
                        var normalized = NormalizeResultMessage(replyMsg.ErrMsg, replyMsg.Code.ToString(), replyMsg.Success);
                        var callbackTimeout = IsCircleCommentCallbackTimeout(normalized);
                        success = replyMsg.Success || callbackTimeout;
                        taskIdRequest = replyMsg.TaskId;
                        resultMessage = success
                            ? (callbackTimeout
                                ? $"朋友圈评论已提交但客户端未捕获完成回调：CircleId={replyMsg.CircleId}; {normalized}"
                                : replyMsg.CommentId > 0
                                    ? $"朋友圈评论成功：CircleId={replyMsg.CircleId}; CommentId={replyMsg.CommentId}; ReplyCommentId={replyMsg.ReplyCommentId}"
                                    : $"朋友圈评论已提交待同步校准：CircleId={replyMsg.CircleId}; ReplyCommentId={replyMsg.ReplyCommentId}")
                            : $"朋友圈评论失败：{normalized}";
                        break;
                    }
                    case EnumMsgType.PullChatRoomQrCodeTaskResultNotice:
                    {
                        var qrMsg = message.Content.Unpack<PullChatRoomQrCodeTaskResultNoticeMessage>();
                        success = qrMsg.Success;
                        taskIdRequest = qrMsg.TaskId;
                        resultMessage = success
                            ? $"ChatRoomId={qrMsg.ChatRoomId}; QrCodeUrl={qrMsg.QrCodeUrl}"
                            : NormalizeResultMessage(qrMsg.ErrMsg, "PullChatRoomQrCodeFailed", success);
                        resultData = new
                        {
                            qrMsg.WeChatId,
                            qrMsg.ChatRoomId,
                            qrMsg.QrCodeUrl
                        };
                        break;
                    }
                    case EnumMsgType.PullWeChatQrCodeTaskResultNotice:
                    {
                        var qrMsg = message.Content.Unpack<PullWeChatQrCodeTaskResultNoticeMessage>();
                        success = qrMsg.Success;
                        resultMessage = success
                            ? $"个人二维码拉取成功：QrCodeUrl={qrMsg.QrCodeUrl}"
                            : NormalizeResultMessage(qrMsg.ErrMsg, "个人二维码拉取失败", success);
                        resultData = new
                        {
                            qrMsg.WeChatId,
                            qrMsg.QrCodeUrl
                        };
                        break;
                    }
                    case EnumMsgType.GetPoiListTaskResultNotice:
                    {
                        var poiMsg = message.Content.Unpack<GetPoiListTaskResultNoticeMessage>();
                        taskIdRequest = poiMsg.TaskId;
                        success = true;
                        resultMessage = $"POI列表拉取成功：Keyword={poiMsg.Keyword}; Count={poiMsg.PoiList.Count}";
                        resultData = new
                        {
                            poiMsg.WeChatId,
                            poiMsg.Keyword,
                            poiList = poiMsg.PoiList.ToArray()
                        };
                        break;
                    }
                    case EnumMsgType.PullEmojiInfoTaskResultNotice:
                    {
                        var emojiMsg = message.Content.Unpack<PullEmojiInfoTaskResultNoticeMessage>();
                        taskIdRequest = emojiMsg.TaskId;
                        success = true;
                        resultMessage = $"表情信息拉取成功：Count={emojiMsg.Emojis.Count}";
                        resultData = new
                        {
                            emojiMsg.WeChatId,
                            emojis = emojiMsg.Emojis.Select(emoji => new
                            {
                                emoji.Md5,
                                emoji.CdnUrl,
                                emoji.Catalog,
                                emoji.Type,
                                emoji.State,
                                emoji.Width,
                                emoji.Height,
                                emoji.Size,
                                emoji.Encrypturl,
                                emoji.Aeskey,
                                emoji.ExternUrl,
                                emoji.ExternMd5,
                                emoji.Name,
                                emoji.GroupId,
                                emoji.ThumbUrl,
                                emoji.Desc
                            }).ToArray()
                        };

                        if (_clientTaskService.TryTakePullEmojiInfoTaskContext(taskIdRequest, out var emojiContext)
                            && emojiContext != null)
                        {
                            await TryStartEmojiCdnDownloadAsync(emojiContext, emojiMsg);
                        }

                        break;
                    }
                    case EnumMsgType.TakeMoneyTaskResultNotice:
                    {
                        var moneyMsg = message.Content.Unpack<TakeMoneyTaskResultNoticeMessage>();
                        taskIdRequest = moneyMsg.TaskId;
                        success = moneyMsg.Success;
                        resultMessage = success
                            ? $"收钱结果成功：Amount={moneyMsg.Amount}; Sender={ResolveContactDisplayName(null, moneyMsg.SenderName, moneyMsg.Sender)}; MsgKey={moneyMsg.MsgKey}"
                            : NormalizeResultMessage(moneyMsg.ErrMsg, moneyMsg.Code.ToString(), success);
                        resultData = new
                        {
                            moneyMsg.WeChatId,
                            moneyMsg.MsgKey,
                            moneyMsg.Amount,
                            moneyMsg.Sender,
                            moneyMsg.SenderName,
                            moneyMsg.Type,
                            Code = moneyMsg.Code.ToString()
                        };
                        break;
                    }
                    case EnumMsgType.FindContactTaskResult:
                    {
                        var findMsg = message.Content.Unpack<FindContactTaskResultNoticeMessage>();
                        success = findMsg.Success;
                        resultMessage = success
                            ? $"搜索联系人成功：{ResolveContactDisplayName(null, findMsg.NickName, findMsg.UserName)}; Alias={findMsg.Alias}; IsFriend={findMsg.IsFriend}"
                            : NormalizeResultMessage(findMsg.ErrMsg, $"未找到联系人：{findMsg.SearchText}", success);
                        resultData = new
                        {
                            findMsg.WeChatId,
                            findMsg.SearchText,
                            findMsg.IsFriend,
                            findMsg.UserName,
                            findMsg.Alias,
                            findMsg.NickName,
                            Gender = findMsg.Gender.ToString(),
                            findMsg.Country,
                            findMsg.Province,
                            findMsg.City,
                            findMsg.Avatar
                        };
                        break;
                    }
                    case EnumMsgType.WeChatLocationTaskResultNotice:
                    {
                        var locationMsg = message.Content.Unpack<WeChatLocationTaskResultNoticeMessage>();
                        success = locationMsg.Success;
                        resultMessage = success
                            ? $"微信位置：Lat={locationMsg.Lat}; Lng={locationMsg.Lng}; Address={locationMsg.Address}"
                            : "微信位置获取失败";
                        resultData = new
                        {
                            locationMsg.WeChatId,
                            locationMsg.IMEI,
                            locationMsg.Lat,
                            locationMsg.Lng,
                            locationMsg.Address
                        };
                        break;
                    }
                    case EnumMsgType.WalletBalanceTaskResultNotice:
                    {
                        var walletMsg = message.Content.Unpack<WalletBalanceTaskResultNoticeMessage>();
                        success = true;
                        resultMessage = $"钱包余额：Balance={walletMsg.Balance}; TrueName={walletMsg.TrueName}; BankCards={walletMsg.BankCard.Count}; RealNameInfo={walletMsg.RealNameInfo}";
                        resultData = new
                        {
                            walletMsg.WeChatId,
                            walletMsg.Balance,
                            walletMsg.TrueName,
                            walletMsg.RealNameInfo,
                            bankCards = walletMsg.BankCard.Select(card => new
                            {
                                card.CardType,
                                card.BankName,
                                card.CardTail,
                                card.Desc,
                                card.Mobile
                            }).ToArray()
                        };
                        break;
                    }
                    case EnumMsgType.PhoneStateTaskResultNotice:
                    {
                        var phoneMsg = message.Content.Unpack<PhoneStateTaskResultNoticeMessage>();
                        success = true;
                        resultMessage = $"手机状态：Battery={phoneMsg.BatteryLevel}%; Charging={phoneMsg.ChargingState}; Net={phoneMsg.NetType}; Free={phoneMsg.SdcardFree}/{phoneMsg.SdcardTotal}";
                        resultData = new
                        {
                            phoneMsg.WeChatId,
                            Imei = phoneMsg.Imei,
                            phoneMsg.BatteryLevel,
                            phoneMsg.ChargingState,
                            phoneMsg.NetType,
                            phoneMsg.SdcardFree,
                            phoneMsg.SdcardTotal
                        };
                        break;
                    }
                    case EnumMsgType.ContactLabelInfoNotice:
                    {
                        var labelMsg = message.Content.Unpack<ContactLabelInfoNoticeMessage>();
                        var savedCount = await _db.ReplaceContactLabels(
                            labelMsg.WeChatId,
                            labelMsg.Labels.Select(label => (label.LabelId, label.LabelName, label.CreateTime)));
                        success = true;
                        taskIdRequest = 0;
                        resultMessage = $"联系人标签列表同步成功：Count={savedCount}";
                        resultData = new
                        {
                            labelMsg.WeChatId,
                            Count = savedCount
                        };
                        contactLabelsUpdatedAccountId = labelMsg.WeChatId;
                        break;
                    }
                    case EnumMsgType.ContactLabelAddNotice:
                    {
                        var labelMsg = message.Content.Unpack<ContactLabelAddNoticeMessage>();
                        var label = labelMsg.Label;
                        var tag = label == null
                            ? null
                            : await _db.UpsertContactLabel(labelMsg.WeChatId, label.LabelId, label.LabelName, label.CreateTime);
                        success = tag != null;
                        taskIdRequest = 0;
                        resultMessage = success
                            ? $"联系人标签已新增/更新：{tag!.tagName}({tag.labelId})"
                            : "联系人标签新增通知参数无效";
                        resultData = success
                            ? new { labelMsg.WeChatId, LabelId = tag!.labelId, LabelName = tag.tagName }
                            : null;
                        contactLabelsUpdatedAccountId = labelMsg.WeChatId;
                        break;
                    }
                    case EnumMsgType.ContactLabelDelNotice:
                    {
                        var labelMsg = message.Content.Unpack<ContactLabelDelNoticeMessage>();
                        var deleted = await _db.MarkContactLabelDeleted(labelMsg.WeChatId, labelMsg.LabelId);
                        success = true;
                        taskIdRequest = 0;
                        resultMessage = deleted
                            ? $"联系人标签已删除：LabelId={labelMsg.LabelId}"
                            : $"联系人标签删除通知已收到，本地未找到标签：LabelId={labelMsg.LabelId}";
                        resultData = new
                        {
                            labelMsg.WeChatId,
                            labelMsg.LabelId,
                            deleted
                        };
                        contactLabelsUpdatedAccountId = labelMsg.WeChatId;
                        break;
                    }
                    case EnumMsgType.OneKeyLikeTaskResultNotice:
                    {
                        var oneKeyLikeMsg = message.Content.Unpack<OneKeyLikeTaskResultNoticeMessage>();
                        success = true;
                        taskIdRequest = oneKeyLikeMsg.TaskId;
                        resultMessage = $"朋友圈一键点赞完成：Count={oneKeyLikeMsg.Count}; EndType={oneKeyLikeMsg.EndType}";
                        break;
                    }
                    case EnumMsgType.QueryHbDetailTaskResultNotice:
                    {
                        var hbMsg = message.Content.Unpack<QueryHbDetailTaskResultNoticeMessage>();
                        success = hbMsg.Success;
                        resultMessage = success
                            ? $"红包详情：Total={hbMsg.TotalAmount}/{hbMsg.TotalNum}; Received={hbMsg.RecAmount}/{hbMsg.RecNum}; Sender={hbMsg.Sender}; Status={hbMsg.HbStatus}"
                            : NormalizeResultMessage(hbMsg.ErrMsg, "红包详情查询失败", success);
                        resultData = new
                        {
                            hbMsg.WeChatId,
                            hbMsg.HbUrl,
                            hbMsg.TotalNum,
                            hbMsg.TotalAmount,
                            hbMsg.RecNum,
                            hbMsg.RecAmount,
                            hbMsg.Sender,
                            hbMsg.Wishing,
                            hbMsg.HbType,
                            hbMsg.HbKind,
                            hbMsg.HbStatus,
                            hbMsg.RevStatus,
                            records = hbMsg.Records.Select(record => new
                            {
                                record.UserName,
                                record.Amount,
                                record.Time
                            }).ToArray()
                        };
                        break;
                    }
                    case EnumMsgType.QueryHbStatusTaskResultNotice:
                    {
                        var hbMsg = message.Content.Unpack<QueryHbStatusTaskResultNoticeMessage>();
                        success = hbMsg.Success;
                        resultMessage = success
                            ? $"红包状态：HbType={hbMsg.HbType}; HbStatus={hbMsg.HbStatus}; RevStatus={hbMsg.RevStatus}; {hbMsg.StatusMsg}"
                            : NormalizeResultMessage(hbMsg.ErrMsg, "红包状态查询失败", success);
                        resultData = new
                        {
                            hbMsg.WeChatId,
                            hbMsg.HbUrl,
                            hbMsg.HbType,
                            hbMsg.HbStatus,
                            hbMsg.RevStatus,
                            hbMsg.StatusMsg
                        };
                        break;
                    }
                    default:
                        _logger.LogInformation("Unhandled task result message: {MsgType}", message.MsgType);
                        break;
                }

                if (taskIdRequest > 0)
                {
                    _clientTaskService.CompleteTask(taskIdRequest, success, resultMessage, resultData);
                }

                if (taskIdRequest > 0
                    && genericTaskResult?.TaskType == EnumMsgType.VoiceTransTextTask)
                {
                    if (_clientTaskService.TryTakeVoiceTransTextTaskContext(taskIdRequest, out var voiceContext)
                        && voiceContext != null)
                    {
                        await ApplyVoiceTransTextResultAsync(voiceContext, genericTaskResult, context);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "收到语音转文字结果但未找到任务上下文，跳过落库: TaskId={TaskId}, Success={Success}, Message={Message}",
                            taskIdRequest,
                            success,
                            genericTaskResult.ErrMsg);
                    }
                }

                if (taskIdRequest > 0
                    && genericTaskResult?.TaskType == EnumMsgType.RevokeMessageTask)
                {
                    if (_clientTaskService.TryTakeRevokeMessageTaskContext(taskIdRequest, out var revokeContext)
                        && revokeContext != null)
                    {
                        await ApplyRevokeMessageResultAsync(revokeContext, genericTaskResult, context);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "收到消息撤回结果但未找到任务上下文，跳过本地撤回标记: TaskId={TaskId}, Success={Success}, Message={Message}",
                            taskIdRequest,
                            success,
                            genericTaskResult.ErrMsg);
                    }
                }

                if (taskIdRequest > 0
                    && genericTaskResult != null
                    && IsForwardTaskType(genericTaskResult.TaskType))
                {
                    if (_clientTaskService.TryTakeForwardTaskContext(taskIdRequest, out var forwardContext)
                        && forwardContext != null)
                    {
                        await ApplyForwardTaskResultAsync(forwardContext, genericTaskResult, context);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "收到消息转发结果但未找到任务上下文，跳过会话刷新补偿: TaskId={TaskId}, TaskType={TaskType}, Success={Success}, Message={Message}",
                            taskIdRequest,
                            genericTaskResult.TaskType,
                            success,
                            genericTaskResult.ErrMsg);
                    }
                }

                if (taskIdRequest > 0
                    && genericTaskResult?.TaskType == EnumMsgType.ContactSetLabelTask)
                {
                    if (_clientTaskService.TryTakeContactSetLabelTaskContext(taskIdRequest, out var labelContext)
                        && labelContext != null)
                    {
                        await ApplyContactSetLabelResultAsync(labelContext, genericTaskResult);
                        if (genericTaskResult.Success)
                        {
                            contactLabelsUpdatedAccountId = labelContext.WeChatId;
                        }
                    }
                    else
                    {
                        _logger.LogWarning(
                            "收到联系人设置标签结果但未找到任务上下文，跳过本地联系人标签更新: TaskId={TaskId}, Success={Success}, Message={Message}",
                            taskIdRequest,
                            success,
                            genericTaskResult.ErrMsg);
                    }
                }

                if (taskIdRequest > 0
                    && genericTaskResult?.TaskType == EnumMsgType.ContactLabelTask)
                {
                    if (_clientTaskService.TryTakeContactLabelTaskContext(taskIdRequest, out var labelContext)
                        && labelContext != null)
                    {
                        await ApplyContactLabelTaskResultAsync(labelContext, genericTaskResult);
                        if (genericTaskResult.Success)
                        {
                            contactLabelsUpdatedAccountId = labelContext.WeChatId;
                        }
                    }
                }

                if (taskIdRequest > 0
                    && genericTaskResult?.TaskType == EnumMsgType.ContactLabelDeleteTask)
                {
                    if (_clientTaskService.TryTakeContactLabelDeleteTaskContext(taskIdRequest, out var labelDeleteContext)
                        && labelDeleteContext != null)
                    {
                        await ApplyContactLabelDeleteResultAsync(labelDeleteContext, genericTaskResult);
                        if (genericTaskResult.Success)
                        {
                            contactLabelsUpdatedAccountId = labelDeleteContext.WeChatId;
                        }
                    }
                }

                if (taskIdRequest > 0
                    && genericTaskResult?.TaskType == EnumMsgType.ChatRoomInviteApproveTask)
                {
                    if (_clientTaskService.TryTakeChatRoomInviteApproveTaskContext(taskIdRequest, out var inviteContext)
                        && inviteContext != null)
                    {
                        var connId = context.Channel.Id.AsLongText();
                        var connInfo = await _connectionManager.GetConnectionAsync(connId);
                        await ApplyChatRoomInviteApproveResultAsync(inviteContext, genericTaskResult, connInfo?.deviceUuid ?? string.Empty, connInfo?.userId ?? string.Empty);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "收到群邀请审批结果但未找到任务上下文，跳过本地审批状态更新: TaskId={TaskId}, Success={Success}, Message={Message}",
                            taskIdRequest,
                            success,
                            genericTaskResult.ErrMsg);
                    }
                }

                ClientTaskService.MomentInteractionTaskContext? momentInteractionContext = null;
                if (taskIdRequest > 0
                    && _clientTaskService.TryTakeMomentInteractionTaskContext(taskIdRequest, out momentInteractionContext)
                    && success
                    && momentInteractionContext != null)
                {
                    var connId = context.Channel.Id.AsLongText();
                    var connInfo = await _connectionManager.GetConnectionAsync(connId);
                    await ApplyMomentInteractionResultAsync(momentInteractionContext, connInfo?.deviceUuid ?? string.Empty);
                }

                if (!string.IsNullOrWhiteSpace(contactLabelsUpdatedAccountId))
                {
                    var connId = context.Channel.Id.AsLongText();
                    var connInfo = await _connectionManager.GetConnectionAsync(connId);
                    var deviceUuid = connInfo?.deviceUuid ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(deviceUuid))
                    {
                        await _eventBus.PublishAsync(new ContactLabelsUpdatedEvent(
                            deviceUuid,
                            contactLabelsUpdatedAccountId,
                            connInfo?.userId ?? string.Empty));
                    }
                }

                if (taskIdRequest > 0 || success || ShouldPublishNoTaskIdResult(message.MsgType))
                {
                    var connId = context.Channel.Id.AsLongText();
                    var connInfo = await _connectionManager.GetConnectionAsync(connId);
                    var deviceUuid = connInfo?.deviceUuid ?? string.Empty;
                    var eventMessage = AppendResultDataJson(resultMessage, resultData);
                    await _eventBus.PublishAsync(new TaskResultReceivedEvent(taskIdRequest, success, eventMessage, string.Empty, deviceUuid)
                    {
                        data = resultData
                    });
                }

                _logger.LogInformation("Task Result Handled: Type={MsgType}, Id={TaskId}, Success={Success}, Message={Message}", message.MsgType, taskIdRequest, success, resultMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling TaskResult message: {MsgType}", message.MsgType);
            }
        }

        /// <summary>
        /// 保存语音转文字通用回执。
        /// <para>
        /// VoiceTransTextTask 成功时 TaskResultNotice.ErrMsg 是识别文本；
        /// 失败时 ErrMsg 是错误信息。这里只写 VoiceToTextLogs，不覆盖原始语音消息。
        /// </para>
        /// </summary>
        private async Task ApplyVoiceTransTextResultAsync(
            ClientTaskService.VoiceTransTextTaskContext context,
            TaskResultNoticeMessage result,
            IChannelHandlerContext channelContext)
        {
            if (context == null || result == null || context.MsgSvrId == 0 || string.IsNullOrWhiteSpace(context.WeChatId))
            {
                return;
            }

            var textOrError = result.ErrMsg ?? string.Empty;
            var log = await _db.SaveVoiceToTextResult(
                context.WeChatId,
                context.FriendId,
                context.MsgSvrId,
                result.Success,
                result.Success ? textOrError : string.Empty,
                result.Success ? string.Empty : textOrError,
                context.TaskId);

            if (log == null)
            {
                _logger.LogWarning(
                    "语音转文字结果未落库，未找到原始消息: WeChatId={WeChatId}, FriendId={FriendId}, MsgSvrId={MsgSvrId}, TaskId={TaskId}, Success={Success}",
                    context.WeChatId,
                    context.FriendId,
                    context.MsgSvrId,
                    context.TaskId,
                    result.Success);
                await PublishTaskResultNotificationAsync(
                    channelContext,
                    false,
                    $"语音转文字结果未落库：WeChatId={context.WeChatId}，FriendId={context.FriendId}，MsgSvrId={context.MsgSvrId}，请先同步或补偿该聊天消息后重试。");
                return;
            }

            _logger.LogInformation(
                "语音转文字结果已落库: WeChatId={WeChatId}, FriendId={FriendId}, MsgSvrId={MsgSvrId}, TaskId={TaskId}, Success={Success}, LogId={LogId}",
                context.WeChatId,
                context.FriendId,
                context.MsgSvrId,
                context.TaskId,
                result.Success,
                log.id);

            await PublishVoiceTransTextMessageUpdatedAsync(channelContext, log.messageId);
        }

        /// <summary>
        /// 语音转文字落库后推送原消息更新。
        /// <para>前端收到同一 messageId/msgSvrId 的消息后会替换旧气泡，从而立即显示 VoiceToTextLogs 结果。</para>
        /// </summary>
        private async Task PublishVoiceTransTextMessageUpdatedAsync(IChannelHandlerContext channelContext, int messageTableId)
        {
            if (messageTableId <= 0)
            {
                return;
            }

            var message = await _db.Messages
                .FirstOrDefaultAsync(item => item.messageId == messageTableId && !item.isDeleted);
            if (message == null)
            {
                return;
            }

            await _db.EnrichMessagesWithMediaMetadataAsync(new List<Message> { message });

            var connId = channelContext.Channel.Id.AsLongText();
            var connInfo = await _connectionManager.GetConnectionAsync(connId);
            if (connInfo == null)
            {
                return;
            }

            await _eventBus.PublishAsync(new MessageReceivedEvent(connInfo.deviceUuid, message, connInfo.userId));
        }

        /// <summary>
        /// 根据聊天发送 1028 回包回填或补插发送消息。
        /// <para>
        /// 1028 自身不带正文和内容类型，所以只有拿到 pending context 时才允许落库；
        /// FriendId 优先使用 pending context，避免安卓端偶发把 Tsk26/Tsk144 写入 FriendId。
        /// </para>
        /// </summary>
        private async Task ApplyTalkToFriendTaskResultAsync(
            ClientTaskService.TalkToFriendTaskContext talkContext,
            TalkToFriendTaskResultNoticeMessage result,
            string effectiveWeChatId,
            string effectiveFriendId,
            IChannelHandlerContext channelContext)
        {
            if (talkContext == null || result == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(effectiveWeChatId) || string.IsNullOrWhiteSpace(effectiveFriendId))
            {
                _logger.LogWarning(
                    "聊天发送 1028 已收到但缺少有效账号或会话，跳过消息回填: TaskId={TaskId}, WeChatId={WeChatId}, FriendId={FriendId}, RawFriendId={RawFriendId}, Success={Success}",
                    talkContext.TaskId,
                    effectiveWeChatId,
                    effectiveFriendId,
                    result.FriendId,
                    result.Success);
                return;
            }

            var savedMessage = await _db.SaveTalkToFriendResultMessageAsync(
                effectiveWeChatId,
                effectiveFriendId,
                talkContext.Content,
                (short)talkContext.ContentType,
                talkContext.TaskId > 0 ? talkContext.TaskId : result.MsgId,
                result.MsgSvrId,
                result.CreateTime,
                result.Success,
                result.ErrMsg);

            if (savedMessage == null)
            {
                _logger.LogInformation(
                    "聊天发送 1028 未产生消息变更: TaskId={TaskId}, WeChatId={WeChatId}, FriendId={FriendId}, Success={Success}, MsgSvrId={MsgSvrId}, ErrMsg={ErrMsg}",
                    talkContext.TaskId,
                    effectiveWeChatId,
                    effectiveFriendId,
                    result.Success,
                    result.MsgSvrId,
                    result.ErrMsg);
                return;
            }

            await SaveTalkToFriendAdvancedContentMetadataAsync(savedMessage, talkContext, result);

            try
            {
                await _db.EnrichMessagesWithMediaMetadataAsync(new List<Message> { savedMessage });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "聊天发送 1028 消息已落库，但补齐媒体展示元数据失败: MessageId={MessageId}, MsgSvrId={MsgSvrId}",
                    savedMessage.messageId,
                    savedMessage.msgSvrId);
            }

            var connId = channelContext.Channel.Id.AsLongText();
            var connInfo = await _connectionManager.GetConnectionAsync(connId);
            if (connInfo == null)
            {
                return;
            }

            await _eventBus.PublishAsync(new MessageReceivedEvent(connInfo.deviceUuid, savedMessage, connInfo.userId));

            _logger.LogInformation(
                "聊天发送 1028 已回填/补插消息并推送 UI: TaskId={TaskId}, WeChatId={WeChatId}, FriendId={FriendId}, MessageId={MessageId}, MsgSvrId={MsgSvrId}, Success={Success}",
                talkContext.TaskId,
                effectiveWeChatId,
                effectiveFriendId,
                savedMessage.messageId,
                savedMessage.msgSvrId,
                result.Success);
        }

        /// <summary>
        /// 为聊天发送 1028 pending 回填消息补写高级内容结构化扩展。
        /// <para>
        /// 1028 自身没有正文；这里使用下发 TalkToFriendTask 时保存的 pending content/contentType，
        /// 解决 WeChatTalkToFriendNotice 延迟或缺失时，发送方向 Link/WeApp/文件/位置等消息只显示 raw JSON 的问题。
        /// </para>
        /// </summary>
        private async Task SaveTalkToFriendAdvancedContentMetadataAsync(
            Message savedMessage,
            ClientTaskService.TalkToFriendTaskContext talkContext,
            TalkToFriendTaskResultNoticeMessage result)
        {
            if (savedMessage == null || talkContext == null || result == null)
            {
                return;
            }

            try
            {
                var pendingContent = FirstNonBlank(talkContext.Content, savedMessage.content);
                var effectiveMessageType = talkContext.ContentType == EnumContentType.UnknownContent
                    ? savedMessage.messageType
                    : (short)talkContext.ContentType;

                var advanced = AdvancedMessageContentParser.Parse(
                    effectiveMessageType,
                    pendingContent,
                    savedMessage.contentXml,
                    null,
                    "TalkToFriendTaskResultNotice",
                    null,
                    savedMessage.msgSvrId ?? (result.MsgSvrId == 0 ? null : result.MsgSvrId),
                    talkContext.TaskId > 0
                        ? talkContext.TaskId
                        : result.MsgId > 0
                            ? result.MsgId
                            : TryParseNullableLong(savedMessage.localMessageId));

                var saved = await _db.SaveAdvancedMessageContentMetadata(savedMessage, advanced);
                if (saved)
                {
                    _logger.LogDebug(
                        "聊天发送 1028 高级消息结构化元数据已保存: TaskId={TaskId}, MessageId={MessageId}, MsgSvrId={MsgSvrId}, ContentType={ContentType}, SemanticKind={SemanticKind}",
                        talkContext.TaskId,
                        savedMessage.messageId,
                        savedMessage.msgSvrId,
                        talkContext.ContentType,
                        advanced?.SemanticKind);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "聊天发送 1028 高级消息结构化解析/落库失败，继续推送基础消息: TaskId={TaskId}, MessageId={MessageId}, MsgSvrId={MsgSvrId}, ContentType={ContentType}",
                    talkContext.TaskId,
                    savedMessage.messageId,
                    savedMessage.msgSvrId,
                    talkContext.ContentType);
            }
        }

        private async Task PublishTaskResultNotificationAsync(
            IChannelHandlerContext channelContext,
            bool success,
            string message)
        {
            var connId = channelContext.Channel.Id.AsLongText();
            var connInfo = await _connectionManager.GetConnectionAsync(connId);
            if (connInfo == null)
            {
                return;
            }

            await _eventBus.PublishAsync(new TaskResultReceivedEvent(0, success, message, string.Empty, connInfo.deviceUuid));
        }

        /// <summary>
        /// 根据消息撤回通用回执标记本地消息。
        /// <para>失败时只记录日志，不改变本地消息；成功时写 isRevoked 和 MessageRevocations。</para>
        /// </summary>
        private async Task ApplyRevokeMessageResultAsync(
            ClientTaskService.RevokeMessageTaskContext revokeContext,
            TaskResultNoticeMessage result,
            IChannelHandlerContext channelContext)
        {
            if (revokeContext == null || result == null || !result.Success)
            {
                return;
            }

            var message = await _db.MarkMessageRevoked(
                revokeContext.WeChatId,
                revokeContext.MsgSvrId,
                revokeContext.FriendId,
                revokeContext.WeChatId,
                "服务端下发撤回任务成功");

            if (message == null)
            {
                _logger.LogWarning(
                    "消息撤回成功但本地未找到消息: WeChatId={WeChatId}, FriendId={FriendId}, MsgSvrId={MsgSvrId}, TaskId={TaskId}",
                    revokeContext.WeChatId,
                    revokeContext.FriendId,
                    revokeContext.MsgSvrId,
                    revokeContext.TaskId);
                return;
            }

            _logger.LogInformation(
                "消息撤回结果已标记本地消息: WeChatId={WeChatId}, FriendId={FriendId}, MsgSvrId={MsgSvrId}, TaskId={TaskId}, MessageId={MessageId}",
                revokeContext.WeChatId,
                revokeContext.FriendId,
                revokeContext.MsgSvrId,
                revokeContext.TaskId,
                message.messageId);

            await PublishConversationsUpdatedAsync(channelContext, revokeContext.WeChatId);
        }

        /// <summary>
        /// 根据转发通用回执刷新会话列表。
        /// <para>
        /// 转发结果只代表微信侧动作完成，真正的新消息仍依赖后续消息 Hook；
        /// 这里用下发时保存的 ForwardContext 触发同账号会话刷新，减少目标会话不同步的空窗。
        /// </para>
        /// </summary>
        private async Task ApplyForwardTaskResultAsync(
            ClientTaskService.ForwardTaskContext forwardContext,
            TaskResultNoticeMessage result,
            IChannelHandlerContext channelContext)
        {
            if (forwardContext == null || result == null || !result.Success)
            {
                return;
            }

            _logger.LogInformation(
                "消息转发结果已确认，触发会话刷新: WeChatId={WeChatId}, TaskId={TaskId}, ForwardType={ForwardType}, SourceCount={SourceCount}, TargetCount={TargetCount}, Targets={Targets}",
                forwardContext.WeChatId,
                forwardContext.TaskId,
                forwardContext.ForwardType,
                forwardContext.SourceMsgSvrIds?.Count ?? 0,
                forwardContext.TargetFriendIds?.Count ?? 0,
                string.Join(",", forwardContext.TargetFriendIds ?? Array.Empty<string>()));

            await PublishConversationsUpdatedAsync(channelContext, forwardContext.WeChatId);
        }

        /// <summary>
        /// 判断是否为三类消息转发任务。
        /// </summary>
        private static bool IsForwardTaskType(EnumMsgType taskType)
        {
            return taskType == EnumMsgType.ForwardMessageTask
                || taskType == EnumMsgType.ForwardMultiMessageTask
                || taskType == EnumMsgType.ForwardMessageByContentTask;
        }

        /// <summary>
        /// 撤回任务落库后通知前端重新读取会话列表，确保摘要和红点状态及时刷新。
        /// </summary>
        private async Task PublishConversationsUpdatedAsync(IChannelHandlerContext context, string accountId)
        {
            if (context == null || string.IsNullOrWhiteSpace(accountId))
            {
                return;
            }

            var connId = context.Channel.Id.AsLongText();
            var connInfo = await _connectionManager.GetConnectionAsync(connId);
            if (connInfo == null)
            {
                return;
            }

            _logger.LogInformation(
                "Publish ConversationsUpdatedEvent after task result: Device={DeviceUuid}, Account={AccountId}, User={UserId}",
                connInfo.deviceUuid,
                accountId,
                connInfo.userId);
            await _eventBus.PublishAsync(new ConversationsUpdatedEvent(connInfo.deviceUuid, accountId, connInfo.userId));
        }

        /// <summary>
        /// 根据群邀请审批通用回执更新本地邀请状态。
        /// <para>成功时把对应 WeChatId + MsgId 的 GroupInvitation 标记为已处理，并通知 Web 端刷新审批列表。</para>
        /// </summary>
        private async Task ApplyChatRoomInviteApproveResultAsync(
            ClientTaskService.ChatRoomInviteApproveTaskContext context,
            TaskResultNoticeMessage result,
            string deviceUuid,
            string ownerId)
        {
            if (context == null || result == null || !result.Success)
            {
                return;
            }

            var marked = await _db.MarkGroupInvitationApproved(context.WeChatId, context.MsgId);
            _logger.LogInformation(
                "群邀请审批结果已处理: WeChatId={WeChatId}, RoomId={RoomId}, MsgId={MsgId}, TaskId={TaskId}, Marked={Marked}",
                context.WeChatId,
                context.RoomId,
                context.MsgId,
                context.TaskId,
                marked);

            if (marked && !string.IsNullOrWhiteSpace(deviceUuid))
            {
                await _eventBus.PublishAsync(new GroupInvitationsUpdatedEvent(deviceUuid, context.WeChatId, ownerId));
            }
        }

        /// <summary>
        /// 根据设置联系人标签回执更新本地联系人标签集合。
        /// <para>失败时不改本地数据；成功后先乐观更新 Contacts.labelIds，后续联系人同步再校准。</para>
        /// </summary>
        private async Task ApplyContactSetLabelResultAsync(
            ClientTaskService.ContactSetLabelTaskContext context,
            TaskResultNoticeMessage result)
        {
            if (context == null || result == null || !result.Success)
            {
                return;
            }

            var updated = await _db.UpdateContactLabelIds(
                context.WeChatId,
                context.FriendId,
                context.LabelIds);

            _logger.LogInformation(
                "联系人标签设置结果已处理: WeChatId={WeChatId}, FriendId={FriendId}, LabelIds={LabelIds}, TaskId={TaskId}, Updated={Updated}",
                context.WeChatId,
                context.FriendId,
                string.Join(",", context.LabelIds),
                context.TaskId,
                updated);
        }

        /// <summary>
        /// 根据创建/重命名标签回执乐观更新标签字典。
        /// <para>LabelId=0 表示新建标签但回包没有真实 ID，此时等待 ContactLabelAddNotice/ContactLabelInfoNotice 校准。</para>
        /// </summary>
        private async Task ApplyContactLabelTaskResultAsync(
            ClientTaskService.ContactLabelTaskContext context,
            TaskResultNoticeMessage result)
        {
            if (context == null || result == null || !result.Success || context.LabelId <= 0)
            {
                return;
            }

            var tag = await _db.UpsertContactLabel(
                context.WeChatId,
                context.LabelId,
                context.LabelName);

            _logger.LogInformation(
                "联系人标签任务结果已乐观更新标签字典: WeChatId={WeChatId}, LabelId={LabelId}, LabelName={LabelName}, TaskId={TaskId}, Updated={Updated}",
                context.WeChatId,
                context.LabelId,
                context.LabelName,
                context.TaskId,
                tag != null);
        }

        /// <summary>
        /// 根据删除标签回执软删除本地标签字典。
        /// </summary>
        private async Task ApplyContactLabelDeleteResultAsync(
            ClientTaskService.ContactLabelDeleteTaskContext context,
            TaskResultNoticeMessage result)
        {
            if (context == null || result == null || !result.Success || context.LabelId <= 0)
            {
                return;
            }

            var deleted = await _db.MarkContactLabelDeleted(context.WeChatId, context.LabelId);
            _logger.LogInformation(
                "联系人标签删除结果已处理: WeChatId={WeChatId}, LabelId={LabelId}, TaskId={TaskId}, Deleted={Deleted}",
                context.WeChatId,
                context.LabelId,
                context.TaskId,
                deleted);
        }

        private static string NormalizeFinderPostMessage(PostSNSNewsTaskResultNoticeMessage msg)
        {
            if (!msg.Success)
            {
                return NormalizeResultMessage(msg.ErrMsg, $"视频号发布失败：Code={msg.Code}", false);
            }

            if (msg.Extra != null && msg.Extra.CircleId > 0)
            {
                return $"视频号发布成功：FinderObjectId={msg.Extra.CircleId}";
            }

            return "视频号发布成功";
        }

        /// <summary>
        /// 根据朋友圈任务成功回执增量更新本地时间线。
        /// <para>
        /// 安卓端 62203 点赞通常只回 TaskResultNotice，评论回包也不一定立即带 CircleCommentNotice；
        /// 因此服务端利用下发任务时保存的上下文先落库并实时推送，后续 CircleDetail/CirclePush 再做校准。
        /// </para>
        /// </summary>
        private async Task ApplyMomentInteractionResultAsync(
            ClientTaskService.MomentInteractionTaskContext context,
            string deviceUuid)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.WeChatId) || context.CircleId == 0)
            {
                return;
            }

            MomentsTimeline? moment = null;
            var selfName = await ResolveWechatDisplayNameAsync(context.WeChatId, context.WeChatId);
            switch (context.Kind)
            {
                case ClientTaskService.MomentInteractionKind.Like:
                    moment = await _db.ApplyMomentLikeResult(
                        context.WeChatId,
                        context.CircleId,
                        context.WeChatId,
                        selfName,
                        context.IsCancel,
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    break;

                case ClientTaskService.MomentInteractionKind.CommentReply:
                    moment = await _db.ApplyMomentCommentResult(
                        context.WeChatId,
                        context.CircleId,
                        context.CommentId,
                        context.ReplyCommentId,
                        context.WeChatId,
                        selfName,
                        context.ToWeChatId,
                        await ResolveWechatDisplayNameAsync(context.WeChatId, context.ToWeChatId),
                        context.Content,
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    break;

                case ClientTaskService.MomentInteractionKind.CommentDelete:
                    moment = await _db.ApplyMomentCommentDeleteResult(
                        context.WeChatId,
                        context.CircleId,
                        context.CommentId);
                    break;
            }

            if (moment == null)
            {
                return;
            }

            await BackfillMomentDisplayNamesAsync(context.WeChatId, new[] { moment });
            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(deviceUuid))
            {
                var notice = new RealtimeDataChangedNoticeDto
                {
                    scope = "moments",
                    changeType = "timeline",
                    deviceUuid = deviceUuid,
                    weChatId = string.Empty,
                    taskId = context.TaskId,
                    success = true,
                    itemCount = 1,
                    summary = "朋友圈互动状态已更新",
                    receivedAt = DateTimeOffset.UtcNow
                };

                await _hubContext.Clients.Group(deviceUuid).SendAsync("MomentTimelineChanged", notice);
                await _eventBus.PublishAsync(new MomentTimelineChangedEvent(notice));
            }

            _logger.LogInformation(
                "朋友圈互动任务结果已增量写入时间线: Kind={Kind}, WeChatId={WeChatId}, CircleId={CircleId}, TaskId={TaskId}",
                context.Kind,
                context.WeChatId,
                context.CircleId,
                context.TaskId);
        }

        /// <summary>
        /// 为朋友圈增量更新回填昵称。
        /// <para>只读联系人表，不改变 DbHelper 负责的持久化口径。</para>
        /// </summary>
        private async Task BackfillMomentDisplayNamesAsync(string ownerWxid, IEnumerable<MomentsTimeline> moments)
        {
            var momentList = moments?.ToList() ?? new List<MomentsTimeline>();
            if (string.IsNullOrWhiteSpace(ownerWxid) || momentList.Count == 0)
            {
                return;
            }

            var displayNames = await BuildContactDisplayNameMapAsync(ownerWxid);
            foreach (var moment in momentList)
            {
                var comments = DeserializeMomentComments(moment.commentsJson);
                var likes = DeserializeMomentLikes(moment.likesJson);
                ApplyDisplayNames(moment, comments, likes, displayNames);
                moment.commentsJson = JsonSerializer.Serialize(comments);
                moment.likesJson = JsonSerializer.Serialize(likes);
            }
        }

        /// <summary>
        /// 构建朋友圈实时推送 DTO。
        /// </summary>
        private async Task<MomentsTimelineDto> BuildMomentDtoAsync(string ownerWxid, MomentsTimeline moment, string deviceUuid)
        {
            var displayNames = await BuildContactDisplayNameMapAsync(ownerWxid);
            var comments = DeserializeMomentComments(moment.commentsJson);
            var likes = DeserializeMomentLikes(moment.likesJson);
            ApplyDisplayNames(moment, comments, likes, displayNames);

            return new MomentsTimelineDto
            {
                deviceUuid = deviceUuid,
                snsId = moment.snsId,
                userName = moment.userName ?? string.Empty,
                nickName = ResolveDisplayName(displayNames, moment.userName, moment.nickName),
                content = MomentContentExtractor.FirstNonEmpty(
                    moment.content,
                    MomentContentExtractor.ExtractTextFromXml(moment.xmlContent)),
                xmlContent = moment.xmlContent ?? string.Empty,
                createTime = moment.createTime,
                stringTime = moment.createTime.ToString(),
                type = InferMomentType(moment),
                images = DeserializeImageList(moment.imagesJson),
                videoUrl = moment.videoUrl ?? string.Empty,
                link = DeserializeMomentLink(moment.linkInfoJson),
                comments = comments,
                likes = likes
            };
        }

        /// <summary>
        /// 按联系人和账号表构建展示名映射。
        /// </summary>
        private async Task<Dictionary<string, string>> BuildContactDisplayNameMapAsync(string ownerWxid)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(ownerWxid))
            {
                return map;
            }

            var contacts = await _db.Contacts
                .AsNoTracking()
                .Where(c => c.ownerWxid == ownerWxid && !c.isDeleted)
                .Select(c => new { c.wxid, c.remarks, c.nickname, c.friendNo })
                .ToListAsync();

            foreach (var contact in contacts)
            {
                if (!string.IsNullOrWhiteSpace(contact.wxid))
                {
                    map[contact.wxid] = ResolveContactDisplayName(contact.remarks, contact.nickname, contact.wxid);
                }
                if (!string.IsNullOrWhiteSpace(contact.friendNo))
                {
                    map.TryAdd(contact.friendNo, ResolveContactDisplayName(contact.remarks, contact.nickname, contact.wxid));
                }
            }

            var accounts = await _db.WechatAccounts
                .AsNoTracking()
                .Where(a => !a.isDeleted && !string.IsNullOrWhiteSpace(a.wxid))
                .Select(a => new { a.wxid, a.nickname })
                .ToListAsync();
            foreach (var account in accounts)
            {
                if (!string.IsNullOrWhiteSpace(account.nickname))
                {
                    map.TryAdd(account.wxid, account.nickname);
                }
            }

            var selfName = accounts.FirstOrDefault(a => string.Equals(a.wxid, ownerWxid, StringComparison.OrdinalIgnoreCase))?.nickname;
            map[ownerWxid] = string.IsNullOrWhiteSpace(selfName) ? ownerWxid : selfName!;
            return map;
        }

        /// <summary>
        /// 解析单个 wxid 的展示名。
        /// </summary>
        private async Task<string> ResolveWechatDisplayNameAsync(string ownerWxid, string? wxid)
        {
            if (string.IsNullOrWhiteSpace(wxid))
            {
                return string.Empty;
            }

            var map = await BuildContactDisplayNameMapAsync(ownerWxid);
            return ResolveDisplayName(map, wxid, wxid);
        }

        private static void ApplyDisplayNames(
            MomentsTimeline moment,
            List<MomentCommentDto> comments,
            List<MomentLikeDto> likes,
            Dictionary<string, string>? displayNames)
        {
            if (displayNames == null || displayNames.Count == 0)
            {
                return;
            }

            moment.nickName = ResolveDisplayName(displayNames, moment.userName, moment.nickName);
            foreach (var comment in comments)
            {
                comment.nickName = ResolveDisplayName(displayNames, comment.userName, comment.nickName);
                comment.replyNickName = ResolveDisplayName(displayNames, comment.replyUserName, comment.replyNickName);
            }

            foreach (var like in likes)
            {
                like.nickName = ResolveDisplayName(displayNames, like.userName, like.nickName);
            }
        }

        private static string ResolveDisplayName(Dictionary<string, string>? displayNames, string? wxid, string? currentName)
        {
            if (!string.IsNullOrWhiteSpace(wxid)
                && displayNames != null
                && displayNames.TryGetValue(wxid, out var displayName)
                && !string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            if (!string.IsNullOrWhiteSpace(currentName) && !LooksLikeRawWxid(currentName))
            {
                return currentName!;
            }

            return wxid ?? string.Empty;
        }

        private static string ResolveContactDisplayName(string? remarks, string? nickname, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(remarks)) return remarks;
            if (!string.IsNullOrWhiteSpace(nickname)) return nickname;
            return fallback;
        }

        private static bool LooksLikeRawWxid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var text = value.Trim();
            return text.StartsWith("wxid_", StringComparison.OrdinalIgnoreCase)
                || text.EndsWith("@chatroom", StringComparison.OrdinalIgnoreCase)
                || (text.StartsWith("v3_", StringComparison.OrdinalIgnoreCase) && text.EndsWith("@stranger", StringComparison.OrdinalIgnoreCase));
        }

        private static List<MomentCommentDto> DeserializeMomentComments(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<MomentCommentDto>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<MomentCommentDto>>(json) ?? new List<MomentCommentDto>();
            }
            catch
            {
                return new List<MomentCommentDto>();
            }
        }

        private static List<MomentLikeDto> DeserializeMomentLikes(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<MomentLikeDto>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<MomentLikeDto>>(json) ?? new List<MomentLikeDto>();
            }
            catch
            {
                return new List<MomentLikeDto>();
            }
        }

        private static List<string> DeserializeImageList(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<string>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static MomentLinkDto DeserializeMomentLink(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new MomentLinkDto();
            }

            try
            {
                return JsonSerializer.Deserialize<MomentLinkDto>(json) ?? new MomentLinkDto();
            }
            catch
            {
                return new MomentLinkDto();
            }
        }

        private static int InferMomentType(MomentsTimeline moment)
        {
            if (!string.IsNullOrWhiteSpace(moment.videoUrl)) return 3;
            if (!string.IsNullOrWhiteSpace(moment.linkInfoJson)) return 4;
            if (!string.IsNullOrWhiteSpace(moment.imagesJson) && DeserializeImageList(moment.imagesJson).Count > 0) return 2;
            return 1;
        }

        private static MomentPostResultDto BuildMomentPostResultDto(
            PostSNSNewsTaskResultNoticeMessage msg,
            ClientTaskService.MomentPostTaskContext? context,
            bool isLate)
        {
            var circleId = msg.Extra?.CircleId ?? 0L;
            var accountMatched = MomentPostRequestValidator.IsSameWechatId(context?.WeChatId, msg.WeChatId);
            var success = msg.Success && accountMatched;
            var hasCircleId = circleId != 0;
            var status = !accountMatched
                ? "AccountMismatch"
                : success
                ? (hasCircleId ? (isLate ? "LatePublished" : "Published") : (isLate ? "LatePublishedNeedSync" : "PublishedNeedSync"))
                : (isLate ? "LateFailed" : "Failed");

            return new MomentPostResultDto
            {
                taskId = msg.TaskId,
                clientRequestId = context?.ClientRequestId ?? string.Empty,
                weChatId = FirstNonBlank(msg.WeChatId, context?.WeChatId),
                success = success,
                circleId = circleId,
                code = msg.Code.ToString(),
                errorMessage = !accountMatched
                    ? "发圈回包账号与下发账号不一致"
                    : success ? string.Empty : TruncateResultText(NormalizeResultMessage(msg.ErrMsg, msg.Code.ToString(), false), 300),
                status = status,
                securityStatus = accountMatched ? "Ok" : "AccountMismatch",
                accountMatched = accountMatched,
                isLate = isLate,
                needSync = success,
                attachmentType = context?.AttachmentType ?? string.Empty,
                attachmentCount = context?.AttachmentCount ?? 0,
                visibleType = context?.VisibleType ?? string.Empty,
                labelCount = context?.LabelCount ?? 0,
                friendCount = context?.FriendCount ?? 0,
                notiUserCount = context?.NotiUserCount ?? 0,
                extCommentCount = context?.ExtCommentCount ?? 0,
                hasComment = context?.HasComment ?? false,
                hasPoi = context?.HasPoi ?? false,
                sendSlow = context?.SendSlow ?? false,
                receivedAt = DateTimeOffset.UtcNow
            };
        }

        private static string NormalizePostMomentMessage(PostSNSNewsTaskResultNoticeMessage msg, MomentPostResultDto? result = null)
        {
            if (result?.accountMatched == false)
            {
                return "朋友圈发布账号不一致，需人工核查";
            }

            if (!msg.Success)
            {
                return NormalizeResultMessage(msg.ErrMsg, msg.Code.ToString(), false);
            }

            // 微信 snsId 以 long 传输时可能为负数，只有 0 才表示无效。
            if (msg.Extra != null && msg.Extra.CircleId != 0)
            {
                var latePrefix = result?.isLate == true ? "迟到回包：" : string.Empty;
                return $"{latePrefix}朋友圈发布成功：CircleId={msg.Extra.CircleId}";
            }

            return result?.isLate == true
                ? "迟到回包：朋友圈发布成功，等待同步校准"
                : "朋友圈发布成功，等待同步校准";
        }

        /// <summary>
        /// 收到消息级表情信息后自动衔接 CDN 下载。
        /// <para>这里不直接写 Message.content 或 MessageMedias；最终媒体 URL 仍由 CDNDownloadResultNotice(1271) 进入 DbHelper 回填。</para>
        /// </summary>
        private async Task TryStartEmojiCdnDownloadAsync(
            ClientTaskService.PullEmojiInfoTaskContext emojiContext,
            PullEmojiInfoTaskResultNoticeMessage emojiMsg)
        {
            if (emojiContext == null || emojiMsg == null || emojiContext.MsgSvrId == 0)
            {
                return;
            }

            var selectedEmoji = SelectEmojiForMediaBackfill(emojiContext, emojiMsg);
            if (selectedEmoji == null)
            {
                _logger.LogWarning(
                    "表情信息结果未找到可下载 URL，跳过自动 CDN 补图: TaskId={TaskId}, WeChatId={WeChatId}, MsgSvrId={MsgSvrId}, Md5={Md5}, Count={Count}",
                    emojiContext.TaskId,
                    emojiContext.WeChatId,
                    emojiContext.MsgSvrId,
                    emojiContext.Md5,
                    emojiMsg.Emojis.Count);
                return;
            }

            var downloadUrl = ResolveEmojiDownloadUrl(selectedEmoji);
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                return;
            }

            var ownerWxid = FirstNonBlank(emojiContext.WeChatId, emojiMsg.WeChatId);
            var fileId = ResolveEmojiFileId(emojiContext, selectedEmoji);
            var fileFmt = ResolveEmojiFileFmt(selectedEmoji, downloadUrl);
            var result = await _clientTaskService.SendCDNDownloadFileTaskAsync(
                emojiContext.ConnectionId,
                ownerWxid,
                downloadUrl,
                selectedEmoji.Aeskey ?? string.Empty,
                CDNFileType.ChatMsgEmoji,
                fileId,
                fileFmt,
                Math.Max(0, selectedEmoji.Size),
                emojiContext.MsgSvrId,
                DateTime.UtcNow.Ticks);

            if (result.success)
            {
                _logger.LogInformation(
                    "表情信息结果已衔接 CDN 下载: TaskId={TaskId}, CdnTaskId={CdnTaskId}, WeChatId={WeChatId}, FriendId={FriendId}, MsgSvrId={MsgSvrId}, Md5={Md5}, UrlSource={UrlSource}",
                    emojiContext.TaskId,
                    result.taskId,
                    ownerWxid,
                    emojiContext.FriendId,
                    emojiContext.MsgSvrId,
                    fileId,
                    ResolveEmojiDownloadUrlSource(selectedEmoji, downloadUrl));
            }
            else
            {
                _logger.LogWarning(
                    "表情信息结果衔接 CDN 下载失败: TaskId={TaskId}, WeChatId={WeChatId}, MsgSvrId={MsgSvrId}, Md5={Md5}, Reason={Reason}",
                    emojiContext.TaskId,
                    ownerWxid,
                    emojiContext.MsgSvrId,
                    fileId,
                    result.message);
            }
        }

        /// <summary>
        /// 从 1273 结果中选择用于消息补图的表情记录。
        /// <para>优先匹配下发时的 MD5；找不到时才兜底选择第一条有下载 URL 的记录，避免无上下文写错消息。</para>
        /// </summary>
        private static EmojiMessage? SelectEmojiForMediaBackfill(
            ClientTaskService.PullEmojiInfoTaskContext context,
            PullEmojiInfoTaskResultNoticeMessage emojiMsg)
        {
            var candidates = emojiMsg.Emojis
                .Where(HasEmojiDownloadUrl)
                .ToList();
            if (candidates.Count == 0)
            {
                return null;
            }

            var expectedMd5 = context.Md5?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(expectedMd5))
            {
                var matched = candidates.FirstOrDefault(emoji =>
                    string.Equals(emoji.Md5?.Trim(), expectedMd5, StringComparison.OrdinalIgnoreCase));
                if (matched != null)
                {
                    return matched;
                }
            }

            return candidates[0];
        }

        private static bool HasEmojiDownloadUrl(EmojiMessage emoji)
        {
            return emoji != null
                && !string.IsNullOrWhiteSpace(ResolveEmojiDownloadUrl(emoji));
        }

        private static string ResolveEmojiDownloadUrl(EmojiMessage emoji)
        {
            if (emoji == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(emoji.Encrypturl) && !string.IsNullOrWhiteSpace(emoji.Aeskey))
            {
                return emoji.Encrypturl.Trim();
            }

            if (!string.IsNullOrWhiteSpace(emoji.CdnUrl) && !string.IsNullOrWhiteSpace(emoji.Aeskey))
            {
                return emoji.CdnUrl.Trim();
            }

            if (!string.IsNullOrWhiteSpace(emoji.ExternUrl) && !string.IsNullOrWhiteSpace(emoji.Aeskey))
            {
                return emoji.ExternUrl.Trim();
            }

            return FirstNonBlank(emoji.Encrypturl, emoji.CdnUrl, emoji.ExternUrl);
        }

        private static string ResolveEmojiDownloadUrlSource(EmojiMessage emoji, string selectedUrl)
        {
            if (emoji == null || string.IsNullOrWhiteSpace(selectedUrl))
            {
                return string.Empty;
            }

            if (string.Equals(selectedUrl, emoji.Encrypturl?.Trim(), StringComparison.OrdinalIgnoreCase)) return "encrypturl";
            if (string.Equals(selectedUrl, emoji.CdnUrl?.Trim(), StringComparison.OrdinalIgnoreCase)) return "cdnUrl";
            if (string.Equals(selectedUrl, emoji.ExternUrl?.Trim(), StringComparison.OrdinalIgnoreCase)) return "externUrl";
            return "unknown";
        }

        private static string ResolveEmojiFileId(ClientTaskService.PullEmojiInfoTaskContext context, EmojiMessage emoji)
        {
            return FirstNonBlank(emoji.Md5, context.Md5);
        }

        private static string ResolveEmojiFileFmt(EmojiMessage emoji, string downloadUrl)
        {
            var ext = FirstNonBlank(
                TryGetFileExtension(emoji.Name),
                TryGetFileExtension(downloadUrl));

            if (!string.IsNullOrWhiteSpace(ext))
            {
                return ext;
            }

            return "gif";
        }

        private static string TryGetFileExtension(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            try
            {
                var source = value.Trim();
                var path = Uri.TryCreate(source, UriKind.Absolute, out var uri) ? uri.LocalPath : source.Split('?', '#')[0];
                var ext = System.IO.Path.GetExtension(path)?.TrimStart('.').Trim().ToLowerInvariant();
                return string.IsNullOrWhiteSpace(ext) || ext.Length > 8 ? string.Empty : ext;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 统一整理通用 TaskResultNotice 的结果摘要。
        /// 2026-05-12：群发任务当前走通用回执，成功时常常没有 ErrMsg；
        /// 如果继续直接返回空串，前端会表现成“点了没反应”。
        /// 因此这里对 WeChatGroupSendTask 单独补一个可读成功摘要。
        /// </summary>
        private static string NormalizeGenericTaskResultMessage(TaskResultNoticeMessage msg)
        {
            if (msg == null)
            {
                return string.Empty;
            }

            if (msg.TaskType == EnumMsgType.WeChatGroupSendTask)
            {
                if (!msg.Success)
                {
                    return NormalizeResultMessage(msg.ErrMsg, "群发消息任务执行失败", false);
                }

                if (!string.IsNullOrWhiteSpace(msg.ErrMsg))
                {
                    return msg.ErrMsg.Trim();
                }

                return "群发消息任务已进入微信发送链";
            }

            if ((int)msg.TaskType == 1102)
            {
                return msg.Success
                    ? "添加好友申请已提交，等待对方验证"
                    : NormalizeResultMessage(msg.ErrMsg, "添加好友申请提交失败", false);
            }

            if ((int)msg.TaskType == 1214)
            {
                return msg.Success
                    ? "群内添加好友申请已提交，等待对方验证"
                    : NormalizeResultMessage(msg.ErrMsg, "群内添加好友申请提交失败", false);
            }

            if ((int)msg.TaskType == 1216)
            {
                return msg.Success
                    ? "删除好友任务已完成，等待联系人同步"
                    : NormalizeResultMessage(msg.ErrMsg, "删除好友任务执行失败", false);
            }

            if ((int)msg.TaskType == 1203)
            {
                return msg.Success
                    ? "朋友圈点赞/取消点赞任务完成"
                    : NormalizeResultMessage(msg.ErrMsg, "朋友圈点赞/取消点赞任务失败", false);
            }

            if (msg.TaskType == EnumMsgType.TalkToFriendTask)
            {
                return msg.Success
                    ? (string.IsNullOrWhiteSpace(msg.ErrMsg) ? "聊天消息任务已完成" : msg.ErrMsg.Trim())
                    : NormalizeResultMessage(msg.ErrMsg, "聊天消息任务执行失败", false);
            }

            if (msg.TaskType == EnumMsgType.SendMultiPictureTask)
            {
                return msg.Success
                    ? (string.IsNullOrWhiteSpace(msg.ErrMsg) ? "图片任务已完成" : msg.ErrMsg.Trim())
                    : NormalizeResultMessage(msg.ErrMsg, "图片任务执行失败", false);
            }

            if (msg.TaskType == EnumMsgType.VoiceTransTextTask)
            {
                if (!msg.Success)
                {
                    return NormalizeResultMessage(msg.ErrMsg, "语音转文字任务执行失败", false);
                }

                return string.IsNullOrWhiteSpace(msg.ErrMsg)
                    ? "语音转文字成功"
                    : $"语音转文字成功：{msg.ErrMsg.Trim()}";
            }

            if (msg.TaskType == EnumMsgType.RevokeMessageTask)
            {
                return msg.Success
                    ? "消息撤回成功，正在刷新会话"
                    : NormalizeResultMessage(msg.ErrMsg, "消息撤回失败", false);
            }

            if (msg.TaskType == EnumMsgType.ForwardMessageTask)
            {
                return msg.Success
                    ? "消息转发任务已完成，正在等待微信消息同步"
                    : NormalizeResultMessage(msg.ErrMsg, "消息转发失败", false);
            }

            if (msg.TaskType == EnumMsgType.ForwardMultiMessageTask)
            {
                return msg.Success
                    ? "多条消息转发任务已完成，正在等待微信消息同步"
                    : NormalizeResultMessage(msg.ErrMsg, "多条消息转发失败", false);
            }

            if (msg.TaskType == EnumMsgType.ForwardMessageByContentTask)
            {
                return msg.Success
                    ? "原始内容转发任务已完成，正在等待微信消息同步"
                    : NormalizeResultMessage(msg.ErrMsg, "原始内容转发失败", false);
            }

            if (msg.TaskType == EnumMsgType.ClearAllChatMsgTask)
            {
                return msg.Success
                    ? "微信端聊天记录清空任务已完成"
                    : NormalizeResultMessage(msg.ErrMsg, "微信端聊天记录清空失败", false);
            }

            if (msg.TaskType == EnumMsgType.SendLuckyMoneyTask)
            {
                return msg.Success
                    ? "发红包任务已完成"
                    : NormalizeResultMessage(msg.ErrMsg, "发红包任务失败", false);
            }

            if (msg.TaskType == EnumMsgType.RemittanceTask)
            {
                return msg.Success
                    ? "转账任务已完成"
                    : NormalizeResultMessage(msg.ErrMsg, "转账任务失败", false);
            }

            if (msg.TaskType == EnumMsgType.TriggerUnReadTask)
            {
                return msg.Success
                    ? "会话已标记为未读，正在校准未读列表"
                    : NormalizeResultMessage(msg.ErrMsg, "标记会话未读失败", false);
            }

            if (msg.TaskType == EnumMsgType.ContactLabelTask)
            {
                return msg.Success
                    ? "联系人标签任务已完成，等待标签列表校准"
                    : NormalizeResultMessage(msg.ErrMsg, "联系人标签任务执行失败", false);
            }

            if (msg.TaskType == EnumMsgType.ContactLabelDeleteTask)
            {
                return msg.Success
                    ? "联系人标签删除成功，等待标签列表校准"
                    : NormalizeResultMessage(msg.ErrMsg, "联系人标签删除失败", false);
            }

            if (msg.TaskType == EnumMsgType.ContactSetLabelTask)
            {
                return msg.Success
                    ? "联系人标签设置成功，正在刷新联系人"
                    : NormalizeResultMessage(msg.ErrMsg, "联系人标签设置失败", false);
            }

            if (msg.TaskType == EnumMsgType.GetA8KeyTask)
            {
                if (!msg.Success)
                {
                    return NormalizeResultMessage(msg.ErrMsg, "A8Key 获取失败", false);
                }

                return string.IsNullOrWhiteSpace(msg.ErrMsg)
                    ? "A8Key 获取成功但客户端未返回 URL"
                    : msg.ErrMsg.Trim();
            }

            if (msg.TaskType == EnumMsgType.WechatSettingTask)
            {
                return msg.Success
                    ? (string.IsNullOrWhiteSpace(msg.ErrMsg) ? "微信资料设置成功" : msg.ErrMsg.Trim())
                    : NormalizeResultMessage(msg.ErrMsg, "微信资料设置失败", false);
            }

            return NormalizeResultMessage(msg.ErrMsg, msg.TaskType.ToString(), msg.Success);
        }

        private static string NormalizeResultMessage(string errMsg, string fallback, bool success)
        {
            if (!string.IsNullOrWhiteSpace(errMsg))
            {
                return errMsg;
            }

            return success ? string.Empty : fallback;
        }

        private static string TruncateResultText(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Trim();
            return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
        }

        /// <summary>
        /// 返回第一个非空白字符串。
        /// </summary>
        private static string FirstNonBlank(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static long? TryParseNullableLong(string? value)
        {
            if (long.TryParse(value?.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        /// <summary>
        /// 判断 1028.FriendId 是否像真实会话 ID。
        /// <para>当前安卓 Tsk26/Tsk144 分流存在把任务代码写入 FriendId 的风险，不能把这类值用于落库。</para>
        /// </summary>
        private static bool IsReliableTalkResultFriendId(string? friendId)
        {
            if (string.IsNullOrWhiteSpace(friendId))
            {
                return false;
            }

            var normalized = friendId.Trim();
            return !normalized.StartsWith("Tsk", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 判断是否为无 TaskId 的专用任务结果。
        /// <para>
        /// 这些协议的结果体没有 TaskId，失败时也必须推给前端，否则页面只能看到“已下发”，看不到真实失败原因。
        /// </para>
        /// </summary>
        private static bool ShouldPublishNoTaskIdResult(EnumMsgType msgType)
        {
            return msgType == EnumMsgType.PullWeChatQrCodeTaskResultNotice
                || msgType == EnumMsgType.FindContactTaskResult
                || msgType == EnumMsgType.WeChatLocationTaskResultNotice
                || msgType == EnumMsgType.WalletBalanceTaskResultNotice
                || msgType == EnumMsgType.PhoneStateTaskResultNotice
                || msgType == EnumMsgType.ContactLabelInfoNotice
                || msgType == EnumMsgType.ContactLabelAddNotice
                || msgType == EnumMsgType.ContactLabelDelNotice
                || msgType == EnumMsgType.QueryHbDetailTaskResultNotice
                || msgType == EnumMsgType.QueryHbStatusTaskResultNotice;
        }

        /// <summary>
        /// 将专用任务结果的结构化数据附加到实时回执文案。
        /// <para>
        /// 当前 TaskResultReceivedEvent 只有 message 字段；为避免改动 SignalR DTO 和前端订阅链，
        /// 先把结构化数据压缩成一段 JSON 附在文案后，后续若 UI 需要细化展示再拆独立 DTO。
        /// </para>
        /// </summary>
        private static string AppendResultDataJson(string message, object? data)
        {
            var text = message ?? string.Empty;
            if (data == null)
            {
                return text;
            }

            try
            {
                var json = JsonSerializer.Serialize(data);
                if (string.IsNullOrWhiteSpace(json) || json == "{}")
                {
                    return text;
                }

                return string.IsNullOrWhiteSpace(text)
                    ? $"Data={json}"
                    : $"{text}; Data={json}";
            }
            catch
            {
                return text;
            }
        }

        /// <summary>
        /// 朋友圈评论在 62203 上可能已进入微信界面执行，但没有命中 Tsk50 完成回调。
        /// <para>这种回执按“已提交待校准”处理，避免网页误报硬失败。</para>
        /// </summary>
        private static bool IsCircleCommentCallbackTimeout(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            return message.Contains("TimeOut WAIT_TO_FINISHED", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Timeout waiting for client response", StringComparison.OrdinalIgnoreCase);
        }
    }
}
