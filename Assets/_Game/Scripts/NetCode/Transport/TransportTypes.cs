using System;

namespace AFPS.NetCode.Transport
{
    /// <summary>
    /// 传输实例当前承担的网络角色。
    /// </summary>
    public enum TransportRole
    {
        None,
        Client,
        Server
    }

    /// <summary>
    /// 一个数据包需要的传输语义，而不是具体网络库的管线类型。
    /// </summary>
    public enum TransportDelivery
    {
        Unreliable,
        UnreliableSequenced,
        ReliableSequenced
    }

    /// <summary>
    /// 传输层向上层报告的事件类型。
    /// </summary>
    public enum TransportEventType
    {
        Connected,
        Disconnected,
        Data,
        ReceiveBufferTooSmall
    }

    /// <summary>
    /// 发送操作的即时结果。成功只表示数据已交给传输层，不表示对端已经收到。
    /// </summary>
    public enum TransportSendResult
    {
        Success,
        NotRunning,
        InvalidConnection,
        PayloadTooLarge,
        TransportError
    }

    /// <summary>
    /// AFPS 自己分配的连接标识，不依赖具体传输库的内部连接编号。
    /// </summary>
    public readonly struct TransportConnectionId : IEquatable<TransportConnectionId>
    {
        /// <summary>
        /// 当前传输实例生命周期内唯一的连接编号；0 表示无效连接。
        /// </summary>
        public readonly uint Value;

        public bool IsValid => Value != 0;

        public TransportConnectionId(uint value)
        {
            Value = value;
        }

        public bool Equals(TransportConnectionId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is TransportConnectionId other && Equals(other);

        public override int GetHashCode() => (int)Value;

        public override string ToString() => IsValid ? Value.ToString() : "Invalid";

        public static bool operator ==(TransportConnectionId left, TransportConnectionId right) => left.Equals(right);

        public static bool operator !=(TransportConnectionId left, TransportConnectionId right) => !left.Equals(right);
    }

    /// <summary>
    /// 传输层事件的元数据。Data 的字节内容写在调用 TryPollEvent 时提供的缓冲区中。
    /// </summary>
    public readonly struct GameTransportEvent
    {
        /// <summary>
        /// 本次连接、断开或数据事件的类型。
        /// </summary>
        public readonly TransportEventType Type;

        /// <summary>
        /// 产生本事件的连接。
        /// </summary>
        public readonly TransportConnectionId ConnectionId;

        /// <summary>
        /// Data 事件实际使用的传输语义；连接事件不使用该字段。
        /// </summary>
        public readonly TransportDelivery Delivery;

        /// <summary>
        /// Data 的有效字节数；缓冲区过小时表示完整数据包所需的字节数。
        /// </summary>
        public readonly int PayloadLength;

        public GameTransportEvent(TransportEventType type, TransportConnectionId connectionId, TransportDelivery delivery = default, int payloadLength = 0)
        {
            Type = type;
            ConnectionId = connectionId;
            Delivery = delivery;
            PayloadLength = payloadLength;
        }
    }
}
