using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Microsoft.Extensions.Logging;
using SCRM.Services;
using System;
using System.Net;
using System.Threading.Tasks;

namespace SCRM.Netty
{
    public class NettyServer : INettyServer
    {
        private readonly ILogger<NettyServer> _logger;
        private IChannel _channel;
        private readonly IEventLoopGroup _bossGroup;
        private readonly IEventLoopGroup _workerGroup;
        private readonly ISignalRMessageService _signalRMessageService;
        private readonly IRocketMQProducerService _rocketMQProducer;

        public NettyServer(
            ILogger<NettyServer> logger,
            ISignalRMessageService signalRMessageService,
            IRocketMQProducerService rocketMQProducer)
        {
            _logger = logger;
            _signalRMessageService = signalRMessageService;
            _rocketMQProducer = rocketMQProducer;
            Port = 8081; // 默认端口

            _bossGroup = new MultithreadEventLoopGroup(1);
            _workerGroup = new MultithreadEventLoopGroup();
            _channel = null!;
        }

        private ServerBootstrap CreateBootstrap()
        {
            var bootstrap = new ServerBootstrap();
            bootstrap.Group(_bossGroup, _workerGroup)
                    .Channel<TcpServerSocketChannel>()
                    .Option(ChannelOption.SoBacklog, 8192)
                    .ChildHandler(new ActionChannelInitializer<IChannel>(channel =>
                    {
                        IChannelPipeline pipeline = channel.Pipeline;
                        pipeline.AddLast(new DotNetty.Codecs.LengthFieldBasedFrameDecoder(65536, 0, 8, 0, 0));
                        pipeline.AddLast(new DotNetty.Codecs.StringDecoder());
                        pipeline.AddLast(new DotNetty.Codecs.StringEncoder());
                        var messageHandlerLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<NettyMessageHandler>.Instance;
                        pipeline.AddLast(new NettyMessageHandler(messageHandlerLogger, _signalRMessageService, _rocketMQProducer));
                    }));

            return bootstrap;
        }

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