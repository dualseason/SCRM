using Microsoft.AspNetCore.Mvc;



using Microsoft.EntityFrameworkCore;



using SCRM.Services;



using SCRM.API.Services.Core;
using SCRM.API.Services.Security;



using SCRM.Services.Data;



using SCRM.Shared.Interfaces;



using SCRM.SHARED.Models.Dtos;



using System;



using System.Text;



using System.Text.Json;



using System.Threading.Tasks;



using System.Collections.Generic;

using System.Linq;

using Microsoft.Extensions.Logging;







namespace SCRM.Controllers



{



    [ApiController]



    [Route("api/[controller]")]



    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "RequireAdminRole")]



    public class ClientTaskController : ControllerBase



    {



        private readonly ClientTaskService _clientTaskService;



        private readonly ConnectionManager _connectionManager;



        private readonly ApplicationDbContext _db;



        private readonly ICrmService _crmService;



        private readonly SensitiveWordPolicyService _sensitiveWordPolicyService;



        private readonly ILogger<ClientTaskController> _logger;



        private static readonly JsonSerializerOptions FinderHistoryJsonOptions = new()



        {



            WriteIndented = true,



            PropertyNamingPolicy = JsonNamingPolicy.CamelCase



        };







        public ClientTaskController(



            ClientTaskService clientTaskService,



            ConnectionManager connectionManager,



            ApplicationDbContext db,



            ICrmService crmService,



            SensitiveWordPolicyService sensitiveWordPolicyService,



            ILogger<ClientTaskController> logger)



        {



            _clientTaskService = clientTaskService;



            _connectionManager = connectionManager;



            _db = db;



            _crmService = crmService;



            _sensitiveWordPolicyService = sensitiveWordPolicyService;



            _logger = logger;



        }







        [HttpGet("connections")]



        public async Task<IActionResult> GetConnections()



        {



            var connections = await _connectionManager.GetAllConnectionsAsync();



            return Ok(connections);



        }







        [HttpPost("heartbeat/{connectionId}")]



        public async Task<IActionResult> SendHeartBeat(string connectionId)



        {



            var result = await _clientTaskService.SendHeartBeatAsync(connectionId);



            return Ok(result);



        }







        [HttpPost("sync-friends/{connectionId}")]
        public async Task<IActionResult> SendSyncFriends(
            string connectionId,
            [FromQuery] string weChatId = "",
            [FromQuery] long taskId = 0)
        {
            var finalTaskId = taskId != 0 ? taskId : DateTime.UtcNow.Ticks;
            var sent = await _clientTaskService.SendSyncFriendListTaskAsync(connectionId, weChatId, finalTaskId);
            var result = sent
                ? TaskResult.Ok(finalTaskId, "好友同步指令已下发，等待手机端回传 FriendPushNotice")
                : TaskResult.Fail(finalTaskId, "好友同步指令下发失败");

            return Ok(result);
        }

        /// <summary>
        /// 按设备 UUID 下发 3056 好友异步同步任务。
        /// <para>业务入口优先使用该接口；联系人最终仍由 FriendPushNotice 进入 DbHelper.SaveContacts 持久化。</para>
        /// </summary>
        [HttpPost("sync-friends-by-device")]
        public async Task<IActionResult> SendSyncFriendsByDevice([FromBody] SyncFriendsByDeviceRequest request)
        {
            var result = await _crmService.SyncFriendListAsync(request.DeviceUuid, request.WeChatId);
            return Ok(result);
        }

        /// <summary>
        /// 按设备 UUID 下发 3050 微信账号快照查询任务。
        /// <para>用于页面显示未登录、服务端重启后缓存未恢复等场景的主动校准。</para>
        /// </summary>
        [HttpPost("refresh-wechat-accounts")]
        public async Task<IActionResult> RefreshWeChatAccounts([FromBody] RefreshWeChatAccountsRequest request)
        {
            var result = await _crmService.RefreshWeChatAccountsAsync(request.DeviceUuid);
            return Ok(result);
        }

        /// <summary>
        /// 下发企微用户同步任务。
        /// <para>Android 会通过 QwUserPUshNotice 异步上报，服务端按联系人口径落库。</para>
        /// </summary>
        [HttpPost("trigger-qw-user-push")]
        public async Task<IActionResult> SendTriggerQwUserPush([FromBody] TriggerQwUserPushRequest request)
        {
            var result = await _clientTaskService.SendTriggerQwUserPushTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 下发聊天消息 MsgSvrId 快照同步任务。
        /// <para>当前接收侧会记录并推送 ChatMsgIdsPushNotice 摘要，不自动做删除/缺失对账。</para>
        /// </summary>
        [HttpPost("trigger-chat-msg-ids-push")]
        public async Task<IActionResult> SendTriggerChatMsgIdsPush([FromBody] TriggerChatMsgIdsPushRequest request)
        {
            var result = await _clientTaskService.SendTriggerChatMsgIdsPushTaskAsync(
                request.ConnectionId,
                request.StartTime,
                request.EndTime,
                request.WeChatId,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 下发历史聊天消息同步任务。
        /// <para>Android 会通过 HistoryMsgPushNotice(2033) 异步上报。</para>
        /// </summary>
        [HttpPost("trigger-history-msg-push")]
        public async Task<IActionResult> SendTriggerHistoryMsgPush([FromBody] TriggerHistoryMsgPushRequest request)
        {
            var result = await _clientTaskService.SendTriggerHistoryMsgPushTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.FriendId,
                request.StartTime,
                request.EndTime,
                request.Flag,
                request.Count,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 下发指定会话已读同步任务。
        /// </summary>
        [HttpPost("trigger-message-read")]
        public async Task<IActionResult> SendTriggerMessageRead([FromBody] TriggerMessageReadRequest request)
        {
            var result = await _clientTaskService.SendTriggerMessageReadTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.FriendId,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 下发未读会话列表同步任务。
        /// </summary>
        [HttpPost("trigger-unread-push")]
        public async Task<IActionResult> SendTriggerUnreadPush([FromBody] TriggerUnreadPushRequest request)
        {
            var result = await _clientTaskService.SendTriggerUnreadPushTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 下发单会话未读同步任务。
        /// </summary>
        [HttpPost("trigger-unread")]
        public async Task<IActionResult> SendTriggerUnRead([FromBody] TriggerUnReadRequest request)
        {
            var result = await _clientTaskService.SendTriggerUnReadTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.FriendId,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 下发业务联系人同步任务。
        /// </summary>
        [HttpPost("trigger-biz-contact-push")]
        public async Task<IActionResult> SendTriggerBizContactPush([FromBody] TriggerBizContactPushRequest request)
        {
            var result = await _clientTaskService.SendTriggerBizContactPushTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 下发企微会话同步任务。
        /// </summary>
        [HttpPost("trigger-qw-conv-push")]
        public async Task<IActionResult> SendTriggerQwConvPush([FromBody] TriggerQwConvPushRequest request)
        {
            var result = await _clientTaskService.SendTriggerQwConvPushTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.StartTime,
                request.EndTime,
                request.Limit,
                request.Offset,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 下发联系人标签列表同步任务。
        /// <para>Android 会通过 ContactLabelInfoNotice(2032) 异步上报，服务端写入 ContactTags。</para>
        /// </summary>
        [HttpPost("trigger-label-push")]
        public async Task<IActionResult> SendTriggerLabelPush([FromBody] TriggerLabelPushRequest request)
        {
            var result = await _clientTaskService.SendTriggerLabelPushTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 下发联系人标签创建/重命名/成员调整任务。
        /// </summary>
        [HttpPost("contact-label")]
        public async Task<IActionResult> SendContactLabel([FromBody] ContactLabelRequest request)
        {
            var result = await _clientTaskService.SendContactLabelTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.LabelName,
                request.LabelId,
                request.AddList,
                request.DelList,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 下发联系人标签删除任务。
        /// </summary>
        [HttpPost("contact-label-delete")]
        public async Task<IActionResult> SendContactLabelDelete([FromBody] ContactLabelDeleteRequest request)
        {
            var result = await _clientTaskService.SendContactLabelDeleteTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.LabelId,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 下发单个好友完整标签集合设置任务。
        /// </summary>
        [HttpPost("contact-set-label")]
        public async Task<IActionResult> SendContactSetLabel([FromBody] ContactSetLabelRequest request)
        {
            var result = await _clientTaskService.SendContactSetLabelTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.FriendId,
                request.LabelIds,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 请求客户端主动回传当前配置快照。
        /// <para>结果由 ConfigPushNotice(1381) 或当前 SmRun 兼容口径 SetConfigTask(1382) 异步上报。</para>
        /// </summary>
        [HttpPost("trigger-config-push")]
        public async Task<IActionResult> SendTriggerConfigPush([FromBody] TriggerConfigPushRequest request)
        {
            var taskId = request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks;
            var sent = await _clientTaskService.SendTriggerConfigPushTaskAsync(request.ConnectionId, taskId);
            return Ok(sent
                ? TaskResult.Ok(taskId, "配置同步指令已下发，等待客户端上报配置快照")
                : TaskResult.Fail(taskId, "配置同步指令下发失败"));
        }

        /// <summary>
        /// 下发微信违禁词列表。
        /// <para>SetForbiddenWord(1383) 不带 TaskId/回执；返回只表示下发是否成功。</para>
        /// </summary>
        [HttpPost("set-forbidden-word")]
        public async Task<IActionResult> SendSetForbiddenWord([FromBody] SetForbiddenWordRequest request)
        {
            var normalizedWords = (request.Words ?? new List<string>())
                .Select(word => word?.Trim() ?? string.Empty)
                .Where(word => !string.IsNullOrWhiteSpace(word))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (!string.IsNullOrWhiteSpace(request.WeChatId))
            {
                await _sensitiveWordPolicyService.SaveDeviceBlockWordsAsync(
                    null,
                    request.WeChatId,
                    normalizedWords);
            }

            var sent = await _clientTaskService.SendSetForbiddenWordAsync(
                request.ConnectionId,
                request.WeChatId,
                normalizedWords);

            return Ok(sent
                ? TaskResult.Ok(0, $"违禁词列表已下发：Count={normalizedWords.Length}")
                : TaskResult.Fail("违禁词列表下发失败"));
        }

        /// <summary>
        /// 下发好友申请历史/补偿列表拉取任务。
        /// <para>Android 会通过 FriendAddReqListNotice(2036) 异步上报，服务端复用 FriendRequests 落库。</para>
        /// </summary>
        [HttpPost("pull-friend-add-req-list")]
        public async Task<IActionResult> SendPullFriendAddReqList([FromBody] PullFriendAddReqListRequest request)
        {
            var result = await _clientTaskService.SendPullFriendAddReqListTaskAsync(
                request.ConnectionId,
                request.StartTime,
                request.OnlyNew,
                request.GetAll,
                request.WeChatId,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 下发单条聊天消息补偿任务。
        /// <para>Android 会通过 RequestTalkMsgTaskResultNotice(1253) 异步上报，服务端按消息口径落库。</para>
        /// </summary>
        [HttpPost("request-talk-msg")]
        public async Task<IActionResult> SendRequestTalkMsg([FromBody] RequestTalkMsgRequest request)
        {
            var result = await _clientTaskService.SendRequestTalkMsgTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.MsgSvrId,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 下发原始聊天正文/XML 补偿任务。
        /// <para>Android 会通过 RequestTalkContentTaskResultNotice(1219) 异步上报，服务端只更新已有消息。</para>
        /// </summary>
        [HttpPost("request-talk-content")]
        public async Task<IActionResult> SendRequestTalkContent([FromBody] RequestTalkContentRequest request)
        {
            var result = await _clientTaskService.SendRequestTalkContentTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.MsgSvrId,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 下发聊天消息详情补偿任务。
        /// </summary>
        [HttpPost("request-talk-detail")]
        public async Task<IActionResult> SendRequestTalkDetail([FromBody] RequestTalkDetailRequest request)
        {
            var result = await _clientTaskService.SendRequestTalkDetailTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.FriendId,
                request.MsgId,
                request.MsgSvrId,
                request.Md5,
                request.GetOriginal,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 下发语音转文字任务。
        /// <para>Android 会通过通用 TaskResultNotice 回传，成功时 ErrMsg 为识别文本。</para>
        /// </summary>
        [HttpPost("voice-trans-text")]
        public async Task<IActionResult> SendVoiceTransText([FromBody] VoiceTransTextRequest request)
        {
            var result = await _clientTaskService.SendVoiceTransTextTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.FriendId,
                request.MsgSvrId,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 下发 GetA8Key 任务。
        /// <para>成功时客户端通常通过通用 TaskResultNotice.ErrMsg 返回 A8Key 后 URL。</para>
        /// </summary>
        [HttpPost("get-a8-key")]
        public async Task<IActionResult> SendGetA8Key([FromBody] GetA8KeyRequest request)
        {
            var result = await _clientTaskService.SendGetA8KeyTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.Type,
                request.Url,
                request.UserName,
                request.MsgSvrId,
                request.Reason,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 下发微信资料/隐私设置任务。
        /// </summary>
        [HttpPost("wechat-setting")]
        public async Task<IActionResult> SendWechatSetting([FromBody] WechatSettingRequest request)
        {
            if (!System.Enum.IsDefined(typeof(Jubo.JuLiao.IM.Wx.Proto.EnumSettings), request.Action))
            {
                return BadRequest(TaskResult.Fail($"不支持的微信设置动作：{request.Action}"));
            }

            var result = await _clientTaskService.SendWechatSettingTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                (Jubo.JuLiao.IM.Wx.Proto.EnumSettings)request.Action,
                request.Content,
                request.IntParam,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 下发消息撤回任务。
        /// </summary>
        [HttpPost("revoke-message")]
        public async Task<IActionResult> SendRevokeMessage([FromBody] RevokeMessageRequest request)
        {
            var result = await _clientTaskService.SendRevokeMessageTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.FriendId,
                request.MsgSvrId,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 下发单条消息转发任务。
        /// </summary>
        [HttpPost("forward-message")]
        public async Task<IActionResult> SendForwardMessage([FromBody] ForwardMessageRequest request)
        {
            var result = await _clientTaskService.SendForwardMessageTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.Talker,
                request.MsgSvrId,
                request.FriendIds,
                request.ExtMsg,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 下发多条消息转发任务。
        /// </summary>
        [HttpPost("forward-multi-message")]
        public async Task<IActionResult> SendForwardMultiMessage([FromBody] ForwardMultiMessageRequest request)
        {
            var result = await _clientTaskService.SendForwardMultiMessageTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.Talker,
                request.MsgIds,
                request.FriendIds,
                request.ExtMsg,
                request.SendRecord,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 下发按原始内容转发消息任务。
        /// </summary>
        [HttpPost("forward-message-by-content")]
        public async Task<IActionResult> SendForwardMessageByContent([FromBody] ForwardMessageByContentRequest request)
        {
            var result = await _clientTaskService.SendForwardMessageByContentTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.FriendIds,
                request.MsgSvrId,
                request.MsgType,
                request.Content,
                request.Thumb,
                request.ExtMsg,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 下发清空微信端聊天记录任务。
        /// </summary>
        [HttpPost("clear-all-chat-msg")]
        public async Task<IActionResult> SendClearAllChatMsg([FromBody] ClearAllChatMsgRequest request)
        {
            var result = await _clientTaskService.SendClearAllChatMsgTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.Flag,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 下发红包详情查询任务；结果通过 QueryHbDetailTaskResultNotice 异步返回。
        /// </summary>
        [HttpPost("query-hb-detail")]
        public async Task<IActionResult> SendQueryHbDetail([FromBody] QueryHbRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.HbUrl))
            {
                return BadRequest(TaskResult.Fail("红包链接为空"));
            }

            var queued = await _clientTaskService.SendQueryHbDetailTaskAsync(request.ConnectionId, request.WeChatId, request.HbUrl.Trim());
            return Ok(queued
                ? TaskResult.Ok(0, "红包详情查询任务已下发，等待客户端异步结果")
                : TaskResult.Fail("红包详情查询任务下发失败"));
        }

        /// <summary>
        /// 下发红包状态查询任务；结果通过 QueryHbStatusTaskResultNotice 异步返回。
        /// </summary>
        [HttpPost("query-hb-status")]
        public async Task<IActionResult> SendQueryHbStatus([FromBody] QueryHbRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.HbUrl))
            {
                return BadRequest(TaskResult.Fail("红包链接为空"));
            }

            var queued = await _clientTaskService.SendQueryHbStatusTaskAsync(request.ConnectionId, request.WeChatId, request.HbUrl.Trim());
            return Ok(queued
                ? TaskResult.Ok(0, "红包状态查询任务已下发，等待客户端异步结果")
                : TaskResult.Fail("红包状态查询任务下发失败"));
        }

        /// <summary>
        /// 下发微信发红包任务，金额单位为分。
        /// <para>支付密码只随本次任务下发，不写入日志或数据库。</para>
        /// </summary>
        [HttpPost("send-lucky-money")]
        public async Task<IActionResult> SendLuckyMoney([FromBody] SendLuckyMoneyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FriendId))
            {
                return BadRequest(TaskResult.Fail("红包接收人为空"));
            }

            if (request.Money < 1 || request.Money > 20000)
            {
                return BadRequest(TaskResult.Fail("红包金额必须在 1 到 20000 分之间"));
            }

            if (request.Number < 1 || request.Number > 100)
            {
                return BadRequest(TaskResult.Fail("红包个数必须在 1 到 100 之间"));
            }

            if (!IsValidPaymentPassword(request.Passwd))
            {
                return BadRequest(TaskResult.Fail("支付密码必须为 6 位数字"));
            }

            var result = await _clientTaskService.SendLuckyMoneyTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.FriendId.Trim(),
                request.Money,
                request.Number,
                request.Passwd.Trim(),
                request.Wish?.Trim() ?? string.Empty,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 下发微信转账任务，金额单位为分。
        /// <para>群内转账时 RoomId 为群会话 ID，FriendId 为收款人 wxid。</para>
        /// </summary>
        [HttpPost("remittance")]
        public async Task<IActionResult> SendRemittance([FromBody] RemittanceRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FriendId))
            {
                return BadRequest(TaskResult.Fail("转账收款人为空"));
            }

            if (request.Money < 1)
            {
                return BadRequest(TaskResult.Fail("转账金额必须大于 0 分"));
            }

            if (!IsValidPaymentPassword(request.Passwd))
            {
                return BadRequest(TaskResult.Fail("支付密码必须为 6 位数字"));
            }

            var result = await _clientTaskService.SendRemittanceTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.FriendId.Trim(),
                request.Money,
                request.Passwd.Trim(),
                request.Memo?.Trim() ?? string.Empty,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks,
                request.RoomId?.Trim() ?? string.Empty);

            return Ok(result);
        }

        /// <summary>
        /// 下发微信账号登出任务。
        /// </summary>
        [HttpPost("wechat-logout")]
        public async Task<IActionResult> SendWechatLogout([FromBody] WechatLogoutRequest request)
        {
            var queued = await _clientTaskService.SendWechatLogoutTaskAsync(request.ConnectionId, request.WeChatId);
            return Ok(queued
                ? TaskResult.Ok(0, "微信登出任务已下发，等待客户端上报账号状态")
                : TaskResult.Fail("微信登出任务下发失败"));
        }

        /// <summary>
        /// 下发 CDN 文件下载任务。
        /// <para>Android 会通过 CDNDownloadResultNotice(1271) 异步上报，服务端按 MsgSvrId 回填媒体 URL。</para>
        /// </summary>
        [HttpPost("download-cdn-file")]
        public async Task<IActionResult> SendDownloadCdnFile([FromBody] DownloadCdnFileRequest request)
        {
            var normalizedFileType = System.Enum.IsDefined(typeof(Jubo.JuLiao.IM.Wx.Proto.CDNFileType), request.FileType)
                ? (Jubo.JuLiao.IM.Wx.Proto.CDNFileType)request.FileType
                : Jubo.JuLiao.IM.Wx.Proto.CDNFileType.ChatMsgFile;

            var result = await _clientTaskService.SendCDNDownloadFileTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.CdnUrl,
                request.CdnKey,
                normalizedFileType,
                request.FileId,
                request.FileFmt,
                request.FileSize,
                request.MsgSvrId,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 启动好友检测/清粉任务。
        /// <para>OnlyCheck=true 时只检测不删除；进度由 PostFriendDetectCountNotice(2028) 异步回传。</para>
        /// </summary>
        [HttpPost("friend-detect/start")]
        public async Task<IActionResult> SendPostFriendDetect([FromBody] StartFriendDetectRequest request)
        {
            var result = await _clientTaskService.SendPostFriendDetectTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.Message,
                request.OnlyCheck,
                request.SkipHour,
                request.Mode,
                request.Max,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 停止好友检测/清粉任务。
        /// </summary>
        [HttpPost("friend-detect/stop")]
        public async Task<IActionResult> SendPostStopFriendDetect([FromBody] StopFriendDetectRequest request)
        {
            var result = await _clientTaskService.SendPostStopFriendDetectTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }

        /// <summary>
        /// 拉取好友检测/清粉最终结果。
        /// </summary>
        [HttpPost("friend-detect/result")]
        public async Task<IActionResult> SendGetFriendDetectResult([FromBody] GetFriendDetectResultRequest request)
        {
            var result = await _clientTaskService.SendGetFriendDetectResultTaskAsync(
                request.ConnectionId,
                request.WeChatId,
                request.TaskId != 0 ? request.TaskId : DateTime.UtcNow.Ticks);

            return Ok(result);
        }











        [HttpPost("chatroom-invite-list/{connectionId}")]



        public async Task<IActionResult> SendGetChatRoomInviteList(string connectionId)



        {



            var result = await _clientTaskService.SendGetChatRoomInviteListTaskAsync(connectionId, string.Empty, DateTime.UtcNow.Ticks);



            return Ok(new { success = result });



        }







        [HttpPost("join-group-by-qr")]



        public async Task<IActionResult> SendJoinGroupByQr([FromBody] JoinGroupByQrRequest request)



        {



            var result = await _clientTaskService.SendJoinGroupByQrTaskAsync(



                request.ConnectionId,



                request.QrUrl,



                request.QrContent,



                request.WeChatId,



                DateTime.UtcNow.Ticks);



            return Ok(result);



        }







        [HttpPost("pull-chatroom-qrcode")]



        public async Task<IActionResult> SendPullChatRoomQrCode([FromBody] PullChatRoomQrCodeRequest request)



        {



            var result = await _clientTaskService.SendPullChatRoomQrCodeTaskAsync(



                request.ConnectionId,



                request.ChatRoomId,



                request.WeChatId,



                DateTime.UtcNow.Ticks);



            return Ok(result);



        }







        [HttpPost("approve-chatroom-invite")]



        public async Task<IActionResult> SendApproveChatRoomInvite([FromBody] ChatRoomInviteApproveRequest request)



        {



            var result = await _clientTaskService.SendChatRoomInviteApproveTaskAsync(



                request.ConnectionId,



                request.MsgSvrId,



                request.RoomId,



                request.MsgContent,



                request.WeChatId,



                request.MsgId != 0 ? request.MsgId : request.MsgSvrId,



                DateTime.UtcNow.Ticks);



            return Ok(result);



        }







        [HttpPost("send-jielong")]



        public async Task<IActionResult> SendJielong([FromBody] SendJielongRequest request)



        {



            var result = await _clientTaskService.SendJielongTaskAsync(



                request.ConnectionId,



                request.ChatRoomId,



                request.Content,



                request.Title,



                request.Sample,



                request.Memo,



                request.MsgSvrId,



                request.WeChatId,



                DateTime.UtcNow.Ticks);



            return Ok(result);



        }







        [HttpPost("talk-to-friend")]



        public async Task<IActionResult> SendTalkToFriend([FromBody] TalkToFriendRequest request)



        {



            var normalizedContentType = NormalizeTalkToFriendContentType(request.ContentType, request.Content);

            var result = await _clientTaskService.SendTalkToFriendTaskAsync(



                request.ConnectionId,



                request.FriendWxId,



                request.Content,



                (Jubo.JuLiao.IM.Wx.Proto.EnumContentType)normalizedContentType,



                request.AtIds);



            return Ok(result);



        }

        [HttpPost("add-friend")]



        public async Task<IActionResult> SendAddFriend([FromBody] AddFriendRequest request)



        {



            var result = await _clientTaskService.SendAddFriendWithSceneTaskAsync(



                request.ConnectionId,



                request.FriendWxId,



                request.Message,



                request.Remark,



                request.Label,



                request.Scene,



                request.Permission,



                request.VerificationImagePath);



            return Ok(new { success = result });



        }







        [HttpPost("add-friend-in-chatroom")]



        public async Task<IActionResult> SendAddFriendInChatRoom([FromBody] AddFriendInChatRoomRequest request)



        {



            var result = await _clientTaskService.SendAddFriendInChatRoomTaskAsync(



                request.ConnectionId,



                request.ChatRoomId,



                request.FriendId,



                request.Message,



                request.Remark,



                request.Permission);



            return Ok(new { success = result });



        }







        [HttpPost("sync-chatrooms/{connectionId}")]



        public async Task<IActionResult> SendSyncChatRooms(string connectionId, [FromQuery] int flag = 0, [FromQuery] string weChatId = "")



        {



            var roomTaskId = DateTime.UtcNow.Ticks;



            var conversationTaskId = roomTaskId + 1;



            var roomSent = await _clientTaskService.SendTriggerChatRoomPushTaskAsync(connectionId, roomTaskId, flag, weChatId);



            var conversationSent = await _clientTaskService.SendTriggerConversationPushTaskAsync(



                connectionId,



                withName: true,



                limit: 100,



                taskId: conversationTaskId);



            _logger.LogInformation(



                "ClientTask sync-chatrooms dispatched: ConnectionId={ConnectionId}, RoomTaskId={RoomTaskId}, RoomSent={RoomSent}, ConversationTaskId={ConversationTaskId}, ConversationSent={ConversationSent}",



                connectionId,



                roomTaskId,



                roomSent,



                conversationTaskId,



                conversationSent);



            return Ok(new { success = roomSent || conversationSent, roomSent, conversationSent, roomTaskId, conversationTaskId });



        }







        [HttpPost("group-send-history/{connectionId}")]



        public async Task<IActionResult> SendGroupSendHistory(string connectionId, [FromQuery] long endTime = 0, [FromQuery] string weChatId = "")



        {



            var result = await _clientTaskService.SendGetGroupSendHistoryTaskAsync(connectionId, endTime: endTime, weChatId: weChatId);



            return Ok(new { success = result });



        }



        [HttpPost("delete-friend")]



        public async Task<IActionResult> SendDeleteFriend([FromBody] DeleteFriendRequest request)



        {



            var result = await _clientTaskService.SendDeleteFriendTaskAsync(



                request.ConnectionId,



                request.FriendId,



                DateTime.UtcNow.Ticks);



            return Ok(result);



        }







        [HttpPost("accept-friend-request")]



        public async Task<IActionResult> SendAcceptFriendRequest([FromBody] AcceptFriendRequest request)



        {



            var result = await _clientTaskService.SendAcceptFriendAddRequestTaskAsync(



                request.ConnectionId,



                request.FriendId,



                request.FriendNick,



                DateTime.UtcNow.Ticks,



                System.Enum.IsDefined(typeof(Jubo.JuLiao.IM.Wx.Proto.AcceptFriendAddRequestTaskMessage.Types.EnumFriendAddOperation), request.Operation)
                    ? (Jubo.JuLiao.IM.Wx.Proto.AcceptFriendAddRequestTaskMessage.Types.EnumFriendAddOperation)request.Operation
                    : Jubo.JuLiao.IM.Wx.Proto.AcceptFriendAddRequestTaskMessage.Types.EnumFriendAddOperation.Accept,



                request.Remark,



                request.ReplyMsg,



                request.AddWithWW,



                request.OnlyWW,



                request.Permission,



                request.WeChatId);



            return Ok(result);



        }





        [HttpPost("add-friends-by-phone")]



        public async Task<IActionResult> SendAddFriendsByPhone([FromBody] AddFriendsByPhoneRequest request)
        {
            var normalizedPhones = (request.Phones ?? new List<string>())
                .Where(phone => !string.IsNullOrWhiteSpace(phone))
                .Select(phone => phone.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedPhones.Count == 0)
            {
                return Ok(TaskResult.Fail("手机号为空"));
            }

            if (normalizedPhones.Count == 1)
            {
                var singleResult = await _clientTaskService.SendAddFriendsTaskAsync(
                    request.ConnectionId,
                    normalizedPhones,
                    request.Message,
                    request.Remark,
                    request.Label,
                    request.Permission,
                    DateTime.UtcNow.Ticks,
                    request.WeChatId);

                return Ok(singleResult);
            }

            // 低层调试 API 也做逐条下发兜底，避免 Android 只读取 Phones[0] 时丢弃后续手机号。
            var baseTaskId = DateTime.UtcNow.Ticks;
            var successCount = 0;
            var errors = new List<string>();

            for (var index = 0; index < normalizedPhones.Count; index++)
            {
                var phone = normalizedPhones[index];
                var taskId = baseTaskId + index;
                var result = await _clientTaskService.SendAddFriendsTaskAsync(
                    request.ConnectionId,
                    new[] { phone },
                    request.Message,
                    request.Remark,
                    request.Label,
                    request.Permission,
                    taskId,
                    request.WeChatId);

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
                var summary = TaskResult.Ok(baseTaskId, $"手机号加好友已逐条下发 {successCount}/{normalizedPhones.Count}，等待客户端回执");
                return Ok(summary);
            }

            return Ok(TaskResult.Fail(baseTaskId, $"手机号加好友全部下发失败：{string.Join("；", errors)}"));
        }





        [HttpPost("add-friend-from-phonebook")]



        public async Task<IActionResult> SendAddFriendFromPhonebook([FromBody] AddFriendFromPhonebookRequest request)



        {



            var result = await _clientTaskService.SendAddFriendFromPhonebookTaskAsync(



                request.ConnectionId,



                request.Message,



                request.Count,



                request.Index,



                DateTime.UtcNow.Ticks,



                request.Reset,



                request.WeChatId);



            return Ok(result);



        }





        [HttpPost("add-friend-name-card")]



        public async Task<IActionResult> SendAddFriendNameCard([FromBody] AddFriendNameCardRequest request)



        {



            var result = await _clientTaskService.SendAddFriendNameCardTaskAsync(



                request.ConnectionId,



                request.MsgSvrId,



                request.Message,



                request.Remark,



                DateTime.UtcNow.Ticks,



                request.WeChatId);



            return Ok(result);



        }





        [HttpPost("send-friend-verify")]



        public async Task<IActionResult> SendFriendVerify([FromBody] SendFriendVerifyRequest request)



        {



            var result = await _clientTaskService.SendFriendVerifyTaskAsync(



                request.ConnectionId,



                request.FriendId,



                request.Message,



                DateTime.UtcNow.Ticks,



                request.WeChatId);



            return Ok(result);



        }







        [HttpPost("modify-friend-memo")]



        public async Task<IActionResult> SendModifyFriendMemo([FromBody] ModifyFriendMemoRequest request)



        {



            var result = await _clientTaskService.SendModifyFriendMemoTaskAsync(



                request.ConnectionId,



                request.FriendId,



                request.Memo,



                request.Desc,



                request.Phone,



                request.DelFlag,



                DateTime.UtcNow.Ticks);



            return Ok(result);



        }







        [HttpPost("set-friend-permission")]



        public async Task<IActionResult> SendSetFriendPermission([FromBody] SetFriendPermissionRequest request)



        {



            var result = await _clientTaskService.SendSetFriendPermissionTaskAsync(



                request.ConnectionId,



                request.FriendId,



                request.PermissionMask,



                DateTime.UtcNow.Ticks);



            return Ok(result);



        }







        [HttpPost("send-multi-picture")]



        public async Task<IActionResult> SendMultiPicture([FromBody] MultiPictureRequest request)



        {

            // 兼容旧 HTTP 入口：Android 15/62203 上 SendMultiPictureTask 可能走系统相册代理链，

            // 出现“客户端回成功但微信没有实际发图”。服务层已改为逐张 TalkToFriendTask(Picture)。



            var result = await _clientTaskService.SendMultiPictureTaskAsync(



                request.ConnectionId,



                request.FriendWxId,



                request.ImageUrls,



                DateTime.UtcNow.Ticks);



            return Ok(result);



        }







        [HttpPost("group-send")]



        public async Task<IActionResult> SendGroupSend([FromBody] GroupSendRequest request)



        {



            var result = await _clientTaskService.SendWeChatGroupSendTaskAsync(



                request.ConnectionId,



                request.FriendIds,



                request.Content,



                request.ContentType,



                request.Duration,



                request.Original,



                DateTime.UtcNow.Ticks);



            return Ok(result);



        }







        [HttpPost("chatroom-action")]



        public async Task<IActionResult> SendChatRoomAction([FromBody] ChatRoomActionRequest request)



        {



            var result = await _clientTaskService.SendChatRoomActionTaskAsync(



                request.ConnectionId,



                request.ChatRoomId,



                (Jubo.JuLiao.IM.Wx.Proto.EnumChatRoomAction)request.Action,



                request.Content,



                request.IntValue,



                DateTime.UtcNow.Ticks);



            return Ok(result);



        }







        [HttpPost("agree-join-chatroom")]



        public async Task<IActionResult> SendAgreeJoinChatRoom([FromBody] AgreeJoinChatRoomRequest request)



        {



            var result = await _clientTaskService.SendAgreeJoinChatRoomTaskAsync(



                request.ConnectionId,



                request.Talker,



                request.MsgSvrId,



                request.MsgContent,



                DateTime.UtcNow.Ticks);



            return Ok(result);



        }







        [HttpPost("sync-moments/{connectionId}")]



        public async Task<IActionResult> SendSyncMoments(
            string connectionId,
            [FromQuery] long startTime = 0,
            [FromQuery] string circleIds = "",
            [FromQuery] string weChatId = "")



        {



            var parsedCircleIds = ParseLongCsv(circleIds);
            var result = await _clientTaskService.SendTriggerCirclePushTaskAsync(
                connectionId,
                DateTime.UtcNow.Ticks,
                weChatId,
                startTime,
                parsedCircleIds);



            return Ok(new { success = result });



        }







        [HttpPost("post-moment")]



        public async Task<IActionResult> SendPostMoment([FromBody] PostMomentRequest request)



        {



            var payload = request.Payload ?? MomentPostRequestDto.FromLegacy(request.Content, request.ImageUrls);
            var deviceUuid = request.DeviceUuid?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(deviceUuid))
            {
                var connection = await _connectionManager.GetConnectionAsync(request.ConnectionId?.Trim() ?? string.Empty);
                deviceUuid = connection?.deviceUuid?.Trim() ?? connection?.deviceInfo?.Trim() ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(deviceUuid))
            {
                return BadRequest(SCRM.SHARED.Models.Dtos.TaskResult.Fail("无法从 DeviceUuid 或 ConnectionId 解析设备"));
            }

            // 调试 REST 入口也必须走 CrmService 的设备归属、敏感内容、moment.post 权限和账号绑定管线，
            // 禁止用 ConnectionId + Payload 直接绕过高级发圈安全边界。
            var result = await _crmService.PostMomentAdvancedAsync(deviceUuid, payload);



            return Ok(result);



        }







        [HttpPost("delete-moment")]



        public async Task<IActionResult> SendDeleteMoment([FromBody] MomentDeleteRequest request)



        {



            var weChatId = await ResolveWeChatIdByConnectionIdAsync(request.ConnectionId);



            if (string.IsNullOrEmpty(weChatId)) return BadRequest(new { success = false, message = "WeChat account not found" });



            var result = await _clientTaskService.SendDeleteSNSNewsTaskAsync(request.ConnectionId, weChatId, request.CircleId, DateTime.UtcNow.Ticks);



            return Ok(result);



        }







        [HttpPost("delete-moment-comment")]



        public async Task<IActionResult> SendDeleteMomentComment([FromBody] MomentCommentDeleteRequest request)



        {



            var weChatId = await ResolveWeChatIdByConnectionIdAsync(request.ConnectionId);



            if (string.IsNullOrEmpty(weChatId)) return BadRequest(new { success = false, message = "WeChat account not found" });



            var result = await _clientTaskService.SendCircleCommentDeleteTaskAsync(request.ConnectionId, weChatId, request.CircleId, request.CommentId, request.PublishTime, DateTime.UtcNow.Ticks);



            return Ok(result);



        }







        [HttpPost("reply-moment-comment")]



        public async Task<IActionResult> SendReplyMomentComment([FromBody] MomentCommentReplyRequest request)



        {



            var weChatId = await ResolveWeChatIdByConnectionIdAsync(request.ConnectionId);



            if (string.IsNullOrEmpty(weChatId)) return BadRequest(new { success = false, message = "WeChat account not found" });



            var result = await _clientTaskService.SendCircleCommentReplyTaskAsync(request.ConnectionId, weChatId, request.CircleId, request.ToWeChatId, request.Content, request.ReplyCommentId, request.IsResend, DateTime.UtcNow.Ticks);



            return Ok(result);



        }







        [HttpPost("pull-friend-moments")]



        public async Task<IActionResult> SendPullFriendMoments([FromBody] MomentPullFriendRequest request)



        {



            var weChatId = await ResolveWeChatIdByConnectionIdAsync(request.ConnectionId);



            if (string.IsNullOrEmpty(weChatId)) return BadRequest(new { success = false, message = "WeChat account not found" });



            var result = await _clientTaskService.SendPullFriendCircleTaskAsync(request.ConnectionId, weChatId, request.FriendId, request.RefSnsId, request.Count, request.StartTime, request.RefTime, DateTime.UtcNow.Ticks);



            return Ok(new { success = result });



        }







        [HttpPost("pull-moment-detail")]



        public async Task<IActionResult> SendPullMomentDetail([FromBody] MomentDetailRequest request)



        {



            var weChatId = await ResolveWeChatIdByConnectionIdAsync(request.ConnectionId);



            if (string.IsNullOrEmpty(weChatId)) return BadRequest(new { success = false, message = "WeChat account not found" });



            var result = await _clientTaskService.SendPullCircleDetailTaskAsync(request.ConnectionId, weChatId, request.CircleId, request.GetBigMap);



            return Ok(new { success = result });



        }







        [HttpPost("sync-moment-messages")]



        public async Task<IActionResult> SendSyncMomentMessages([FromBody] MomentMsgSyncRequest request)



        {



            var weChatId = await ResolveWeChatIdByConnectionIdAsync(request.ConnectionId);



            if (string.IsNullOrEmpty(weChatId)) return BadRequest(new { success = false, message = "WeChat account not found" });



            var result = await _clientTaskService.SendTriggerCircleMsgPushTaskAsync(request.ConnectionId, weChatId, request.OnlyComment, request.GetAll, DateTime.UtcNow.Ticks);



            return Ok(new { success = result });



        }

        /// <summary>
        /// 下发朋友圈一键点赞任务。
        /// </summary>
        [HttpPost("one-key-like-moments")]
        public async Task<IActionResult> SendOneKeyLikeMoments([FromBody] OneKeyLikeMomentRequest request)
        {
            var weChatId = string.IsNullOrWhiteSpace(request.WeChatId)
                ? await ResolveWeChatIdByConnectionIdAsync(request.ConnectionId)
                : request.WeChatId;
            if (string.IsNullOrEmpty(weChatId)) return BadRequest(new { success = false, message = "WeChat account not found" });

            var result = await _clientTaskService.SendOneKeyLikeTaskAsync(
                request.ConnectionId,
                DateTime.UtcNow.Ticks,
                weChatId,
                request.Rate,
                request.Num,
                request.EndTime,
                request.TimeOut);
            return Ok(result);
        }







        [HttpPost("mark-moment-message-read")]



        public async Task<IActionResult> SendMarkMomentMessageRead([FromBody] MomentMsgReadRequest request)



        {



            var weChatId = await ResolveWeChatIdByConnectionIdAsync(request.ConnectionId);



            if (string.IsNullOrEmpty(weChatId)) return BadRequest(new { success = false, message = "WeChat account not found" });



            var result = await _clientTaskService.SendCircleMsgReadTaskAsync(request.ConnectionId, weChatId, request.CircleId, request.CommentId);



            return Ok(new { success = result });



        }







        [HttpPost("clear-moment-message")]



        public async Task<IActionResult> SendClearMomentMessage([FromBody] MomentMsgClearRequest request)



        {



            var weChatId = await ResolveWeChatIdByConnectionIdAsync(request.ConnectionId);



            if (string.IsNullOrEmpty(weChatId)) return BadRequest(new { success = false, message = "WeChat account not found" });



            var result = await _clientTaskService.SendCircleMsgClearTaskAsync(request.ConnectionId, weChatId, request.CircleId, request.CommentId, request.IsRead);



            return Ok(new { success = result });



        }







        [HttpPost("sph-mention/{connectionId}")]



        public async Task<IActionResult> SendSphGetMention(string connectionId, [FromBody] SphGetMentionRequest request)



        {



            var result = await _clientTaskService.SendSphGetMentionTaskAsync(connectionId, request.LastLikeId, request.LastCommentId, request.LastFollowId, DateTime.UtcNow.Ticks);



            return Ok(new { success = result });



        }







        [HttpPost("sph-get-comment")]



        public async Task<IActionResult> SendSphGetComment([FromBody] SphGetCommentRequest request)



        {



            var result = await _clientTaskService.SendSphGetCommentTaskAsync(



                request.ConnectionId,



                request.FeedId,



                request.NonceId,



                request.FeedAuth,



                request.RefCommentId,



                request.ReplyCommentId,



                request.SortType,



                DateTime.UtcNow.Ticks);



            return Ok(new { success = result });



        }







        [HttpPost("sph-user-page")]



        public async Task<IActionResult> SendSphUserPage([FromBody] SphUserPageRequest request)



        {



            var result = await _clientTaskService.SendSphUserPageTaskAsync(request.ConnectionId, request.SphUserName, DateTime.UtcNow.Ticks);



            return Ok(new { success = result });



        }







        [HttpPost("sph-post")]



        public async Task<IActionResult> SendSphPost([FromBody] SphPostRequest request)



        {



            var result = await _clientTaskService.SendSphPostTaskAsync(request.ConnectionId, request.Content, request.Medias, request.MediaType, request.Cover, DateTime.UtcNow.Ticks);



            return Ok(result);



        }







        [HttpPost("sph-comment")]



        public async Task<IActionResult> SendSphComment([FromBody] SphCommentRequest request)



        {



            var result = await _clientTaskService.SendSphCommentTaskAsync(request.ConnectionId, request.FeedId, request.NonceId, request.FeedAuth, request.Type, request.Content, request.Media, request.ReplyCommentId, request.ReplyUsername, DateTime.UtcNow.Ticks);



            return Ok(result);



        }







        [HttpPost("sph-like")]



        public async Task<IActionResult> SendSphLike([FromBody] SphLikeRequest request)



        {



            var result = await _clientTaskService.SendSphLikeTaskAsync(request.ConnectionId, request.FeedId, request.Type, request.IsCancel, DateTime.UtcNow.Ticks);



            return Ok(result);



        }







        [HttpPost("sph-del-comment")]



        public async Task<IActionResult> SendSphDelComment([FromBody] SphDelCommentRequest request)



        {



            var result = await _clientTaskService.SendSphDelCommentTaskAsync(request.ConnectionId, request.FeedId, request.CommentId, DateTime.UtcNow.Ticks);



            return Ok(result);



        }











        /// <summary>

        /// 查询指定设备最近的视频号提及历史。

        /// </summary>

        [HttpGet("finder/mention-history")]

        public async Task<IActionResult> GetFinderMentionHistory(

            [FromQuery] string deviceUuid,

            [FromQuery] int count = 10,

            [FromQuery] bool? success = null,

            [FromQuery] long? taskId = null,

            [FromQuery] DateTimeOffset? receivedFrom = null,

            [FromQuery] DateTimeOffset? receivedTo = null)

        {

            if (string.IsNullOrWhiteSpace(deviceUuid))

            {

                return BadRequest(new { success = false, message = "deviceUuid 不能为空。" });

            }



            var normalizedCount = NormalizeFinderHistoryCount(count);

            var items = await _crmService.GetFinderMentionHistoryAsync(deviceUuid, normalizedCount, success, taskId, receivedFrom, receivedTo);

            return Ok(new FinderHistoryQueryResponse<FinderMentionNoticeDto>

            {

                DeviceUuid = deviceUuid,

                ResultType = "mention",

                RequestedCount = normalizedCount,

                ReturnedCount = items.Count,

                Success = success,

                TaskId = taskId,

                ReceivedFrom = receivedFrom,

                ReceivedTo = receivedTo,

                Items = items

            });

        }



        /// <summary>

        /// 查询指定设备最近的视频号用户页历史。

        /// </summary>

        [HttpGet("finder/userpage-history")]

        public async Task<IActionResult> GetFinderUserPageHistory(

            [FromQuery] string deviceUuid,

            [FromQuery] int count = 10,

            [FromQuery] bool? success = null,

            [FromQuery] long? taskId = null,

            [FromQuery] DateTimeOffset? receivedFrom = null,

            [FromQuery] DateTimeOffset? receivedTo = null)

        {

            if (string.IsNullOrWhiteSpace(deviceUuid))

            {

                return BadRequest(new { success = false, message = "deviceUuid 不能为空。" });

            }



            var normalizedCount = NormalizeFinderHistoryCount(count);

            var items = await _crmService.GetFinderUserPageHistoryAsync(deviceUuid, normalizedCount, success, taskId, receivedFrom, receivedTo);

            return Ok(new FinderHistoryQueryResponse<FinderUserPageDto>

            {

                DeviceUuid = deviceUuid,

                ResultType = "userpage",

                RequestedCount = normalizedCount,

                ReturnedCount = items.Count,

                Success = success,

                TaskId = taskId,

                ReceivedFrom = receivedFrom,

                ReceivedTo = receivedTo,

                Items = items

            });

        }



        /// <summary>

        /// 查询指定设备最近的视频号评论历史。

        /// </summary>

        [HttpGet("finder/comment-history")]

        public async Task<IActionResult> GetFinderCommentHistory(

            [FromQuery] string deviceUuid,

            [FromQuery] int count = 10,

            [FromQuery] bool? success = null,

            [FromQuery] long? taskId = null,

            [FromQuery] DateTimeOffset? receivedFrom = null,

            [FromQuery] DateTimeOffset? receivedTo = null)

        {

            if (string.IsNullOrWhiteSpace(deviceUuid))

            {

                return BadRequest(new { success = false, message = "deviceUuid 不能为空。" });

            }



            var normalizedCount = NormalizeFinderHistoryCount(count);

            var items = await _crmService.GetFinderCommentHistoryAsync(deviceUuid, normalizedCount, success, taskId, receivedFrom, receivedTo);

            return Ok(new FinderHistoryQueryResponse<FinderCommentListDto>

            {

                DeviceUuid = deviceUuid,

                ResultType = "comment",

                RequestedCount = normalizedCount,

                ReturnedCount = items.Count,

                Success = success,

                TaskId = taskId,

                ReceivedFrom = receivedFrom,

                ReceivedTo = receivedTo,

                Items = items

            });

        }



        /// <summary>

        /// 导出指定设备最近的视频号历史为 JSON 文件，便于回归复盘。

        /// </summary>

        [HttpGet("finder/history-export")]

        public async Task<IActionResult> ExportFinderHistory(

            [FromQuery] string deviceUuid,

            [FromQuery] int count = 10,

            [FromQuery] bool? success = null,

            [FromQuery] long? taskId = null,

            [FromQuery] DateTimeOffset? receivedFrom = null,

            [FromQuery] DateTimeOffset? receivedTo = null)

        {

            var normalizedCount = NormalizeFinderHistoryCount(count);
            var exportPayload = await _crmService.ExportFinderHistoryAsync(deviceUuid, normalizedCount, success, taskId, receivedFrom, receivedTo);
            if (!exportPayload.success)
            {
                if (string.IsNullOrWhiteSpace(deviceUuid))
                {
                    return BadRequest(new { success = false, message = exportPayload.message });
                }

                return StatusCode(403, new { success = false, message = exportPayload.message });
            }



            var json = JsonSerializer.Serialize(exportPayload, FinderHistoryJsonOptions);

            var fileName = BuildFinderHistoryExportFileName(deviceUuid);

            return File(Encoding.UTF8.GetBytes(json), "application/json; charset=utf-8", fileName);

        }



        private static int NormalizeFinderHistoryCount(int count)



        {



            if (count <= 0)



            {



                return 10;



            }







            return Math.Min(count, 100);



        }







        private static string BuildFinderHistoryExportFileName(string deviceUuid)



        {



            var safeDeviceUuid = deviceUuid;



            foreach (var invalidChar in System.IO.Path.GetInvalidFileNameChars())



            {



                safeDeviceUuid = safeDeviceUuid.Replace(invalidChar, '_');



            }







            return $"finder-history-{safeDeviceUuid}-{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.json";



        }







        private async Task<string> ResolveWeChatIdByConnectionIdAsync(string connectionId)



        {



            var connection = await _connectionManager.GetConnectionAsync(connectionId);



            if (connection == null || string.IsNullOrWhiteSpace(connection.deviceUuid))



            {



                return string.Empty;



            }







            var account = await _db.WechatAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.clientUuid == connection.deviceUuid && !a.isDeleted && a.accountStatus == 1);



            account ??= await _db.WechatAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.clientUuid == connection.deviceUuid && !a.isDeleted);



            return account?.wxid ?? string.Empty;



        }








        /// <summary>
        /// 归一化聊天内容类型，兼容旧 HTTP 调用按文本提交图片/视频/文件 URL。
        /// </summary>
        private static int NormalizeTalkToFriendContentType(int type, string? content)
        {
            if (type != (int)Jubo.JuLiao.IM.Wx.Proto.EnumContentType.UnknownContent
                && type != (int)Jubo.JuLiao.IM.Wx.Proto.EnumContentType.Text)
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
                return (int)Jubo.JuLiao.IM.Wx.Proto.EnumContentType.Picture;
            }

            if (lower.EndsWith(".mp4") || lower.EndsWith(".mov") || lower.EndsWith(".m4v")
                || lower.EndsWith(".3gp") || lower.EndsWith(".avi")
                || lower.EndsWith(".mkv") || lower.EndsWith(".webm"))
            {
                return (int)Jubo.JuLiao.IM.Wx.Proto.EnumContentType.Video;
            }

            if (text.StartsWith("{", StringComparison.Ordinal) && text.Contains("\"url\"", StringComparison.OrdinalIgnoreCase))
            {
                return (int)Jubo.JuLiao.IM.Wx.Proto.EnumContentType.File;
            }

            return type == (int)Jubo.JuLiao.IM.Wx.Proto.EnumContentType.UnknownContent
                ? (int)Jubo.JuLiao.IM.Wx.Proto.EnumContentType.Text
                : type;
        }

        /// <summary>
        /// 解析逗号分隔的朋友圈 ID 列表。
        /// <para>微信 snsId 以 long 传输时可能为负数，只有 0 才表示无效。</para>
        /// </summary>
        private static List<long> ParseLongCsv(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new List<long>();
            }

            return value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(item => long.TryParse(item, out var parsed) ? parsed : 0L)
                .Where(item => item != 0)
                .Distinct()
                .ToList();
        }

        private static bool IsValidPaymentPassword(string? passwd)
        {
            var value = passwd?.Trim() ?? string.Empty;
            return value.Length == 6 && value.All(char.IsDigit);
        }

    }











    public class DeleteFriendRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public string FriendId { get; set; } = string.Empty;



    }







    public class JoinGroupByQrRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public string WeChatId { get; set; } = string.Empty;



        public string QrUrl { get; set; } = string.Empty;



        public string QrContent { get; set; } = string.Empty;



    }







    public class PullChatRoomQrCodeRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public string WeChatId { get; set; } = string.Empty;



        public string ChatRoomId { get; set; } = string.Empty;



    }







    public class ChatRoomInviteApproveRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public string WeChatId { get; set; } = string.Empty;



        public string RoomId { get; set; } = string.Empty;



        public long MsgSvrId { get; set; } = 0;

        /// <summary>
        /// 62203 群邀请确认使用的消息 ID；为空时兼容旧字段 MsgSvrId。
        /// </summary>
        public long MsgId { get; set; } = 0;



        public string MsgContent { get; set; } = string.Empty;



    }







    public class SendJielongRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public string WeChatId { get; set; } = string.Empty;



        public string ChatRoomId { get; set; } = string.Empty;



        public string Content { get; set; } = string.Empty;



        public string Title { get; set; } = string.Empty;



        public string Sample { get; set; } = string.Empty;



        public string Memo { get; set; } = string.Empty;



        public long MsgSvrId { get; set; } = 0;



    }







    public class AcceptFriendRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public string FriendId { get; set; } = string.Empty;



        public string FriendNick { get; set; } = string.Empty;



        /// <summary>目标微信账号 wxid；为空时由客户端使用当前登录账号。</summary>



        public string WeChatId { get; set; } = string.Empty;



        /// <summary>操作类型：1=接受，2=拒绝。</summary>



        public int Operation { get; set; } = 1;



        /// <summary>接受好友请求时设置的备注。</summary>



        public string Remark { get; set; } = string.Empty;



        /// <summary>拒绝好友请求时返回给对方的说明。</summary>



        public string ReplyMsg { get; set; } = string.Empty;



        /// <summary>是否同时添加企业微信。</summary>



        public bool AddWithWW { get; set; }



        /// <summary>是否仅添加微信。</summary>



        public bool OnlyWW { get; set; }



        /// <summary>权限位：8=仅聊天，2=不让他看我朋友圈，1=不看他朋友圈。</summary>



        public int Permission { get; set; }



    }



    public class AddFriendsByPhoneRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public string WeChatId { get; set; } = string.Empty;



        public List<string> Phones { get; set; } = new();



        public string Message { get; set; } = string.Empty;



        public string Remark { get; set; } = string.Empty;



        public string Label { get; set; } = string.Empty;



        public int Permission { get; set; }



    }



    public class AddFriendFromPhonebookRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public string WeChatId { get; set; } = string.Empty;



        public string Message { get; set; } = string.Empty;



        public int Count { get; set; } = 1;



        public int Index { get; set; }



        /// <summary>62203/当前 Android 分发层暂未确认读取该字段，保留协议兼容。</summary>



        public bool Reset { get; set; }



    }



    public class AddFriendNameCardRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public string WeChatId { get; set; } = string.Empty;



        public long MsgSvrId { get; set; }



        public string Message { get; set; } = string.Empty;



        public string Remark { get; set; } = string.Empty;



    }



    public class SendFriendVerifyRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public string WeChatId { get; set; } = string.Empty;



        public string FriendId { get; set; } = string.Empty;



        public string Message { get; set; } = string.Empty;



    }







    public class ModifyFriendMemoRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public string FriendId { get; set; } = string.Empty;



        public string Memo { get; set; } = string.Empty;



        public string Desc { get; set; } = string.Empty;



        public string Phone { get; set; } = string.Empty;



        public int DelFlag { get; set; } = 0;



    }







    public class SetFriendPermissionRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public string FriendId { get; set; } = string.Empty;







        /// <summary>



        /// 权限位掩码：8=仅聊天，2=不让他看我朋友圈，1=不看他朋友圈。



        /// </summary>



        public int PermissionMask { get; set; } = 0;



    }







    public class MultiPictureRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public string FriendWxId { get; set; } = string.Empty;



        public List<string> ImageUrls { get; set; } = new();



    }







    public class GroupSendRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public List<string> FriendIds { get; set; } = new();



        public string Content { get; set; } = string.Empty;



        /// <summary>群发内容类型，沿用 WeChatGroupSendTask 协议枚举；默认 0=Text。</summary>



        public int ContentType { get; set; } = 0;



        public int Duration { get; set; } = 0;



        public bool Original { get; set; } = false;



    }







    public class ChatRoomActionRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public string ChatRoomId { get; set; } = string.Empty;



        /// <summary>群操作枚举值，沿用 EnumChatRoomAction。</summary>



        public int Action { get; set; } = 0;



        public string Content { get; set; } = string.Empty;



        public int IntValue { get; set; } = 0;



    }







    public class AgreeJoinChatRoomRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public string Talker { get; set; } = string.Empty;



        public long MsgSvrId { get; set; } = 0;



        public string MsgContent { get; set; } = string.Empty;



    }







    public class PostMomentRequest



    {



        public string ConnectionId { get; set; } = string.Empty;

        /// <summary>
        /// 优先使用设备 UUID；ConnectionId 仅作为兼容旧调试入口的反查字段。
        /// </summary>
        public string DeviceUuid { get; set; } = string.Empty;



        public string Content { get; set; } = string.Empty;



        public List<string> ImageUrls { get; set; } = new();



        /// <summary>
        /// 高级朋友圈协议载荷；为空时兼容旧的 Content + ImageUrls。
        /// </summary>
        public MomentPostRequestDto? Payload { get; set; }



    }







    public class MomentDeleteRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public long CircleId { get; set; }



    }







    public class MomentCommentDeleteRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public long CircleId { get; set; }



        public long CommentId { get; set; }



        public long PublishTime { get; set; }



    }







    public class MomentCommentReplyRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public long CircleId { get; set; }



        public string ToWeChatId { get; set; } = string.Empty;



        public string Content { get; set; } = string.Empty;



        public long ReplyCommentId { get; set; }



        public bool IsResend { get; set; } = false;



    }







    public class MomentPullFriendRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public string FriendId { get; set; } = string.Empty;



        public long RefSnsId { get; set; } = 0;



        public int Count { get; set; } = 20;



        public long StartTime { get; set; } = 0;



        public long RefTime { get; set; } = 0;



    }







    public class MomentDetailRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public long CircleId { get; set; }



        public bool GetBigMap { get; set; } = false;



    }







    public class MomentMsgSyncRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public bool OnlyComment { get; set; } = false;



        public bool GetAll { get; set; } = true;



    }







    public class MomentMsgReadRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public long CircleId { get; set; }



        public int CommentId { get; set; } = 0;



    }







    public class MomentMsgClearRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public long CircleId { get; set; }



        public int CommentId { get; set; } = 0;



        public bool IsRead { get; set; } = true;



    }







    public class SphGetMentionRequest



    {



        public long LastLikeId { get; set; } = 0;



        public long LastCommentId { get; set; } = 0;



        public long LastFollowId { get; set; } = 0;



    }







    public class SphGetCommentRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public long FeedId { get; set; }



        public string NonceId { get; set; } = string.Empty;



        public string FeedAuth { get; set; } = string.Empty;



        public long RefCommentId { get; set; } = 0;



        public long ReplyCommentId { get; set; } = 0;



        public int SortType { get; set; } = 0;



    }







    public class SphUserPageRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public string SphUserName { get; set; } = string.Empty;



    }







    public class SphPostRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public string Content { get; set; } = string.Empty;



        public List<string> Medias { get; set; } = new();



        public int MediaType { get; set; } = 0;



        public string Cover { get; set; } = string.Empty;



    }







    public class SphCommentRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public long FeedId { get; set; }



        public string NonceId { get; set; } = string.Empty;



        public string FeedAuth { get; set; } = string.Empty;



        public int Type { get; set; }



        public string Content { get; set; } = string.Empty;



        public string Media { get; set; } = string.Empty;



        public long ReplyCommentId { get; set; }



        public string ReplyUsername { get; set; } = string.Empty;



    }







    public class SphLikeRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public long FeedId { get; set; }



        public int Type { get; set; } = 1;



        public bool IsCancel { get; set; } = false;



    }







    public class SphDelCommentRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public long FeedId { get; set; }



        public long CommentId { get; set; }



    }







    public class TalkToFriendRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public string FriendWxId { get; set; } = string.Empty;



        public string Content { get; set; } = string.Empty;



        /// <summary>消息类型，默认 1=Text；可传 2=Image、4=Video、6=Link、8=File、13=WeApp 等协议枚举值。</summary>



        public int ContentType { get; set; } = 1;



        /// <summary>群聊 @ 成员 wxid 列表，多个值用英文逗号分隔。</summary>



        public string AtIds { get; set; } = string.Empty;



    }







    public class AddFriendInChatRoomRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public string ChatRoomId { get; set; } = string.Empty;



        public string FriendId { get; set; } = string.Empty;



        public string Message { get; set; } = string.Empty;



        public string Remark { get; set; } = string.Empty;



        public int Permission { get; set; } = 0;



    }







    public class AddFriendRequest



    {



        public string ConnectionId { get; set; } = string.Empty;



        public string FriendWxId { get; set; } = string.Empty;



        public string Message { get; set; } = string.Empty;



        public int Scene { get; set; } = 3;



        public string Remark { get; set; } = string.Empty;



        public string Label { get; set; } = string.Empty;



        public int Permission { get; set; } = 0;



        public string VerificationImagePath { get; set; } = string.Empty;



    }

    public class TriggerQwUserPushRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        public long TaskId { get; set; }
    }

    public class TriggerChatMsgIdsPushRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        /// <summary>Android 微信 message.createTime 口径的毫秒级开始时间。</summary>
        public long StartTime { get; set; }
        /// <summary>Android 微信 message.createTime 口径的毫秒级结束时间。</summary>
        public long EndTime { get; set; }
        public long TaskId { get; set; }
    }

    public class TriggerHistoryMsgPushRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        public string FriendId { get; set; } = string.Empty;
        public long StartTime { get; set; }
        public long EndTime { get; set; }
        public int Flag { get; set; }
        public int Count { get; set; } = 50;
        public long TaskId { get; set; }
    }

    public class TriggerMessageReadRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        public string FriendId { get; set; } = string.Empty;
        public long TaskId { get; set; }
    }

    public class TriggerUnreadPushRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        public long TaskId { get; set; }
    }

    public class TriggerUnReadRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        public string FriendId { get; set; } = string.Empty;
        public long TaskId { get; set; }
    }

    public class TriggerBizContactPushRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        public long TaskId { get; set; }
    }

    public class TriggerQwConvPushRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        public long StartTime { get; set; }
        public long EndTime { get; set; }
        public int Limit { get; set; } = 100;
        public int Offset { get; set; }
        public long TaskId { get; set; }
    }

    public class TriggerLabelPushRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        public long TaskId { get; set; }
    }

    public class ContactLabelRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        /// <summary>标签名；LabelId=0 时表示创建新标签。</summary>
        public string LabelName { get; set; } = string.Empty;
        /// <summary>微信端标签 ID；0 表示新建。</summary>
        public int LabelId { get; set; }
        /// <summary>需要加入该标签的 wxid CSV。</summary>
        public string AddList { get; set; } = string.Empty;
        /// <summary>需要移出该标签的 wxid CSV。</summary>
        public string DelList { get; set; } = string.Empty;
        public long TaskId { get; set; }
    }

    public class ContactLabelDeleteRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        public int LabelId { get; set; }
        public long TaskId { get; set; }
    }

    public class ContactSetLabelRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        public string FriendId { get; set; } = string.Empty;
        /// <summary>完整标签 ID 集合。</summary>
        public List<int> LabelIds { get; set; } = new();
        public long TaskId { get; set; }
    }

    public class OneKeyLikeMomentRequest
    {
        public string ConnectionId { get; set; } = string.Empty;

        public string WeChatId { get; set; } = string.Empty;

        /// <summary>
        /// 点赞频率/概率参数，默认 100，沿用客户端 62203 字段。
        /// </summary>
        public int Rate { get; set; } = 100;

        /// <summary>
        /// 最大点赞数量，0 表示由客户端默认策略决定。
        /// </summary>
        public int Num { get; set; } = 0;

        /// <summary>
        /// 截止时间戳/结束时间，0 表示不指定。
        /// </summary>
        public int EndTime { get; set; } = 0;

        /// <summary>
        /// 超时时间，0 表示客户端默认。
        /// </summary>
        public int TimeOut { get; set; } = 0;
    }

    public class TriggerConfigPushRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public long TaskId { get; set; }
    }

    public class SetForbiddenWordRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        /// <summary>需要写入 Android 本地违禁词列表的关键词；空数组表示清空。</summary>
        public List<string> Words { get; set; } = new();
    }

    public class PullFriendAddReqListRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        /// <summary>微信 fmessage 请求时间起点；0 表示由客户端使用默认补偿范围。</summary>
        public long StartTime { get; set; }
        /// <summary>是否只拉新增申请。</summary>
        public bool OnlyNew { get; set; } = true;
        /// <summary>是否强制拉全量申请列表。</summary>
        public bool GetAll { get; set; }
        public long TaskId { get; set; }
    }

    public class RequestTalkMsgRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        public long MsgSvrId { get; set; }
        public long TaskId { get; set; }
    }

    public class RequestTalkContentRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        public long MsgSvrId { get; set; }
        public long TaskId { get; set; }
    }

    public class RequestTalkDetailRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        public string FriendId { get; set; } = string.Empty;
        public long MsgId { get; set; }
        public string MsgSvrId { get; set; } = string.Empty;
        public string Md5 { get; set; } = string.Empty;
        public bool GetOriginal { get; set; }
        public long TaskId { get; set; }
    }

    public class VoiceTransTextRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        /// <summary>好友或群会话 wxid。</summary>
        public string FriendId { get; set; } = string.Empty;
        /// <summary>目标语音消息的服务器消息 ID。</summary>
        public long MsgSvrId { get; set; }
        public long TaskId { get; set; }
    }

    public class GetA8KeyRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        public int Type { get; set; }
        /// <summary>需要微信转换 A8Key 的原始 URL。</summary>
        public string Url { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string MsgSvrId { get; set; } = string.Empty;
        public int Reason { get; set; }
        public long TaskId { get; set; }
    }

    public class WechatSettingRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        /// <summary>EnumSettings 数值：0昵称、1头像、2加好友验证、3性别、4地区、5签名。</summary>
        public int Action { get; set; }
        /// <summary>文本参数；地区建议 CN_省份_城市，头像为本地路径或 URL。</summary>
        public string Content { get; set; } = string.Empty;
        /// <summary>整数参数；NeedVerify 用 0/1，ChangeGender 用 1/2。</summary>
        public int IntParam { get; set; }
        public long TaskId { get; set; }
    }

    public class RevokeMessageRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        /// <summary>好友或群会话 wxid。</summary>
        public string FriendId { get; set; } = string.Empty;
        /// <summary>要撤回的服务器消息 ID。</summary>
        public long MsgSvrId { get; set; }
        public long TaskId { get; set; }
    }

    public class ForwardMessageRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        /// <summary>原消息所在会话 wxid。</summary>
        public string Talker { get; set; } = string.Empty;
        /// <summary>要转发的原消息服务器消息 ID。</summary>
        public long MsgSvrId { get; set; }
        /// <summary>目标接收人 wxid 列表，多个目标用逗号分隔。</summary>
        public string FriendIds { get; set; } = string.Empty;
        /// <summary>附加文本/扩展信息。</summary>
        public string ExtMsg { get; set; } = string.Empty;
        public long TaskId { get; set; }
    }

    public class ForwardMultiMessageRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        /// <summary>原消息所在会话 wxid。</summary>
        public string Talker { get; set; } = string.Empty;
        /// <summary>要转发的原消息服务器消息 ID 列表。</summary>
        public List<long> MsgIds { get; set; } = new();
        /// <summary>目标接收人 wxid 列表，多个目标用逗号分隔。</summary>
        public string FriendIds { get; set; } = string.Empty;
        /// <summary>附加文本/扩展信息。</summary>
        public string ExtMsg { get; set; } = string.Empty;
        /// <summary>是否让微信侧生成转发记录。</summary>
        public bool SendRecord { get; set; }
        public long TaskId { get; set; }
    }

    public class ForwardMessageByContentRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        /// <summary>目标接收人 wxid 列表，多个目标用逗号分隔。</summary>
        public string FriendIds { get; set; } = string.Empty;
        /// <summary>原消息服务器消息 ID。</summary>
        public long MsgSvrId { get; set; }
        /// <summary>微信原始消息类型。</summary>
        public int MsgType { get; set; }
        /// <summary>原始消息正文或 XML。</summary>
        public string Content { get; set; } = string.Empty;
        /// <summary>缩略图、封面或扩展缩略数据。</summary>
        public string Thumb { get; set; } = string.Empty;
        /// <summary>转发附加说明。</summary>
        public string ExtMsg { get; set; } = string.Empty;
        public long TaskId { get; set; }
    }

    public class ClearAllChatMsgRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        /// <summary>62203 协议预留标志；当前 Android 侧主要使用 TaskId。</summary>
        public int Flag { get; set; }
        public long TaskId { get; set; }
    }

    public class QueryHbRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        /// <summary>红包 nativeUrl / hbUrl。</summary>
        public string HbUrl { get; set; } = string.Empty;
    }

    public class SendLuckyMoneyRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        /// <summary>接收方 wxid 或群 ID。</summary>
        public string FriendId { get; set; } = string.Empty;
        /// <summary>金额，单位：分。62203 Android 侧校验 1..20000。</summary>
        public int Money { get; set; }
        /// <summary>红包个数，默认 1。</summary>
        public int Number { get; set; } = 1;
        /// <summary>支付密码，仅用于本次下发，不得落库或写日志。</summary>
        public string Passwd { get; set; } = string.Empty;
        /// <summary>祝福语。</summary>
        public string Wish { get; set; } = string.Empty;
        public long TaskId { get; set; }
    }

    public class SyncFriendsByDeviceRequest
    {
        public string DeviceUuid { get; set; } = string.Empty;

        /// <summary>
        /// 当前微信 wxid。留空时服务端按设备当前登录账号兜底。
        /// </summary>
        public string WeChatId { get; set; } = string.Empty;
    }

    public class RefreshWeChatAccountsRequest
    {
        public string DeviceUuid { get; set; } = string.Empty;
    }

    public class RemittanceRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        /// <summary>收款人 wxid。</summary>
        public string FriendId { get; set; } = string.Empty;
        /// <summary>金额，单位：分。</summary>
        public int Money { get; set; }
        /// <summary>支付密码，仅用于本次下发，不得落库或写日志。</summary>
        public string Passwd { get; set; } = string.Empty;
        /// <summary>转账备注。</summary>
        public string Memo { get; set; } = string.Empty;
        /// <summary>群内转账时的群会话 ID；单聊留空。</summary>
        public string RoomId { get; set; } = string.Empty;
        public long TaskId { get; set; }
    }

    public class WechatLogoutRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
    }

    public class DownloadCdnFileRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        public string CdnUrl { get; set; } = string.Empty;
        public string CdnKey { get; set; } = string.Empty;
        /// <summary>CDN 文件类型，使用 CDNFileType 的数值；默认 7=聊天文件。</summary>
        public int FileType { get; set; } = 7;
        public string FileId { get; set; } = string.Empty;
        public string FileFmt { get; set; } = string.Empty;
        public int FileSize { get; set; }
        public long MsgSvrId { get; set; }
        public long TaskId { get; set; }
    }

    public class StartFriendDetectRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        /// <summary>检测时发送给微信好友的探测消息。</summary>
        public string Message { get; set; } = string.Empty;
        /// <summary>是否只检测不删除；默认只检测，避免误删。</summary>
        public bool OnlyCheck { get; set; } = true;
        /// <summary>跳过最近活跃小时数。</summary>
        public int SkipHour { get; set; } = 24;
        /// <summary>检测模式位，沿用 Android/62203 协议。</summary>
        public int Mode { get; set; }
        /// <summary>最大检测数量；0 表示由客户端决定。</summary>
        public int Max { get; set; }
        public long TaskId { get; set; }
    }

    public class StopFriendDetectRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        public long TaskId { get; set; }
    }

    public class GetFriendDetectResultRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WeChatId { get; set; } = string.Empty;
        public long TaskId { get; set; }
    }







    /// <summary>



    /// Finder 历史查询响应。



    /// </summary>



    public class FinderHistoryQueryResponse<TItem>

    {

        public string DeviceUuid { get; set; } = string.Empty;

        public string ResultType { get; set; } = string.Empty;

        public int RequestedCount { get; set; }

        public int ReturnedCount { get; set; }

        public bool? Success { get; set; }

        public long? TaskId { get; set; }

        public DateTimeOffset? ReceivedFrom { get; set; }

        public DateTimeOffset? ReceivedTo { get; set; }

        public List<TItem> Items { get; set; } = new();

    }



}



