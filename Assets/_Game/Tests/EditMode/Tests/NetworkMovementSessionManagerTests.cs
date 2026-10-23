using System;
using System.Collections.Generic;
using AFPS.Math;
using AFPS.NetCode.InputReplication;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Match;
using AFPS.NetCode.Prediction;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Sessions;
using AFPS.NetCode.SnapshotInterpolation;
using AFPS.NetCode.StateReplication;
using AFPS.NetCode.Transport;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Weapons;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class NetworkMovementSessionManagerTests
    {
        [Test]
        public void Respawn_RestoresStaminaWhileTeleportPreservesFatigue()
        {
            var initial = new PlayerState
            {
                Tick = 123, IsGrounded = true, StaminaSpentFixed = Fix.Half, SprintExhausted = true
            };
            var session = new ServerAuthoritativeMovementSession(new FakeTransport(TransportRole.Server),
                new TransportConnectionId(1), initial, new PlayerSimulationConfig(6f, 20f, 20f, 8f),
                0.02f, 16, fixedCollisionWorld:
                    AFPS.Simulation.Characters.Collision.FixedFlatGroundCollisionWorld.Instance);
            session.Teleport(FixVec3.Forward);
            Assert.That(session.CurrentState.StaminaSpentFixed, Is.EqualTo(Fix.Half));
            Assert.That(session.CurrentState.SprintExhausted, Is.True);
            session.Respawn(FixVec3.Zero);
            Assert.That(session.CurrentState.Tick, Is.EqualTo(123));
            Assert.That(session.CurrentState.StaminaSpentFixed, Is.EqualTo(Fix.Zero));
            Assert.That(session.CurrentState.SprintExhausted, Is.False);
            Assert.That(session.CurrentState.IsSprinting, Is.False);
            session.GameplayEnabled = false;
            var command = new PlayerInputCommand { Tick = 124, SprintHeld = true, MoveY = 1f };
            var bytes = new byte[InputCommandBatchCodec.GetPacketSize(1)];
            Assert.That(InputCommandBatchCodec.TrySerialize(new InputCommandBatch(
                new ArraySegment<PlayerInputCommand>(new[] { command })), 1,
                new ArraySegment<byte>(bytes), out _), Is.True);
            Assert.That(session.TryReceiveInputPacket(new ArraySegment<byte>(bytes), out _), Is.True);
            Assert.That(session.TryAdvance(500, out var state, out _), Is.True);
            Assert.That(state.State.IsSprinting, Is.False);
            Assert.That(state.State.StaminaSpentFixed, Is.EqualTo(Fix.Zero));
            Assert.That(state.State.VelocityFixed, Is.EqualTo(FixVec3.Zero));
        }

        [Test]
        public void SpawnAndTeleport_PreserveRawCoordinatesTickAndLook()
        {
            var initial = new PlayerState
            {
                Tick = 77,
                PositionFixed = new FixVec3(Fix.FromRaw(655360001), Fix.Zero, Fix.FromRaw(987654321)),
                VelocityFixed = FixVec3.One,
                YawFixedDegrees = Fix.FromRaw(1234567),
                PitchFixedDegrees = Fix.FromRaw(-123456),
                IsGrounded = true
            };
            var transport = new FakeTransport(TransportRole.Server);
            var manager = CreateManager(transport, null, spawnState: initial, spawnSpacing: 1f / 65536f);
            var first = new TransportConnectionId(1);
            var second = new TransportConnectionId(2);
            Assert.That(manager.HandleConnected(NetworkTransportSide.Server, first), Is.True);
            Assert.That(manager.HandleConnected(NetworkTransportSide.Server, second), Is.True);
            manager.TryGetServerSession(first, out var firstSession);
            manager.TryGetServerSession(second, out var secondSession);
            Assert.That(firstSession.CurrentState.PositionFixed, Is.EqualTo(initial.PositionFixed));
            Assert.That(secondSession.CurrentState.PositionFixed.X.Raw, Is.EqualTo(655360002));
            Assert.That(secondSession.CurrentState.PositionFixed.Z.Raw, Is.EqualTo(987654321));
            var destination = new FixVec3(Fix.FromRaw(-655360001), Fix.FromRaw(12345), Fix.FromRaw(456789));
            firstSession.Teleport(destination);
            Assert.That(firstSession.CurrentState.PositionFixed, Is.EqualTo(destination));
            Assert.That(firstSession.CurrentState.Tick, Is.EqualTo(initial.Tick));
            Assert.That(firstSession.CurrentState.YawFixedDegrees, Is.EqualTo(initial.YawFixedDegrees));
            Assert.That(firstSession.CurrentState.PitchFixedDegrees, Is.EqualTo(initial.PitchFixedDegrees));
            Assert.That(firstSession.CurrentState.VelocityFixed, Is.EqualTo(FixVec3.Zero));
        }

        [Test]
        public void UnrepresentableSpawn_DoesNotCreateOrAssignPartialPlayer()
        {
            var initial = new PlayerState { PositionFixed = new FixVec3(Fix.MaxValue, Fix.Zero, Fix.Zero) };
            var transport = new FakeTransport(TransportRole.Server);
            var manager = CreateManager(transport, null, spawnState: initial, spawnSpacing: 1f / 65536f);
            Assert.That(manager.HandleConnected(NetworkTransportSide.Server, new TransportConnectionId(1)), Is.True);
            int sentBefore = transport.SentPackets.Count;
            var rejected = new TransportConnectionId(2);
            Assert.That(manager.HandleConnected(NetworkTransportSide.Server, rejected), Is.False);
            Assert.That(manager.HandleConnected(NetworkTransportSide.Server, rejected), Is.False);
            Assert.That(manager.ServerSessionCount, Is.EqualTo(1));
            Assert.That(manager.TryGetServerEntityId(rejected, out _), Is.False);
            Assert.That(transport.SentPackets.Count, Is.EqualTo(sentBefore));
        }

        [Test]
        public void EqualDistanceBudget_SelectsLowestEntityId()
        {
            var transport = new FakeTransport(TransportRole.Server);
            var manager = CreateManager(transport, null, snapshotBudget: 1);
            var observer = new TransportConnectionId(1);
            for (uint id = 1; id <= 24; id++)
            {
                var connection = new TransportConnectionId(id);
                manager.HandleConnected(NetworkTransportSide.Server, connection);
                manager.TryGetServerSession(connection, out var session);
                session.Teleport(FixVec3.Zero);
            }
            transport.SentPackets.Clear();
            transport.SentConnectionIds.Clear();
            manager.AdvanceServerSessions(100);
            Assert.That(ContainsFullSnapshotFor(transport, observer, 2), Is.True);
            Assert.That(ContainsFullSnapshotFor(transport, observer, 3), Is.False);
            Assert.That(manager.Metrics.SnapshotPacketsSent, Is.EqualTo(24));
        }

        [Test]
        public void InterestRadius_RejectsOneRawOutsideBoundaryAtLargeCoordinates()
        {
            var transport = new FakeTransport(TransportRole.Server);
            var manager = CreateManager(transport, null, interestRadius: 1f);
            var observer = new TransportConnectionId(1);
            var target = new TransportConnectionId(2);
            manager.HandleConnected(NetworkTransportSide.Server, observer);
            manager.HandleConnected(NetworkTransportSide.Server, target);
            manager.TryGetServerSession(observer, out var observerSession);
            manager.TryGetServerSession(target, out var targetSession);
            observerSession.Teleport(new FixVec3(Fix.FromInt(10000), Fix.Zero, Fix.Zero));
            targetSession.Teleport(new FixVec3(Fix.FromRaw(655425537), Fix.Zero, Fix.Zero));
            transport.SentPackets.Clear();
            transport.SentConnectionIds.Clear();
            manager.AdvanceServerSessions(100);
            Assert.That(ContainsFullSnapshotFor(transport, observer, 2), Is.False);
            targetSession.Teleport(new FixVec3(Fix.FromRaw(655425536), Fix.Zero, Fix.Zero));
            manager.AdvanceServerSessions(101);
            Assert.That(ContainsFullSnapshotFor(transport, observer, 2), Is.True);
        }

        [Test]
        public void RemoteSnapshots_PreserveAuthorityRawThroughFullAndDeltaAtLargeCoordinates()
        {
            var transport = new FakeTransport(TransportRole.Server);
            var manager = CreateManager(transport, null);
            var first = new TransportConnectionId(10);
            var second = new TransportConnectionId(20);
            manager.HandleConnected(NetworkTransportSide.Server, first);
            manager.HandleConnected(NetworkTransportSide.Server, second);
            manager.TryGetServerSession(first, out var authority);
            manager.TryGetServerSession(second, out var opponent);
            authority.Teleport(new UnityEngine.Vector3(10000f, 0f, 0f));
            opponent.Teleport(new UnityEngine.Vector3(10002f, 0f, 0f));
            transport.SentPackets.Clear();
            transport.SentConnectionIds.Clear();

            SendTick(manager, first, second, 1, 100,
                new PlayerInputCommand { MoveX = 1f }, default);
            RemotePlayerSnapshot baseline = default;
            // 在一万米处 float 已不能表示每个 Raw；该断言确保测试能捕获服务器生成时的浮点往返。
            Assert.That(authority.CurrentState.PositionFixed.X.Raw, Is.Not.EqualTo(
                AFPS.Math.Fix.FromFloat(authority.CurrentState.Position.x).Raw));
            // 按实体筛选，避免依赖字典的枚举顺序。
            foreach (var packet in transport.SentPackets)
            {
                if (RemotePlayerSnapshotCodec.TryDeserialize(packet, out _, out var snapshot)
                    && snapshot.EntityId == 1)
                {
                    baseline = snapshot;
                }
            }
            Assert.That(baseline.EntityId, Is.EqualTo(1));
            Assert.That(baseline.PositionFixed, Is.EqualTo(authority.CurrentState.PositionFixed));
            Assert.That(baseline.VelocityFixed, Is.EqualTo(authority.CurrentState.VelocityFixed));

            transport.SentPackets.Clear();
            transport.SentConnectionIds.Clear();
            SendTick(manager, first, second, 2, 101,
                new PlayerInputCommand { MoveX = 1f }, default);
            bool found = false;
            foreach (var packet in transport.SentPackets)
            {
                if (RemotePlayerSnapshotDeltaCodec.TryDeserialize(packet, baseline, out _, out var snapshot))
                {
                    Assert.That(snapshot.PositionFixed, Is.EqualTo(authority.CurrentState.PositionFixed));
                    Assert.That(snapshot.VelocityFixed, Is.EqualTo(authority.CurrentState.VelocityFixed));
                    Assert.That(snapshot.YawFixedDegrees, Is.EqualTo(authority.CurrentState.YawFixedDegrees));
                    Assert.That(snapshot.PitchFixedDegrees, Is.EqualTo(authority.CurrentState.PitchFixedDegrees));
                    found = true;
                }
            }
            Assert.That(found, Is.True);
        }

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
            ArraySegment<byte> authoritativePacket = FindPacket(serverTransport, NetworkMessageType.AuthoritativePlayerState);
            Assert.That(authoritativePacket.Array, Is.Not.Null);
            Assert.That(manager.TryHandleData(NetworkTransportSide.Client, clientConnection, authoritativePacket, out ReconciliationResult reconciliation), Is.True);
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
        public void AuthoritativeHitscan_PreservesFractionalDamageInServerHealthAcrossTicks()
        {
            FakeTransport transport = new FakeTransport(TransportRole.Server);
            Fix damage = Fix.FromRaw(1310721); // 20 生命值再加 1 Raw；不能被旧结果协议的 0.01 精度反写。
            WeaponSimulationConfig weapon = new WeaponSimulationConfig(1, WeaponFireMode.Hitscan,
                30, 5, damage, Fix.FromInt(200), Fix.Zero, Fix.FromRaw(104858));
            NetworkMovementSessionManager manager = CreateManager(transport, null, weaponConfig: weapon);
            TransportConnectionId shooter = new TransportConnectionId(10);
            TransportConnectionId target = new TransportConnectionId(20);
            manager.HandleConnected(NetworkTransportSide.Server, shooter);
            manager.HandleConnected(NetworkTransportSide.Server, target);
            manager.TryGetServerEntityId(target, out uint targetId);
            for (uint tick = 1; tick <= 6; tick++)
            {
                SendTick(manager, shooter, target, tick, 99 + tick,
                    new PlayerInputCommand
                    {
                        LookYaw = 90f,
                        FirePressed = tick == 1 || tick == 6,
                        ShotSequence = tick == 1 ? 1u : tick == 6 ? 2u : 0u
                    }, default);
            }
            Assert.That(manager.TryGetServerHealthFixed(targetId, out Fix health), Is.True);
            Assert.That(health.Raw, Is.EqualTo(6553600 - 2 * damage.Raw));
            Assert.That(manager.TryGetServerDeathmatchPlayer(targetId, out var player), Is.True);
            Assert.That(player.HealthFixed, Is.EqualTo(health));
            Assert.That(FindLastShotResult(transport, out var result), Is.True);
            Assert.That(result.DidHit, Is.True);
            Assert.That(result.TargetEntityId, Is.EqualTo(targetId));
        }

        [Test]
        public void DeathmatchStateCodec_RoundTripsScoreHealthAndRespawnState()
        {
            DeathmatchPlayerState[] players =
            {
                new DeathmatchPlayerState(1, 100, 4, 2, 0, true),
                new DeathmatchPlayerState(2, 0, 1, 5, 75, false)
            };
            DeathmatchState expected = new DeathmatchState(DeathmatchPhase.Playing, 1234, 9000, 0, players);
            byte[] packet = new byte[DeathmatchStateCodec.GetPacketSize(players.Length)];

            Assert.That(DeathmatchStateCodec.TrySerialize(expected, 7, new ArraySegment<byte>(packet), out int bytes), Is.True);
            Assert.That(bytes, Is.EqualTo(packet.Length));
            Assert.That(DeathmatchStateCodec.TryDeserialize(new ArraySegment<byte>(packet), out PacketHeader header, out DeathmatchState actual), Is.True);
            Assert.That(header.Sequence, Is.EqualTo(7));
            Assert.That(actual.Phase, Is.EqualTo(DeathmatchPhase.Playing));
            Assert.That(actual.RemainingTicks, Is.EqualTo(9000));
            Assert.That(actual.PlayerCount, Is.EqualTo(2));
            Assert.That(actual.GetPlayer(1).EntityId, Is.EqualTo(2));
            Assert.That(actual.GetPlayer(1).RespawnTicksRemaining, Is.EqualTo(75));
            Assert.That(actual.GetPlayer(1).IsAlive, Is.False);
        }

        [TestCase(0)]
        [TestCase(655360001)]
        public void Deathmatch_KillUpdatesScoreThenRespawnsTarget(int originRaw)
        {
            FakeTransport transport = new FakeTransport(TransportRole.Server);
            var initial = new PlayerState
            {
                PositionFixed = new FixVec3(Fix.FromRaw(originRaw), Fix.Zero, Fix.Zero),
                IsGrounded = true
            };
            NetworkMovementSessionManager manager = CreateManager(
                transport, null, deathmatchRespawnSeconds: 0.04f, spawnState: initial);
            TransportConnectionId shooter = new TransportConnectionId(10);
            TransportConnectionId target = new TransportConnectionId(20);
            manager.HandleConnected(NetworkTransportSide.Server, shooter);
            manager.HandleConnected(NetworkTransportSide.Server, target);
            manager.TryGetServerEntityId(shooter, out uint shooterEntity);
            manager.TryGetServerEntityId(target, out uint targetEntity);

            for (uint i = 0; i < 5; i++)
            {
                SendTick(manager, shooter, target, i + 1, 100 + i * 5, new PlayerInputCommand { LookYaw = 90f, FirePressed = true, ShotSequence = i + 1 }, default);
            }

            Assert.That(manager.TryGetServerDeathmatchPlayer(shooterEntity, out DeathmatchPlayerState shooterState), Is.True);
            Assert.That(shooterState.Kills, Is.EqualTo(1));
            Assert.That(manager.TryGetServerDeathmatchPlayer(targetEntity, out DeathmatchPlayerState deadState), Is.True);
            Assert.That(deadState.IsAlive, Is.False);
            Assert.That(deadState.Deaths, Is.EqualTo(1));

            SendTick(manager, shooter, target, 6, 122, default, default);
            Assert.That(manager.TryGetServerDeathmatchPlayer(targetEntity, out DeathmatchPlayerState respawnedState), Is.True);
            Assert.That(respawnedState.IsAlive, Is.True);
            Assert.That(respawnedState.Health, Is.EqualTo(100));
            Assert.That(manager.TryGetServerSession(target, out var respawnedSession), Is.True);
            Assert.That(respawnedSession.CurrentState.PositionFixed.X.Raw, Is.EqualTo(originRaw + 163840));
        }

        [Test]
        public void Deathmatch_ReachingKillLimitFinishesWithWinner()
        {
            FakeTransport transport = new FakeTransport(TransportRole.Server);
            NetworkMovementSessionManager manager = CreateManager(transport, null, deathmatchKillLimit: 1);
            TransportConnectionId shooter = new TransportConnectionId(10);
            TransportConnectionId target = new TransportConnectionId(20);
            manager.HandleConnected(NetworkTransportSide.Server, shooter);
            manager.HandleConnected(NetworkTransportSide.Server, target);
            manager.TryGetServerEntityId(shooter, out uint shooterEntity);

            for (uint i = 0; i < 5; i++)
            {
                SendTick(manager, shooter, target, i + 1, 100 + i * 5, new PlayerInputCommand { LookYaw = 90f, FirePressed = true, ShotSequence = i + 1 }, default);
            }

            Assert.That(manager.ServerDeathmatchPhase, Is.EqualTo(DeathmatchPhase.Finished));
            ArraySegment<byte> packet = FindPacket(transport, NetworkMessageType.DeathmatchState, true);
            Assert.That(DeathmatchStateCodec.TryDeserialize(packet, out _, out DeathmatchState state), Is.True);
            Assert.That(state.Phase, Is.EqualTo(DeathmatchPhase.Finished));
            Assert.That(state.WinnerEntityId, Is.EqualTo(shooterEntity));
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

        [Test]
        public void TimeSync_IdleRoundTripMeasuresRttExpiresAndReconnectClearsSamples()
        {
            ulong now = 1000000;
            FakeTransport serverTransport = new FakeTransport(TransportRole.Server);
            FakeTransport clientTransport = new FakeTransport(TransportRole.Client);
            NetworkMovementSessionManager server = CreateManager(serverTransport, null, clock: () => now);
            NetworkMovementSessionManager client = CreateManager(null, clientTransport, clock: () => now);
            TransportConnectionId connection = new TransportConnectionId(10);
            server.HandleConnected(NetworkTransportSide.Server, connection);
            client.HandleConnected(NetworkTransportSide.Client, connection);
            serverTransport.SentPackets.Clear();
            client.AdvanceTimeSync(0, 0f);
            Assert.That(clientTransport.SentPackets.Count, Is.EqualTo(1));
            client.AdvanceTimeSync(0, 0f);
            Assert.That(clientTransport.SentPackets.Count, Is.EqualTo(1));
            // 不发送任何输入，20ms 上行 + 20ms 下行，服务器处理时间为零。
            now += 20000;
            server.AdvanceTimeSync(500, 0.5f);
            Assert.That(server.TryHandleData(NetworkTransportSide.Server, connection,
                clientTransport.SentPackets[0], out _), Is.True);
            now += 20000;
            Assert.That(client.TryHandleData(NetworkTransportSide.Client, connection,
                serverTransport.SentPackets[0], out _), Is.True);
            Assert.That(client.HasTimeSync, Is.True);
            Assert.That(client.Metrics.LastNetworkRoundTripMilliseconds, Is.EqualTo(40d));
            Assert.That(client.TryGetSynchronizedServerTick(out double estimated), Is.True);
            Assert.That(estimated, Is.EqualTo(501.5d).Within(0.0001d));
            Assert.That(client.TryHandleData(NetworkTransportSide.Client, new TransportConnectionId(99),
                serverTransport.SentPackets[0], out _), Is.False);
            Assert.That(client.TryHandleData(NetworkTransportSide.Client, connection,
                serverTransport.SentPackets[0], out _), Is.False);
            now += 3000001;
            Assert.That(client.HasTimeSync, Is.False);
            Assert.That(client.TryGetSynchronizedServerTick(out _), Is.False);
            client.AdvanceTimeSync(0, 0f);
            Assert.That(clientTransport.SentPackets.Count, Is.EqualTo(2));
            client.HandleDisconnected(NetworkTransportSide.Client, connection);
            Assert.That(client.HasTimeSync, Is.False);
            Assert.That(client.Metrics.TimeSyncResponsesReceived, Is.Zero);
            now++;
            client.HandleConnected(NetworkTransportSide.Client, connection);
            Assert.That(client.TryHandleData(NetworkTransportSide.Client, connection,
                serverTransport.SentPackets[0], out _), Is.False);
            server.HandleDisconnected(NetworkTransportSide.Server, connection);
            Assert.That(server.TryHandleData(NetworkTransportSide.Server, connection,
                clientTransport.SentPackets[0], out _), Is.False);
        }

        [Test]
        public void TimeSync_FailedSendIsThrottledAndRetriedWithoutInput()
        {
            ulong now = 0;
            FakeTransport transport = new FakeTransport(TransportRole.Client);
            NetworkMovementSessionManager client = CreateManager(null, transport, clock: () => now);
            TransportConnectionId connection = new TransportConnectionId(10);
            client.HandleConnected(NetworkTransportSide.Client, connection);
            transport.FailNextUnreliableConnection = connection;
            client.AdvanceTimeSync(0, 0f);
            Assert.That(client.Metrics.TimeSyncRequestsSent, Is.Zero);
            now = 499999;
            client.AdvanceTimeSync(0, 0f);
            Assert.That(transport.SentPackets.Count, Is.Zero);
            now = 500000;
            client.AdvanceTimeSync(0, 0f);
            Assert.That(client.Metrics.TimeSyncRequestsSent, Is.EqualTo(1));
            Assert.That(transport.SentPackets.Count, Is.EqualTo(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => client.AdvanceTimeSync(0, float.NaN));
        }
        [Test]
        public void WeaponReload_InputTraversesSessionAndRestoresMagazineAfterWorldTicks()
        {
            FakeTransport transport = new FakeTransport(TransportRole.Server);
            NetworkMovementSessionManager manager = CreateManager(transport, null);
            TransportConnectionId shooter = new TransportConnectionId(10);
            TransportConnectionId other = new TransportConnectionId(20);
            manager.HandleConnected(NetworkTransportSide.Server, shooter);
            manager.HandleConnected(NetworkTransportSide.Server, other);
            manager.TryGetServerSession(shooter, out var session);
            for (uint tick = 1; tick <= 102; tick++)
            {
                bool fire = tick == 1 || tick == 3 || tick == 102;
                SendTick(manager, shooter, other, tick, tick + 99,
                    new PlayerInputCommand
                    {
                        FirePressed = fire,
                        ReloadPressed = tick == 2,
                        ShotSequence = fire ? (tick == 1 ? 1u : tick == 3 ? 2u : 3u) : 0u
                    }, default);
                if (tick == 3)
                {
                    Assert.That(session.LastWeaponFireResult.RejectReason,
                        Is.EqualTo(AFPS.NetCode.Weapons.WeaponFireRejectReason.Reloading));
                    Assert.That(session.LastWeaponFireResult.RemainingAmmo, Is.EqualTo(29));
                }
            }
            Assert.That(session.LastWeaponFireResult.Accepted, Is.True);
            Assert.That(session.LastWeaponFireResult.RemainingAmmo, Is.EqualTo(29));
        }
        [Test]
        public void WeaponState_PrivateReplicationAcknowledgesPredictionAndReloadCompletion()
        {
            var serverTransport = new FakeTransport(TransportRole.Server);
            var clientTransport = new FakeTransport(TransportRole.Client);
            var manager = CreateManager(serverTransport, clientTransport);
            var shooter = new TransportConnectionId(10);
            var other = new TransportConnectionId(20);
            var client = new TransportConnectionId(30);
            manager.HandleConnected(NetworkTransportSide.Server, shooter);
            manager.HandleConnected(NetworkTransportSide.Server, other);
            manager.HandleConnected(NetworkTransportSide.Client, client);
            Assert.That(manager.TryHandleData(NetworkTransportSide.Client, client,
                serverTransport.SentPackets[0], out _), Is.True);
            int snapshots = 0;
            for (uint tick = 1; tick <= 103; tick++)
            {
                var command = new PlayerInputCommand
                {
                    Tick = tick,
                    FirePressed = tick == 2,
                    ShotSequence = tick == 2 ? 1u : 0u,
                    ReloadPressed = tick == 3
                };
                Assert.That(manager.TryPredictAndSend(command, out _, out _), Is.True);
                if (tick == 2)
                {
                    Assert.That(manager.ClientWeapon.Ammo, Is.EqualTo(29));
                }
                if (tick == 3)
                {
                    Assert.That(manager.ClientWeapon.IsReloading, Is.True);
                    Assert.That(manager.ClientWeapon.CanFire, Is.False);
                }
                serverTransport.SentPackets.Clear();
                serverTransport.SentConnectionIds.Clear();
                Assert.That(manager.TryHandleData(NetworkTransportSide.Server, shooter,
                    clientTransport.SentPackets[clientTransport.SentPackets.Count - 1], out _), Is.True);
                manager.TryHandleData(NetworkTransportSide.Server, other,
                    CreateInputPacket(new PlayerInputCommand { Tick = tick }), out _);
                manager.AdvanceServerSessions(99 + tick);
                for (int index = 0; index < serverTransport.SentPackets.Count; index++)
                {
                    var packet = serverTransport.SentPackets[index];
                    if (!AuthoritativeWeaponStateCodec.TryDeserialize(packet, out _, out var state))
                    {
                        continue;
                    }
                    if (serverTransport.SentConnectionIds[index] != shooter)
                    {
                        Assert.That(state.EntityId, Is.EqualTo(2));
                        continue;
                    }
                    snapshots++;
                    Assert.That(state.EntityId, Is.EqualTo(1));
                    Assert.That(manager.TryHandleData(NetworkTransportSide.Client, client, packet, out _), Is.True);
                    Assert.That(manager.TryHandleData(NetworkTransportSide.Client, client, packet, out _), Is.False);
                }
            }
            Assert.That(manager.ClientWeapon.Ammo, Is.EqualTo(30));
            Assert.That(manager.ClientWeapon.IsReloading, Is.False);
            Assert.That(manager.ClientWeapon.CanFire, Is.True);
            Assert.That(snapshots, Is.LessThan(25));
            manager.HandleDisconnected(NetworkTransportSide.Client, client);
            Assert.That(manager.ClientWeapon.HasState, Is.False);
        }
        [Test]
        public void Respawn_RestoresWeaponAndRejectsDeadActions()
        {
            var transport = new FakeTransport(TransportRole.Server);
            var weapon = new WeaponSimulationConfig(1, WeaponFireMode.Hitscan, 2, 5, 100f, 100f, 0f, 1.6f);
            var manager = CreateManager(transport, null, weaponConfig: weapon, deathmatchRespawnSeconds: 0.04f);
            var shooter = new TransportConnectionId(10);
            var target = new TransportConnectionId(20);
            manager.HandleConnected(NetworkTransportSide.Server, shooter);
            manager.HandleConnected(NetworkTransportSide.Server, target);
            manager.TryGetServerSession(target, out var session);
            SendTick(manager, shooter, target, 1, 100, default,
                new PlayerInputCommand { FirePressed = true, ShotSequence = 1, LookYaw = 90f });
            SendTick(manager, shooter, target, 2, 101,
                new PlayerInputCommand { FirePressed = true, ShotSequence = 1, LookYaw = 90f },
                new PlayerInputCommand { ReloadPressed = true, LookYaw = 90f });
            FixVec3 deadPosition = session.CurrentState.PositionFixed;
            SendTick(manager, shooter, target, 3, 102, default,
                new PlayerInputCommand { MoveY = 1f, JumpPressed = true, FirePressed = true, ShotSequence = 2 });
            Assert.That(session.CurrentState.PositionFixed, Is.EqualTo(deadPosition));
            Assert.That(session.LastWeaponFireResult.Accepted, Is.False);

            transport.SentPackets.Clear();
            transport.SentConnectionIds.Clear();
            SendTick(manager, shooter, target, 4, 103, default,
                new PlayerInputCommand { FirePressed = true, ShotSequence = 3, LookYaw = 90f });
            Assert.That(session.LastWeaponFireResult.Accepted, Is.True, "新生命不应沿用旧生命的冷却或换弹。");
            Assert.That(session.LastWeaponFireResult.RemainingAmmo, Is.EqualTo(1));
            bool received = false;
            for (int i = 0; i < transport.SentPackets.Count; i++)
            {
                if (transport.SentConnectionIds[i] == target &&
                    AuthoritativeWeaponStateCodec.TryDeserialize(transport.SentPackets[i], out _, out var state))
                {
                    received = true;
                    Assert.That(state.Ammo, Is.EqualTo(1));
                    Assert.That(state.IsEquipped, Is.True);
                    Assert.That(state.IsReloading, Is.False);
                }
            }
            Assert.That(received, Is.True);
            SendTick(manager, shooter, target, 5, 108, default,
                new PlayerInputCommand { FirePressed = true, ShotSequence = 1 });
            Assert.That(session.LastWeaponFireResult.RejectReason,
                Is.EqualTo(AFPS.NetCode.Weapons.WeaponFireRejectReason.DuplicateOrOldSequence));
        }

        [TestCase(100u, false)]
        [TestCase(101u, false)]
        [TestCase(102u, false)]
        [TestCase(103u, true)]
        public void Respawn_RewindCannotHitPreviousLife(uint requestedTick, bool expectedHit)
        {
            var transport = new FakeTransport(TransportRole.Server);
            var weapon = new WeaponSimulationConfig(1, WeaponFireMode.Hitscan, 30, 5, 100f, 100f, 0f, 1.6f);
            var manager = CreateManager(transport, null, weaponConfig: weapon, deathmatchRespawnSeconds: 0.04f);
            var shooter = new TransportConnectionId(10);
            var target = new TransportConnectionId(20);
            manager.HandleConnected(NetworkTransportSide.Server, shooter);
            manager.HandleConnected(NetworkTransportSide.Server, target);
            SendTick(manager, shooter, target, 1, 100, default, default);
            SendTick(manager, shooter, target, 2, 101,
                new PlayerInputCommand { FirePressed = true, ShotSequence = 1, LookYaw = 90f }, default);
            SendTick(manager, shooter, target, 3, 103, default, default);
            transport.SentPackets.Clear();
            SendTick(manager, shooter, target, 4, 106,
                new PlayerInputCommand
                {
                    FirePressed = true,
                    ShotSequence = 2,
                    LookYaw = 90f,
                    ShotServerTick = requestedTick
                }, default);
            Assert.That(FindLastShotResult(transport, out var result), Is.True);
            Assert.That(result.DidHit, Is.EqualTo(expectedHit));
        }

        [Test]
        public void Deathmatch_TimeoutPrecedesRespawnAndDisablesActions()
        {
            var transport = new FakeTransport(TransportRole.Server);
            var weapon = new WeaponSimulationConfig(1, WeaponFireMode.Hitscan, 30, 5, 100f, 100f, 0f, 1.6f);
            var manager = CreateManager(transport, null, weaponConfig: weapon,
                deathmatchRespawnSeconds: 0.04f, deathmatchDurationSeconds: 0.06f);
            var shooter = new TransportConnectionId(10);
            var target = new TransportConnectionId(20);
            manager.HandleConnected(NetworkTransportSide.Server, shooter);
            manager.HandleConnected(NetworkTransportSide.Server, target);
            manager.TryGetServerEntityId(target, out uint targetEntity);
            manager.TryGetServerSession(shooter, out var session);
            SendTick(manager, shooter, target, 1, 100, default, default);
            SendTick(manager, shooter, target, 2, 101,
                new PlayerInputCommand { FirePressed = true, ShotSequence = 1, LookYaw = 90f }, default);
            FixVec3 lastPosition = session.CurrentState.PositionFixed;
            SendTick(manager, shooter, target, 3, 103,
                new PlayerInputCommand { MoveY = 1f, JumpPressed = true, FirePressed = true, ShotSequence = 2 }, default);
            Assert.That(manager.ServerDeathmatchPhase, Is.EqualTo(DeathmatchPhase.Finished));
            Assert.That(manager.TryGetServerDeathmatchPlayer(targetEntity, out var targetState), Is.True);
            Assert.That(targetState.IsAlive, Is.False);
            Assert.That(session.CurrentState.PositionFixed, Is.EqualTo(lastPosition));
            Assert.That(session.LastWeaponFireResult.Accepted, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Deathmatch_EmptyRoomStartsFreshRoundWithoutReusingIdentity(bool finished)
        {
            var transport = new FakeTransport(TransportRole.Server);
            var manager = CreateManager(transport, null, deathmatchDurationSeconds: 0.2f);
            var first = new TransportConnectionId(10);
            var second = new TransportConnectionId(20);
            manager.HandleConnected(NetworkTransportSide.Server, first);
            manager.HandleConnected(NetworkTransportSide.Server, second);
            manager.TryGetServerEntityId(second, out uint previousEntity);
            SendTick(manager, first, second, 1, 100, default, default);
            if (finished)
            {
                SendTick(manager, first, second, 2, 110, default, default);
            }
            var previousPhase = manager.ServerDeathmatchPhase;
            Assert.That(DeathmatchStateCodec.TryDeserialize(
                FindPacket(transport, NetworkMessageType.DeathmatchState, true), out var previousHeader, out _), Is.True);
            Assert.That(manager.HandleDisconnected(NetworkTransportSide.Server, first), Is.True);
            Assert.That(manager.ServerDeathmatchPhase, Is.EqualTo(previousPhase));
            Assert.That(manager.HandleDisconnected(NetworkTransportSide.Server, second), Is.True);
            Assert.That(manager.ServerDeathmatchPhase, Is.EqualTo(DeathmatchPhase.WaitingForPlayers));
            Assert.That(manager.AdvanceServerSessions(500), Is.Zero);

            manager.HandleConnected(NetworkTransportSide.Server, first);
            manager.TryGetServerEntityId(first, out uint newEntity);
            Assert.That(newEntity, Is.GreaterThan(previousEntity));
            manager.TryHandleData(NetworkTransportSide.Server, first,
                CreateInputPacket(new PlayerInputCommand { Tick = 1 }), out _);
            Assert.That(manager.AdvanceServerSessions(600), Is.EqualTo(1));
            Assert.That(manager.ServerDeathmatchPhase, Is.EqualTo(DeathmatchPhase.WaitingForPlayers));
            manager.HandleConnected(NetworkTransportSide.Server, second);
            manager.TryHandleData(NetworkTransportSide.Server, first,
                CreateInputPacket(new PlayerInputCommand { Tick = 2 }), out _);
            manager.TryHandleData(NetworkTransportSide.Server, second,
                CreateInputPacket(new PlayerInputCommand { Tick = 1 }), out _);
            Assert.That(manager.AdvanceServerSessions(700), Is.EqualTo(2));
            Assert.That(DeathmatchStateCodec.TryDeserialize(
                FindPacket(transport, NetworkMessageType.DeathmatchState, true), out var newHeader, out var state), Is.True);
            Assert.That(newHeader.Sequence, Is.GreaterThan(previousHeader.Sequence));
            Assert.That(state.Phase, Is.EqualTo(DeathmatchPhase.Playing));
            Assert.That(state.RemainingTicks, Is.EqualTo(10));
            Assert.That(state.WinnerEntityId, Is.Zero);
            Assert.That(state.PlayerCount, Is.EqualTo(2));
            for (int i = 0; i < state.PlayerCount; i++)
            {
                Assert.That(state.GetPlayer(i).Health, Is.EqualTo(100));
                Assert.That(state.GetPlayer(i).Kills, Is.Zero);
                Assert.That(state.GetPlayer(i).Deaths, Is.Zero);
            }
        }

        [Test]
        public void Deathmatch_EmptyRoomDiscardsPreviousRoundProjectiles()
        {
            var transport = new FakeTransport(TransportRole.Server);
            var weapon = new WeaponSimulationConfig(1, WeaponFireMode.Projectile, 30, 5, 20f, 100f, 1f, 1.6f);
            var manager = CreateManager(transport, null, weaponConfig: weapon);
            var first = new TransportConnectionId(10);
            var second = new TransportConnectionId(20);
            manager.HandleConnected(NetworkTransportSide.Server, first);
            manager.HandleConnected(NetworkTransportSide.Server, second);
            SendTick(manager, first, second, 1, 100,
                new PlayerInputCommand { FirePressed = true, ShotSequence = 1, LookYaw = 90f }, default);
            Assert.That(FindLastShotResult(transport, out var shot), Is.True);
            Assert.That(shot.DidHit, Is.False);
            manager.HandleDisconnected(NetworkTransportSide.Server, first);
            manager.HandleDisconnected(NetworkTransportSide.Server, second);
            manager.HandleConnected(NetworkTransportSide.Server, first);
            manager.HandleConnected(NetworkTransportSide.Server, second);
            manager.TryGetServerEntityId(first, out uint nextEntity);
            manager.TryGetServerSession(first, out var session);
            // 把新玩家放在旧弹道前方 0.6 米；若未清除旧弹丸，约数个 Tick 后会受到伤害。
            session.Teleport(new FixVec3(Fix.FromFloat(0.6f), Fix.Zero, Fix.Zero));
            transport.SentPackets.Clear();
            for (uint tick = 1; tick <= 10; tick++)
            {
                SendTick(manager, first, second, tick, 199 + tick, default, default);
            }
            Assert.That(manager.TryGetServerHealthFixed(nextEntity, out Fix health), Is.True);
            Assert.That(health, Is.EqualTo(Fix.FromInt(100)));
            Assert.That(FindLastShotResult(transport, out _), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Deathmatch_LateJoinReceivesExistingRoundAndWeaponPermission(bool finished)
        {
            var transport = new FakeTransport(TransportRole.Server);
            var manager = CreateManager(transport, null, deathmatchDurationSeconds: 0.2f);
            var first = new TransportConnectionId(10);
            var second = new TransportConnectionId(20);
            var late = new TransportConnectionId(30);
            manager.HandleConnected(NetworkTransportSide.Server, first);
            manager.HandleConnected(NetworkTransportSide.Server, second);
            SendTick(manager, first, second, 1, 100, default, default);
            if (finished)
            {
                SendTick(manager, first, second, 2, 110, default, default);
            }
            manager.HandleConnected(NetworkTransportSide.Server, late);
            manager.TryGetServerEntityId(late, out uint lateEntity);
            uint inputTick = finished ? 3u : 2u;
            uint serverTick = finished ? 111u : 105u;
            manager.TryHandleData(NetworkTransportSide.Server, first,
                CreateInputPacket(new PlayerInputCommand { Tick = inputTick }), out _);
            manager.TryHandleData(NetworkTransportSide.Server, second,
                CreateInputPacket(new PlayerInputCommand { Tick = inputTick }), out _);
            manager.TryHandleData(NetworkTransportSide.Server, late,
                CreateInputPacket(new PlayerInputCommand { Tick = 1 }), out _);
            Assert.That(manager.AdvanceServerSessions(serverTick), Is.EqualTo(3));
            bool receivedRound = false;
            bool receivedWeapon = false;
            for (int i = 0; i < transport.SentPackets.Count; i++)
            {
                if (transport.SentConnectionIds[i] != late)
                {
                    continue;
                }
                if (DeathmatchStateCodec.TryDeserialize(transport.SentPackets[i], out _, out var state))
                {
                    receivedRound = true;
                    Assert.That(state.Phase, Is.EqualTo(finished ? DeathmatchPhase.Finished : DeathmatchPhase.Playing));
                    Assert.That(state.RemainingTicks, Is.EqualTo(finished ? 0 : 5));
                    Assert.That(state.WinnerEntityId, Is.Zero);
                    Assert.That(state.PlayerCount, Is.EqualTo(3));
                    Assert.That(state.TryGetPlayer(lateEntity, out var player), Is.True);
                    Assert.That(player.Kills, Is.Zero);
                    Assert.That(player.Health, Is.EqualTo(100));
                }
                if (AuthoritativeWeaponStateCodec.TryDeserialize(transport.SentPackets[i], out _, out var weapon))
                {
                    receivedWeapon = true;
                    Assert.That(weapon.EntityId, Is.EqualTo(lateEntity));
                    Assert.That(weapon.IsEquipped, Is.EqualTo(!finished));
                }
            }
            Assert.That(receivedRound, Is.True);
            Assert.That(receivedWeapon, Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Deathmatch_TimeoutSelectsUniqueLeaderOrDraw(int scoringMode)
        {
            var transport = new FakeTransport(TransportRole.Server);
            var weapon = new WeaponSimulationConfig(1, WeaponFireMode.Hitscan, 30, 5, 100f, 100f, 0f, 1.6f);
            var manager = CreateManager(transport, null, weaponConfig: weapon,
                deathmatchRespawnSeconds: 0.04f, deathmatchDurationSeconds: 0.2f);
            var first = new TransportConnectionId(10);
            var second = new TransportConnectionId(20);
            manager.HandleConnected(NetworkTransportSide.Server, first);
            manager.HandleConnected(NetworkTransportSide.Server, second);
            manager.TryGetServerEntityId(first, out uint firstEntity);
            SendTick(manager, first, second, 1, 100,
                new PlayerInputCommand
                {
                    FirePressed = scoringMode > 0,
                    ShotSequence = scoringMode > 0 ? 1u : 0u,
                    LookYaw = 90f
                }, default);
            SendTick(manager, first, second, 2, 102, default, default);
            SendTick(manager, first, second, 3, 105, default,
                new PlayerInputCommand
                {
                    FirePressed = scoringMode == 2,
                    ShotSequence = scoringMode == 2 ? 1u : 0u,
                    LookYaw = 270f
                });
            SendTick(manager, first, second, 4, 110, default, default);
            Assert.That(DeathmatchStateCodec.TryDeserialize(
                FindPacket(transport, NetworkMessageType.DeathmatchState, true), out _, out var state), Is.True);
            Assert.That(state.Phase, Is.EqualTo(DeathmatchPhase.Finished));
            Assert.That(state.WinnerEntityId, Is.EqualTo(scoringMode == 1 ? firstEntity : 0u));
            Assert.That(state.RemainingTicks, Is.Zero);
            Assert.That(state.GetPlayer(0).Kills, Is.EqualTo(scoringMode > 0 ? 1 : 0));
            Assert.That(state.GetPlayer(1).Kills, Is.EqualTo(scoringMode == 2 ? 1 : 0));
        }

        [Test]
        public void Deathmatch_PlayerDepartureKeepsClockAndRemovesScoreEntry()
        {
            var transport = new FakeTransport(TransportRole.Server);
            var manager = CreateManager(transport, null, deathmatchDurationSeconds: 0.2f);
            var first = new TransportConnectionId(10);
            var second = new TransportConnectionId(20);
            manager.HandleConnected(NetworkTransportSide.Server, first);
            manager.HandleConnected(NetworkTransportSide.Server, second);
            manager.TryGetServerEntityId(first, out uint remainingEntity);
            manager.TryGetServerEntityId(second, out uint departedEntity);
            SendTick(manager, first, second, 1, 100, default, default);
            manager.HandleDisconnected(NetworkTransportSide.Server, second);
            manager.TryHandleData(NetworkTransportSide.Server, first,
                CreateInputPacket(new PlayerInputCommand { Tick = 2 }), out _);
            Assert.That(manager.AdvanceServerSessions(105), Is.EqualTo(1));
            Assert.That(DeathmatchStateCodec.TryDeserialize(
                FindPacket(transport, NetworkMessageType.DeathmatchState, true), out _, out var state), Is.True);
            Assert.That(state.Phase, Is.EqualTo(DeathmatchPhase.Playing));
            Assert.That(state.RemainingTicks, Is.EqualTo(5));
            Assert.That(state.PlayerCount, Is.EqualTo(1));
            Assert.That(state.TryGetPlayer(departedEntity, out _), Is.False);
            manager.TryHandleData(NetworkTransportSide.Server, first,
                CreateInputPacket(new PlayerInputCommand { Tick = 3 }), out _);
            manager.AdvanceServerSessions(110);
            Assert.That(DeathmatchStateCodec.TryDeserialize(
                FindPacket(transport, NetworkMessageType.DeathmatchState, true), out _, out state), Is.True);
            Assert.That(state.WinnerEntityId, Is.EqualTo(remainingEntity));
        }

        [Test]
        public void Deathmatch_FinishedWinnerStaysFixedAfterDepartureAndLateJoin()
        {
            var transport = new FakeTransport(TransportRole.Server);
            var weapon = new WeaponSimulationConfig(1, WeaponFireMode.Hitscan, 30, 5, 100f, 100f, 0f, 1.6f);
            var manager = CreateManager(transport, null, weaponConfig: weapon, deathmatchKillLimit: 1);
            var first = new TransportConnectionId(10);
            var second = new TransportConnectionId(20);
            var late = new TransportConnectionId(30);
            manager.HandleConnected(NetworkTransportSide.Server, first);
            manager.HandleConnected(NetworkTransportSide.Server, second);
            manager.TryGetServerEntityId(first, out uint winnerEntity);
            SendTick(manager, first, second, 1, 100,
                new PlayerInputCommand { FirePressed = true, ShotSequence = 1, LookYaw = 90f }, default);
            manager.HandleDisconnected(NetworkTransportSide.Server, first);
            manager.HandleConnected(NetworkTransportSide.Server, late);
            manager.TryHandleData(NetworkTransportSide.Server, second,
                CreateInputPacket(new PlayerInputCommand { Tick = 2 }), out _);
            manager.TryHandleData(NetworkTransportSide.Server, late,
                CreateInputPacket(new PlayerInputCommand { Tick = 1 }), out _);
            Assert.That(manager.AdvanceServerSessions(105), Is.EqualTo(2));
            Assert.That(DeathmatchStateCodec.TryDeserialize(
                FindPacket(transport, NetworkMessageType.DeathmatchState, true), out _, out var state), Is.True);
            Assert.That(state.Phase, Is.EqualTo(DeathmatchPhase.Finished));
            // 终局结果引用获胜时的实体身份；赢家离开不能把比赛改判给其他人或晚加入者。
            Assert.That(state.WinnerEntityId, Is.EqualTo(winnerEntity));
            Assert.That(state.TryGetPlayer(winnerEntity, out _), Is.False);
        }

        [Test]
        public void Deathmatch_TimeoutSupportsServerTickWrap()
        {
            var transport = new FakeTransport(TransportRole.Server);
            var manager = CreateManager(transport, null, deathmatchDurationSeconds: 0.2f);
            var first = new TransportConnectionId(10);
            var second = new TransportConnectionId(20);
            manager.HandleConnected(NetworkTransportSide.Server, first);
            manager.HandleConnected(NetworkTransportSide.Server, second);
            SendTick(manager, first, second, 1, uint.MaxValue - 4, default, default);
            SendTick(manager, first, second, 2, 4, default, default);
            // 经过 9 Tick 尚未到时，也尚未达到 10 Tick 广播周期，直接检查服务器阶段。
            Assert.That(manager.ServerDeathmatchPhase, Is.EqualTo(DeathmatchPhase.Playing));
            SendTick(manager, first, second, 3, 5, default, default);
            Assert.That(DeathmatchStateCodec.TryDeserialize(
                FindPacket(transport, NetworkMessageType.DeathmatchState, true), out _, out var state), Is.True);
            Assert.That(state.Phase, Is.EqualTo(DeathmatchPhase.Finished));
            Assert.That(state.RemainingTicks, Is.Zero);
        }

        [Test]
        public void ShotPresentation_DrainsBurstInOrderAndRejectsDuplicateOrWrongConnection()
        {
            var transport = new FakeTransport(TransportRole.Client);
            var manager = CreateManager(null, transport);
            var connection = new TransportConnectionId(30);
            manager.HandleConnected(NetworkTransportSide.Client, connection);
            var first = CreateShotPresentationPacket(10, 1);
            var second = CreateShotPresentationPacket(11, 2);
            Assert.That(manager.TryHandleData(NetworkTransportSide.Client, new TransportConnectionId(99), first, out _), Is.False);
            Assert.That(manager.TryHandleData(NetworkTransportSide.Client, connection, first, out _), Is.True);
            Assert.That(manager.TryHandleData(NetworkTransportSide.Client, connection, first, out _), Is.False);
            Assert.That(manager.TryHandleData(NetworkTransportSide.Client, connection, second, out _), Is.True);
            Assert.That(manager.ShotResultVersion, Is.EqualTo(2));
            Assert.That(manager.LastReceivedShotResult.Shot.ShotSequence, Is.EqualTo(2));
            Assert.That(manager.TryDequeueShotPresentation(out var result), Is.True);
            Assert.That(result.Shot.ShotSequence, Is.EqualTo(1));
            Assert.That(manager.TryDequeueShotPresentation(out result), Is.True);
            Assert.That(result.Shot.ShotSequence, Is.EqualTo(2));
            Assert.That(manager.TryDequeueShotPresentation(out _), Is.False);
        }

        [Test]
        public void ShotPresentation_BoundsBacklogAndCountsDroppedOldestResults()
        {
            var manager = CreateManager(null, new FakeTransport(TransportRole.Client));
            var connection = new TransportConnectionId(30);
            manager.HandleConnected(NetworkTransportSide.Client, connection);
            uint total = NetworkMovementSessionManager.ShotPresentationCapacity + 3u;
            for (uint i = 1; i <= total; i++)
            {
                Assert.That(manager.TryHandleData(NetworkTransportSide.Client, connection,
                    CreateShotPresentationPacket(i, i), out _), Is.True);
            }
            Assert.That(manager.PendingShotPresentationCount, Is.EqualTo(NetworkMovementSessionManager.ShotPresentationCapacity));
            Assert.That(manager.DroppedShotPresentationCount, Is.EqualTo(3));
            for (uint sequence = 4; sequence <= total; sequence++)
            {
                Assert.That(manager.TryDequeueShotPresentation(out var result), Is.True);
                Assert.That(result.Shot.ShotSequence, Is.EqualTo(sequence));
            }
            Assert.That(manager.TryDequeueShotPresentation(out _), Is.False);
        }

        [Test]
        public void ShotPresentation_ReconnectClearsBacklogAndAcceptsNewServerSequence()
        {
            var manager = CreateManager(null, new FakeTransport(TransportRole.Client));
            var oldConnection = new TransportConnectionId(30);
            var newConnection = new TransportConnectionId(31);
            manager.HandleConnected(NetworkTransportSide.Client, oldConnection);
            Assert.That(manager.TryHandleData(NetworkTransportSide.Client, oldConnection,
                CreateShotPresentationPacket(500, 1), out _), Is.True);
            Assert.That(manager.HandleDisconnected(NetworkTransportSide.Client, oldConnection), Is.True);
            Assert.That(manager.PendingShotPresentationCount, Is.Zero);
            Assert.That(manager.ShotResultVersion, Is.Zero);
            Assert.That(manager.LastReceivedShotResult.Shot.EntityId, Is.Zero);
            manager.HandleConnected(NetworkTransportSide.Client, newConnection);
            var packet = CreateShotPresentationPacket(1, 2);
            Assert.That(manager.TryHandleData(NetworkTransportSide.Client, oldConnection, packet, out _), Is.False);
            Assert.That(manager.TryHandleData(NetworkTransportSide.Client, newConnection, packet, out _), Is.True);
            Assert.That(manager.TryDequeueShotPresentation(out var result), Is.True);
            Assert.That(result.Shot.ShotSequence, Is.EqualTo(2));
        }

        [Test]
        public void ShotPresentation_SupportsPacketSequenceWrapAndRejectsMalformedResult()
        {
            var manager = CreateManager(null, new FakeTransport(TransportRole.Client));
            var connection = new TransportConnectionId(30);
            manager.HandleConnected(NetworkTransportSide.Client, connection);
            Assert.That(manager.TryHandleData(NetworkTransportSide.Client, connection,
                CreateShotPresentationPacket(uint.MaxValue, 1), out _), Is.True);
            Assert.That(manager.TryHandleData(NetworkTransportSide.Client, connection,
                CreateShotPresentationPacket(0, 2), out _), Is.True);
            var packet = CreateShotPresentationPacket(1, 3);
            var truncated = new ArraySegment<byte>(packet.Array, packet.Offset, packet.Count - 1);
            Assert.That(manager.TryHandleData(NetworkTransportSide.Client, connection, truncated, out _), Is.False);
            Assert.That(manager.TryHandleData(NetworkTransportSide.Client, connection,
                CreateShotPresentationPacket(uint.MaxValue, 1), out _), Is.False);
            Assert.That(manager.PendingShotPresentationCount, Is.EqualTo(2));
        }

        private static ArraySegment<byte> CreateShotPresentationPacket(uint packetSequence, uint shotSequence)
        {
            var shot = new AuthoritativeShot(1, shotSequence, 100, WeaponSimulationConfig.DefaultRifle,
                new FixVec3(Fix.Zero, Fix.One, Fix.Zero), FixVec3.Forward, 100);
            var result = new AuthoritativeShotResult(shot, 29, true, 2, Fix.FromInt(80),
                new FixVec3(Fix.Zero, Fix.One, Fix.FromInt(5)));
            var bytes = new byte[AuthoritativeShotResultCodec.PacketSize];
            Assert.That(AuthoritativeShotResultCodec.TrySerialize(result, packetSequence,
                new ArraySegment<byte>(bytes), out _), Is.True);
            return new ArraySegment<byte>(bytes);
        }

        [Test]
        public void ShotRejection_RetriesFailedSendAndOnlyRoutesToOwner()
        {
            var transport = new FakeTransport(TransportRole.Server);
            var manager = CreateManager(transport, null);
            var first = new TransportConnectionId(10);
            var second = new TransportConnectionId(20);
            manager.HandleConnected(NetworkTransportSide.Server, first);
            manager.HandleConnected(NetworkTransportSide.Server, second);
            var assignment = transport.SentPackets[0];
            SendTick(manager, first, second, 1, 100,
                new PlayerInputCommand { FirePressed = true, ShotSequence = 1 }, default);
            transport.FailNextMessageType = NetworkMessageType.ShotRejection;
            SendTick(manager, first, second, 2, 101,
                new PlayerInputCommand { FirePressed = true, ShotSequence = 2 }, default);
            Assert.That(transport.FailedReliableSendCount, Is.EqualTo(1));
            SendTick(manager, first, second, 3, 102, default, default);
            var client = CreateManager(null, new FakeTransport(TransportRole.Client));
            var connection = new TransportConnectionId(30);
            client.HandleConnected(NetworkTransportSide.Client, connection);
            Assert.That(client.TryHandleData(NetworkTransportSide.Client, connection, assignment, out _), Is.True);
            int rejectionCount = 0;
            for (int i = 0; i < transport.SentPackets.Count; i++)
            {
                var packet = transport.SentPackets[i];
                if (!ShotRejectionCodec.TryDeserialize(packet, out _, out var value))
                {
                    continue;
                }
                rejectionCount++;
                Assert.That(transport.SentConnectionIds[i], Is.EqualTo(first));
                Assert.That(value.InputTick, Is.EqualTo(2));
                Assert.That(value.ServerTick, Is.EqualTo(101));
                Assert.That(client.TryHandleData(NetworkTransportSide.Client, connection, packet, out _), Is.True);
                Assert.That(client.TryHandleData(NetworkTransportSide.Client, connection, packet, out _), Is.False);
                Assert.That(client.TryDequeueShotRejection(out var rejected), Is.True);
                Assert.That(rejected.Reason, Is.EqualTo(WeaponFireRejectReason.FireRateLimited));
                Assert.That(rejected.ShotSequence, Is.EqualTo(2));
            }
            Assert.That(rejectionCount, Is.EqualTo(1));
        }

        [Test]
        public void Loadout_SwitchPreservesPerWeaponAmmoAndRespawnResetsBoth()
        {
            var transport = new FakeTransport(TransportRole.Server);
            var manager = CreateManager(transport, null);
            var first = new TransportConnectionId(10);
            var second = new TransportConnectionId(20);
            manager.HandleConnected(NetworkTransportSide.Server, first);
            manager.HandleConnected(NetworkTransportSide.Server, second);
            manager.TryGetServerSession(first, out var session);
            SendTick(manager, first, second, 1, 100, new PlayerInputCommand { FirePressed = true, ShotSequence = 1 }, default);
            Assert.That(session.LastWeaponFireResult.RemainingAmmo, Is.EqualTo(29));
            SendTick(manager, first, second, 2, 101, new PlayerInputCommand { SwitchWeaponPressed = true }, default);
            SendTick(manager, first, second, 3, 110, new PlayerInputCommand { FirePressed = true, ShotSequence = 2 }, default);
            Assert.That(session.LastWeaponFireResult.RejectReason, Is.EqualTo(WeaponFireRejectReason.WeaponUnavailable));
            SendTick(manager, first, second, 4, 111, new PlayerInputCommand { FirePressed = true, ShotSequence = 3 }, default);
            Assert.That(session.LastWeaponFireResult.Accepted, Is.True);
            Assert.That(session.LastWeaponFireResult.Shot.FireMode, Is.EqualTo(WeaponFireMode.Projectile));
            Assert.That(session.LastWeaponFireResult.RemainingAmmo, Is.EqualTo(4));
            SendTick(manager, first, second, 5, 112, new PlayerInputCommand { SwitchWeaponPressed = true }, default);
            SendTick(manager, first, second, 6, 122, new PlayerInputCommand { FirePressed = true, ShotSequence = 4 }, default);
            Assert.That(session.LastWeaponFireResult.RemainingAmmo, Is.EqualTo(28));
            SendTick(manager, first, second, 7, 123, new PlayerInputCommand { SwitchWeaponPressed = true }, default);
            // 发射器独立冷却仍从 111 Tick 起算；切换不能把 35 Tick 冷却缩短成装备时长。
            SendTick(manager, first, second, 8, 146, new PlayerInputCommand { FirePressed = true, ShotSequence = 5 }, default);
            Assert.That(session.LastWeaponFireResult.RemainingAmmo, Is.EqualTo(3));
            session.Respawn(session.CurrentState.PositionFixed);
            SendTick(manager, first, second, 9, 147, new PlayerInputCommand { FirePressed = true, ShotSequence = 6 }, default);
            Assert.That(session.LastWeaponFireResult.Accepted, Is.True);
            Assert.That(session.LastWeaponFireResult.Shot.FireMode, Is.EqualTo(WeaponFireMode.Hitscan));
            Assert.That(session.LastWeaponFireResult.RemainingAmmo, Is.EqualTo(29));
            SendTick(manager, first, second, 10, 148, new PlayerInputCommand { SwitchWeaponPressed = true }, default);
            SendTick(manager, first, second, 11, 158, new PlayerInputCommand { FirePressed = true, ShotSequence = 7 }, default);
            Assert.That(session.LastWeaponFireResult.RemainingAmmo, Is.EqualTo(4));
        }

        [Test]
        public void Loadout_RejectsReplayedShotSequenceAcrossDifferentWeapons()
        {
            var manager = CreateManager(new FakeTransport(TransportRole.Server), null);
            var first = new TransportConnectionId(10);
            var second = new TransportConnectionId(20);
            manager.HandleConnected(NetworkTransportSide.Server, first);
            manager.HandleConnected(NetworkTransportSide.Server, second);
            manager.TryGetServerSession(first, out var session);
            SendTick(manager, first, second, 1, 100, new PlayerInputCommand { FirePressed = true, ShotSequence = 1 }, default);
            SendTick(manager, first, second, 2, 101, new PlayerInputCommand { SwitchWeaponPressed = true }, default);
            SendTick(manager, first, second, 3, 111, new PlayerInputCommand { FirePressed = true, ShotSequence = 1 }, default);
            Assert.That(session.LastWeaponFireResult.RejectReason, Is.EqualTo(WeaponFireRejectReason.DuplicateOrOldSequence));
            SendTick(manager, first, second, 4, 112, new PlayerInputCommand { FirePressed = true, ShotSequence = 2 }, default);
            Assert.That(session.LastWeaponFireResult.Accepted, Is.True);
        }

        [Test]
        public void Launcher_DirectKillAndSplashSelfDamageDoNotDoubleDamageTarget()
        {
            var transport = new FakeTransport(TransportRole.Server);
            var manager = CreateManager(transport, null);
            var first = new TransportConnectionId(10);
            var second = new TransportConnectionId(20);
            manager.HandleConnected(NetworkTransportSide.Server, first);
            manager.HandleConnected(NetworkTransportSide.Server, second);
            manager.TryGetServerEntityId(first, out uint shooter);
            manager.TryGetServerEntityId(second, out uint victim);
            SendTick(manager, first, second, 1, 100, new PlayerInputCommand { SwitchWeaponPressed = true }, default);
            SendTick(manager, first, second, 2, 110,
                new PlayerInputCommand { FirePressed = true, ShotSequence = 1, LookYaw = 90f }, default);
            for (uint tick = 3; tick <= 8; tick++)
            {
                SendTick(manager, first, second, tick, 108 + tick, default, default);
            }
            Assert.That(manager.TryGetServerDeathmatchPlayer(victim, out var target), Is.True);
            Assert.That(target.IsAlive, Is.False);
            Assert.That(target.Deaths, Is.EqualTo(1));
            Assert.That(manager.TryGetServerDeathmatchPlayer(shooter, out var owner), Is.True);
            Assert.That(owner.Kills, Is.EqualTo(1));
            Assert.That(owner.HealthFixed.Raw, Is.LessThan(Fix.FromInt(100).Raw));
            Assert.That(owner.HealthFixed.Raw, Is.GreaterThan(0));
        }

        private static NetworkMovementSessionManager CreateManager(
            IGameTransport serverTransport,
            IGameTransport clientTransport,
            uint maximumRewindTicks = 10,
            float interestRadius = 150f,
            int snapshotBudget = 32,
            int deathmatchKillLimit = 10,
            float deathmatchRespawnSeconds = 3f,
            PlayerState? spawnState = null,
            float spawnSpacing = 2.5f,
            WeaponSimulationConfig? weaponConfig = null,
            Func<ulong> clock = null,
            float deathmatchDurationSeconds = 300f)
        {
            PlayerState initialState = spawnState ?? new PlayerState { Tick = 0, IsGrounded = true };
            PlayerSimulationConfig config = new PlayerSimulationConfig(6f, 20f, 20f, 8f);
            return new NetworkMovementSessionManager(
                serverTransport, clientTransport, initialState, initialState, config,
                0.02f, 64, 3, 16, 2, 2,
                AuthoritativePlayerStateCodec.RecommendedPositionErrorThreshold,
                AuthoritativePlayerStateCodec.RecommendedVelocityErrorThreshold,
                serverSpawnSpacing: spawnSpacing,
                weaponConfig: weaponConfig,
                maximumRewindTicks: maximumRewindTicks,
                interestRadius: interestRadius,
                snapshotBudgetPerClientPerTick: snapshotBudget,
                deathmatchKillLimit: deathmatchKillLimit,
                deathmatchRespawnSeconds: deathmatchRespawnSeconds,
                deathmatchDurationSeconds: deathmatchDurationSeconds,
                monotonicMicroseconds: clock);
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

        private static ArraySegment<byte> FindPacket(FakeTransport transport, NetworkMessageType messageType, bool searchFromEnd = false)
        {
            int index = searchFromEnd ? transport.SentPackets.Count - 1 : 0;
            int end = searchFromEnd ? -1 : transport.SentPackets.Count;
            int step = searchFromEnd ? -1 : 1;
            for (; index != end; index += step)
            {
                if (PacketHeaderCodec.TryRead(transport.SentPackets[index], out PacketHeader header) && header.MessageType == messageType)
                {
                    return transport.SentPackets[index];
                }
            }

            return default;
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
            public NetworkMessageType FailNextMessageType;
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
                if (FailNextMessageType != NetworkMessageType.Unknown
                    && PacketHeaderCodec.TryRead(payload, out var header) && header.MessageType == FailNextMessageType)
                {
                    FailNextMessageType = NetworkMessageType.Unknown;
                    FailedReliableSendCount++;
                    return TransportSendResult.TransportError;
                }
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
