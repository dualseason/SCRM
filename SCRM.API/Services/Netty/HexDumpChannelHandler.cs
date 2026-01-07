using System;
using System.Text;
using System.Threading.Tasks;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using DotNetty.Common.Concurrency;
using Microsoft.Extensions.Logging;

namespace SCRM.Services.Netty
{
    public class HexDumpChannelHandler : ChannelHandlerAdapter
    {
        private readonly ILogger _logger;
        private readonly string _direction;

        public HexDumpChannelHandler(ILogger logger, string direction = "OUT")
        {
            _logger = logger;
            _direction = direction;
        }

        public override System.Threading.Tasks.Task WriteAsync(IChannelHandlerContext context, object message)
        {
            if (message is IByteBuffer buffer)
            {
                int length = buffer.ReadableBytes;
                if (length > 0)
                {
                    string hex = ByteBufferUtil.HexDump(buffer);
                    _logger.LogInformation($"[{_direction}] Length: {length}, Hex: {hex}");
                }
            }
            return base.WriteAsync(context, message);
        }
    }
}