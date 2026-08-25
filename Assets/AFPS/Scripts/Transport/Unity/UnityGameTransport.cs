using System;
using System.Collections.Generic;
using AFPS.NetCode.Transport;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;

namespace AFPS.Transport.Unity
{
    /// <summary>
    /// 将 AFPS 的通用传输契约适配到 Unity Transport 2.x。
    /// 该对象拥有原生网络资源，因此必须调用 Stop 或 Dispose。
    /// </summary>
    public sealed class UnityGameTransport : IGameTransport
    {
        private readonly Dictionary<uint, NetworkConnection> connections = new Dictionary<uint, NetworkConnection>();
        private readonly List<uint> pollOrder = new List<uint>();
        private readonly Queue<uint> pendingConnected = new Queue<uint>();

        private NetworkDriver driver;
        private NetworkPipeline unreliableSequencedPipeline;
        private NetworkPipeline reliableSequencedPipeline;
        private uint nextConnectionId = 1;
        private int pollIndex;
        private int maxConnections;

        public bool IsRunning => driver.IsCreated;

        public TransportRole Role { get; private set; }

        public bool TryStartServer(ushort port, int maximumConnections, out string error)
        {
            if (IsRunning)
            {
                error = "传输实例已经启动。";
                return false;
            }

            if (maximumConnections <= 0)
            {
                error = "服务器最大连接数必须大于 0。";
                return false;
            }

            CreateDriver();
            NetworkEndpoint endpoint = NetworkEndpoint.AnyIpv4.WithPort(port);
            if (driver.Bind(endpoint) != 0)
            {
                error = $"无法绑定 UDP 端口 {port}。";
                Stop();
                return false;
            }

            if (driver.Listen() != 0)
            {
                error = $"无法监听 UDP 端口 {port}。";
                Stop();
                return false;
            }

            maxConnections = maximumConnections;
            Role = TransportRole.Server;
            error = null;
            return true;
        }

        public bool TryStartClient(string address, ushort port, out string error)
        {
            if (IsRunning)
            {
                error = "传输实例已经启动。";
                return false;
            }

            if (!NetworkEndpoint.TryParse(address, port, out NetworkEndpoint endpoint))
            {
                error = $"无法解析服务器地址 {address}:{port}。当前接口需要 IP 地址。";
                return false;
            }

            CreateDriver();
            NetworkConnection connection = driver.Connect(endpoint);
            if (!connection.IsCreated)
            {
                error = $"无法开始连接服务器 {address}:{port}。";
                Stop();
                return false;
            }

            AddConnection(connection, false);
            Role = TransportRole.Client;
            error = null;
            return true;
        }

        public void Pump()
        {
            if (!IsRunning)
            {
                return;
            }

            driver.ScheduleUpdate().Complete();
            if (Role == TransportRole.Server)
            {
                AcceptServerConnections();
            }
        }

        public unsafe bool TryPollEvent(ArraySegment<byte> receiveBuffer, out GameTransportEvent transportEvent)
        {
            transportEvent = default;
            if (!IsRunning)
            {
                return false;
            }

            if (pendingConnected.Count > 0)
            {
                transportEvent = new GameTransportEvent(TransportEventType.Connected, new TransportConnectionId(pendingConnected.Dequeue()));
                return true;
            }

            int examinedConnections = 0;
            while (examinedConnections < pollOrder.Count)
            {
                if (pollIndex >= pollOrder.Count)
                {
                    pollIndex = 0;
                }

                uint id = pollOrder[pollIndex];
                NetworkConnection connection = connections[id];
                NetworkEvent.Type eventType = driver.PopEventForConnection(connection, out var reader, out NetworkPipeline pipeline);
                if (eventType == NetworkEvent.Type.Empty)
                {
                    pollIndex++;
                    examinedConnections++;
                    continue;
                }

                if (eventType == NetworkEvent.Type.Disconnect)
                {
                    RemoveConnectionAt(pollIndex);
                    transportEvent = new GameTransportEvent(TransportEventType.Disconnected, new TransportConnectionId(id));
                    return true;
                }

                pollIndex++;
                if (eventType == NetworkEvent.Type.Connect)
                {
                    transportEvent = new GameTransportEvent(TransportEventType.Connected, new TransportConnectionId(id));
                    return true;
                }

                if (eventType != NetworkEvent.Type.Data)
                {
                    continue;
                }

                TransportDelivery delivery = GetDelivery(pipeline);
                if (receiveBuffer.Array == null || receiveBuffer.Count < reader.Length)
                {
                    transportEvent = new GameTransportEvent(TransportEventType.ReceiveBufferTooSmall, new TransportConnectionId(id), delivery, reader.Length);
                    return true;
                }

                if (reader.Length > 0)
                {
                    fixed (byte* destination = &receiveBuffer.Array[receiveBuffer.Offset])
                    {
                        reader.ReadBytesUnsafe(destination, reader.Length);
                    }
                }

                transportEvent = new GameTransportEvent(TransportEventType.Data, new TransportConnectionId(id), delivery, reader.Length);
                return true;
            }

            return false;
        }

        public unsafe TransportSendResult Send(TransportConnectionId connectionId, TransportDelivery delivery, ArraySegment<byte> payload)
        {
            if (!IsRunning)
            {
                return TransportSendResult.NotRunning;
            }

            if (!connectionId.IsValid || !connections.TryGetValue(connectionId.Value, out NetworkConnection connection))
            {
                return TransportSendResult.InvalidConnection;
            }

            if (payload.Count > 0 && payload.Array == null)
            {
                return TransportSendResult.TransportError;
            }

            NetworkPipeline pipeline = GetPipeline(delivery);
            int beginResult = driver.BeginSend(pipeline, connection, out var writer, payload.Count);
            if (beginResult != 0)
            {
                return beginResult == (int)global::Unity.Networking.Transport.Error.StatusCode.NetworkPacketOverflow ? TransportSendResult.PayloadTooLarge : TransportSendResult.TransportError;
            }

            if (payload.Count > 0)
            {
                bool wrotePayload;
                fixed (byte* source = &payload.Array[payload.Offset])
                {
                    wrotePayload = writer.WriteBytesUnsafe(source, payload.Count);
                }

                if (!wrotePayload)
                {
                    driver.AbortSend(writer);
                    return TransportSendResult.PayloadTooLarge;
                }
            }

            int endResult = driver.EndSend(writer);
            return endResult >= 0 ? TransportSendResult.Success : TransportSendResult.TransportError;
        }

        public void Disconnect(TransportConnectionId connectionId)
        {
            if (IsRunning && connections.TryGetValue(connectionId.Value, out NetworkConnection connection))
            {
                driver.Disconnect(connection);
            }
        }

        public void Stop()
        {
            if (driver.IsCreated)
            {
                driver.Dispose();
            }

            connections.Clear();
            pollOrder.Clear();
            pendingConnected.Clear();
            nextConnectionId = 1;
            pollIndex = 0;
            maxConnections = 0;
            Role = TransportRole.None;
        }

        public void Dispose() => Stop();

        private void CreateDriver()
        {
            driver = NetworkDriver.Create();
            unreliableSequencedPipeline = driver.CreatePipeline(typeof(UnreliableSequencedPipelineStage));
            reliableSequencedPipeline = driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
        }

        private void AcceptServerConnections()
        {
            NetworkConnection connection;
            while ((connection = driver.Accept()) != default)
            {
                if (connections.Count >= maxConnections)
                {
                    driver.Disconnect(connection);
                    continue;
                }

                AddConnection(connection, true);
            }
        }

        private void AddConnection(NetworkConnection connection, bool reportConnectedImmediately)
        {
            uint id = AllocateConnectionId();
            connections.Add(id, connection);
            pollOrder.Add(id);
            if (reportConnectedImmediately)
            {
                pendingConnected.Enqueue(id);
            }
        }

        private uint AllocateConnectionId()
        {
            uint id = nextConnectionId++;
            if (id == 0)
            {
                id = nextConnectionId++;
            }

            return id;
        }

        private void RemoveConnectionAt(int index)
        {
            uint id = pollOrder[index];
            connections.Remove(id);
            pollOrder.RemoveAt(index);
            if (pollIndex > index)
            {
                pollIndex--;
            }

            if (pollIndex >= pollOrder.Count)
            {
                pollIndex = 0;
            }
        }

        private NetworkPipeline GetPipeline(TransportDelivery delivery)
        {
            switch (delivery)
            {
                case TransportDelivery.Unreliable:
                    return NetworkPipeline.Null;
                case TransportDelivery.UnreliableSequenced:
                    return unreliableSequencedPipeline;
                case TransportDelivery.ReliableSequenced:
                    return reliableSequencedPipeline;
                default:
                    throw new ArgumentOutOfRangeException(nameof(delivery), delivery, null);
            }
        }

        private TransportDelivery GetDelivery(NetworkPipeline pipeline)
        {
            if (pipeline == unreliableSequencedPipeline)
            {
                return TransportDelivery.UnreliableSequenced;
            }

            if (pipeline == reliableSequencedPipeline)
            {
                return TransportDelivery.ReliableSequenced;
            }

            return TransportDelivery.Unreliable;
        }
    }
}
