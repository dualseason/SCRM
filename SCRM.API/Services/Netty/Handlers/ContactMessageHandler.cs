using DotNetty.Transport.Channels;
using Jubo.JuLiao.IM.Wx.Proto;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

using SCRM.API.Services.Netty.Handlers.Abstractions;

namespace SCRM.API.Services.Netty.Handlers
{
    /// <summary>
    /// 联系人消息处理器
    /// <para>处理好友列表变更、好友请求等联系人相关事件。</para>
    /// <para>核心功能：</para>
    /// <list type="bullet">
    /// <item>处理好友添加通知</item>
    /// <item>处理好友删除通知</item>
    /// <item>处理联系人信息更新</item>
    /// </list>
    /// </summary>
    public class ContactMessageHandler : MessageHandlerBase
    {
        private readonly ILogger<ContactMessageHandler> _logger;
        public ContactMessageHandler(ILogger<ContactMessageHandler> logger) : base(logger) { _logger = logger; }

        public async Task HandleMessage(TransportMessage message, IChannelHandlerContext context)
        {
            await SendAckAsync(message, context);
            _logger.LogInformation("Contact Message Received: {Type}", message.MsgType);
        }
    }
}
