using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;
using Google.Protobuf;
using SCRM.SHARED.Proto;

namespace SCRM.Services.Netty
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
