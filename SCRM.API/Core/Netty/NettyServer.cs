using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using System;
using System.Net;
using System.Threading.Tasks;
using SCRM.Shared.Core;

namespace SCRM.Core.Netty
{
    public class NettyServer : INettyServer
    {
        private readonly IServiceProvider _serviceProvider;
        private IChannel _channel;
        private readonly IEventLoopGroup _bossGroup;
        private readonly IEventLoopGroup _workerGroup;

        public NettyServer(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            Port = 8081;

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
                        
                        pipeline.AddLast(new ProtobufDecoder());
                        pipeline.AddLast(new ProtobufEncoder());

                        var messageRouter = (MessageRouter)_serviceProvider.GetService(typeof(MessageRouter))!;
                        
                        pipeline.AddLast(new NettyMessageHandler(messageRouter));
                    }));

            return bootstrap;
        }

        public async Task StartAsync()
        {
            try
            {
                if (_channel == null || !_channel.Open)
                {
                    Utility.logger.Information("Starting Netty server on port {Port}", Port);
                    var bootstrap = CreateBootstrap();
                    _channel = await bootstrap.BindAsync(IPAddress.Any, Port);
                    Utility.logger.Information("Netty server started successfully on port {Port}", Port);
                }
            }
            catch (Exception ex)
            {
                Utility.logger.Error(ex, "Failed to start Netty server");
                throw;
            }
        }

        public async Task StopAsync()
        {
            try
            {
                if (_channel != null && _channel.Open)
                {
                    Utility.logger.Information("Stopping Netty server");
                    await _channel.CloseAsync();
                    Utility.logger.Information("Netty server stopped successfully");
                }

                await Task.WhenAll(
                    _bossGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)),
                    _workerGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1))
                );
            }
            catch (Exception ex)
            {
                Utility.logger.Error(ex, "Failed to stop Netty server");
                throw;
            }
        }

        public bool IsRunning => _channel?.Open ?? false;
        public int Port { get; set; }
    }
}