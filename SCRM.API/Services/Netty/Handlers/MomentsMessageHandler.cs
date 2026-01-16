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
using SCRM.API.Services.Data;
using SCRM.Services.Events;
using SCRM.API.Services;
using SCRM.API.Models.Entities;
using SCRM.API.Models.Events;
using System.Collections.Generic;
using System.Linq;

namespace SCRM.API.Services.Netty.Handlers
{
    /// <summary>
    /// 朋友圈消息处理器
    /// 负责处理朋友圈列表、详情、发布结果等
    /// Scoped Service
    /// </summary>
    public class MomentsMessageHandler
    {
        private readonly ILogger<MomentsMessageHandler> _logger;
        private readonly ConnectionManager _connectionManager;
        private readonly ApplicationDbContext _dbContext;
        private readonly IHubContext<ClientHub> _hubContext;
        private readonly IEventBus _eventBus;
        private readonly ClientTaskService _clientTaskService;

        public MomentsMessageHandler(
            ILogger<MomentsMessageHandler> logger,
            ConnectionManager connectionManager,
            ApplicationDbContext dbContext,
            IHubContext<ClientHub> hubContext,
            IEventBus eventBus,
            ClientTaskService clientTaskService)
        {
            _logger = logger;
            _connectionManager = connectionManager;
            _dbContext = dbContext;
            _hubContext = hubContext;
            _eventBus = eventBus;
            _clientTaskService = clientTaskService;
        }

        /// <summary>
        /// 处理朋友圈推送通知 (3.13)
        /// 1. 保存到数据库 (MomentsTimeline)
        /// 2. 推送到 SignalR 前端
        /// </summary>
        public async Task HandleCirclePushNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<CirclePushNoticeMessage>();
            _logger.LogInformation("朋友圈推送通知：{WeChatId}，数量={Count}，页码={Page}", 
                notice.WeChatId, notice.Circles.Count, notice.Page);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            
            if (connectionInfo == null || !long.TryParse(connectionInfo.userId, out long accountId)) return;
            
            foreach (var circle in notice.Circles)
            {
                long snsId = circle.CircleId;
                
                // 1. 检查是否存在
                var exists = await _dbContext.MomentsTimelines.AnyAsync(m => m.snsId == snsId);
                if (!exists)
                {
                    var entity = new MomentsTimeline
                    {
                        snsId = snsId,
                        wechatAccountId = (int)accountId,
                        userName = circle.WeChatId,
                        nickName = "", 
                        content = circle.Content?.Text ?? "",
                        createTime = circle.PublishTime,
                        receivedAt = DateTime.UtcNow.Ticks,
                        ownerWxid = notice.WeChatId,
                        
                        imagesJson = System.Text.Json.JsonSerializer.Serialize(
                            circle.Content?.Images.Select(i => i.ThumbImg).ToList() ?? new List<string>()
                        ),
                        commentsJson = System.Text.Json.JsonSerializer.Serialize(
                            circle.Comments.Select(c => new SCRM.SHARED.Models.Dtos.MomentCommentDto { authorName = c.FromName, content = c.Content }).ToList()
                        ),
                        likesJson = System.Text.Json.JsonSerializer.Serialize(
                            circle.Likes.Select(l => new SCRM.SHARED.Models.Dtos.MomentLikeDto { userName = l.FriendId, nickName = l.NickName }).ToList()
                        )
                    };
                    
                    _dbContext.MomentsTimelines.Add(entity);
                }
                
                // 2. 构建 DTO (用于实时推送)
                var dto = new SCRM.SHARED.Models.Dtos.MomentsTimelineDto
                {
                    snsId = snsId,
                    userName = circle.WeChatId,
                    nickName = "", 
                    content = circle.Content?.Text ?? "",
                    createTime = circle.PublishTime,
                    images = circle.Content?.Images.Select(i => i.ThumbImg).ToList() ?? new List<string>(),
                    comments = circle.Comments.Select(c => new SCRM.SHARED.Models.Dtos.MomentCommentDto { authorName = c.FromName, content = c.Content }).ToList(),
                    likes = circle.Likes.Select(l => new SCRM.SHARED.Models.Dtos.MomentLikeDto { userName = l.FriendId, nickName = l.NickName }).ToList()
                };
                
                // 3. 发送给客户端
                await _hubContext.Clients.Group(connectionInfo.deviceInfo ?? connectionId).SendAsync("MomentReceived", dto);
            }
            
            await _dbContext.SaveChangesAsync();
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理朋友圈详情通知 (3.26 结果)
        /// 1. 保存朋友圈内容到 MomentsPost (详细表)
        /// 2. 通知前端
        /// </summary>
        public async Task HandleCircleDetailNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<CircleDetailNoticeMessage>();
            if (notice.Circle == null) return;

            var circle = notice.Circle;
            _logger.LogInformation("朋友圈详情通知：{WeChatId} - {CircleId} (作者：{Author})", 
                notice.WeChatId, circle.CircleId, circle.WeChatId);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);

            if (connectionInfo == null || !long.TryParse(connectionInfo.userId, out long accountId)) return;

            // 1. 转换时间
            var publishTime = DateTimeOffset.FromUnixTimeSeconds(circle.PublishTime).UtcDateTime;

            // 2. 查找或创建 MomentsPost
            var post = await _dbContext.MomentsPosts
                .FirstOrDefaultAsync(p => p.wechatAccountId == accountId && p.authorWxid == circle.WeChatId && p.publishTime == publishTime);

            if (post == null)
            {
                post = new MomentsPost
                {
                    wechatAccountId = (int)accountId,
                    authorWxid = circle.WeChatId,
                    publishTime = publishTime,
                    createdAt = DateTime.UtcNow,
                    isDeleted = false
                };
                _dbContext.MomentsPosts.Add(post);
            }

            // 3. 更新内容
            if (circle.Content != null)
            {
                post.postContent = circle.Content.Text ?? "";
                
                if (circle.Content.Images != null && circle.Content.Images.Count > 0)
                {
                    post.postCover = circle.Content.Images[0].ThumbImg ?? circle.Content.Images[0].Url ?? "";
                     post.imagesJson = System.Text.Json.JsonSerializer.Serialize(
                         circle.Content.Images.Select(i => i.Url).ToList()
                     );
                }
                else if (circle.Content.Video != null)
                {
                    post.postCover = circle.Content.Video.ThumbImg ?? "";
                    post.videoUrl = circle.Content.Video.Url;
                }
                 else if (circle.Content.Link != null) 
                {
                     post.linkInfoJson = System.Text.Json.JsonSerializer.Serialize(new {
                         Title = circle.Content.Link.Description,
                         Url = circle.Content.Link.Url,
                         Thumb = circle.Content.Link.ThumbImg
                     });
                }
            }
            
            post.likeCount = circle.Likes.Count;
            post.commentCount = circle.Comments.Count;
            post.updatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            // 4. 同步点赞
            var existingLikes = await _dbContext.MomentsLikes.Where(l => l.postId == post.id).ToListAsync();
            _dbContext.MomentsLikes.RemoveRange(existingLikes);

            foreach (var likeProto in circle.Likes)
            {
                _dbContext.MomentsLikes.Add(new MomentsLike
                {
                    postId = post.id,
                    likerWxid = likeProto.FriendId,
                    likerNickname = likeProto.NickName ?? "",
                    likeTime = DateTimeOffset.FromUnixTimeSeconds(likeProto.PublishTime).UtcDateTime,
                    createdAt = DateTime.UtcNow
                });
            }

            // 5. 同步评论
            var existingComments = await _dbContext.MomentsComments.Where(c => c.postId == post.id).ToListAsync();
            _dbContext.MomentsComments.RemoveRange(existingComments);

            foreach (var cmtProto in circle.Comments)
            {
                _dbContext.MomentsComments.Add(new MomentsComment
                {
                    postId = post.id,
                    commenterWxid = cmtProto.FromWeChatId,
                    weChatCommentId = cmtProto.CommentId, 
                    replyCommentId = cmtProto.ReplyCommentId, 
                    commentContent = cmtProto.Content ?? "",
                    replyToWxid = cmtProto.ToWeChatId ?? "",
                    commentTime = DateTimeOffset.FromUnixTimeSeconds(cmtProto.PublishTime).UtcDateTime,
                    createdAt = DateTime.UtcNow
                });
            }

            await _dbContext.SaveChangesAsync();
            
            // 6. 前端通知
            await _hubContext.Clients.Group(connectionInfo.deviceInfo ?? connectionId).SendAsync("MomentReceived", post.authorWxid, post.postContent);
            await _hubContext.Clients.Group(connectionInfo.deviceInfo ?? connectionId).SendAsync("CircleDetailUpdated", notice.WeChatId, circle.CircleId);

            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理朋友圈新发布通知 (3.13 监听)
        /// 监听特定用户的新动态
        /// </summary>
        public async Task HandleCircleNewPublishNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<CircleNewPublishNoticeMessage>();
            if (notice.Circle == null) return;

            _logger.LogInformation("朋友圈新发布通知：{WeChatId} 收到来自 {Author} 的新动态", 
                notice.WeChatId, notice.Circle.WeChatId);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);

            if (connectionInfo == null || !long.TryParse(connectionInfo.userId, out long accountId)) return;

             // 1. 幂等性检查
            var exists = await _dbContext.MomentsTimelines
                .AnyAsync(t => t.wechatAccountId == accountId && t.snsId == notice.Circle.CircleId);

            if (!exists)
            {
                var timeline = new MomentsTimeline
                {
                    wechatAccountId = (int)accountId,
                    snsId = notice.Circle.CircleId,
                    userName = notice.Circle.WeChatId,
                    nickName = "", 
                    content = notice.Circle.Content?.Text ?? "",
                    createTime = notice.Circle.PublishTime,
                    receivedAt = DateTime.UtcNow.Ticks,
                    ownerWxid = notice.WeChatId,
                    imagesJson = "[]",
                    commentsJson = "[]",
                    likesJson = "[]"
                };

                if (notice.Circle.Content != null)
                {
                    if (notice.Circle.Content.Images != null && notice.Circle.Content.Images.Count > 0)
                    {
                        timeline.imagesJson = System.Text.Json.JsonSerializer.Serialize(
                            notice.Circle.Content.Images.Select(i => i.Url).ToList()
                        );
                    }
                    if (notice.Circle.Content.Video != null && !string.IsNullOrEmpty(notice.Circle.Content.Video.Url))
                    {
                        timeline.videoUrl = notice.Circle.Content.Video.Url;
                    }
                    if (notice.Circle.Content.Link != null && !string.IsNullOrEmpty(notice.Circle.Content.Link.Url))
                    {
                        timeline.linkInfoJson = System.Text.Json.JsonSerializer.Serialize(new {
                            Title = notice.Circle.Content.Link.Description,
                            Url = notice.Circle.Content.Link.Url,
                            Thumb = notice.Circle.Content.Link.ThumbImg
                        });
                    }
                     if (!string.IsNullOrEmpty(notice.Circle.Content.Ext))
                    {
                         timeline.xmlContent = notice.Circle.Content.Ext;
                    }
                }

                _dbContext.MomentsTimelines.Add(timeline);
                await _dbContext.SaveChangesAsync();

                // 2. 通知 SignalR
                var deviceGroup = connectionInfo.deviceInfo ?? connectionId;
                var dto = new SCRM.SHARED.Models.Dtos.MomentsTimelineDto
                {
                    snsId = timeline.snsId,
                    userName = timeline.userName,
                    nickName = timeline.nickName,
                    content = timeline.content,
                    createTime = timeline.createTime,
                    images = !string.IsNullOrEmpty(timeline.imagesJson) ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(timeline.imagesJson) ?? new List<string>() : new List<string>(),
                    videoUrl = timeline.videoUrl ?? string.Empty,
                    link = !string.IsNullOrEmpty(timeline.linkInfoJson) ? System.Text.Json.JsonSerializer.Deserialize<SCRM.SHARED.Models.Dtos.MomentLinkDto>(timeline.linkInfoJson) ?? new() : new(),
                    comments = new(),
                    likes = new()
                };
                await _hubContext.Clients.Group(deviceGroup).SendAsync("MomentTimelineReceived", dto);
            }

            // 3. 发布事件 (Compatible)
            var evt = new CircleNewPublishEvent(
                    notice.WeChatId,
                    notice.Circle.WeChatId,
                    notice.Circle.CircleId,
                    notice.Circle.Content?.Text ?? "",
                    connectionId,
                    accountId);
            await _eventBus.PublishAsync(evt);
            
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理发送朋友圈任务结果 (4.2)
        /// </summary>
        public async Task HandlePostSNSNewsTaskResultNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var result = message.Content.Unpack<PostSNSNewsTaskResultNoticeMessage>();
            _logger.LogInformation("发朋友圈结果：成功={Success}, 任务Id={TaskId}", result.Success, result.TaskId);
            _clientTaskService.CompleteTask(result.TaskId, result.Success);
            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理一键点赞任务结果通知 (4.22)
        /// </summary>
        public async Task HandleOneKeyLikeTaskResultNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var result = message.Content.Unpack<OneKeyLikeTaskResultNoticeMessage>();
            _logger.LogInformation("一键点赞任务结果：TaskId={TaskId}, Count={Count}, EndType={EndType}", result.TaskId, result.Count, result.EndType);
            _clientTaskService.CompleteTask(result.TaskId, true, $"Count: {result.Count}, EndType: {result.EndType}");
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
             
             await context.WriteAndFlushAsync(response);
        }
    }
}
