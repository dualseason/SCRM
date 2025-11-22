using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;
using Google.Protobuf;
using Jubo.JuLiao.IM.Wx.Proto;
using System.Collections.Generic;

namespace SCRM.Core.Netty
{
    public class ProtobufDecoder : ByteToMessageDecoder
    {
        protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            if (input.ReadableBytes < 4)
            {
                return; // 等待更多数据
            }

            input.MarkReaderIndex();
            int length = input.ReadInt();

            if (input.ReadableBytes < length)
            {
                input.ResetReaderIndex();
                return; // 等待完整消息
            }

            byte[] bytes = new byte[length];
            input.ReadBytes(bytes);

            try
            {
                var message = TransportMessage.Parser.ParseFrom(bytes);
                output.Add(message);
            }
            catch (InvalidProtocolBufferException ex)
            {
                throw new CodecException("Failed to decode TransportMessage", ex);
            }
        }
    }
}
