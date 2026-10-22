using System;
using System.Collections.Generic;
using AFPS.Math;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Prediction;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Sessions;
using AFPS.NetCode.Transport;
using AFPS.Simulation.Characters;
using AFPS.Simulation.World;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class SimulationAdmissionTests
    {
        [Test]
        public void ReadyGatesEntityCreationAndUsesVerifiedCollisionWorld()
        {
            var pair = new Pair();
            pair.Connect();
            Assert.That(pair.Server.ServerSessionCount, Is.Zero);
            Assert.That(pair.Client.ClientSession, Is.Null);
            var input = new PlayerInputCommand { Tick = 1, MoveY = 1f };
            Assert.That(pair.Client.TryPredictAndSend(input, out _, out _), Is.False);
            Assert.That(pair.Server.TryHandleData(NetworkTransportSide.Server, ServerId, InputPacket(input), out _), Is.False);

            pair.RouteClient(); // Hello：只发 Welcome，不分配实体。
            Assert.That(pair.Server.ServerSessionCount, Is.Zero);
            pair.RouteServer(); // Welcome：Ready 成功入队后才创建预测会话。
            Assert.That(pair.Client.ClientSession, Is.Not.Null);
            Assert.That(pair.Server.ServerSessionCount, Is.Zero);
            pair.Flush();
            Assert.That(pair.Server.ServerSessionCount, Is.EqualTo(1));
            Assert.That(pair.Client.RemotePlayers.LocalEntityId, Is.EqualTo(1));
            Assert.That(pair.Client.ClientAdmissionStatus, Is.EqualTo(ConnectionAdmissionStatus.Admitted));

            pair.Client.TryPredictAndSend(input, out _, out _);
            pair.Flush();
            pair.Server.AdvanceServerSessions(100);
            pair.Flush();
            pair.Server.TryGetServerSession(ServerId, out var authority);
            // 地面在 Y=2；若任何一端仍落到默认平地，此处的贴地状态与后续 Raw 比较会失败。
            Assert.That(authority.CurrentState.IsGrounded, Is.True);
            Assert.That(authority.CurrentState.PositionFixed.Y, Is.EqualTo(Fix.FromInt(2)));
            Assert.That(authority.CurrentState.RawEquals(pair.Client.ClientSession.CurrentState), Is.True);
        }

        [TestCase(SimulationCompatibility.WorldMismatch)]
        [TestCase(SimulationCompatibility.ConfigurationMismatch)]
        public void IncompatibleContextNeverCreatesPlayer(SimulationCompatibility mismatch)
        {
            var pair = new Pair(mismatch);
            pair.Connect();
            pair.Flush();
            Assert.That(pair.Server.ServerSessionCount, Is.Zero);
            Assert.That(pair.Client.ClientSession, Is.Null);
            Assert.That(pair.Client.ClientAdmissionStatus, Is.EqualTo(
                mismatch == SimulationCompatibility.WorldMismatch
                    ? ConnectionAdmissionStatus.WorldMismatch : ConnectionAdmissionStatus.ConfigurationMismatch));
            Assert.That(pair.Client.ExpectedServerIdentity, Is.EqualTo(pair.Server.WorldContext.Identity));
            Assert.That(pair.ClientTransport.Disconnected, Does.Contain(ClientId));
            Assert.That(pair.ServerTransport.Sent.FindAll(p => p.Type == NetworkMessageType.PlayerSessionAssignment), Is.Empty);
        }

        [Test]
        public void DifferentAlgorithmIsRejectedOnBothProtocolSides()
        {
            var pair = new Pair();
            pair.Connect();
            var local = pair.Client.WorldContext.Identity;
            var other = new SimulationWorldIdentity(local.AlgorithmVersion + 1, local.WorldHash, local.ConfigurationHash);
            var hello = Handshake(NetworkMessageType.SimulationHello, 1, other);
            Assert.That(pair.Server.TryHandleData(NetworkTransportSide.Server, ServerId, hello, out _), Is.True);
            Assert.That(SimulationHandshakeCodec.TryDeserialize(pair.ServerTransport.Pending.Dequeue().Data,
                out _, out _, out SimulationCompatibility reason), Is.True);
            Assert.That(reason, Is.EqualTo(SimulationCompatibility.AlgorithmMismatch));
            Assert.That(pair.Server.ServerSessionCount, Is.Zero);

            var welcome = Handshake(NetworkMessageType.SimulationWelcome, 1, other, reason);
            Assert.That(pair.Client.TryHandleData(NetworkTransportSide.Client, ClientId, welcome, out _), Is.True);
            Assert.That(pair.Client.ClientAdmissionStatus, Is.EqualTo(ConnectionAdmissionStatus.AlgorithmMismatch));
            Assert.That(pair.Client.ClientSession, Is.Null);
        }

        [TestCase(NetworkMessageType.SimulationHello)]
        [TestCase(NetworkMessageType.SimulationWelcome)]
        [TestCase(NetworkMessageType.SimulationReady)]
        public void FailedSendRetriesWithoutPrematureSession(NetworkMessageType failedType)
        {
            var pair = new Pair();
            if (failedType == NetworkMessageType.SimulationHello)
            {
                pair.ClientTransport.FailNext = true;
            }

            pair.Connect();
            if (failedType != NetworkMessageType.SimulationHello)
            {
                pair.ServerTransport.FailNext = failedType == NetworkMessageType.SimulationWelcome;
                pair.RouteClient();
                if (failedType == NetworkMessageType.SimulationReady)
                {
                    pair.ClientTransport.FailNext = true;
                    pair.RouteServer();
                }
            }

            Assert.That(pair.Server.ServerSessionCount, Is.Zero);
            Assert.That(pair.Client.ClientSession, Is.Null);
            // 未成功发出 Welcome 时，即使收到相同指纹的 Ready 也不能准入。
            if (failedType == NetworkMessageType.SimulationWelcome)
            {
                Assert.That(pair.Server.TryHandleData(NetworkTransportSide.Server, ServerId,
                    Handshake(NetworkMessageType.SimulationReady, 1, pair.Server.WorldContext.Identity), out _), Is.False);
            }

            pair.Server.AdvanceServerSessions(2);
            pair.Client.AdvanceServerSessions(2);
            pair.Flush();
            Assert.That(pair.Server.ServerSessionCount, Is.EqualTo(1));
            Assert.That(pair.Client.ClientAdmissionStatus, Is.EqualTo(ConnectionAdmissionStatus.Admitted));
        }

        [Test]
        public void DuplicatesDoNotResetPredictionOrAllocateAnotherEntity()
        {
            var pair = new Pair();
            pair.Connect();
            pair.Flush();
            var prediction = pair.Client.ClientSession;
            foreach (Packet packet in pair.ClientTransport.Sent.ToArray())
            {
                pair.Server.TryHandleData(NetworkTransportSide.Server, ServerId, packet.Data, out _);
            }

            pair.Flush();
            Assert.That(pair.Client.ClientSession, Is.SameAs(prediction));
            Assert.That(pair.Server.ServerSessionCount, Is.EqualTo(1));
            foreach (Packet packet in pair.ServerTransport.Sent.FindAll(p => p.Type == NetworkMessageType.PlayerSessionAssignment))
            {
                Assert.That(PlayerSessionAssignmentCodec.TryDeserialize(packet.Data, out PlayerSessionAssignment assignment), Is.True);
                Assert.That(assignment.EntityId, Is.EqualTo(1));
            }
        }

        [Test]
        public void FailedAssignmentIsRetriedWithTheSameEntityAndPredictionSession()
        {
            var pair = new Pair();
            pair.Connect();
            pair.RouteClient();
            pair.RouteServer();
            var prediction = pair.Client.ClientSession;
            pair.ServerTransport.FailNext = true;
            pair.RouteClient();
            Assert.That(pair.Server.ServerSessionCount, Is.EqualTo(1));
            Assert.That(pair.Client.RemotePlayers.LocalEntityId, Is.Zero);
            pair.Client.AdvanceServerSessions(2);
            pair.Flush();
            Assert.That(pair.Client.RemotePlayers.LocalEntityId, Is.EqualTo(1));
            Assert.That(pair.Server.ServerSessionCount, Is.EqualTo(1));
            Assert.That(pair.Client.ClientSession, Is.SameAs(prediction));
        }

        [Test]
        public void MissingReadyDeliveryOrAssignmentEventuallyRemovesLocalPrediction()
        {
            var pair = new Pair();
            pair.Connect();
            pair.RouteClient();
            pair.RouteServer();
            Assert.That(pair.Client.ClientSession, Is.Not.Null);
            // 不转交 Ready，模拟可靠通道持续无法交付；本地会话不能无限等待实体分配。
            pair.Client.AdvanceServerSessions(8);
            Assert.That(pair.Client.ClientAdmissionStatus, Is.EqualTo(ConnectionAdmissionStatus.TimedOut));
            Assert.That(pair.Client.ClientSession, Is.Null);
            Assert.That(pair.Server.ServerSessionCount, Is.Zero);
            Assert.That(pair.ClientTransport.Disconnected, Does.Contain(ClientId));
        }

        [Test]
        public void PendingDisconnectAndReusedConnectionRejectOldWelcome()
        {
            var pair = new Pair();
            pair.Connect();
            pair.RouteClient();
            Packet stale = pair.ServerTransport.Pending.Dequeue();
            pair.Client.HandleDisconnected(NetworkTransportSide.Client, ClientId);
            pair.Server.HandleDisconnected(NetworkTransportSide.Server, ServerId);
            pair.Connect();
            Assert.That(pair.Client.TryHandleData(NetworkTransportSide.Client, ClientId, stale.Data, out _), Is.False);
            Assert.That(pair.Client.ClientSession, Is.Null);
            pair.Flush();
            Assert.That(pair.Client.RemotePlayers.LocalEntityId, Is.EqualTo(1), "挂起连接不消耗实体 ID。");
        }

        [Test]
        public void TimeoutSpansTickWrapAndDoesNotCreateFallbackPlayer()
        {
            var pair = new Pair();
            pair.Server.AdvanceServerSessions(uint.MaxValue - 3);
            pair.Client.AdvanceServerSessions(uint.MaxValue - 3);
            pair.Connect();
            pair.Server.AdvanceServerSessions(3);
            pair.Client.AdvanceServerSessions(3);
            Assert.That(pair.Client.ClientAdmissionStatus, Is.EqualTo(ConnectionAdmissionStatus.AwaitingHandshake));
            pair.Server.AdvanceServerSessions(4);
            pair.Client.AdvanceServerSessions(4);
            Assert.That(pair.Client.ClientAdmissionStatus, Is.EqualTo(ConnectionAdmissionStatus.TimedOut));
            Assert.That(pair.ServerTransport.Disconnected, Does.Contain(ServerId));
            Assert.That(pair.Server.ServerSessionCount, Is.Zero);
            Assert.That(pair.Client.ClientSession, Is.Null);
        }

        [Test]
        public void FirstClockSampleAfterLongMenuWaitDoesNotImmediatelyExpireConnections()
        {
            var server = new FakeTransport(TransportRole.Server);
            var client = new FakeTransport(TransportRole.Client);
            var manager = CreateManager(server, client, Context(), initializeClock: false);
            manager.HandleConnected(NetworkTransportSide.Server, ServerId);
            manager.HandleConnected(NetworkTransportSide.Client, ClientId);
            manager.AdvanceServerSessions(10000);
            manager.AdvanceServerSessions(10007);
            Assert.That(manager.ClientAdmissionStatus, Is.EqualTo(ConnectionAdmissionStatus.AwaitingHandshake));
            Assert.That(server.Disconnected, Is.Empty);
            manager.AdvanceServerSessions(10008);
            Assert.That(manager.ClientAdmissionStatus, Is.EqualTo(ConnectionAdmissionStatus.TimedOut));
            Assert.That(server.Disconnected, Does.Contain(ServerId));
        }

        [Test]
        public void ForeignConnectionAndInconsistentWelcomeAreIgnored()
        {
            var pair = new Pair();
            pair.Connect();
            var identity = pair.Client.WorldContext.Identity;
            var welcome = Handshake(NetworkMessageType.SimulationWelcome, 1, identity);
            Assert.That(pair.Client.TryHandleData(NetworkTransportSide.Client, new TransportConnectionId(999), welcome, out _), Is.False);
            var inconsistent = Handshake(NetworkMessageType.SimulationWelcome, 1, identity, SimulationCompatibility.WorldMismatch);
            Assert.That(pair.Client.TryHandleData(NetworkTransportSide.Client, ClientId, inconsistent, out _), Is.False);
            Assert.That(pair.Client.ClientAdmissionStatus, Is.EqualTo(ConnectionAdmissionStatus.AwaitingHandshake));
            pair.Flush();
            Assert.That(pair.Client.ClientAdmissionStatus, Is.EqualTo(ConnectionAdmissionStatus.Admitted));
        }

        [Test]
        public void LateRejectionCannotChangeAnAdmittedConnectionsIdentity()
        {
            var pair = new Pair();
            pair.Connect();
            pair.Flush();
            var prediction = pair.Client.ClientSession;
            var local = pair.Client.WorldContext.Identity;
            var different = new SimulationWorldIdentity(local.AlgorithmVersion, local.WorldHash + 1, local.ConfigurationHash);
            Assert.That(pair.Client.TryHandleData(NetworkTransportSide.Client, ClientId,
                Handshake(NetworkMessageType.SimulationWelcome, 1, different, SimulationCompatibility.WorldMismatch), out _), Is.False);
            Assert.That(pair.Client.ClientAdmissionStatus, Is.EqualTo(ConnectionAdmissionStatus.Admitted));
            Assert.That(pair.Client.ClientSession, Is.SameAs(prediction));
            Assert.That(pair.Client.ExpectedServerIdentity, Is.EqualTo(local));
        }

        [Test]
        public void PendingServerConnectionsAreBoundedAndReleasedOnTimeout()
        {
            var transport = new FakeTransport(TransportRole.Server);
            var manager = CreateManager(transport, null, Context());
            for (uint i = 1; i <= 64; i++)
            {
                Assert.That(manager.HandleConnected(NetworkTransportSide.Server, new TransportConnectionId(i)), Is.True);
            }

            Assert.That(manager.HandleConnected(NetworkTransportSide.Server, new TransportConnectionId(65)), Is.False);
            Assert.That(transport.Disconnected, Does.Contain(new TransportConnectionId(65)));
            manager.AdvanceServerSessions(8);
            Assert.That(manager.HandleConnected(NetworkTransportSide.Server, new TransportConnectionId(66)), Is.True);
            Assert.That(manager.ServerSessionCount, Is.Zero);
        }

        [Test]
        public void HostCanUseIdenticalTransportIdsOnItsTwoSides()
        {
            var server = new FakeTransport(TransportRole.Server);
            var client = new FakeTransport(TransportRole.Client);
            var manager = CreateManager(server, client, Context());
            manager.HandleConnected(NetworkTransportSide.Server, ServerId);
            manager.HandleConnected(NetworkTransportSide.Client, ServerId);
            for (int i = 0; i < 8; i++)
            {
                while (client.Pending.Count > 0)
                {
                    manager.TryHandleData(NetworkTransportSide.Server, ServerId, client.Pending.Dequeue().Data, out _);
                }

                while (server.Pending.Count > 0)
                {
                    manager.TryHandleData(NetworkTransportSide.Client, ServerId, server.Pending.Dequeue().Data, out _);
                }
            }

            Assert.That(manager.ServerSessionCount, Is.EqualTo(1));
            Assert.That(manager.ClientSession, Is.Not.Null);
            manager.HandleDisconnected(NetworkTransportSide.Client, ServerId);
            Assert.That(manager.ServerSessionCount, Is.EqualTo(1));
            manager.HandleDisconnected(NetworkTransportSide.Server, ServerId);
            Assert.That(manager.ServerSessionCount, Is.Zero);
        }

        [Test]
        public void ContextCannotAdvertiseDifferentSimulationParametersOrAnotherBackend()
        {
            var transport = new FakeTransport(TransportRole.Server);
            var context = Context();
            PlayerState initial = InitialState();
            Assert.Throws<ArgumentException>(() => new NetworkMovementSessionManager(transport, null, initial, initial,
                new PlayerSimulationConfig(7f, 20f, 20f, 8f), 0.02f, 64, 3, 16, 2, 2, 0, 0, simulationWorld: context));
            Assert.Throws<ArgumentException>(() => new NetworkMovementSessionManager(transport, null, initial, initial,
                context.Configuration, 0.04f, 64, 3, 16, 2, 2, 0, 0, simulationWorld: context));
            Assert.Throws<ArgumentException>(() => new NetworkMovementSessionManager(transport, null, initial, initial,
                context.Configuration, 0.02f, 64, 3, 16, 2, 2, 0, 0,
                collisionWorld: AFPS.Simulation.Characters.Collision.FlatGroundCollisionWorld.Instance, simulationWorld: context));
            Assert.Throws<ArgumentException>(() => new NetworkMovementSessionManager(transport, null, initial, initial,
                context.Configuration, 0.02f, 64, 3, 16, 2, 2, 0, 0,
                serverSpawnSpacing: 3f, simulationWorld: context));
            Assert.Throws<ArgumentException>(() => new NetworkMovementSessionManager(transport, null, initial, initial,
                context.Configuration, 0.02f, 64, 3, 16, 2, 2, 0, 0,
                interestRadius: 149f, simulationWorld: context));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateManager(transport, null, context, timeout: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateManager(transport, null, context, retry: 8));
        }

        internal static readonly TransportConnectionId ServerId = new TransportConnectionId(10);
        internal static readonly TransportConnectionId ClientId = new TransportConnectionId(20);

        internal static SimulationWorldContext Context(SimulationCompatibility mismatch = SimulationCompatibility.Compatible)
        {
            var world = new FixedWorld(null, new[]
            {
                new FixedWorldPlane(1, 0, FixedSurface.Solid | FixedSurface.Ground, FixedAxis.Y,
                    Fix.FromInt(mismatch == SimulationCompatibility.WorldMismatch ? 3 : 2), true)
            });
            var configuration = new PlayerSimulationConfig(
                Fix.FromInt(mismatch == SimulationCompatibility.ConfigurationMismatch ? 7 : 6),
                Fix.FromInt(20), Fix.FromInt(20), Fix.FromInt(8));
            var spatial = new SimulationSessionSpatialConfig(
                InitialState().PositionFixed, Fix.FromRatio(5, 2), Fix.FromInt(150));
            return new SimulationWorldContext(world, configuration, SimulationStep.Step50Hz, sessionSpatial: spatial);
        }

        internal static NetworkMovementSessionManager CreateManager(
            IGameTransport server,
            IGameTransport client,
            SimulationWorldContext context,
            uint timeout = 8,
            uint retry = 2,
            bool initializeClock = true)
        {
            PlayerState initial = InitialState();
            var manager = new NetworkMovementSessionManager(server, client, initial, initial, context.Configuration,
                0.02f, 64, 3, 16, 2, 2, 0, 0, simulationWorld: context,
                admissionTimeoutTicks: timeout, admissionRetryTicks: retry);
            if (initializeClock)
            {
                manager.AdvanceServerSessions(0);
            }

            return manager;
        }

        private static PlayerState InitialState() => new PlayerState
        {
            PositionFixed = new FixVec3(Fix.Zero, Fix.FromInt(2), Fix.Zero),
            IsGrounded = true
        };

        internal static ArraySegment<byte> Handshake(NetworkMessageType type, uint attempt,
            SimulationWorldIdentity identity, SimulationCompatibility result = SimulationCompatibility.Compatible)
        {
            byte[] buffer = new byte[SimulationHandshakeCodec.MaximumPacketSize];
            Assert.That(SimulationHandshakeCodec.TrySerialize(type, attempt, identity, result,
                new ArraySegment<byte>(buffer), out int count), Is.True);
            return new ArraySegment<byte>(buffer, 0, count);
        }

        private static ArraySegment<byte> InputPacket(PlayerInputCommand input)
        {
            byte[] buffer = new byte[InputCommandBatchCodec.GetPacketSize(1)];
            var batch = new InputCommandBatch(new ArraySegment<PlayerInputCommand>(new[] { input }));
            Assert.That(InputCommandBatchCodec.TrySerialize(batch, 1, new ArraySegment<byte>(buffer), out int count), Is.True);
            return new ArraySegment<byte>(buffer, 0, count);
        }

        private sealed class Pair
        {
            internal readonly FakeTransport ServerTransport = new FakeTransport(TransportRole.Server);
            internal readonly FakeTransport ClientTransport = new FakeTransport(TransportRole.Client);
            internal readonly NetworkMovementSessionManager Server;
            internal readonly NetworkMovementSessionManager Client;

            internal Pair(SimulationCompatibility mismatch = SimulationCompatibility.Compatible)
            {
                Server = CreateManager(ServerTransport, null, Context());
                Client = CreateManager(null, ClientTransport, Context(mismatch));
            }

            internal void Connect()
            {
                Assert.That(Server.HandleConnected(NetworkTransportSide.Server, ServerId), Is.True);
                Assert.That(Client.HandleConnected(NetworkTransportSide.Client, ClientId), Is.True);
            }

            internal void RouteClient() => Server.TryHandleData(
                NetworkTransportSide.Server, ServerId, ClientTransport.Pending.Dequeue().Data, out _);

            internal void RouteServer() => Client.TryHandleData(
                NetworkTransportSide.Client, ClientId, ServerTransport.Pending.Dequeue().Data, out _);

            internal void Flush()
            {
                for (int i = 0; i < 32 && (ClientTransport.Pending.Count > 0 || ServerTransport.Pending.Count > 0); i++)
                {
                    while (ClientTransport.Pending.Count > 0)
                    {
                        RouteClient();
                    }

                    while (ServerTransport.Pending.Count > 0)
                    {
                        RouteServer();
                    }
                }

                Assert.That(ClientTransport.Pending.Count + ServerTransport.Pending.Count, Is.Zero);
            }
        }

        private sealed class Packet
        {
            internal readonly ArraySegment<byte> Data;
            internal readonly NetworkMessageType Type;

            internal Packet(ArraySegment<byte> source)
            {
                byte[] copy = new byte[source.Count];
                Array.Copy(source.Array, source.Offset, copy, 0, source.Count);
                Data = new ArraySegment<byte>(copy);
                PacketHeaderCodec.TryRead(Data, out PacketHeader header);
                Type = header.MessageType;
            }
        }

        private sealed class FakeTransport : IGameTransport
        {
            internal readonly Queue<Packet> Pending = new Queue<Packet>();
            internal readonly List<Packet> Sent = new List<Packet>();
            internal readonly List<TransportConnectionId> Disconnected = new List<TransportConnectionId>();
            internal bool FailNext;
            public bool IsRunning => true;
            public TransportRole Role { get; }

            internal FakeTransport(TransportRole role) => Role = role;

            public TransportSendResult Send(TransportConnectionId id, TransportDelivery delivery, ArraySegment<byte> data)
            {
                if (FailNext)
                {
                    FailNext = false;
                    return TransportSendResult.TransportError;
                }

                var packet = new Packet(data);
                Pending.Enqueue(packet);
                Sent.Add(packet);
                return TransportSendResult.Success;
            }

            public bool TryStartServer(ushort port, int maxConnections, out string error)
            {
                error = null;
                return false;
            }

            public bool TryStartClient(string address, ushort port, out string error)
            {
                error = null;
                return false;
            }

            public bool TryPollEvent(ArraySegment<byte> receiveBuffer, out GameTransportEvent transportEvent)
            {
                transportEvent = default;
                return false;
            }

            public void Disconnect(TransportConnectionId id)
            {
                Disconnected.Add(id);
            }

            public void Pump()
            {
            }

            public void Stop()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
