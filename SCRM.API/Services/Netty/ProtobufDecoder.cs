using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;
using Google.Protobuf;
using SCRM.SHARED.Proto;
using System.Collections.Generic;

namespace SCRM.Services.Netty
{
    /// <summary>
    /// Protobuf 解码器
    /// 处理粘包拆包：先读取4字节长度头，再读取 Protobuf 字节流反序列化为 TransportMessage
    /// </summary>
    public class ProtobufDecoder : ByteToMessageDecoder
    {
        protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            // 如果可读字节少于4个（长度头），则等待
            if (input.ReadableBytes < 4)
            {
                return; // 等待更多数据
            }

            input.MarkReaderIndex();
            // 读取消息体长度（大端序 int32）
            int length = input.ReadInt();

            // 如果可读字节少于消息体长度，说明包不完整，回滚指针等待
            if (input.ReadableBytes < length)
            {
                input.ResetReaderIndex();
                return; // 等待完整消息
            }

            // 读取消息体字节数组
            byte[] bytes = new byte[length];
            input.ReadBytes(bytes);

            try
            {
                // 反序列化为 TransportMessage
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
