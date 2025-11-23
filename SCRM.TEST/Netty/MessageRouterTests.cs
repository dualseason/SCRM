using Xunit;
using FluentAssertions;
using Moq;
using SCRM.Core.Netty;
using Jubo.JuLiao.IM.Wx.Proto;
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

        public MessageRouterTests(TestInitializer initializer) : base(initializer)
        {
            var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<ConnectionManager>>();
            _connectionManager = new ConnectionManager(mockLogger.Object);
            _router = new MessageRouter(_connectionManager);
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

            TransportMessage? sentMessage = null;
            _mockContext.Setup(x => x.WriteAndFlushAsync(It.IsAny<object>()))
                .Callback<object>(msg => sentMessage = msg as TransportMessage)
                .Returns(Task.CompletedTask);

            // Act
            await _router.RouteMessage(message, _mockContext.Object);

            // Assert
            sentMessage.Should().NotBeNull();
            sentMessage!.MsgType.Should().Be(EnumMsgType.MsgReceivedAck);
            sentMessage.RefMessageId.Should().Be(123);
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
