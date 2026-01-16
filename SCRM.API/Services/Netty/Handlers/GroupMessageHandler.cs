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
using SCRM.API.Models.Entities;
using System.Linq;

namespace SCRM.API.Services.Netty.Handlers
{
    /// <summary>
    /// 群组消息处理器
    /// 负责处理群聊列表同步、群成员变更等
    /// Scoped Service
    /// </summary>
    public class GroupMessageHandler
    {
        private readonly ILogger<GroupMessageHandler> _logger;
        private readonly ConnectionManager _connectionManager;
        private readonly ApplicationDbContext _dbContext;
        private readonly IHubContext<ClientHub> _hubContext;

        public GroupMessageHandler(
            ILogger<GroupMessageHandler> logger,
            ConnectionManager connectionManager,
            ApplicationDbContext dbContext,
            IHubContext<ClientHub> hubContext)
        {
            _logger = logger;
            _connectionManager = connectionManager;
            _dbContext = dbContext;
            _hubContext = hubContext;
        }

        /// <summary>
        /// 处理群聊列表推送通知 (3.23 / 3.20 结果)
        /// 1. 保存/更新群聊信息
        /// 2. 更新群成员数量
        /// 3. 通知 SignalR 前端
        /// </summary>
        public async Task HandleChatRoomPushNotice(TransportMessage message, IChannelHandlerContext context)
        {
            ChatRoomPushNoticeMessage notice = message.Content.Unpack<ChatRoomPushNoticeMessage>();
            _logger.LogInformation("群聊列表推送：{WeChatId} 数量={Count}", notice.WeChatId, notice.ChatRooms.Count);

            var connectionId = context.Channel.Id.AsLongText();
            var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
            
            if (connectionInfo == null || !long.TryParse(connectionInfo.userId, out long accountId)) return;

            // 批量保存
            int newCount = 0;
            int updateCount = 0;

            foreach (var room in notice.ChatRooms)
            {
                // 1. 查找或创建 Group
                var group = await _dbContext.Groups
                    .FirstOrDefaultAsync(g => g.wechatAccountId == accountId && g.groupWxid == room.UserName);
                
                if (group == null)
                {
                    newCount++;
                    group = new Group
                    {
                        wechatAccountId = (int)accountId,
                        groupWxid = room.UserName,
                        groupName = room.NickName ?? "",
                        ownerWxid = room.Owner ?? "",
                        groupNotice = room.Notice ?? "",
                        groupAvatar = room.Avatar ?? "", // 小图
                        groupDescription = "",
                        memberCount = room.MemberList.Count,
                        createdAt = DateTime.UtcNow,
                        updatedAt = DateTime.UtcNow,
                        isDeleted = false
                    };
                    _dbContext.Groups.Add(group);
                }
                else
                {
                    updateCount++;
                    group.groupName = room.NickName ?? "";
                    group.ownerWxid = room.Owner ?? "";
                    group.groupNotice = room.Notice ?? "";
                    group.groupAvatar = room.Avatar ?? "";
                    group.memberCount = room.MemberList.Count; 
                    group.updatedAt = DateTime.UtcNow;
                }
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("账号 {AccountId} 已保存 {New} 个新群聊，{Update} 个更新。", newCount, updateCount, accountId);
            
            // 通知 Web 端刷新
            await _hubContext.Clients.Group(connectionInfo.deviceInfo ?? connectionId).SendAsync("ChatRoomsUpdated", accountId);

            await SendAckAsync(message, context);
        }

        /// <summary>
        /// 处理群成员列表通知
        /// </summary>
        public async Task HandleChatRoomMembersNotice(TransportMessage message, IChannelHandlerContext context)
        {
            var notice = message.Content.Unpack<ChatRoomMembersNoticeMessage>();
            _logger.LogInformation("群成员通知：{WeChatId}, 成员数={Count}", notice.WeChatId, notice.Members.Count);
            
            // TODO: 具体成员同步逻辑
            
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
