using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using System;
using System.Net;
using System.Threading.Tasks;
using SCRM.Shared.Core;
using SCRM.Services;
using Microsoft.Extensions.Options;
using SCRM.Models.Configurations;

namespace SCRM.Services.Netty
{
    /// <summary>
    /// Netty 服务器
    /// 负责启动 TCP 监听，配置 Channel Pipeline（Protobuf 编解码器，业务处理器）
    /// </summary>
    public class NettyServer
    {
        private readonly Microsoft.Extensions.Logging.ILogger<NettyServer> _logger;

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
        private readonly NettySettings _nettySettings;

        public NettyServer(IServiceProvider serviceProvider, IOptions<NettySettings> nettySettings, Microsoft.Extensions.Logging.ILogger<NettyServer> logger)
        {
            _serviceProvider = serviceProvider;
            _nettySettings = nettySettings.Value;
            _logger = logger;
            Port = _nettySettings.Port;

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
                        
                        // 入站解码器：将接收到的 ByteBuf 解码为 TransportMessage
                        pipeline.AddLast(new ProtobufDecoder());
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
                if (_channel == null || !_channel.Open)
                {
                    _logger.LogInformation("Starting Netty server on port {Port}", Port);
                    var bootstrap = CreateBootstrap();
                    _channel = await bootstrap.BindAsync(IPAddress.Any, Port);
                    _logger.LogInformation("Netty server started successfully on port {Port}", Port);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Netty server");
                throw;
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
        public int Port { get; set; }
    }
}