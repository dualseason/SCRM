using Xunit;
using FluentAssertions;
using Jubo.JuLiao.IM.Wx.Proto;
using Google.Protobuf;

namespace SCRM.TEST.Proto
{
    public class ProtoSerializationTests
    {
        [Fact]
        public void TransportMessage_Serialization_ShouldSucceed()
        {
            // Arrange
            var message = new TransportMessage
            {
                Id = 12345,
                AccessToken = "test-token-123",
                MsgType = EnumMsgType.HeartBeatReq,
                RefMessageId = 0
            };

            // Act
            byte[] bytes = message.ToByteArray();
            var deserialized = TransportMessage.Parser.ParseFrom(bytes);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.Id.Should().Be(12345);
            deserialized.AccessToken.Should().Be("test-token-123");
            deserialized.MsgType.Should().Be(EnumMsgType.HeartBeatReq);
        }

        [Fact]
        public void TransportMessage_EmptyContent_ShouldSerializeCorrectly()
        {
            // Arrange
            var message = new TransportMessage
            {
                Id = 99999,
                AccessToken = "token-abc",
                MsgType = EnumMsgType.HeartBeatReq
            };

            // Act
            byte[] bytes = message.ToByteArray();
            var deserialized = TransportMessage.Parser.ParseFrom(bytes);

            // Assert
            deserialized.Id.Should().Be(99999);
            deserialized.AccessToken.Should().Be("token-abc");
        }

        [Theory]
        [InlineData(EnumMsgType.HeartBeatReq)]
        [InlineData(EnumMsgType.DeviceAuthReq)]
        [InlineData(EnumMsgType.TalkToFriendTask)]
        [InlineData(EnumMsgType.WeChatOnlineNotice)]
        public void TransportMessage_DifferentMessageTypes_ShouldSerialize(EnumMsgType msgType)
        {
            // Arrange
            var message = new TransportMessage
            {
                Id = 1,
                MsgType = msgType
            };

            // Act
            byte[] bytes = message.ToByteArray();
            var deserialized = TransportMessage.Parser.ParseFrom(bytes);

            // Assert
            deserialized.MsgType.Should().Be(msgType);
        }

        [Fact]
        public void TransportMessage_LargeId_ShouldHandleCorrectly()
        {
            // Arrange
            var message = new TransportMessage
            {
                Id = long.MaxValue,
                MsgType = EnumMsgType.HeartBeatReq
            };

            // Act
            byte[] bytes = message.ToByteArray();
            var deserialized = TransportMessage.Parser.ParseFrom(bytes);

            // Assert
            deserialized.Id.Should().Be(long.MaxValue);
        }

        [Fact]
        public void EnumMsgType_AllValues_ShouldBeValid()
        {
            // Assert - 验证关键消息类型存在
            ((int)EnumMsgType.HeartBeatReq).Should().Be(1001);
            ((int)EnumMsgType.DeviceAuthReq).Should().Be(1010);
            ((int)EnumMsgType.TalkToFriendTask).Should().Be(1070);
            ((int)EnumMsgType.WeChatOnlineNotice).Should().Be(1020);
        }
    }
}
