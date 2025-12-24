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
            
            // 手动写入4字节长度头 (大端序)，确保客户端能正确拆包
            // 这种方式比使用 LengthFieldPrepender 更直观且易于调试
            output.WriteInt(bytes.Length);
            
            // 写入消息体
            output.WriteBytes(bytes);
        }
    }
}
