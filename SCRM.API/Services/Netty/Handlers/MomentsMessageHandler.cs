using DotNetty.Transport.Channels;
using Jubo.JuLiao.IM.Wx.Proto;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SCRM.API.Hubs;
using SCRM.API.Models.Entities;
using SCRM.API.Services.Core;
using SCRM.API.Services.Netty.Handlers.Abstractions;
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
    /// 朋友圈消息处理器
    /// <para>处理朋友圈动态、评论与点赞通知。</para>
    /// <para>核心功能：</para>
    /// <list type="bullet">
    /// <item>处理朋友圈新动态通知</item>
    /// <item>处理评论/点赞互动通知</item>
    /// <item>同步朋友圈数据</item>
    /// </list>
    /// </summary>
    public class MomentsMessageHandler : MessageHandlerBase
    {
        private const string FinderResultTypeMention = "mention";
        private const string FinderResultTypeUserPage = "userpage";
        private const string FinderResultTypeComment = "comment";
        private const int FinderHistoryLimitPerDevice = 10;
        private readonly ILogger<MomentsMessageHandler> _logger;
        private readonly ApplicationDbContext _db;
        private readonly IHubContext<ClientHub> _hubContext;
        private readonly IEventBus _eventBus;
        private readonly ConnectionManager _connManager;

        public MomentsMessageHandler(
            ILogger<MomentsMessageHandler> logger,
            ApplicationDbContext db,
            IHubContext<ClientHub> hubContext,
            IEventBus eventBus,
            ConnectionManager connManager) : base(logger)
        {
            _logger = logger;
            _db = db;
            _hubContext = hubContext;
            _eventBus = eventBus;
            _connManager = connManager;
        }

        public async Task HandleMessage(TransportMessage message, IChannelHandlerContext context)
        {
            await SendAckAsync(message, context);
            try
            {
                switch (message.MsgType)
                {
                    case EnumMsgType.CirclePushNotice:
                        await HandleCirclePushAsync(message.Content.Unpack<CirclePushNoticeMessage>());
                        break;
                    case EnumMsgType.CircleDetailNotice:
                        await HandleCircleDetailAsync(message.Content.Unpack<CircleDetailNoticeMessage>());
                        break;
                    case EnumMsgType.CircleNewPublishNotice:
                        await HandleCircleNewPublishAsync(message.Content.Unpack<CircleNewPublishNoticeMessage>());
                        break;
                    case EnumMsgType.CircleMsgPushNotice:
                        await HandleCircleMsgPushAsync(message.Content.Unpack<CircleMsgPushNoticeMessage>(), context);
                        break;
                    case EnumMsgType.CircleLikeNotice:
                        await HandleCircleLikeAsync(message.Content.Unpack<CircleLikeNoticeMessage>());
                        break;
                    case EnumMsgType.CircleCommentNotice:
                        await HandleCircleCommentAsync(message.Content.Unpack<CircleCommentNoticeMessage>());
                        break;
                    case EnumMsgType.CircleDelNotice:
                        await HandleCircleDeleteAsync(message.Content.Unpack<CircleDelNoticeMessage>());
                        break;
                    case EnumMsgType.SphMentionListNotice:
                        await HandleSphMentionAsync(message.Content.Unpack<SphMentionListNoticeMessage>());
                        break;
                    case EnumMsgType.SphUserPagePushNotice:
                        await HandleSphUserPageAsync(message.Content.Unpack<SphUserPageNoticeMessage>());
                        break;
                    case EnumMsgType.SphCommentListNotice:
                        await HandleSphCommentListAsync(message.Content.Unpack<SphCommentListNoticeMessage>());
                        break;
                    case EnumMsgType.SphPostTaskResultNotice:
                        _logger.LogInformation("SphPostTaskResultNotice received; current proto only defines enum, no dedicated message body is available yet.");
                        break;
                    default:
                        _logger.LogInformation("Moments Message Received: {Type}", message.MsgType);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Moments/Sph message unpack failed: {Type}", message.MsgType);
            }
        }

        /// <summary>
        /// 批量处理朋友圈时间线推送。
        /// </summary>
        private async Task HandleCirclePushAsync(CirclePushNoticeMessage msg)
        {
            var account = await GetAccountAsync(msg.WeChatId);
            if (account == null)
            {
                return;
            }

            var payloadCircleCount = msg.Circles?.Count ?? 0;
            var declaredCircleCount = msg.Count;
            // 以实际 payload 条数为准；DeclaredCount 只作为诊断字段，避免客户端声明非 0 但正文为空时误判同步成功。
            var effectiveCircleCount = payloadCircleCount;
            var touchedMoments = new List<MomentsTimeline>();
            if (msg.Circles != null)
            {
                foreach (var circle in msg.Circles)
                {
                    touchedMoments.Add(await UpsertMomentAsync(account.wxid, circle));
                }
            }

            await BackfillMomentDisplayNamesAsync(account.wxid, touchedMoments);
            await _db.SaveChangesAsync();
            LogCirclePushMappedMoments(account.wxid, touchedMoments);
            await PublishMomentsAsync(account.clientUuid, account.wxid, touchedMoments);
            await PublishCirclePushTaskResultAsync(account.clientUuid, msg, effectiveCircleCount, payloadCircleCount, declaredCircleCount);

            _logger.LogInformation(
                "CirclePushNotice: WeChatId={WeChatId}, PayloadCount={PayloadCount}, DeclaredCount={DeclaredCount}, EffectiveCount={EffectiveCount}, Page={Page}, TaskId={TaskId}, RetCode={RetCode}, RetTips={RetTips}",
                msg.WeChatId,
                payloadCircleCount,
                declaredCircleCount,
                effectiveCircleCount,
                msg.Page,
                msg.TaskId,
                msg.RetCode,
                msg.RetTips);
        }

        /// <summary>
        /// 处理单条朋友圈详情回包。
        /// </summary>
        private async Task HandleCircleDetailAsync(CircleDetailNoticeMessage msg)
        {
            var account = await GetAccountAsync(msg.WeChatId);
            if (account == null || msg.Circle == null)
            {
                return;
            }

            var moment = await UpsertMomentAsync(account.wxid, msg.Circle);
            await BackfillMomentDisplayNamesAsync(account.wxid, new[] { moment });
            await _db.SaveChangesAsync();
            await PublishMomentsAsync(account.clientUuid, account.wxid, new[] { moment });

            _logger.LogInformation("CircleDetailNotice: WeChatId={WeChatId}, CircleId={CircleId}", msg.WeChatId, msg.Circle.CircleId);
        }

        /// <summary>
        /// 处理新发布朋友圈通知。
        /// </summary>
        private async Task HandleCircleNewPublishAsync(CircleNewPublishNoticeMessage msg)
        {
            var account = await GetAccountAsync(msg.WeChatId);
            if (account == null || msg.Circle == null)
            {
                return;
            }

            var moment = await UpsertMomentAsync(account.wxid, msg.Circle);
            await BackfillMomentDisplayNamesAsync(account.wxid, new[] { moment });
            await _db.SaveChangesAsync();
            await PublishMomentsAsync(account.clientUuid, account.wxid, new[] { moment });

            _logger.LogInformation("CircleNewPublishNotice: WeChatId={WeChatId}, CircleId={CircleId}", msg.WeChatId, msg.Circle.CircleId);
        }

        /// <summary>
        /// 处理朋友圈互动消息（评论/点赞）推送。
        /// </summary>
        private async Task HandleCircleMsgPushAsync(CircleMsgPushNoticeMessage msg, IChannelHandlerContext context)
        {
            var account = await GetAccountAsync(msg.WeChatId);
            if (account == null)
            {
                await PublishTaskResultAsync(context, msg.TaskId, false, $"朋友圈互动消息同步失败：账号不存在 {msg.WeChatId}");
                return;
            }

            var touchedMoments = new List<MomentsTimeline>();
            var circleIds = msg.Comments.Select(c => c.CircleId)
                .Concat(msg.Likes.Select(l => l.CircleId))
                .Where(id => id != 0)
                .Distinct()
                .ToList();

            foreach (var circleId in circleIds)
            {
                var moment = await GetOrCreateMomentAsync(account.wxid, circleId);
                var mergedComments = MergeComments(
                    DeserializeComments(moment.commentsJson),
                    msg.Comments.Where(c => c.CircleId == circleId).Select(comment => MapComment(comment, account.wxid)));
                var mergedLikes = MergeLikes(
                    DeserializeLikes(moment.likesJson),
                    msg.Likes.Where(l => l.CircleId == circleId).Select(MapLike));

                moment.commentsJson = JsonSerializer.Serialize(mergedComments);
                moment.likesJson = JsonSerializer.Serialize(mergedLikes);
                moment.receivedAt = DateTime.UtcNow.Ticks;
                if (moment.createTime == 0)
                {
                    moment.createTime = msg.Comments.Where(c => c.CircleId == circleId).Select(c => c.PublishTime)
                        .Concat(msg.Likes.Where(l => l.CircleId == circleId).Select(l => l.PublishTime))
                        .DefaultIfEmpty(0)
                        .Max();
                }
                if (string.IsNullOrWhiteSpace(moment.userName))
                {
                    moment.userName = account.wxid;
                }

                touchedMoments.Add(moment);
            }

            await BackfillMomentDisplayNamesAsync(account.wxid, touchedMoments);
            await _db.SaveChangesAsync();
            await PublishMomentsAsync(account.clientUuid, account.wxid, touchedMoments);

            _logger.LogInformation(
                "CircleMsgPushNotice: WeChatId={WeChatId}, CommentCount={CommentCount}, LikeCount={LikeCount}, TaskId={TaskId}",
                msg.WeChatId,
                msg.Comments.Count,
                msg.Likes.Count,
                msg.TaskId);

            var resultMessage = msg.Comments.Count == 0 && msg.Likes.Count == 0
                ? "朋友圈互动消息同步已返回 0 条"
                : $"朋友圈互动消息同步完成：评论 {msg.Comments.Count} 条，点赞 {msg.Likes.Count} 条";
            await PublishTaskResultAsync(context, msg.TaskId, true, resultMessage, account.clientUuid);
        }

        /// <summary>
        /// 处理单条朋友圈点赞/取消点赞通知。
        /// <para>安卓端 62203 会在点赞链路直接上报 CircleLikeNotice；这里增量合并 likesJson 并实时推送前端。</para>
        /// </summary>
        private async Task HandleCircleLikeAsync(CircleLikeNoticeMessage msg)
        {
            var account = await GetAccountAsync(msg.WeChatId);
            // 微信 snsId 以 long 传输时可能为负数，只有 0 才表示无效。
            if (account == null || msg.CircleId == 0 || string.IsNullOrWhiteSpace(msg.FriendId))
            {
                return;
            }

            var moment = await GetOrCreateMomentAsync(account.wxid, msg.CircleId);
            var likes = DeserializeLikes(moment.likesJson);
            likes.RemoveAll(item => string.Equals(item.userName, msg.FriendId, StringComparison.OrdinalIgnoreCase));
            if (!msg.IsDelete)
            {
                likes.Add(new MomentLikeDto
                {
                    userName = msg.FriendId,
                    nickName = msg.NickName ?? string.Empty,
                    createTime = msg.PublishTime > 0 ? msg.PublishTime : DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });
            }

            moment.likesJson = JsonSerializer.Serialize(likes.OrderBy(item => item.createTime).ToList());
            moment.receivedAt = DateTime.UtcNow.Ticks;
            if (string.IsNullOrWhiteSpace(moment.userName))
            {
                moment.userName = account.wxid;
            }

            await BackfillMomentDisplayNamesAsync(account.wxid, new[] { moment });
            await _db.SaveChangesAsync();
            await PublishMomentsAsync(account.clientUuid, account.wxid, new[] { moment });

            _logger.LogInformation(
                "CircleLikeNotice: WeChatId={WeChatId}, CircleId={CircleId}, FriendId={FriendId}, IsDelete={IsDelete}",
                msg.WeChatId,
                msg.CircleId,
                msg.FriendId,
                msg.IsDelete);
        }

        /// <summary>
        /// 处理单条朋友圈评论/删评通知。
        /// <para>不绕过朋友圈表结构，只增量维护 commentsJson，随后通过 SignalR 推送当前朋友圈卡片。</para>
        /// </summary>
        private async Task HandleCircleCommentAsync(CircleCommentNoticeMessage msg)
        {
            var account = await GetAccountAsync(msg.WeChatId);
            // 微信 snsId 以 long 传输时可能为负数，只有 0 才表示无效。
            if (account == null || msg.CircleId == 0)
            {
                return;
            }

            var moment = await GetOrCreateMomentAsync(account.wxid, msg.CircleId);
            var comments = DeserializeComments(moment.commentsJson);
            var commentId = msg.Comment?.CommentId ?? 0;
            if (commentId > 0)
            {
                comments.RemoveAll(item => item.commentId == commentId);
            }

            if (!msg.IsDelete && msg.Comment != null)
            {
                var mapped = MapComment(msg.Comment, account.wxid);
                if (mapped.commentId == 0)
                {
                    mapped.commentId = -DateTime.UtcNow.Ticks;
                }

                comments.Add(mapped);
            }

            moment.commentsJson = JsonSerializer.Serialize(comments.OrderBy(item => item.createTime).ToList());
            moment.receivedAt = DateTime.UtcNow.Ticks;
            if (string.IsNullOrWhiteSpace(moment.userName))
            {
                moment.userName = account.wxid;
            }

            await BackfillMomentDisplayNamesAsync(account.wxid, new[] { moment });
            await _db.SaveChangesAsync();
            await PublishMomentsAsync(account.clientUuid, account.wxid, new[] { moment });

            _logger.LogInformation(
                "CircleCommentNotice: WeChatId={WeChatId}, CircleId={CircleId}, CommentId={CommentId}, IsDelete={IsDelete}",
                msg.WeChatId,
                msg.CircleId,
                commentId,
                msg.IsDelete);
        }

        /// <summary>
        /// 处理朋友圈删除通知。
        /// </summary>
        private async Task HandleCircleDeleteAsync(CircleDelNoticeMessage msg)
        {
            var account = await GetAccountAsync(msg.WeChatId);
            if (account == null)
            {
                return;
            }

            var moment = await _db.MomentsTimelines.FirstOrDefaultAsync(m => m.ownerWxid == account.wxid && m.snsId == msg.CircleId);
            if (moment == null)
            {
                _logger.LogInformation("CircleDelNotice ignored: WeChatId={WeChatId}, CircleId={CircleId}, Reason=NotFound", msg.WeChatId, msg.CircleId);
                return;
            }

            _db.MomentsTimelines.Remove(moment);
            await _db.SaveChangesAsync();

            _logger.LogInformation("CircleDelNotice: WeChatId={WeChatId}, CircleId={CircleId}", msg.WeChatId, msg.CircleId);
        }

        private async Task HandleSphMentionAsync(SphMentionListNoticeMessage mention)
        {
            var account = await GetAccountAsync(mention.WeChatId);
            var dto = BuildFinderMentionDto(account?.clientUuid, mention);
            await PersistFinderResultHistoryAsync(
                account?.clientUuid,
                mention.WeChatId,
                FinderResultTypeMention,
                mention.TaskId,
                mention.Success,
                $"点赞 {mention.LikeList.Count} / 评论 {mention.CommentList.Count} / 关注 {mention.FollowList.Count}",
                dto);
            await PublishFinderChangedAsync(
                account?.clientUuid,
                FinderResultTypeMention,
                mention.TaskId,
                mention.Success,
                mention.LikeList.Count + mention.CommentList.Count + mention.FollowList.Count,
                $"视频号提及结果已更新：点赞 {mention.LikeList.Count} / 评论 {mention.CommentList.Count} / 关注 {mention.FollowList.Count}");

            _logger.LogInformation(
                "SphMentionListNotice: WeChatId={WeChatId}, Likes={LikeCount}, Comments={CommentCount}, Follows={FollowCount}, TaskId={TaskId}",
                mention.WeChatId,
                mention.LikeList.Count,
                mention.CommentList.Count,
                mention.FollowList.Count,
                mention.TaskId);
        }

        private async Task HandleSphUserPageAsync(SphUserPageNoticeMessage userPage)
        {
            var account = await GetAccountAsync(userPage.WeChatId);
            var dto = BuildFinderUserPageDto(account?.clientUuid, userPage);
            await PersistFinderResultHistoryAsync(
                account?.clientUuid,
                userPage.WeChatId,
                FinderResultTypeUserPage,
                userPage.TaskId,
                userPage.Success,
                $"作品 {userPage.SphList.Count} 条",
                dto);
            await PublishFinderChangedAsync(
                account?.clientUuid,
                FinderResultTypeUserPage,
                userPage.TaskId,
                userPage.Success,
                userPage.SphList.Count,
                $"视频号用户页已更新：作品 {userPage.SphList.Count} 条");

            _logger.LogInformation(
                "SphUserPagePushNotice: WeChatId={WeChatId}, UserName={UserName}, Items={ItemCount}, Success={Success}, TaskId={TaskId}",
                userPage.WeChatId,
                userPage.UserName,
                userPage.SphList.Count,
                userPage.Success,
                userPage.TaskId);
        }

        private async Task HandleSphCommentListAsync(SphCommentListNoticeMessage comments)
        {
            var account = await GetAccountAsync(comments.WeChatId);
            var dto = BuildFinderCommentListDto(account?.clientUuid, comments);
            await PersistFinderResultHistoryAsync(
                account?.clientUuid,
                comments.WeChatId,
                FinderResultTypeComment,
                comments.TaskId,
                comments.Success,
                $"评论 {comments.CommentList.Count} 条",
                dto);
            await PublishFinderChangedAsync(
                account?.clientUuid,
                FinderResultTypeComment,
                comments.TaskId,
                comments.Success,
                comments.CommentList.Count,
                $"视频号评论列表已更新：评论 {comments.CommentList.Count} 条");

            _logger.LogInformation(
                "SphCommentListNotice: WeChatId={WeChatId}, Comments={CommentCount}, Success={Success}, TaskId={TaskId}",
                comments.WeChatId,
                comments.CommentList.Count,
                comments.Success,
                comments.TaskId);
        }

        private async Task<WechatAccount?> GetAccountAsync(string ownerWxid)
        {
            if (string.IsNullOrWhiteSpace(ownerWxid))
            {
                return null;
            }

            var account = await _db.WechatAccounts.FirstOrDefaultAsync(w => w.wxid == ownerWxid && !w.isDeleted);
            if (account == null)
            {
                _logger.LogWarning("Moments notice ignored: account not found for WeChatId={WeChatId}", ownerWxid);
            }
            return account;
        }

        private async Task<MomentsTimeline> UpsertMomentAsync(string ownerWxid, CircleInformationMessage circle)
        {
            var moment = await GetOrCreateMomentAsync(ownerWxid, circle.CircleId);
            FillMoment(moment, ownerWxid, circle);
            return moment;
        }

        private async Task<MomentsTimeline> GetOrCreateMomentAsync(string ownerWxid, long circleId)
        {
            var moment = await _db.MomentsTimelines.FirstOrDefaultAsync(m => m.ownerWxid == ownerWxid && m.snsId == circleId);
            if (moment != null)
            {
                return moment;
            }

            moment = new MomentsTimeline
            {
                ownerWxid = ownerWxid,
                snsId = circleId,
                wechatAccountId = 0,
                receivedAt = DateTime.UtcNow.Ticks,
                createTime = 0,
                userName = string.Empty,
                nickName = string.Empty,
                content = string.Empty,
                imagesJson = "[]",
                commentsJson = "[]",
                likesJson = "[]",
                videoUrl = string.Empty,
                linkInfoJson = string.Empty,
                xmlContent = string.Empty
            };
            _db.MomentsTimelines.Add(moment);
            return moment;
        }

        private void FillMoment(MomentsTimeline moment, string ownerWxid, CircleInformationMessage circle)
        {
            var content = circle.Content;
            var existingComments = DeserializeComments(moment.commentsJson);
            var existingLikes = DeserializeLikes(moment.likesJson);
            var incomingComments = circle.Comments.Select(comment => MapComment(comment, ownerWxid)).ToList();
            var incomingLikes = circle.Likes.Select(MapLike).ToList();
            var xmlContent = content?.Ext ?? string.Empty;

            moment.ownerWxid = ownerWxid;
            moment.snsId = circle.CircleId;
            moment.userName = circle.WeChatId ?? string.Empty;
            moment.nickName = moment.nickName ?? string.Empty;
            moment.content = MomentContentExtractor.FirstNonEmpty(
                content?.Text,
                MomentContentExtractor.ExtractTextFromXml(xmlContent));
            moment.createTime = circle.PublishTime;
            moment.imagesJson = JsonSerializer.Serialize(MapImages(content));
            // 详情/列表快照偶尔只带基础内容，不带刚刚由增量通知写入的互动。
            // 这里采用“快照 + 已有增量”的合并策略，避免评论详情回拉把先前点赞覆盖为空。
            moment.commentsJson = JsonSerializer.Serialize(MergeComments(existingComments, incomingComments));
            moment.likesJson = JsonSerializer.Serialize(MergeLikes(existingLikes, incomingLikes));
            moment.videoUrl = content?.Video?.Url ?? string.Empty;
            moment.linkInfoJson = SerializeLink(content);
            moment.xmlContent = xmlContent;
            moment.receivedAt = DateTime.UtcNow.Ticks;
        }

        /// <summary>
        /// 输出朋友圈字段映射摘要，便于下次回测快速判断真实内容落在 Text、Ext、图片、视频还是链接。
        /// </summary>
        private void LogCirclePushMappedMoments(string ownerWxid, IReadOnlyCollection<MomentsTimeline> moments)
        {
            foreach (var moment in moments.Take(3))
            {
                _logger.LogInformation(
                    "CirclePushNotice payload moment mapped: Owner={OwnerWxid}, CircleId={CircleId}, Author={Author}, TextLen={TextLen}, XmlLen={XmlLen}, ImageCount={ImageCount}, HasVideo={HasVideo}, HasLink={HasLink}, PublishTime={PublishTime}",
                    ownerWxid,
                    moment.snsId,
                    moment.userName,
                    moment.content?.Length ?? 0,
                    moment.xmlContent?.Length ?? 0,
                    DeserializeImages(moment.imagesJson).Count,
                    !string.IsNullOrWhiteSpace(moment.videoUrl),
                    HasMomentLink(moment.linkInfoJson),
                    moment.createTime);
            }
        }

        private static bool HasMomentLink(string? linkInfoJson)
        {
            if (string.IsNullOrWhiteSpace(linkInfoJson))
            {
                return false;
            }

            try
            {
                var link = JsonSerializer.Deserialize<MomentLinkDto>(linkInfoJson);
                return link != null
                    && (!string.IsNullOrWhiteSpace(link.title)
                        || !string.IsNullOrWhiteSpace(link.url)
                        || !string.IsNullOrWhiteSpace(link.thumb));
            }
            catch
            {
                return false;
            }
        }

        private static List<string> MapImages(CircleInformationMessage.Types.CircleContentMessage? content)
        {
            if (content == null || content.Images == null || content.Images.Count == 0)
            {
                return new List<string>();
            }

            return content.Images
                .Select(item => !string.IsNullOrWhiteSpace(item.Url) ? item.Url : item.ThumbImg)
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .ToList()!;
        }

        private static string SerializeLink(CircleInformationMessage.Types.CircleContentMessage? content)
        {
            if (content?.Link == null)
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(content.Link.Url) && string.IsNullOrWhiteSpace(content.Link.Description) && string.IsNullOrWhiteSpace(content.Link.ThumbImg))
            {
                return string.Empty;
            }

            return JsonSerializer.Serialize(new MomentLinkDto
            {
                title = content.Link.Description ?? string.Empty,
                url = content.Link.Url ?? string.Empty,
                thumb = content.Link.ThumbImg ?? string.Empty
            });
        }

        private static MomentCommentDto MapComment(CircleCommentMessage comment, string ownerWxid)
        {
            var fromWxid = comment.FromWeChatId ?? string.Empty;
            return new MomentCommentDto
            {
                commentId = comment.CommentId,
                userName = fromWxid,
                nickName = comment.FromName ?? string.Empty,
                content = comment.Content ?? string.Empty,
                createTime = comment.PublishTime,
                replyUserName = comment.ToWeChatId ?? string.Empty,
                replyNickName = comment.ToName ?? string.Empty,
                authorName = string.IsNullOrWhiteSpace(ownerWxid) ? fromWxid : ownerWxid
            };
        }

        private static MomentLikeDto MapLike(CircleLikeMessage like)
        {
            return new MomentLikeDto
            {
                userName = like.FriendId ?? string.Empty,
                nickName = like.NickName ?? string.Empty,
                createTime = like.PublishTime
            };
        }

        private static List<MomentCommentDto> DeserializeComments(string json)
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

        private static List<MomentLikeDto> DeserializeLikes(string json)
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

        private static List<MomentCommentDto> MergeComments(IEnumerable<MomentCommentDto> existing, IEnumerable<MomentCommentDto> incoming)
        {
            var map = new Dictionary<string, MomentCommentDto>();
            foreach (var item in existing.Concat(incoming))
            {
                var key = item.commentId > 0
                    ? item.commentId.ToString()
                    : $"{item.userName}|{item.createTime}|{item.content}";
                map[key] = item;
            }

            return map.Values.OrderBy(item => item.createTime).ToList();
        }

        private static List<MomentLikeDto> MergeLikes(IEnumerable<MomentLikeDto> existing, IEnumerable<MomentLikeDto> incoming)
        {
            var map = new Dictionary<string, MomentLikeDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in existing.Concat(incoming))
            {
                if (item == null || string.IsNullOrWhiteSpace(item.userName))
                {
                    continue;
                }

                var key = item.userName.Trim();
                if (!map.TryGetValue(key, out var oldItem)
                    || item.createTime >= oldItem.createTime
                    || string.IsNullOrWhiteSpace(oldItem.nickName))
                {
                    map[key] = item;
                }
            }

            return map.Values.OrderBy(item => item.createTime).ToList();
        }


        /// <summary>
        /// 按联系人表回填朋友圈展示昵称。
        /// <para>联系人持久化仍由 DbHelper.SaveContacts 维护，这里只读取联系人表并更新本次朋友圈快照/推送。</para>
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
                var comments = DeserializeComments(moment.commentsJson);
                var likes = DeserializeLikes(moment.likesJson);
                ApplyDisplayNames(moment, comments, likes, displayNames);
                moment.commentsJson = JsonSerializer.Serialize(comments);
                moment.likesJson = JsonSerializer.Serialize(likes);
            }
        }

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
                if (string.IsNullOrWhiteSpace(contact.wxid))
                {
                    continue;
                }

                map[contact.wxid] = ResolveDisplayName(contact.remarks, contact.nickname, contact.wxid);
                if (!string.IsNullOrWhiteSpace(contact.friendNo))
                {
                    map.TryAdd(contact.friendNo, ResolveDisplayName(contact.remarks, contact.nickname, contact.wxid));
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

            var selfName = await _db.WechatAccounts
                .AsNoTracking()
                .Where(a => a.wxid == ownerWxid && !a.isDeleted)
                .Select(a => a.nickname)
                .FirstOrDefaultAsync();
            map[ownerWxid] = string.IsNullOrWhiteSpace(selfName) ? ownerWxid : selfName!;
            return map;
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
            if (!string.IsNullOrWhiteSpace(wxid) && displayNames != null && displayNames.TryGetValue(wxid, out var displayName) && !string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            if (!string.IsNullOrWhiteSpace(currentName) && !LooksLikeRawWxid(currentName))
            {
                return currentName!;
            }

            return wxid ?? string.Empty;
        }

        private static string ResolveDisplayName(string? remarks, string? nickname, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(remarks)) return remarks;
            if (!string.IsNullOrWhiteSpace(nickname)) return nickname;
            return fallback;
        }

        /// <summary>
        /// 判断展示名是否只是原始微信标识。
        /// <para>朋友圈上报经常把 nickName 填成 wxid；这种值不能优先于联系人表昵称。</para>
        /// </summary>
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

        /// <summary>
        /// 将朋友圈同步回包摘要转成统一任务结果，避免前端只有“已下发”却看不到最终返回情况。
        /// </summary>
        private async Task PublishCirclePushTaskResultAsync(
            string? deviceUuid,
            CirclePushNoticeMessage msg,
            int effectiveCircleCount,
            int payloadCircleCount,
            int declaredCircleCount)
        {
            if (msg.TaskId <= 0 || string.IsNullOrWhiteSpace(deviceUuid))
            {
                return;
            }

            var normalizedTips = string.IsNullOrWhiteSpace(msg.RetTips) ? "无" : msg.RetTips.Trim();
            var hasRows = effectiveCircleCount > 0;
            var isSuccess = msg.RetCode == 0;
            string resultMessage;

            if (!isSuccess)
            {
                resultMessage = $"朋友圈同步返回异常：RetCode={msg.RetCode}; PayloadCount={payloadCircleCount}; DeclaredCount={declaredCircleCount}; Page={msg.Page}; RetTips={normalizedTips}";
            }
            else if (!hasRows)
            {
                resultMessage = $"朋友圈同步已返回 0 条：RetCode={msg.RetCode}; PayloadCount={payloadCircleCount}; DeclaredCount={declaredCircleCount}; Page={msg.Page}; RetTips={normalizedTips}";
            }
            else
            {
                resultMessage = $"朋友圈同步完成：本次返回 {effectiveCircleCount} 条，PayloadCount={payloadCircleCount}，DeclaredCount={declaredCircleCount}，Page={msg.Page}";
            }

            await _eventBus.PublishAsync(new TaskResultReceivedEvent(msg.TaskId, isSuccess, resultMessage, string.Empty, deviceUuid!));
        }

        /// <summary>
        /// 发布朋友圈异步数据任务结果。
        /// <para>CircleMsgPushNotice 等数据型回包不会再额外发送通用 TaskResultNotice，因此在 handler 内直接补齐任务闭环。</para>
        /// </summary>
        private async Task PublishTaskResultAsync(
            IChannelHandlerContext context,
            long taskId,
            bool success,
            string message,
            string? fallbackDeviceUuid = null)
        {
            if (taskId <= 0)
            {
                return;
            }

            var connId = context.Channel.Id.AsLongText();
            var connInfo = await _connManager.GetConnectionAsync(connId);
            var deviceUuid = connInfo?.deviceUuid ?? fallbackDeviceUuid ?? string.Empty;
            if (string.IsNullOrWhiteSpace(deviceUuid))
            {
                return;
            }

            await _eventBus.PublishAsync(new TaskResultReceivedEvent(taskId, success, message, connId, deviceUuid));
        }

        private async Task PublishMomentsAsync(string? deviceUuid, string ownerWxid, IEnumerable<MomentsTimeline> moments)
        {
            if (string.IsNullOrWhiteSpace(deviceUuid))
            {
                return;
            }

            var targetDeviceGroup = deviceUuid!;
            var itemCount = moments?.Count() ?? 0;
            var notice = new RealtimeDataChangedNoticeDto
            {
                scope = "moments",
                changeType = "timeline",
                deviceUuid = targetDeviceGroup,
                weChatId = string.Empty,
                taskId = 0,
                success = true,
                itemCount = itemCount,
                summary = itemCount <= 1 ? "朋友圈时间线已更新" : $"朋友圈时间线已更新：{itemCount} 条",
                receivedAt = DateTimeOffset.UtcNow
            };

            await _hubContext.Clients.Group(targetDeviceGroup).SendAsync("MomentTimelineChanged", notice);
            await _eventBus.PublishAsync(new MomentTimelineChangedEvent(notice));
        }

        /// <summary>
        /// 发布视频号变更通知到 SignalR 与站内事件总线。
        /// <para>这里只广播安全摘要，页面收到后通过服务层重拉已脱敏历史，避免按设备组直接广播原始正文和媒体字段。</para>
        /// </summary>
        private async Task PublishFinderChangedAsync(
            string? deviceUuid,
            string changeType,
            long taskId,
            bool success,
            int itemCount,
            string summary)
        {
            var notice = new RealtimeDataChangedNoticeDto
            {
                scope = "finder",
                changeType = changeType ?? string.Empty,
                deviceUuid = deviceUuid ?? string.Empty,
                weChatId = string.Empty,
                taskId = taskId,
                success = success,
                itemCount = itemCount,
                summary = summary ?? string.Empty,
                receivedAt = DateTimeOffset.UtcNow
            };

            if (!string.IsNullOrWhiteSpace(deviceUuid))
            {
                await _hubContext.Clients.Group(deviceUuid).SendAsync("FinderResultChanged", notice);
            }

            await _eventBus.PublishAsync(new FinderResultChangedEvent(notice));
        }

        /// <summary>
        /// 持久化视频号结果历史，并按设备+类型裁剪为最近 N 条。
        /// </summary>
        private async Task PersistFinderResultHistoryAsync<TDto>(
            string? deviceUuid,
            string? weChatId,
            string resultType,
            long taskId,
            bool success,
            string summary,
            TDto dto)
        {
            var ownerKey = BuildFinderOwnerKey(deviceUuid, weChatId);
            if (string.IsNullOrWhiteSpace(ownerKey))
            {
                return;
            }

            var receivedAt = ResolveFinderReceivedAt(dto);
            var payloadJson = JsonSerializer.Serialize(dto);
            var existing = await _db.FinderResultHistories
                .FirstOrDefaultAsync(item => item.ownerKey == ownerKey && item.resultType == resultType && item.taskId == taskId && item.receivedAt == receivedAt.UtcDateTime);

            if (existing == null)
            {
                existing = new FinderResultHistory
                {
                    ownerKey = ownerKey,
                    deviceUuid = deviceUuid ?? string.Empty,
                    weChatId = weChatId ?? string.Empty,
                    resultType = resultType,
                    taskId = taskId,
                    success = success,
                    summary = summary,
                    payloadJson = payloadJson,
                    receivedAt = receivedAt.UtcDateTime,
                    createdAt = DateTime.UtcNow
                };
                _db.FinderResultHistories.Add(existing);
            }
            else
            {
                existing.deviceUuid = deviceUuid ?? string.Empty;
                existing.weChatId = weChatId ?? string.Empty;
                existing.success = success;
                existing.summary = summary;
                existing.payloadJson = payloadJson;
                existing.receivedAt = receivedAt.UtcDateTime;
            }

            await _db.SaveChangesAsync();

            var staleRows = await _db.FinderResultHistories
                .Where(item => item.ownerKey == ownerKey && item.resultType == resultType)
                .OrderByDescending(item => item.receivedAt)
                .ThenByDescending(item => item.id)
                .Skip(FinderHistoryLimitPerDevice)
                .ToListAsync();

            if (staleRows.Count > 0)
            {
                _db.FinderResultHistories.RemoveRange(staleRows);
                await _db.SaveChangesAsync();
            }
        }

        private static string BuildFinderOwnerKey(string? deviceUuid, string? weChatId)
        {
            if (!string.IsNullOrWhiteSpace(deviceUuid))
            {
                return $"device:{deviceUuid}";
            }

            if (!string.IsNullOrWhiteSpace(weChatId))
            {
                return $"wx:{weChatId}";
            }

            return string.Empty;
        }

        private static DateTimeOffset ResolveFinderReceivedAt<TDto>(TDto dto)
        {
            return dto switch
            {
                FinderMentionNoticeDto mention => mention.receivedAt,
                FinderUserPageDto userPage => userPage.receivedAt,
                FinderCommentListDto comment => comment.receivedAt,
                _ => DateTimeOffset.UtcNow
            };
        }

        private static FinderMentionNoticeDto BuildFinderMentionDto(string? deviceUuid, SphMentionListNoticeMessage mention)
        {
            return new FinderMentionNoticeDto
            {
                deviceUuid = deviceUuid ?? string.Empty,
                weChatId = mention.WeChatId ?? string.Empty,
                sphUserName = mention.SphUserName ?? string.Empty,
                success = mention.Success,
                errMsg = mention.ErrMsg ?? string.Empty,
                taskId = mention.TaskId,
                receivedAt = DateTimeOffset.UtcNow,
                likeList = mention.LikeList.Select(BuildFinderMentionItemDto).ToList(),
                commentList = mention.CommentList.Select(BuildFinderMentionItemDto).ToList(),
                followList = mention.FollowList.Select(BuildFinderMentionItemDto).ToList()
            };
        }

        private static FinderMentionItemDto BuildFinderMentionItemDto(MentionMessage item)
        {
            return new FinderMentionItemDto
            {
                sphItem = BuildFinderBriefDto(item.SphItem),
                id = item.Id,
                type = item.Type,
                mentionId = item.MentionId,
                commentId = item.CommentId,
                userName = item.UserName ?? string.Empty,
                nickName = item.NickName ?? string.Empty,
                avatar = item.Avatar ?? string.Empty,
                content = item.Content ?? string.Empty,
                contentType = item.ContentType,
                createTime = item.CreateTime,
                replayUsername = item.ReplayUsername ?? string.Empty,
                replayNickname = item.ReplayNickname ?? string.Empty,
                rootCommentId = item.RootCommentId,
                refContent = item.RefContent ?? string.Empty,
                fansId = item.FansId,
                followId = item.FollowId,
                relationType = item.RelationType
            };
        }

        private static FinderBriefDto BuildFinderBriefDto(SphBriefMessage brief)
        {
            return new FinderBriefDto
            {
                feedId = brief?.FeedId ?? 0,
                userName = brief?.UserName ?? string.Empty,
                desc = brief?.Desc ?? string.Empty,
                thumb = brief?.Thumb ?? string.Empty,
                nonceId = brief?.NonceId ?? string.Empty,
                type = brief?.Type ?? 0
            };
        }

        private static FinderUserPageDto BuildFinderUserPageDto(string? deviceUuid, SphUserPageNoticeMessage userPage)
        {
            return new FinderUserPageDto
            {
                deviceUuid = deviceUuid ?? string.Empty,
                weChatId = userPage.WeChatId ?? string.Empty,
                userName = userPage.UserName ?? string.Empty,
                nickName = userPage.NickName ?? string.Empty,
                avatar = userPage.Avatar ?? string.Empty,
                signature = userPage.Signature ?? string.Empty,
                gender = (int)userPage.Gender,
                province = userPage.Province ?? string.Empty,
                city = userPage.City ?? string.Empty,
                success = userPage.Success,
                errMsg = userPage.ErrMsg ?? string.Empty,
                taskId = userPage.TaskId,
                receivedAt = DateTimeOffset.UtcNow,
                sphList = userPage.SphList.Select(item => new FinderUserPageItemDto
                {
                    feedId = item.FeedId,
                    userName = item.UserName ?? string.Empty,
                    desc = item.Desc ?? string.Empty,
                    thumb = item.Thumb ?? string.Empty,
                    nonceId = item.NonceId ?? string.Empty,
                    type = item.Type,
                    timestamp = item.Timestamp,
                    readCnt = item.ReadCnt,
                    likeCnt = item.LikeCnt,
                    commentCnt = item.CommentCnt,
                    favCnt = item.FavCnt,
                    forwardCnt = item.ForwardCnt
                }).ToList()
            };
        }

        private static FinderCommentListDto BuildFinderCommentListDto(string? deviceUuid, SphCommentListNoticeMessage comments)
        {
            return new FinderCommentListDto
            {
                deviceUuid = deviceUuid ?? string.Empty,
                weChatId = comments.WeChatId ?? string.Empty,
                sphUserName = comments.SphUserName ?? string.Empty,
                success = comments.Success,
                errMsg = comments.ErrMsg ?? string.Empty,
                taskId = comments.TaskId,
                receivedAt = DateTimeOffset.UtcNow,
                commentList = comments.CommentList.Select(item => new FinderCommentItemDto
                {
                    commentId = item.CommentId,
                    type = item.Type,
                    feedId = item.FeedId,
                    userName = item.UserName ?? string.Empty,
                    nickName = item.NickName ?? string.Empty,
                    avatar = item.Avatar ?? string.Empty,
                    content = item.Content ?? string.Empty,
                    contentType = item.ContentType,
                    createTime = item.CreateTime,
                    likeCount = item.LikeCount,
                    replyId = item.ReplyId
                }).ToList()
            };
        }

        private static MomentsTimelineDto BuildMomentDto(MomentsTimeline moment, Dictionary<string, string>? displayNames = null)
        {
            var comments = DeserializeComments(moment.commentsJson);
            var likes = DeserializeLikes(moment.likesJson);
            ApplyDisplayNames(moment, comments, likes, displayNames);

            return new MomentsTimelineDto
            {
                snsId = moment.snsId,
                userName = moment.userName,
                nickName = ResolveDisplayName(displayNames, moment.userName, moment.nickName),
                content = MomentContentExtractor.FirstNonEmpty(
                    moment.content,
                    MomentContentExtractor.ExtractTextFromXml(moment.xmlContent)),
                xmlContent = moment.xmlContent ?? string.Empty,
                createTime = moment.createTime,
                stringTime = moment.createTime.ToString(),
                images = DeserializeImages(moment.imagesJson),
                videoUrl = moment.videoUrl ?? string.Empty,
                link = !string.IsNullOrWhiteSpace(moment.linkInfoJson)
                    ? JsonSerializer.Deserialize<MomentLinkDto>(moment.linkInfoJson) ?? new MomentLinkDto()
                    : new MomentLinkDto(),
                comments = comments,
                likes = likes
            };
        }

        private static List<string> DeserializeImages(string json)
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
    }
}
