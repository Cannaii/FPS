using System;
using System.Collections.Generic;
using AFPS.NetCode.InputReplication;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Prediction;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Sessions;
using AFPS.NetCode.StateReplication;
using AFPS.NetCode.Transport;
using AFPS.Simulation.Characters;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class NetworkMovementSessionManagerTests
    {
        [Test]
        public void ConnectionLifecycle_CreatesAndRemovesExpectedSessions()
        {
            FakeTransport serverTransport = new FakeTransport(TransportRole.Server);
            FakeTransport clientTransport = new FakeTransport(TransportRole.Client);
            NetworkMovementSessionManager manager = CreateManager(serverTransport, clientTransport);
            TransportConnectionId serverConnection = new TransportConnectionId(10);
            TransportConnectionId clientConnection = new TransportConnectionId(20);

            Assert.That(manager.HandleConnected(NetworkTransportSide.Server, serverConnection), Is.True);
            Assert.That(manager.HandleConnected(NetworkTransportSide.Client, clientConnection), Is.True);
            Assert.That(manager.ServerSessionCount, Is.EqualTo(1));
            Assert.That(manager.ClientSession, Is.Not.Null);
            Assert.That(manager.HandleDisconnected(NetworkTransportSide.Server, serverConnection), Is.True);
            Assert.That(manager.HandleDisconnected(NetworkTransportSide.Client, clientConnection), Is.True);
            Assert.That(manager.ServerSessionCount, Is.Zero);
            Assert.That(manager.ClientSession, Is.Null);
        }

        [Test]
        public void RoutedInputAndState_DriveAuthorityAndClientReconciliation()
        {
            FakeTransport serverTransport = new FakeTransport(TransportRole.Server);
            FakeTransport clientTransport = new FakeTransport(TransportRole.Client);
            NetworkMovementSessionManager manager = CreateManager(serverTransport, clientTransport);
            TransportConnectionId serverConnection = new TransportConnectionId(10);
            TransportConnectionId clientConnection = new TransportConnectionId(20);
            manager.HandleConnected(NetworkTransportSide.Server, serverConnection);
            manager.HandleConnected(NetworkTransportSide.Client, clientConnection);
            serverTransport.SentPackets.Clear();
            serverTransport.SentConnectionIds.Clear();

            PlayerInputCommand command = new PlayerInputCommand { Tick = 1, MoveY = 1f };
            Assert.That(manager.TryPredictAndSend(command, out PlayerState predictedState, out InputBatchSendResult inputSend), Is.True);
            Assert.That(inputSend.Succeeded, Is.True);
            Assert.That(predictedState.Tick, Is.EqualTo(1));
            Assert.That(manager.TryHandleData(NetworkTransportSide.Server, serverConnection, clientTransport.SentPackets[0], out _), Is.True);

            Assert.That(manager.AdvanceServerSessions(100), Is.EqualTo(1));
            Assert.That(serverTransport.SentPackets, Has.Count.EqualTo(1));
            Assert.That(manager.TryHandleData(NetworkTransportSide.Client, clientConnection, serverTransport.SentPackets[0], out ReconciliationResult reconciliation), Is.True);
            Assert.That(reconciliation.Status, Is.EqualTo(ReconciliationStatus.NoCorrection));
            Assert.That(manager.ClientSession.CurrentState.Tick, Is.EqualTo(1));
        }

        [Test]
        public void TwoServerPlayers_ReceiveIdentityRemoteSnapshotsAndDespawn()
        {
            FakeTransport serverTransport = new FakeTransport(TransportRole.Server);
            NetworkMovementSessionManager manager = CreateManager(serverTransport, null);
            TransportConnectionId firstConnection = new TransportConnectionId(10);
            TransportConnectionId secondConnection = new TransportConnectionId(20);

            Assert.That(manager.HandleConnected(NetworkTransportSide.Server, firstConnection), Is.True);
            Assert.That(manager.HandleConnected(NetworkTransportSide.Server, secondConnection), Is.True);
            Assert.That(PlayerSessionAssignmentCodec.TryDeserialize(serverTransport.SentPackets[0], out PlayerSessionAssignment firstAssignment), Is.True);
            Assert.That(PlayerSessionAssignmentCodec.TryDeserialize(serverTransport.SentPackets[1], out PlayerSessionAssignment secondAssignment), Is.True);
            Assert.That(firstAssignment.EntityId, Is.Not.EqualTo(secondAssignment.EntityId));
            serverTransport.SentPackets.Clear();
            serverTransport.SentConnectionIds.Clear();

            Assert.That(manager.TryHandleData(NetworkTransportSide.Server, firstConnection, CreateInputPacket(1, 0f), out _), Is.True);
            Assert.That(manager.TryHandleData(NetworkTransportSide.Server, secondConnection, CreateInputPacket(1, 90f), out _), Is.True);
            Assert.That(manager.AdvanceServerSessions(100), Is.EqualTo(2));

            int remoteSnapshotCount = 0;
            for (int i = 0; i < serverTransport.SentPackets.Count; i++)
            {
                if (!RemotePlayerSnapshotCodec.TryDeserialize(serverTransport.SentPackets[i], out _, out AFPS.NetCode.SnapshotInterpolation.RemotePlayerSnapshot snapshot))
                {
                    continue;
                }

                remoteSnapshotCount++;
                TransportConnectionId expectedTarget = snapshot.EntityId == firstAssignment.EntityId ? secondConnection : firstConnection;
                Assert.That(serverTransport.SentConnectionIds[i], Is.EqualTo(expectedTarget));
            }

            Assert.That(remoteSnapshotCount, Is.EqualTo(2));
            serverTransport.SentPackets.Clear();
            serverTransport.SentConnectionIds.Clear();
            Assert.That(manager.HandleDisconnected(NetworkTransportSide.Server, firstConnection), Is.True);
            Assert.That(serverTransport.SentPackets, Has.Count.EqualTo(1));
            Assert.That(PlayerDespawnCodec.TryDeserialize(serverTransport.SentPackets[0], out uint despawnedEntity), Is.True);
            Assert.That(despawnedEntity, Is.EqualTo(firstAssignment.EntityId));
            Assert.That(serverTransport.SentConnectionIds[0], Is.EqualTo(secondConnection));
        }

        [Test]
        public void DataRouting_RejectsWrongMessageSideAndUnknownConnection()
        {
            FakeTransport serverTransport = new FakeTransport(TransportRole.Server);
            FakeTransport clientTransport = new FakeTransport(TransportRole.Client);
            NetworkMovementSessionManager manager = CreateManager(serverTransport, clientTransport);
            TransportConnectionId serverConnection = new TransportConnectionId(10);
            TransportConnectionId clientConnection = new TransportConnectionId(20);
            manager.HandleConnected(NetworkTransportSide.Server, serverConnection);
            manager.HandleConnected(NetworkTransportSide.Client, clientConnection);
            manager.TryPredictAndSend(new PlayerInputCommand { Tick = 1 }, out _, out _);

            ArraySegment<byte> inputPacket = clientTransport.SentPackets[0];
            Assert.That(manager.TryHandleData(NetworkTransportSide.Client, clientConnection, inputPacket, out _), Is.False);
            Assert.That(manager.TryHandleData(NetworkTransportSide.Server, new TransportConnectionId(999), inputPacket, out _), Is.False);
        }

        [Test]
        public void AuthoritativeHitscan_DamagesTargetAndBroadcastsResultToBothPlayers()
        {
            FakeTransport serverTransport = new FakeTransport(TransportRole.Server);
            NetworkMovementSessionManager manager = CreateManager(serverTransport, null);
            TransportConnectionId shooterConnection = new TransportConnectionId(10);
            TransportConnectionId targetConnection = new TransportConnectionId(20);
            manager.HandleConnected(NetworkTransportSide.Server, shooterConnection);
            manager.HandleConnected(NetworkTransportSide.Server, targetConnection);
            manager.TryGetServerEntityId(shooterConnection, out uint shooterEntity);
            manager.TryGetServerEntityId(targetConnection, out uint targetEntity);
            serverTransport.SentPackets.Clear();
            serverTransport.SentConnectionIds.Clear();

            Assert.That(manager.TryHandleData(NetworkTransportSide.Server, shooterConnection, CreateInputPacket(new PlayerInputCommand { Tick = 1, LookYaw = 90f, FirePressed = true, ShotSequence = 1 }), out _), Is.True);
            Assert.That(manager.TryHandleData(NetworkTransportSide.Server, targetConnection, CreateInputPacket(new PlayerInputCommand { Tick = 1 }), out _), Is.True);
            Assert.That(manager.AdvanceServerSessions(100), Is.EqualTo(2));

            int resultPackets = 0;
            for (int i = 0; i < serverTransport.SentPackets.Count; i++)
            {
                if (!AuthoritativeShotResultCodec.TryDeserialize(serverTransport.SentPackets[i], out _, out var result))
                {
                    continue;
                }

                resultPackets++;
                Assert.That(result.Shot.EntityId, Is.EqualTo(shooterEntity));
                Assert.That(result.TargetEntityId, Is.EqualTo(targetEntity));
                Assert.That(result.TargetHealth, Is.EqualTo(80f).Within(0.01f));
            }

            Assert.That(resultPackets, Is.EqualTo(2));
            Assert.That(manager.TryGetServerHealth(targetEntity, out float health), Is.True);
            Assert.That(health, Is.EqualTo(80f));
            Assert.That(manager.Metrics.RewindQueries, Is.EqualTo(1));
            Assert.That(manager.Metrics.ShotResultsSent, Is.EqualTo(2));
            Assert.That(manager.Metrics.LastRewindMicroseconds, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void RewindHitscan_HitsHistoricalExposureAfterTargetMovedAcrossCoverEdge()
        {
            FakeTransport serverTransport = new FakeTransport(TransportRole.Server);
            NetworkMovementSessionManager manager = CreateManager(serverTransport, null, 20);
            TransportConnectionId shooter = new TransportConnectionId(10);
            TransportConnectionId target = new TransportConnectionId(20);
            manager.HandleConnected(NetworkTransportSide.Server, shooter);
            manager.HandleConnected(NetworkTransportSide.Server, target);
            manager.TryGetServerEntityId(target, out uint targetEntity);
            serverTransport.SentPackets.Clear();

            SendTick(manager, shooter, target, 1, 100, default, default);
            for (uint inputTick = 2; inputTick <= 16; inputTick++)
            {
                SendTick(manager, shooter, target, inputTick, 99 + inputTick, default, new PlayerInputCommand { MoveY = 1f, LookYaw = 0f });
            }

            manager.TryGetServerSession(target, out ServerAuthoritativeMovementSession targetSession);
            Assert.That(targetSession.CurrentState.Position.z, Is.GreaterThan(0.5f), "目标必须已离开当前射线胶囊范围，才能证明使用了历史帧。");

            SendTick(manager, shooter, target, 17, 116, new PlayerInputCommand { LookYaw = 90f, FirePressed = true, ShotSequence = 1, ShotServerTick = 100 }, default);

            Assert.That(manager.TryGetServerHealth(targetEntity, out float health), Is.True);
            Assert.That(health, Is.EqualTo(80f));
            Assert.That(FindLastShotResult(serverTransport, out var result), Is.True);
            Assert.That(result.DidHit, Is.True);
            Assert.That(result.TargetEntityId, Is.EqualTo(targetEntity));
        }

        [Test]
        public void RewindHitscan_RejectsShotTickOutsideMaximumWindow()
        {
            FakeTransport serverTransport = new FakeTransport(TransportRole.Server);
            NetworkMovementSessionManager manager = CreateManager(serverTransport, null, 5);
            TransportConnectionId shooter = new TransportConnectionId(10);
            TransportConnectionId target = new TransportConnectionId(20);
            manager.HandleConnected(NetworkTransportSide.Server, shooter);
            manager.HandleConnected(NetworkTransportSide.Server, target);
            manager.TryGetServerEntityId(target, out uint targetEntity);
            serverTransport.SentPackets.Clear();

            for (uint inputTick = 1; inputTick <= 7; inputTick++)
            {
                PlayerInputCommand shooterCommand = inputTick == 7 ? new PlayerInputCommand { LookYaw = 90f, FirePressed = true, ShotSequence = 1, ShotServerTick = 100 } : default;
                SendTick(manager, shooter, target, inputTick, 99 + inputTick, shooterCommand, default);
            }

            Assert.That(manager.TryGetServerHealth(targetEntity, out float health), Is.True);
            Assert.That(health, Is.EqualTo(100f));
            Assert.That(FindLastShotResult(serverTransport, out var result), Is.True);
            Assert.That(result.DidHit, Is.False);
        }

        [Test]
        public void SnapshotReplication_AppliesInterestPriorityBudgetAndMetricsAtScale()
        {
            FakeTransport serverTransport = new FakeTransport(TransportRole.Server);
            NetworkMovementSessionManager manager = CreateManager(serverTransport, null, 10, 8f, 2);
            List<TransportConnectionId> connections = new List<TransportConnectionId>();
            for (uint i = 1; i <= 6; i++)
            {
                TransportConnectionId connection = new TransportConnectionId(i);
                connections.Add(connection);
                Assert.That(manager.HandleConnected(NetworkTransportSide.Server, connection), Is.True);
            }

            serverTransport.SentPackets.Clear();
            serverTransport.SentConnectionIds.Clear();
            for (int i = 0; i < connections.Count; i++)
            {
                Assert.That(manager.TryHandleData(NetworkTransportSide.Server, connections[i], CreateInputPacket(new PlayerInputCommand { Tick = 1 }), out _), Is.True);
            }

            Assert.That(manager.AdvanceServerSessions(100), Is.EqualTo(6));
            Dictionary<TransportConnectionId, int> snapshotsPerClient = new Dictionary<TransportConnectionId, int>();
            for (int i = 0; i < serverTransport.SentPackets.Count; i++)
            {
                if (!RemotePlayerSnapshotCodec.TryDeserialize(serverTransport.SentPackets[i], out _, out _)) continue;
                TransportConnectionId target = serverTransport.SentConnectionIds[i];
                snapshotsPerClient[target] = snapshotsPerClient.TryGetValue(target, out int count) ? count + 1 : 1;
            }

            Assert.That(snapshotsPerClient, Has.Count.EqualTo(6));
            foreach (int count in snapshotsPerClient.Values) Assert.That(count, Is.EqualTo(2));
            Assert.That(manager.Metrics.SnapshotPacketsSent, Is.EqualTo(12));
            Assert.That(manager.Metrics.SnapshotBytesSent, Is.EqualTo((ulong)(12 * RemotePlayerSnapshotCodec.PacketSize)));
            Assert.That(manager.Metrics.LastServerTickMicroseconds, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void SnapshotReplication_SendsCompactDeltaBetweenPeriodicFullBaselines()
        {
            FakeTransport serverTransport = new FakeTransport(TransportRole.Server);
            NetworkMovementSessionManager manager = CreateManager(serverTransport, null);
            TransportConnectionId first = new TransportConnectionId(1);
            TransportConnectionId second = new TransportConnectionId(2);
            manager.HandleConnected(NetworkTransportSide.Server, first);
            manager.HandleConnected(NetworkTransportSide.Server, second);
            serverTransport.SentPackets.Clear();

            SendTick(manager, first, second, 1, 100, default, default);
            ulong fullBytes = manager.Metrics.SnapshotBytesSent;
            serverTransport.SentPackets.Clear();
            SendTick(manager, first, second, 2, 101, default, default);

            int deltaCount = 0;
            for (int i = 0; i < serverTransport.SentPackets.Count; i++)
            {
                if (PacketHeaderCodec.TryRead(serverTransport.SentPackets[i], out PacketHeader header) && header.MessageType == NetworkMessageType.RemotePlayerSnapshotDelta)
                {
                    deltaCount++;
                    Assert.That(serverTransport.SentPackets[i].Count, Is.LessThan(RemotePlayerSnapshotCodec.PacketSize));
                }
            }

            Assert.That(deltaCount, Is.EqualTo(2));
            Assert.That(manager.Metrics.SnapshotBytesSent - fullBytes, Is.LessThan((ulong)(2 * RemotePlayerSnapshotCodec.PacketSize)));
        }

        [Test]
        public void SnapshotReplication_SendsTargetedDespawnWhenKnownEntityLeavesInterestRange()
        {
            FakeTransport serverTransport = new FakeTransport(TransportRole.Server);
            NetworkMovementSessionManager manager = CreateManager(serverTransport, null, 10, 3f, 32);
            TransportConnectionId observer = new TransportConnectionId(1);
            TransportConnectionId movingPlayer = new TransportConnectionId(2);
            manager.HandleConnected(NetworkTransportSide.Server, observer);
            manager.HandleConnected(NetworkTransportSide.Server, movingPlayer);
            manager.TryGetServerEntityId(movingPlayer, out uint movingEntityId);
            serverTransport.SentPackets.Clear();
            serverTransport.SentConnectionIds.Clear();

            SendTick(manager, observer, movingPlayer, 1, 100, default, default);
            Assert.That(serverTransport.SentPackets.Exists(packet => RemotePlayerSnapshotCodec.TryDeserialize(packet, out _, out AFPS.NetCode.SnapshotInterpolation.RemotePlayerSnapshot snapshot) && snapshot.EntityId == movingEntityId), Is.True);
            serverTransport.SentPackets.Clear();
            serverTransport.SentConnectionIds.Clear();
            serverTransport.FailNextReliableConnection = observer;

            for (uint tick = 2; tick <= 24; tick++)
            {
                SendTick(manager, observer, movingPlayer, tick, 99 + tick, default, new PlayerInputCommand { MoveY = 1f });
            }

            int targetedDespawnCount = 0;
            for (int i = 0; i < serverTransport.SentPackets.Count; i++)
            {
                if (serverTransport.SentConnectionIds[i] == observer && PlayerDespawnCodec.TryDeserialize(serverTransport.SentPackets[i], out uint entityId) && entityId == movingEntityId)
                {
                    targetedDespawnCount++;
                }
            }

            Assert.That(serverTransport.FailedReliableSendCount, Is.EqualTo(1));
            Assert.That(targetedDespawnCount, Is.EqualTo(1), "离开兴趣范围时应只向观察者可靠移除一次远端实体。");
        }

        [Test]
        public void SnapshotReplication_RetriesFullBaselineAfterInitialUnreliableSendFailure()
        {
            FakeTransport serverTransport = new FakeTransport(TransportRole.Server);
            NetworkMovementSessionManager manager = CreateManager(serverTransport, null);
            TransportConnectionId observer = new TransportConnectionId(1);
            TransportConnectionId remote = new TransportConnectionId(2);
            manager.HandleConnected(NetworkTransportSide.Server, observer);
            manager.HandleConnected(NetworkTransportSide.Server, remote);
            manager.TryGetServerEntityId(remote, out uint remoteEntityId);
            serverTransport.SentPackets.Clear();
            serverTransport.SentConnectionIds.Clear();
            serverTransport.FailNextUnreliableConnection = observer;

            SendTick(manager, observer, remote, 1, 100, default, default);
            Assert.That(serverTransport.FailedUnreliableSendCount, Is.EqualTo(1));
            Assert.That(ContainsFullSnapshotFor(serverTransport, observer, remoteEntityId), Is.False);
            serverTransport.SentPackets.Clear();
            serverTransport.SentConnectionIds.Clear();

            SendTick(manager, observer, remote, 2, 101, default, default);

            Assert.That(ContainsFullSnapshotFor(serverTransport, observer, remoteEntityId), Is.True, "首次全量包发送失败后不能用未提交的服务器基线发送 Delta。");
        }

        [Test]
        public void Scale_64PlayersStayWithinSnapshotBudgetAndMeasuredTickCeiling()
        {
            const int playerCount = 64;
            const int perClientBudget = 8;
            FakeTransport serverTransport = new FakeTransport(TransportRole.Server);
            NetworkMovementSessionManager manager = CreateManager(serverTransport, null, 10, 10000f, perClientBudget);
            TransportConnectionId[] connections = new TransportConnectionId[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                connections[i] = new TransportConnectionId((uint)(i + 1));
                Assert.That(manager.HandleConnected(NetworkTransportSide.Server, connections[i]), Is.True);
            }

            serverTransport.SentPackets.Clear();
            serverTransport.SentConnectionIds.Clear();
            for (int i = 0; i < playerCount; i++)
            {
                Assert.That(manager.TryHandleData(NetworkTransportSide.Server, connections[i], CreateInputPacket(new PlayerInputCommand { Tick = 1 }), out _), Is.True);
            }

            Assert.That(manager.AdvanceServerSessions(100), Is.EqualTo(playerCount));
            Assert.That(manager.Metrics.SnapshotPacketsSent, Is.EqualTo((ulong)(playerCount * perClientBudget)));
            Assert.That(manager.Metrics.LastServerTickMicroseconds, Is.LessThan(1000000), "64 玩家测试 Tick 不应超过一秒的安全上限。");
        }

        private static NetworkMovementSessionManager CreateManager(IGameTransport serverTransport, IGameTransport clientTransport, uint maximumRewindTicks = 10, float interestRadius = 150f, int snapshotBudget = 32)
        {
            PlayerState initialState = new PlayerState { Tick = 0, IsGrounded = true };
            PlayerSimulationConfig config = new PlayerSimulationConfig(6f, 20f, 20f, 8f);
            return new NetworkMovementSessionManager(serverTransport, clientTransport, initialState, initialState, config, 0.02f, 64, 3, 16, 2, 2, AuthoritativePlayerStateCodec.RecommendedPositionErrorThreshold, AuthoritativePlayerStateCodec.RecommendedVelocityErrorThreshold, maximumRewindTicks: maximumRewindTicks, interestRadius: interestRadius, snapshotBudgetPerClientPerTick: snapshotBudget);
        }

        private static void SendTick(NetworkMovementSessionManager manager, TransportConnectionId shooter, TransportConnectionId target, uint inputTick, uint serverTick, PlayerInputCommand shooterCommand, PlayerInputCommand targetCommand)
        {
            shooterCommand.Tick = inputTick;
            targetCommand.Tick = inputTick;
            Assert.That(manager.TryHandleData(NetworkTransportSide.Server, shooter, CreateInputPacket(shooterCommand), out _), Is.True);
            Assert.That(manager.TryHandleData(NetworkTransportSide.Server, target, CreateInputPacket(targetCommand), out _), Is.True);
            Assert.That(manager.AdvanceServerSessions(serverTick), Is.EqualTo(2));
        }

        private static bool FindLastShotResult(FakeTransport transport, out AFPS.NetCode.Weapons.AuthoritativeShotResult result)
        {
            for (int i = transport.SentPackets.Count - 1; i >= 0; i--)
            {
                if (AuthoritativeShotResultCodec.TryDeserialize(transport.SentPackets[i], out _, out result))
                {
                    return true;
                }
            }

            result = default;
            return false;
        }

        private static bool ContainsFullSnapshotFor(FakeTransport transport, TransportConnectionId connectionId, uint entityId)
        {
            for (int i = 0; i < transport.SentPackets.Count; i++)
            {
                if (transport.SentConnectionIds[i] == connectionId && RemotePlayerSnapshotCodec.TryDeserialize(transport.SentPackets[i], out _, out AFPS.NetCode.SnapshotInterpolation.RemotePlayerSnapshot snapshot) && snapshot.EntityId == entityId)
                {
                    return true;
                }
            }

            return false;
        }

        private static ArraySegment<byte> CreateInputPacket(uint tick, float yaw)
        {
            PlayerInputCommand[] commands = { new PlayerInputCommand { Tick = tick, MoveY = 1f, LookYaw = yaw } };
            return CreateInputPacket(commands[0]);
        }

        private static ArraySegment<byte> CreateInputPacket(PlayerInputCommand command)
        {
            PlayerInputCommand[] commands = { command };
            byte[] packet = new byte[InputCommandBatchCodec.GetPacketSize(1)];
            Assert.That(InputCommandBatchCodec.TrySerialize(new InputCommandBatch(new ArraySegment<PlayerInputCommand>(commands)), command.Tick, new ArraySegment<byte>(packet), out _), Is.True);
            return new ArraySegment<byte>(packet);
        }

        private sealed class FakeTransport : IGameTransport
        {
            public readonly List<ArraySegment<byte>> SentPackets = new List<ArraySegment<byte>>();
            public readonly List<TransportConnectionId> SentConnectionIds = new List<TransportConnectionId>();
            public TransportConnectionId FailNextReliableConnection;
            public int FailedReliableSendCount;
            public TransportConnectionId FailNextUnreliableConnection;
            public int FailedUnreliableSendCount;
            public bool IsRunning => true;
            public TransportRole Role { get; }

            public FakeTransport(TransportRole role)
            {
                Role = role;
            }

            public bool TryStartServer(ushort port, int maxConnections, out string error) { error = null; return false; }
            public bool TryStartClient(string address, ushort port, out string error) { error = null; return false; }
            public void Pump() { }
            public bool TryPollEvent(ArraySegment<byte> receiveBuffer, out GameTransportEvent transportEvent) { transportEvent = default; return false; }

            public TransportSendResult Send(TransportConnectionId connectionId, TransportDelivery delivery, ArraySegment<byte> payload)
            {
                if (delivery == TransportDelivery.ReliableSequenced && connectionId == FailNextReliableConnection)
                {
                    FailNextReliableConnection = default;
                    FailedReliableSendCount++;
                    return TransportSendResult.TransportError;
                }

                if (delivery == TransportDelivery.Unreliable && connectionId == FailNextUnreliableConnection)
                {
                    FailNextUnreliableConnection = default;
                    FailedUnreliableSendCount++;
                    return TransportSendResult.TransportError;
                }

                byte[] copy = new byte[payload.Count];
                Array.Copy(payload.Array, payload.Offset, copy, 0, payload.Count);
                SentPackets.Add(new ArraySegment<byte>(copy));
                SentConnectionIds.Add(connectionId);
                return TransportSendResult.Success;
            }

            public void Disconnect(TransportConnectionId connectionId) { }
            public void Stop() { }
            public void Dispose() { }
        }
    }
}
