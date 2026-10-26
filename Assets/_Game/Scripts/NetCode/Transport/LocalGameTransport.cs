using System;
using System.Collections.Generic;

namespace AFPS.NetCode.Transport
{
    /// <summary>单人练习专用的主线程字节传输对；不创建套接字，仍经过完整协议编解码。</summary>
    public sealed class LocalGameTransport : IGameTransport
    {
        private const int MaxPayloadBytes = 2048;
        private const int MaxQueuedPackets = 256; // 每侧最多缓存256个包，避免暂停消费时无限分配。
        private static readonly TransportConnectionId Connection = new TransportConnectionId(1);
        private readonly Queue<(GameTransportEvent Event, byte[] Payload)> events =
            new Queue<(GameTransportEvent, byte[])>();
        private LocalGameTransport peer;
        public bool IsRunning { get; private set; }
        public TransportRole Role { get; private set; }

        private LocalGameTransport(TransportRole role)
        {
            Role = role;
            IsRunning = true;
            events.Enqueue((new GameTransportEvent(TransportEventType.Connected, Connection), null));
        }

        public static void CreatePair(out LocalGameTransport server, out LocalGameTransport client)
        {
            server = new LocalGameTransport(TransportRole.Server);
            client = new LocalGameTransport(TransportRole.Client);
            server.peer = client;
            client.peer = server;
        }

        public bool TryStartServer(ushort port, int maxConnections, out string error)
        {
            error = "本地传输只能通过CreatePair创建，不接受外部连接。";
            return false;
        }

        public bool TryStartClient(string address, ushort port, out string error)
        {
            error = "本地传输只能通过CreatePair创建，不连接网络地址。";
            return false;
        }

        public void Pump()
        {
        }

        public bool TryPollEvent(ArraySegment<byte> receiveBuffer, out GameTransportEvent transportEvent)
        {
            transportEvent = default;
            if (!IsRunning || events.Count == 0)
            {
                return false;
            }
            var packet = events.Dequeue();
            transportEvent = packet.Event;
            if (packet.Payload == null)
            {
                return true;
            }
            if (receiveBuffer.Count < packet.Payload.Length)
            {
                transportEvent = new GameTransportEvent(TransportEventType.ReceiveBufferTooSmall,
                    Connection, packet.Event.Delivery, packet.Payload.Length);
                return true;
            }
            Array.Copy(packet.Payload, 0, receiveBuffer.Array, receiveBuffer.Offset, packet.Payload.Length);
            return true;
        }

        public TransportSendResult Send(TransportConnectionId connectionId, TransportDelivery delivery,
            ArraySegment<byte> payload)
        {
            if (!IsRunning)
            {
                return TransportSendResult.NotRunning;
            }
            if (connectionId != Connection || peer == null || !peer.IsRunning)
            {
                return TransportSendResult.InvalidConnection;
            }
            if (payload.Count > MaxPayloadBytes)
            {
                return TransportSendResult.PayloadTooLarge;
            }
            if (peer.events.Count >= MaxQueuedPackets || payload.Array == null)
            {
                return TransportSendResult.TransportError;
            }

            // 发送方立即复用序列化缓冲区；必须复制字节，不能让后续包覆盖尚未消费的内容。
            var copy = new byte[payload.Count];
            Array.Copy(payload.Array, payload.Offset, copy, 0, payload.Count);
            peer.events.Enqueue((new GameTransportEvent(TransportEventType.Data,
                Connection, delivery, copy.Length), copy));
            return TransportSendResult.Success;
        }

        public void Disconnect(TransportConnectionId connectionId)
        {
            if (connectionId == Connection && IsRunning && peer != null)
            {
                var other = peer;
                peer = null;
                other.peer = null;
                // 断开先丢弃待处理数据，防止清会话后消费旧输入或旧实体分配。
                events.Clear();
                other.events.Clear();
                events.Enqueue((new GameTransportEvent(TransportEventType.Disconnected, Connection), null));
                other.events.Enqueue((new GameTransportEvent(TransportEventType.Disconnected, Connection), null));
            }
        }

        public void Stop()
        {
            Disconnect(Connection);
            events.Clear();
            IsRunning = false;
            Role = TransportRole.None;
        }

        public void Dispose() => Stop();
    }
}
