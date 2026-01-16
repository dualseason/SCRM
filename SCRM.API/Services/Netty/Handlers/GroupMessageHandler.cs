using DotNetty.Transport.Channels;
using Jubo.JuLiao.IM.Wx.Proto;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

using SCRM.API.Services.Netty.Handlers.Abstractions;

namespace SCRM.API.Services.Netty.Handlers
{
    /// <summary>
    /// 群组消息处理器
    /// <para>处理群聊相关的非聊天类通知。</para>
    /// <para>核心功能：</para>
    /// <list type="bullet">
    /// <item>处理群成员变更通知</item>
    /// <item>处理入群/退群通知</item>
    /// <item>处理群公告更新</item>
    /// </list>
    /// </summary>
    public class GroupMessageHandler : MessageHandlerBase
    {
        private readonly ILogger<GroupMessageHandler> _logger;
        public GroupMessageHandler(ILogger<GroupMessageHandler> logger) : base(logger) { _logger = logger; }

        public async Task HandleMessage(TransportMessage message, IChannelHandlerContext context)
        {
            await SendAckAsync(message, context);
            _logger.LogInformation("Group Message Received: {Type}", message.MsgType);
        }
    }
}
