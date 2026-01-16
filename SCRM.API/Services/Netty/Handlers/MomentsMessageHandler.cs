using DotNetty.Transport.Channels;
using Jubo.JuLiao.IM.Wx.Proto;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

using SCRM.API.Services.Netty.Handlers.Abstractions;

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
        private readonly ILogger<MomentsMessageHandler> _logger;
        public MomentsMessageHandler(ILogger<MomentsMessageHandler> logger) : base(logger) { _logger = logger; }

        public async Task HandleMessage(TransportMessage message, IChannelHandlerContext context)
        {
            await SendAckAsync(message, context);
             _logger.LogInformation("Moments Message Received: {Type}", message.MsgType);
        }
    }
}
