using ProtoBuf;
using System;

namespace SCRM.Core.Netty
{
    [ProtoContract]
    public class NettyMessage
    {
        [ProtoMember(1)]
        public string Type { get; set; } = string.Empty;

        [ProtoMember(2)]
        public string TargetType { get; set; } = string.Empty;

        [ProtoMember(3)]
        public string TargetId { get; set; } = string.Empty;

        // Protobuf-net handles object serialization if the type is known or dynamic
        // For simplicity, we might want to serialize Content as a string or byte[] if it's arbitrary JSON
        // But let's try to keep it as object for now, assuming it's a primitive or a known contract
        // However, 'object' is tricky in Protobuf. Let's change it to string (JSON) for compatibility
        // or keep it as is if we are sure.
        // Given the previous code used JSON serialization, let's stick to string Content for Protobuf simplicity
        // and serialize the inner object to JSON string.
        [ProtoMember(4)]
        public string Content { get; set; } = string.Empty; 

        [ProtoMember(5)]
        public string? Tag { get; set; }

        [ProtoMember(6)]
        public string? Topic { get; set; }

        [ProtoMember(7)]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [ProtoMember(8)]
        public string? Source { get; set; }
    }
}
