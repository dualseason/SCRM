using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using DotNetty.Codecs;
using System;
using System.Net;
using System.Threading.Tasks;
using SCRM.Shared.Core;
using SCRM.Services;
using SCRM.API.Services.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SCRM.Models.Configurations;

namespace SCRM.Services.Netty
{
    /// <summary>
    /// Netty 服务器
    /// 负责启动 TCP 监听，配置 Channel Pipeline（Protobuf 编解码器，业务处理器）
    /// [AntiGravity] Refactored: Port Configuration from AppSettings (Infrastructure)
    /// </summary>
    public class NettyServer
    {
        private readonly Microsoft.Extensions.Logging.ILogger<NettyServer> _logger;
        private readonly NettySettings _nettySettings;

        private readonly IServiceProvider _serviceProvider;
        private IChannel _channel;
        /// <summary>
        /// Boss 线程组，用于处理客户端连接请求
        /// </summary>
        private readonly IEventLoopGroup _bossGroup;
        /// <summary>
        /// Worker 线程组，用于处理 Socket IO 读写
        /// </summary>
        private readonly IEventLoopGroup _workerGroup;

        public int Port { get; private set; }

        public NettyServer(IServiceProvider serviceProvider, Microsoft.Extensions.Logging.ILogger<NettyServer> logger, IOptions<NettySettings> nettySettings)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _nettySettings = nettySettings.Value;

            _bossGroup = new MultithreadEventLoopGroup(1);
            _workerGroup = new MultithreadEventLoopGroup();
            _channel = null!;
        }

        /// <summary>
        /// 创建并配置服务器启动引导程序
        /// </summary>
        /// <returns>ServerBootstrap 实例</returns>
        private ServerBootstrap CreateBootstrap()
        {
            var bootstrap = new ServerBootstrap();
            bootstrap.Group(_bossGroup, _workerGroup)
                    .Channel<TcpServerSocketChannel>()
                    .Option(ChannelOption.SoBacklog, 8192) // 设置连接等待队列大小
                    .ChildHandler(new ActionChannelInitializer<IChannel>(channel =>
                    {
                        IChannelPipeline pipeline = channel.Pipeline;
                        
                        // 入站解码器：处理 TCP 粘包/拆包，确保 ProtobufDecoder 接收到完整的数据包
                        // MaxFrameLength: 100MB, LengthFieldOffset: 0, LengthFieldLength: 4, LengthAdjustment: 0, InitialBytesToStrip: 4
                        // Strip=4 意味着移除 4 字节长度头，下游 ProtobufDecoder 只接收纯消息体
                        pipeline.AddLast(new LengthFieldBasedFrameDecoder(100 * 1024 * 1024, 0, 4, 0, 4));

                        // 入站解码器：将接收到的 ByteBuf 解码为 TransportMessage
                        pipeline.AddLast(new ProtobufDecoder());
                        
                        // 调试: 打印出站消息的 Hex (必须放在 Encoder 之前添加，这样在 Outbound 流程中它会在 Encoder 之后执行)
                        // 出站顺序: Tail -> Encoder -> HexDump -> Head
                        var hexLogger = (Microsoft.Extensions.Logging.ILogger<HexDumpChannelHandler>)_serviceProvider.GetService(typeof(Microsoft.Extensions.Logging.ILogger<HexDumpChannelHandler>));
                        pipeline.AddLast(new HexDumpChannelHandler(hexLogger));

                        // 出站编码器：将 TransportMessage 编码为 ByteBuf 发送
                        pipeline.AddLast(new ProtobufEncoder());

                        // 业务处理均需通过依赖注入获取
                        var messageRouter = (MessageRouter)_serviceProvider.GetService(typeof(MessageRouter))!;
                        var connectionManager = (ConnectionManager)_serviceProvider.GetService(typeof(ConnectionManager))!;
                        var handlerLogger = (Microsoft.Extensions.Logging.ILogger<NettyMessageHandler>)_serviceProvider.GetService(typeof(Microsoft.Extensions.Logging.ILogger<NettyMessageHandler>))!;
                        
                        // 业务处理器：处理解码后的消息
                        pipeline.AddLast(new NettyMessageHandler(messageRouter, connectionManager, handlerLogger));
                    }));

            return bootstrap;
        }

        /// <summary>
        /// 异步启动服务器
        /// </summary>
        public async Task StartAsync()
        {
            try
            {
                // 使用 AppSettings 配置的端口进行绑定 (基础设施配置)
                this.Port = _nettySettings.Port;
                
                if (Port <= 0)
                {
                    _logger.LogError("以前的端口配置无效 (Port={Port}). Defaulting to 8647", Port);
                    this.Port = 8647;
                }

                _logger.LogInformation("正在启动 Netty 服务器，绑定端口: {Port}", Port); // Simplified log

                var bootstrap = CreateBootstrap();
                _channel = await bootstrap.BindAsync(IPAddress.Any, Port);
                
                _logger.LogInformation("Netty 服务器启动成功，监听端口: {Port}", Port);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Netty 服务器启动失败");
                throw; // 抛出异常以便 Host 知道启动失败
            }
        }

        /// <summary>
        /// 异步停止服务器
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                if (_channel != null && _channel.Open)
                {
                    _logger.LogInformation("Stopping Netty server");
                    await _channel.CloseAsync();
                    _logger.LogInformation("Netty server stopped successfully");
                }

                // 优雅关闭线程组
                await Task.WhenAll(
                    _bossGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)),
                    _workerGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1))
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop Netty server");
                throw;
            }
        }

        public bool IsRunning => _channel?.Open ?? false;
    }
}