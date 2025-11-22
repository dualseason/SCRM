using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;
using Google.Protobuf;
using Jubo.JuLiao.IM.Wx.Proto;

namespace SCRM.Core.Netty
{
    public class ProtobufEncoder : MessageToByteEncoder<TransportMessage>
    {
        protected override void Encode(IChannelHandlerContext context, TransportMessage message, IByteBuffer output)
        {
            byte[] bytes = message.ToByteArray();
            output.WriteInt(bytes.Length);
            output.WriteBytes(bytes);
        }
    }
}
