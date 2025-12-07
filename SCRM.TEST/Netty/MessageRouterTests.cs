using Xunit;
using FluentAssertions;
using Moq;
using SCRM.Core.Netty;
using SCRM.SHARED.Proto;
using DotNetty.Transport.Channels;
using System.Threading.Tasks;
using SCRM.Services;

namespace SCRM.TEST.Netty
{
    public class MessageRouterTests : TestBase
    {
        private readonly MessageRouter _router;
        private readonly Mock<IChannelHandlerContext> _mockContext;
        private readonly Mock<IChannel> _mockChannel;
        private readonly ConnectionManager _connectionManager;

        private readonly Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory> _mockScopeFactory;

        public MessageRouterTests(TestInitializer initializer) : base(initializer)
        {
            var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<ConnectionManager>>();
            _connectionManager = new ConnectionManager(mockLogger.Object);
            _mockScopeFactory = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();
            _router = new MessageRouter(_connectionManager, _mockScopeFactory.Object);
            _mockContext = new Mock<IChannelHandlerContext>();
            _mockChannel = new Mock<IChannel>();
            _mockContext.Setup(x => x.Channel).Returns(_mockChannel.Object);
        }

        [Fact]
        public async Task RouteMessage_HeartBeat_ShouldSendAck()
        {
            // Arrange
            var message = new TransportMessage
            {
                Id = 123,
                MsgType = EnumMsgType.HeartBeatReq,
                AccessToken = "test-token"
            };

            object? sentMessage = null;
            var mockChannelId = "test-channel-id-12345";

            // Mock the Channel ID
            var channelIdMock = new Mock<DotNetty.Transport.Channels.IChannelId>();
            channelIdMock.Setup(x => x.AsLongText()).Returns(mockChannelId);
            _mockChannel.Setup(x => x.Id).Returns(channelIdMock.Object);

            _mockContext.Setup(x => x.WriteAndFlushAsync(It.IsAny<object>()))
                .Callback<object>(msg => sentMessage = msg)
                .Returns(Task.CompletedTask);

            // Act
            await _router.RouteMessage(message, _mockContext.Object);

            // Assert
            sentMessage.Should().NotBeNull();
            sentMessage.Should().BeOfType<TransportMessage>();

            var transportMessage = sentMessage as TransportMessage;
            transportMessage!.MsgType.Should().Be(EnumMsgType.MsgReceivedAck);
            transportMessage.Id.Should().Be(123);
            transportMessage.RefMessageId.Should().Be(123);
        }

        [Fact]
        public async Task RouteMessage_DeviceAuth_ShouldNotThrow()
        {
            // Arrange
            var message = new TransportMessage
            {
                Id = 456,
                MsgType = EnumMsgType.DeviceAuthReq
            };

            // Act
            Func<Task> act = async () => await _router.RouteMessage(message, _mockContext.Object);

            // Assert
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task RouteMessage_TalkToFriend_ShouldNotThrow()
        {
            // Arrange
            var message = new TransportMessage
            {
                Id = 789,
                MsgType = EnumMsgType.TalkToFriendTask
            };

            // Act
            Func<Task> act = async () => await _router.RouteMessage(message, _mockContext.Object);

            // Assert
            await act.Should().NotThrowAsync();
        }

        [Theory]
        [InlineData(EnumMsgType.WeChatOnlineNotice)]
        [InlineData(EnumMsgType.WeChatOfflineNotice)]
        public async Task RouteMessage_WeChatStatus_ShouldNotThrow(EnumMsgType msgType)
        {
            // Arrange
            var message = new TransportMessage
            {
                Id = 999,
                MsgType = msgType
            };

            // Act
            Func<Task> act = async () => await _router.RouteMessage(message, _mockContext.Object);

            // Assert
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task RouteMessage_UnknownType_ShouldNotThrow()
        {
            // Arrange
            var message = new TransportMessage
            {
                Id = 111,
                MsgType = EnumMsgType.UnknownMsg
            };

            // Act
            Func<Task> act = async () => await _router.RouteMessage(message, _mockContext.Object);

            // Assert
            await act.Should().NotThrowAsync();
        }
    }
}
