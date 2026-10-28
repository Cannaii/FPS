using System;
using System.Collections.Generic;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Transport;
using AFPS.Simulation.World;

namespace AFPS.NetCode.Sessions
{
    public enum ConnectionAdmissionStatus
    {
        Disconnected,
        AwaitingHandshake,
        Admitted,
        AlgorithmMismatch,
        WorldMismatch,
        ConfigurationMismatch,
        TimedOut,
        SessionCreationFailed
    }

    /// <summary>传输连接先完成 Hello→Welcome→Ready，再进入玩家生命周期；不分配未获准实体。</summary>
    internal sealed class SimulationSessionAdmission
    {
        private sealed class Connection
        {
            internal readonly TransportConnectionId Id;
            internal uint StartedTick;
            internal uint Attempt;
            internal uint LastSendTick;
            internal bool HasSent;
            internal bool HasHello;
            internal bool HasWelcome;
            internal bool HasResumeGrant;
            internal SimulationWorldIdentity PeerIdentity;
            internal SessionResumeCredential ResumeCredential;
            internal ConnectionAdmissionStatus Status = ConnectionAdmissionStatus.AwaitingHandshake;

            internal Connection(TransportConnectionId id, uint startedTick)
            {
                Id = id;
                StartedTick = startedTick;
            }
        }

        private readonly SimulationWorldIdentity identity;
        private readonly IGameTransport serverTransport;
        private readonly IGameTransport clientTransport;
        private readonly Func<NetworkTransportSide, TransportConnectionId, bool> createSession;
        private readonly Func<NetworkTransportSide, TransportConnectionId, bool> removeSession;
        private readonly Func<bool> hasClientAssignment;
        private readonly Action<TransportConnectionId> resendAssignment;
        private readonly Func<TransportConnectionId, SessionResumeCredential, bool> resumeSession;
        private readonly Action<TransportConnectionId, uint> sendResumeGrant;
        private readonly Func<uint, SessionResumeCredential, bool> receiveResumeGrant;
        private readonly Func<SessionResumeCredential> getClientCredential;
        private readonly uint timeoutTicks;
        private readonly uint retryTicks;
        private readonly Dictionary<TransportConnectionId, Connection> serverConnections = new Dictionary<TransportConnectionId, Connection>();
        private readonly List<TransportConnectionId> expiredConnections = new List<TransportConnectionId>();
        private readonly byte[] sendBuffer = new byte[SimulationHandshakeCodec.MaximumPacketSize];
        private Connection client;
        private uint currentTick;
        private bool clockInitialized;
        private uint nextAttempt = 1;

        internal ConnectionAdmissionStatus ClientStatus { get; private set; }
        internal SimulationWorldIdentity? ExpectedServerIdentity { get; private set; }
        internal uint ClientAttempt => client?.Attempt ?? 0;
        internal bool ClientIsResuming => client != null && client.ResumeCredential.IsValid;

        internal uint ServerAttempt(TransportConnectionId connectionId)
        {
            return serverConnections.TryGetValue(connectionId, out var connection) ? connection.Attempt : 0;
        }

        internal SimulationSessionAdmission(
            SimulationWorldIdentity identity,
            IGameTransport serverTransport,
            IGameTransport clientTransport,
            Func<NetworkTransportSide, TransportConnectionId, bool> createSession,
            Func<NetworkTransportSide, TransportConnectionId, bool> removeSession,
            Func<bool> hasClientAssignment,
            Action<TransportConnectionId> resendAssignment,
            uint timeoutTicks,
            uint retryTicks,
            Func<TransportConnectionId, SessionResumeCredential, bool> resumeSession,
            Action<TransportConnectionId, uint> sendResumeGrant,
            Func<uint, SessionResumeCredential, bool> receiveResumeGrant,
            Func<SessionResumeCredential> getClientCredential)
        {
            // 时间单位为本地模拟 Tick；窗口小于半个 uint 周期，以保证回绕后的差值无歧义。
            if (timeoutTicks == 0 || timeoutTicks > int.MaxValue || retryTicks == 0 || retryTicks >= timeoutTicks)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutTicks));
            }

            this.identity = identity;
            this.serverTransport = serverTransport;
            this.clientTransport = clientTransport;
            this.createSession = createSession;
            this.removeSession = removeSession;
            this.hasClientAssignment = hasClientAssignment;
            this.resendAssignment = resendAssignment;
            this.timeoutTicks = timeoutTicks;
            this.retryTicks = retryTicks;
            this.resumeSession = resumeSession;
            this.sendResumeGrant = sendResumeGrant;
            this.receiveResumeGrant = receiveResumeGrant;
            this.getClientCredential = getClientCredential;
        }

        internal bool HandleConnected(NetworkTransportSide side, TransportConnectionId id)
        {
            if (!id.IsValid)
            {
                return false;
            }

            if (side == NetworkTransportSide.Server)
            {
                if (serverTransport == null || serverConnections.ContainsKey(id))
                {
                    return false;
                }

                // 当前对局协议最多 64 人；等待准入的连接也计入上限，不能无限积累挂起状态。
                if (serverConnections.Count >= DeathmatchStateCodec.MaximumPlayerCount)
                {
                    serverTransport.Disconnect(id);
                    return false;
                }

                serverConnections.Add(id, new Connection(id, currentTick));
                return true;
            }

            if (side != NetworkTransportSide.Client || clientTransport == null || client != null)
            {
                return false;
            }

            client = new Connection(id, currentTick)
            {
                Attempt = nextAttempt,
                ResumeCredential = getClientCredential()
            };
            nextAttempt = unchecked(nextAttempt + 1);
            if (nextAttempt == 0)
            {
                nextAttempt = 1;
            }

            ClientStatus = ConnectionAdmissionStatus.AwaitingHandshake;
            ExpectedServerIdentity = null;
            SendHello();
            return true;
        }

        internal bool HandleDisconnected(NetworkTransportSide side, TransportConnectionId id)
        {
            if (side == NetworkTransportSide.Server)
            {
                if (!serverConnections.TryGetValue(id, out Connection connection))
                {
                    return false;
                }

                serverConnections.Remove(id);
                if (connection.Status == ConnectionAdmissionStatus.Admitted)
                {
                    removeSession(side, id);
                }

                return true;
            }

            if (side != NetworkTransportSide.Client || client == null || client.Id != id)
            {
                return false;
            }

            if (client.Status == ConnectionAdmissionStatus.Admitted)
            {
                removeSession(side, id);
            }

            client = null;
            ClientStatus = ConnectionAdmissionStatus.Disconnected;
            return true;
        }

        internal bool IsAdmitted(NetworkTransportSide side, TransportConnectionId id)
        {
            if (side == NetworkTransportSide.Server)
            {
                return serverConnections.TryGetValue(id, out Connection connection)
                    && connection.Status == ConnectionAdmissionStatus.Admitted;
            }

            return side == NetworkTransportSide.Client && client != null && client.Id == id
                && client.Status == ConnectionAdmissionStatus.Admitted;
        }

        internal bool TryHandleControl(
            NetworkTransportSide side,
            TransportConnectionId id,
            ArraySegment<byte> packet,
            out bool handled)
        {
            handled = false;
            if (!PacketHeaderCodec.TryRead(packet, out PacketHeader header))
            {
                return true;
            }

            if (header.MessageType == NetworkMessageType.SessionResumeRequest)
            {
                if (side == NetworkTransportSide.Server && serverConnections.TryGetValue(id, out var pending)
                    && SessionResumeCodec.TryReadRequest(packet, out uint attempt, out var peerIdentity, out var credential))
                {
                    handled = HandleServerHello(pending, attempt, peerIdentity, credential);
                }
                return true;
            }

            if (header.MessageType == NetworkMessageType.SessionResumeGrant)
            {
                if (side == NetworkTransportSide.Client && client != null && client.Id == id
                    && client.Status == ConnectionAdmissionStatus.Admitted && hasClientAssignment()
                    && SessionResumeCodec.TryReadGrant(packet, out uint attempt, out uint entityId, out var credential)
                    && attempt == client.Attempt)
                {
                    handled = receiveResumeGrant(entityId, credential);
                    client.HasResumeGrant |= handled;
                }
                return true;
            }

            if (!SimulationHandshakeCodec.IsHandshake(header.MessageType))
            {
                return false;
            }

            if (!SimulationHandshakeCodec.TryDeserialize(packet, out header, out SimulationWorldIdentity peer,
                out SimulationCompatibility result))
            {
                return true;
            }

            if (side == NetworkTransportSide.Server && serverConnections.TryGetValue(id, out Connection connection))
            {
                handled = HandleServerControl(connection, header, peer);
            }
            else if (side == NetworkTransportSide.Client && client != null && client.Id == id)
            {
                handled = HandleClientControl(header, peer, result);
            }

            return true;
        }

        private bool HandleServerControl(Connection connection, PacketHeader header, SimulationWorldIdentity peer)
        {
            if (connection.HasHello && (connection.Attempt != header.Sequence || !connection.PeerIdentity.Equals(peer)))
            {
                return false;
            }

            if (header.MessageType == NetworkMessageType.SimulationHello)
            {
                return HandleServerHello(connection, header.Sequence, peer, default);
            }

            if (header.MessageType != NetworkMessageType.SimulationReady || !connection.HasHello || !connection.HasWelcome
                || !identity.Equals(peer))
            {
                return false;
            }

            if (connection.Status == ConnectionAdmissionStatus.Admitted)
            {
                // 分配包可能因发送队列满而未入队；重复 Ready 只重发已有身份，不重新分配实体。
                resendAssignment(connection.Id);
                sendResumeGrant(connection.Id, connection.Attempt);
                return true;
            }

            // Ready 必须来自已确认服务器指纹的客户端；此前输入、射击与实体分配均被隔离。
            bool created = connection.ResumeCredential.IsValid
                ? resumeSession(connection.Id, connection.ResumeCredential)
                : createSession(NetworkTransportSide.Server, connection.Id);
            if (!created)
            {
                serverConnections.Remove(connection.Id);
                serverTransport.Disconnect(connection.Id);
                return false;
            }

            connection.Status = ConnectionAdmissionStatus.Admitted;
            sendResumeGrant(connection.Id, connection.Attempt);
            return true;
        }

        private bool HandleServerHello(Connection connection, uint attempt,
            SimulationWorldIdentity peer, SessionResumeCredential credential)
        {
            // 首包锁定尝试、世界和准入类型；同连接不能从恢复降级成新玩家，或在Ready前换凭据。
            if (connection.HasHello && (connection.Attempt != attempt
                || !connection.PeerIdentity.Equals(peer) || !connection.ResumeCredential.Equals(credential)))
            {
                return false;
            }
            connection.HasHello = true;
            connection.Attempt = attempt;
            connection.PeerIdentity = peer;
            connection.ResumeCredential = credential;
            SendWelcome(connection);
            return true;
        }

        private bool HandleClientControl(
            PacketHeader header,
            SimulationWorldIdentity peer,
            SimulationCompatibility result)
        {
            if (header.MessageType != NetworkMessageType.SimulationWelcome || header.Sequence != client.Attempt)
            {
                return false;
            }

            SimulationCompatibility comparison = SimulationHandshakeCodec.Compare(identity, peer);
            if (result != comparison)
            {
                return false;
            }

            // 已准入连接的身份不能在同一尝试中变更；迟到或伪造的拒绝包不能留下活跃预测会话。
            if (client.Status == ConnectionAdmissionStatus.Admitted && comparison != SimulationCompatibility.Compatible)
            {
                return false;
            }

            ExpectedServerIdentity = peer;
            if (comparison != SimulationCompatibility.Compatible)
            {
                ClientStatus = comparison == SimulationCompatibility.AlgorithmMismatch
                    ? ConnectionAdmissionStatus.AlgorithmMismatch
                    : comparison == SimulationCompatibility.WorldMismatch
                        ? ConnectionAdmissionStatus.WorldMismatch
                        : ConnectionAdmissionStatus.ConfigurationMismatch;
                clientTransport.Disconnect(client.Id);
                client = null;
                return true;
            }

            if (!Send(clientTransport, client, NetworkMessageType.SimulationReady, SimulationCompatibility.Compatible))
            {
                return true;
            }

            if (client.Status == ConnectionAdmissionStatus.Admitted)
            {
                return true;
            }

            // Ready 已入可靠发送队列后才创建预测会话；服务器的后续分配包因而不会先于本地会话。
            if (!createSession(NetworkTransportSide.Client, client.Id))
            {
                ClientStatus = ConnectionAdmissionStatus.SessionCreationFailed;
                clientTransport.Disconnect(client.Id);
                client = null;
                return false;
            }

            client.Status = ConnectionAdmissionStatus.Admitted;
            ClientStatus = ConnectionAdmissionStatus.Admitted;
            return true;
        }

        internal void Advance(uint tick)
        {
            currentTick = tick;
            if (!clockInitialized)
            {
                // 菜单停留期间场景 Tick 可能已很大；首次推进才确定时钟原点，不能把新连接算成超时。
                clockInitialized = true;
                foreach (Connection connection in serverConnections.Values)
                {
                    connection.StartedTick = tick;
                    connection.LastSendTick = tick;
                }

                if (client != null)
                {
                    client.StartedTick = tick;
                    client.LastSendTick = tick;
                }
            }

            expiredConnections.Clear();
            foreach (Connection connection in serverConnections.Values)
            {
                if (connection.Status == ConnectionAdmissionStatus.Admitted)
                {
                    continue;
                }

                if (unchecked(tick - connection.StartedTick) >= timeoutTicks)
                {
                    expiredConnections.Add(connection.Id);
                }
                else if (connection.HasHello && ShouldRetry(connection))
                {
                    SendWelcome(connection);
                }
            }

            foreach (TransportConnectionId id in expiredConnections)
            {
                // 先移除状态再请求断线，随后到达的 Disconnect 事件不能再次删除其他会话。
                serverConnections.Remove(id);
                serverTransport.Disconnect(id);
            }

            if (client == null || (client.Status == ConnectionAdmissionStatus.Admitted
                && hasClientAssignment() && client.HasResumeGrant))
            {
                return;
            }

            if (unchecked(tick - client.StartedTick) >= timeoutTicks)
            {
                if (client.Status == ConnectionAdmissionStatus.Admitted)
                {
                    removeSession(NetworkTransportSide.Client, client.Id);
                }

                ClientStatus = ConnectionAdmissionStatus.TimedOut;
                clientTransport.Disconnect(client.Id);
                client = null;
            }
            else if (ShouldRetry(client))
            {
                if (client.Status == ConnectionAdmissionStatus.Admitted)
                {
                    Send(clientTransport, client, NetworkMessageType.SimulationReady, SimulationCompatibility.Compatible);
                }
                else
                {
                    SendHello();
                }
            }
        }

        private bool ShouldRetry(Connection connection)
        {
            return !connection.HasSent || unchecked(currentTick - connection.LastSendTick) >= retryTicks;
        }

        private void SendHello()
        {
            if (client.ResumeCredential.IsValid)
            {
                client.LastSendTick = currentTick;
                client.HasSent = true;
                byte[] buffer = new byte[SessionResumeCodec.RequestPacketSize];
                if (SessionResumeCodec.TryWriteRequest(identity, client.ResumeCredential, client.Attempt,
                    new ArraySegment<byte>(buffer), out int count))
                {
                    clientTransport.Send(client.Id, TransportDelivery.ReliableSequenced,
                        new ArraySegment<byte>(buffer, 0, count));
                }
                return;
            }
            Send(clientTransport, client, NetworkMessageType.SimulationHello, SimulationCompatibility.Compatible);
        }

        private void SendWelcome(Connection connection)
        {
            // 拒绝也先发可靠回复，保留连接至客户端主动断开或超时，避免立即 Disconnect 丢掉原因包。
            SimulationCompatibility comparison = SimulationHandshakeCodec.Compare(identity, connection.PeerIdentity);
            if (Send(serverTransport, connection, NetworkMessageType.SimulationWelcome, comparison)
                && comparison == SimulationCompatibility.Compatible)
            {
                connection.HasWelcome = true;
            }
        }

        private bool Send(
            IGameTransport transport,
            Connection connection,
            NetworkMessageType type,
            SimulationCompatibility result)
        {
            connection.LastSendTick = currentTick;
            connection.HasSent = true;
            if (!SimulationHandshakeCodec.TrySerialize(type, connection.Attempt, identity, result,
                new ArraySegment<byte>(sendBuffer), out int bytes))
            {
                return false;
            }

            return transport.Send(connection.Id, TransportDelivery.ReliableSequenced,
                new ArraySegment<byte>(sendBuffer, 0, bytes)) == TransportSendResult.Success;
        }
    }
}
