using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;
using Google.Protobuf;
using Jubo.JuLiao.IM.Wx.Proto;
using System.Collections.Generic;

namespace SCRM.Services.Netty
{
    /// <summary>
    /// Protobuf 解码器
    /// 处理粘包拆包：先读取4字节长度头，再读取 Protobuf 字节流反序列化为 TransportMessage
    /// </summary>
    /// <summary>
    /// Protobuf 解码器
    /// 此时已由 LengthFieldBasedFrameDecoder 处理完粘包，Input 即为完整的 Protobuf 内容（无长度头）
    /// </summary>
    public class ProtobufDecoder : MessageToMessageDecoder<IByteBuffer>
    {
        protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            // 读取所有可读字节（即一个完整的消息体）
            int length = input.ReadableBytes;
            if (length <= 0) return;

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
