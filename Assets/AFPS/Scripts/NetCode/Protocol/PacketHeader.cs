namespace AFPS.NetCode.Protocol
{
    /// <summary>
    /// 每个 AFPS 数据包开头都携带的固定元数据。
    /// </summary>
    public readonly struct PacketHeader
    {
        /// <summary>
        /// 线上的四字节协议标记，按小端序写入后对应 ASCII 字符 AFPS。
        /// </summary>
        public const uint ExpectedMagic = 0x53504641;

        /// <summary>
        /// 当前协议版本；产生不兼容的二进制布局变化时必须递增。
        /// </summary>
        public const byte CurrentProtocolVersion = 1;

        /// <summary>
        /// 固定包头占用的字节数。
        /// </summary>
        public const int Size = 12;

        /// <summary>
        /// 标识数据属于 AFPS 协议，用于尽早拒绝错误数据。
        /// </summary>
        public readonly uint Magic;

        /// <summary>
        /// 发送端使用的 AFPS 二进制协议版本。
        /// </summary>
        public readonly byte ProtocolVersion;

        /// <summary>
        /// 包头之后负载的消息类型。
        /// </summary>
        public readonly NetworkMessageType MessageType;

        /// <summary>
        /// 不包含包头的负载字节数。
        /// </summary>
        public readonly ushort PayloadLength;

        /// <summary>
        /// 发送端针对当前连接递增的包序号，用于统计丢包、重复和乱序；允许 uint 回绕。
        /// </summary>
        public readonly uint Sequence;

        public PacketHeader(NetworkMessageType messageType, ushort payloadLength, uint sequence)
            : this(ExpectedMagic, CurrentProtocolVersion, messageType, payloadLength, sequence)
        {
        }

        internal PacketHeader(uint magic, byte protocolVersion, NetworkMessageType messageType, ushort payloadLength, uint sequence)
        {
            Magic = magic;
            ProtocolVersion = protocolVersion;
            MessageType = messageType;
            PayloadLength = payloadLength;
            Sequence = sequence;
        }
    }
}
