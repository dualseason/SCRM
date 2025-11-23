using Xunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SCRM.Core.Netty;
using SCRM.Services;
using System.Threading.Tasks;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using System.Net;
using Jubo.JuLiao.IM.Wx.Proto;
using Google.Protobuf;
using System;
using Google.Protobuf.WellKnownTypes;
using Serilog;

namespace SCRM.TEST.Netty
{
    public class ServerIntegrationTests : IAsyncLifetime
    {
        private ServiceProvider _serviceProvider;
        private NettyServer _server;
        private ConnectionManager _connectionManager;
        private ClientTaskService _clientTaskService;
        private IChannel _clientChannel;
        private MultithreadEventLoopGroup _clientGroup;

        public async Task InitializeAsync()
        {
            // Setup DI
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            services.AddSingleton<ConnectionManager>();
            services.AddSingleton<MessageRouter>();
            services.AddSingleton<NettyServer>();
            services.AddSingleton<NettyMessageService>();
            services.AddSingleton<ClientTaskService>();

            // Initialize static logger for NettyServer
            SCRM.Shared.Core.Utility.logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            _serviceProvider = services.BuildServiceProvider();

            _connectionManager = _serviceProvider.GetRequiredService<ConnectionManager>();
            _server = _serviceProvider.GetRequiredService<NettyServer>();
            _clientTaskService = _serviceProvider.GetRequiredService<ClientTaskService>();

            // Start Server
            _server.Port = 8082; // Use a different port for testing
            await _server.StartAsync();
        }

        public async Task DisposeAsync()
        {
            if (_clientChannel != null) await _clientChannel.CloseAsync();
            if (_clientGroup != null) await _clientGroup.ShutdownGracefullyAsync();
            await _server.StopAsync();
            await _serviceProvider.DisposeAsync();
        }

        private async Task<IChannel> ConnectClientAsync(Action<TransportMessage> onMessageReceived)
        {
            _clientGroup = new MultithreadEventLoopGroup();
            var bootstrap = new Bootstrap();
            bootstrap
                .Group(_clientGroup)
                .Channel<TcpSocketChannel>()
                .Option(ChannelOption.TcpNodelay, true)
                .Handler(new ActionChannelInitializer<ISocketChannel>(channel =>
                {
                    IChannelPipeline pipeline = channel.Pipeline;
                    pipeline.AddLast(new ProtobufEncoder());
                    pipeline.AddLast(new ProtobufDecoder());
                    pipeline.AddLast(new SimpleClientHandler(onMessageReceived));
                }));

            return await bootstrap.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 8082));
        }

        [Fact]
        public async Task FullFlow_IntegrationTest()
        {
            // 1. Connect Client
            TransportMessage receivedMessage = null;
            var tcs = new TaskCompletionSource<TransportMessage>();

            _clientChannel = await ConnectClientAsync(msg =>
            {
                receivedMessage = msg;
                tcs.TrySetResult(msg);
            });

            _clientChannel.Active.Should().BeTrue();

            // 2. Send DeviceAuthReq
            var authReq = new DeviceAuthReqMessage
            {
                AuthType = DeviceAuthReqMessage.Types.EnumAuthType.DeviceCode,
                Credential = "test-device-credential"
            };

            var authMsg = new TransportMessage
            {
                Id = 1,
                MsgType = EnumMsgType.DeviceAuthReq,
                Content = Any.Pack(authReq)
            };

            await _clientChannel.WriteAndFlushAsync(authMsg);

            // 3. Verify DeviceAuthRsp
            var response = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            response.MsgType.Should().Be(EnumMsgType.DeviceAuthRsp);
            var authRsp = response.Content.Unpack<DeviceAuthRspMessage>();
            authRsp.AccessToken.Should().NotBeNullOrEmpty();

            // 4. Verify ConnectionManager has the connection
            // Wait a bit for async registration
            await Task.Delay(100);
            var connections = await _connectionManager.GetAllConnectionsAsync();
            var connectionInfo = connections.FirstOrDefault(c => c.DeviceInfo == "test-device-credential");
            connectionInfo.Should().NotBeNull();
            var connectionId = connectionInfo.ConnectionId;

            // 5. Send HeartBeatReq
            tcs = new TaskCompletionSource<TransportMessage>(); // Reset TCS
            var heartBeatMsg = new TransportMessage
            {
                Id = 2,
                MsgType = EnumMsgType.HeartBeatReq
            };
            await _clientChannel.WriteAndFlushAsync(heartBeatMsg);

            // 6. Verify MsgReceivedAck
            response = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            response.MsgType.Should().Be(EnumMsgType.MsgReceivedAck);
            response.RefMessageId.Should().Be(2);

            // 7. Server sends Task (TalkToFriend)
            tcs = new TaskCompletionSource<TransportMessage>(); // Reset TCS
            await _clientTaskService.SendTalkToFriendTaskAsync(connectionId, "wxid_friend", "Hello World");

            // 8. Verify Client receives Task
            response = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            response.MsgType.Should().Be(EnumMsgType.TalkToFriendTask);
            var taskMsg = response.Content.Unpack<TalkToFriendTaskMessage>();
            taskMsg.FriendId.Should().Be("wxid_friend");
            taskMsg.Content.ToStringUtf8().Should().Be("Hello World");
        }

        private class SimpleClientHandler : SimpleChannelInboundHandler<TransportMessage>
        {
            private readonly Action<TransportMessage> _onMessageReceived;

            public SimpleClientHandler(Action<TransportMessage> onMessageReceived)
            {
                _onMessageReceived = onMessageReceived;
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, TransportMessage msg)
            {
                _onMessageReceived(msg);
            }
        }
    }
}
