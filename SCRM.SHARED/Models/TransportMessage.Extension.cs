using System;

namespace Jubo.JuLiao.IM.Wx.Proto
{
    public sealed partial class TransportMessage
    {
        /// <summary>
        /// 构造函数扩展：设置默认 ID 为当前时间戳
        /// </summary>
        partial void OnConstruction()
        {
            this.Id = DateTime.UtcNow.Ticks;
            this.AccessToken = string.Empty;
            this.MsgType = EnumMsgType.UnknownMsg;
            this.Content = null;
            this.RefMessageId = 0;
        }
    }
}
