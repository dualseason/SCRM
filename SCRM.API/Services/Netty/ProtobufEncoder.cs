using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;
using Google.Protobuf;
using SCRM.SHARED.Proto;

namespace SCRM.Services.Netty
{
    /// <summary>
    /// Protobuf 编码器
    /// 负责将 TransportMessage 序列化并添加 4 字节长度头
    /// </summary>
    public class ProtobufEncoder : MessageToByteEncoder<TransportMessage>
    {
        protected override void Encode(IChannelHandlerContext context, TransportMessage message, IByteBuffer output)
        {
            // 序列化消息为字节数组
            byte[] bytes = message.ToByteArray();
            // 先写入4字节长度（大端序）
            output.WriteInt(bytes.Length);
            // 再写入消息体
            output.WriteBytes(bytes);
        }
    }
}
