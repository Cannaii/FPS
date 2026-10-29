using System;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Transport;
using AFPS.Simulation.Characters;
using NUnit.Framework;
using static AFPS.Tests.EditMode.SimulationAdmissionTests;

namespace AFPS.Tests.EditMode
{
    public sealed class ClientRecoveryProtocolTests
    {
        [Test]
        public void ResumeRequestAndAtomicSeed_GateBothSidesUntilAcknowledged_AndIgnoreDuplicateSeed()
        {
            var pair = Playing();
            pair.Server.TryGetServerSession(ServerId, out var authority);
            var before = authority.CurrentState;
            Reconnect(pair);
            Assert.That(pair.ClientTransport.Sent.Exists(p => p.Type == NetworkMessageType.SessionResumeRequest), Is.True);
            Assert.That(pair.Client.ClientSession.CurrentState.RawEquals(before), Is.True);
            Assert.That(pair.Client.CanPredictClientInput, Is.False);
            Assert.That(pair.Server.IsServerRecoveryPending(pair.ActiveServerId), Is.True);
            Assert.That(pair.Client.TryPredictAndSend(new PlayerInputCommand { Tick = 1 }, out _, out _), Is.False);
            Confirm(pair);
            Assert.That(pair.Client.CanPredictClientInput, Is.True);
            Assert.That(pair.Server.IsServerRecoveryPending(pair.ActiveServerId), Is.False);
            Assert.That(pair.Server.TryGetServerSession(pair.ActiveServerId, out var same), Is.True);
            Assert.That(same, Is.SameAs(authority));
            var prediction = pair.Client.ClientSession;
            var seed = SeedPacket(pair);
            Assert.That(pair.Client.TryHandleData(NetworkTransportSide.Client, pair.ActiveClientId, seed, out _), Is.True);
            Assert.That(pair.Client.ClientSession, Is.SameAs(prediction));
            Assert.That(pair.Client.RemotePlayers.LocalEntityId, Is.EqualTo(1));
        }

        [TestCase(NetworkMessageType.SessionRecoveryAck)]
        [TestCase(NetworkMessageType.SessionRecoveryConfirmed)]
        public void FailedReceipt_RetriesWithoutReinitializingEitherSession(NetworkMessageType type)
        {
            var pair = Playing();
            Reconnect(pair);
            if (type == NetworkMessageType.SessionRecoveryAck)
            {
                pair.ClientTransport.FailType = type;
            }
            else
            {
                pair.ServerTransport.FailType = type;
            }
            Confirm(pair);
            Assert.That(pair.Client.CanPredictClientInput, Is.False);
            var original = pair.Client.ClientSession;
            RunningMatchFixture.Advance(pair.Client, 126);
            FlushOwnPackets(pair);
            Assert.That(pair.Client.CanPredictClientInput, Is.True);
            Assert.That(pair.Client.ClientSession, Is.SameAs(original));
            Assert.That(pair.Server.ServerSessionCount, Is.EqualTo(2));
        }

        [Test]
        public void FailedAtomicSeed_IsRetriedBeforeAnyClientInputCanRun()
        {
            var pair = Playing();
            pair.ServerTransport.FailType = NetworkMessageType.SessionRecoverySeed;
            Reconnect(pair);
            Assert.That(pair.Client.CanPredictClientInput, Is.False);
            Confirm(pair);
            Assert.That(pair.Client.CanPredictClientInput, Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void WrongAckAttemptEpochOrEntity_DoesNotOpenServerGate(int mutation)
        {
            var pair = Playing();
            Reconnect(pair);
            SessionRecoveryCodec.TryReadSeed(SeedPacket(pair), out uint epoch, out var seed);
            byte[] ack = new byte[SessionRecoveryCodec.ReceiptPacketSize];
            SessionRecoveryCodec.TryWriteReceipt(NetworkMessageType.SessionRecoveryAck,
                seed.Attempt + (mutation == 0 ? 1u : 0), epoch + (mutation == 1 ? 1u : 0),
                seed.EntityId + (mutation == 2 ? 1u : 0), new ArraySegment<byte>(ack), out _);
            Assert.That(pair.Server.TryHandleData(NetworkTransportSide.Server, pair.ActiveServerId,
                new ArraySegment<byte>(ack), out _), Is.False);
            Assert.That(pair.Server.IsServerRecoveryPending(pair.ActiveServerId), Is.True);
        }

        [Test]
        public void StateChangeWhileAwaitingAck_InvalidatesOldEpochAndRebasesOnNewSeed()
        {
            var pair = Playing();
            Reconnect(pair);
            SessionRecoveryCodec.TryReadSeed(SeedPacket(pair), out uint epoch, out var seed);
            pair.Server.TryGetServerSession(pair.ActiveServerId, out var authority);
            authority.Respawn(authority.CurrentState.PositionFixed + AFPS.Math.FixVec3.Right);
            byte[] ack = new byte[SessionRecoveryCodec.ReceiptPacketSize];
            SessionRecoveryCodec.TryWriteReceipt(NetworkMessageType.SessionRecoveryAck,
                seed.Attempt, epoch, seed.EntityId, new ArraySegment<byte>(ack), out _);
            Assert.That(pair.Server.TryHandleData(NetworkTransportSide.Server, pair.ActiveServerId,
                new ArraySegment<byte>(ack), out _), Is.False);
            FlushOwnPackets(pair);
            SessionRecoveryCodec.TryReadSeed(SeedPacket(pair), out uint refreshed, out _);
            Assert.That(SequenceMath.IsNewer(refreshed, epoch), Is.True);
            Assert.That(pair.Client.ClientSession.CurrentState.RawEquals(authority.CurrentState), Is.True);
            Confirm(pair);
            Assert.That(pair.Client.CanPredictClientInput, Is.True);
        }

        [Test]
        public void UnacknowledgedInputs_ReplayAtMostFourPerTickAndContinueShotSequence()
        {
            var pair = Playing();
            PredictUnsent(pair, 8, 17);
            Reconnect(pair);
            Assert.That(pair.Client.PendingRecoveryInputCount, Is.EqualTo(8));
            Assert.That(pair.Client.ClientShotSequenceFloor, Is.EqualTo(17));
            Confirm(pair);
            Assert.That(pair.Client.CanPredictClientInput, Is.False);
            RunningMatchFixture.Advance(pair.Client, 102);
            Assert.That(pair.Client.ClientSession.CurrentState.Tick, Is.EqualTo(4));
            Assert.That(pair.Client.PendingRecoveryInputCount, Is.EqualTo(4));
            FlushOwnPackets(pair);
            RunningMatchFixture.Advance(pair.Client, 103);
            Assert.That(pair.Client.ClientSession.CurrentState.Tick, Is.EqualTo(8));
            Assert.That(pair.Client.PendingRecoveryInputCount, Is.Zero);
            Assert.That(pair.Client.CanPredictClientInput, Is.True);
        }

        [TestCase(20)]
        [TestCase(40)]
        public void MissingHistoryOrServerWindowOverflow_HardRebasesWithoutInventingInputs(int count)
        {
            var pair = Playing();
            PredictUnsent(pair, count);
            Reconnect(pair);
            Assert.That(pair.Client.PendingRecoveryInputCount, Is.Zero);
            Assert.That(pair.Client.ClientSession.CurrentState.Tick, Is.Zero);
            Confirm(pair);
            Assert.That(pair.Client.CanPredictClientInput, Is.True);
        }

        [Test]
        public void FinishedMatch_DiscardsOldInputsBeforeAllowingNormalNeutralInput()
        {
            var pair = Playing(0.1f);
            PredictUnsent(pair, 8);
            pair.Server.HandleDisconnected(NetworkTransportSide.Server, ServerId);
            pair.Client.HandleDisconnected(NetworkTransportSide.Client, ClientId);
            RunningMatchFixture.Advance(pair.Server, 105);
            ConnectReplacement(pair);
            FlushOwnPackets(pair);
            RunningMatchFixture.Advance(pair.Server, 106);
            FlushOwnPackets(pair);
            RunningMatchFixture.Advance(pair.Client, 106);
            FlushOwnPackets(pair);
            Assert.That(pair.Client.PendingRecoveryInputCount, Is.Zero);
            Assert.That(pair.Client.CanPredictClientInput, Is.True);
            Assert.That(pair.Client.ClientDeathmatchState.Phase, Is.EqualTo(AFPS.NetCode.Match.DeathmatchPhase.Finished));
        }

        private static Pair Playing(float duration = 300f)
        {
            var pair = new Pair(durationSeconds: duration);
            pair.Connect();
            FlushOwnPackets(pair);
            var peer = new TransportConnectionId(50);
            pair.Server.HandleConnected(NetworkTransportSide.Server, peer);
            pair.Server.TryHandleData(NetworkTransportSide.Server, peer,
                Handshake(NetworkMessageType.SimulationHello, 1, pair.Server.WorldContext.Identity), out _);
            pair.Server.TryHandleData(NetworkTransportSide.Server, peer,
                Handshake(NetworkMessageType.SimulationReady, 1, pair.Server.WorldContext.Identity), out _);
            // 第二个服务器测试连接没有客户端，不能把其分配包路由给本地玩家。
            pair.ServerTransport.Pending.Clear();
            RunningMatchFixture.Advance(pair.Server, 100);
            FlushOwnPackets(pair);
            return pair;
        }

        [Test]
        public void DeathWithUnchangedRawState_StillInvalidatesAcknowledgementEpoch()
        {
            var pair = Playing();
            Reconnect(pair);
            SessionRecoveryCodec.TryReadSeed(SeedPacket(pair), out uint epoch, out var seed);
            pair.Server.TryGetServerSession(pair.ActiveServerId, out var authority);
            for (uint shot = 1; shot <= 5; shot++)
            {
                Assert.That(pair.Server.TryHandleData(NetworkTransportSide.Server, new TransportConnectionId(50),
                    InputPacket(new PlayerInputCommand
                    {
                        Tick = shot,
                        LookYaw = -90f,
                        FirePressed = true,
                        ShotSequence = shot
                    }), out _), Is.True);
                RunningMatchFixture.Advance(pair.Server, 100 + shot * 5);
            }
            Assert.That(authority.CurrentState.RawEquals(seed.Movement.State), Is.True);
            Assert.That(pair.Server.TryGetServerDeathmatchPlayer(seed.EntityId, out var player), Is.True);
            Assert.That(player.Deaths, Is.EqualTo(1));
            byte[] bytes = new byte[SessionRecoveryCodec.ReceiptPacketSize];
            SessionRecoveryCodec.TryWriteReceipt(NetworkMessageType.SessionRecoveryAck,
                seed.Attempt, epoch, seed.EntityId, new ArraySegment<byte>(bytes), out _);
            Assert.That(pair.Server.TryHandleData(NetworkTransportSide.Server, pair.ActiveServerId,
                new ArraySegment<byte>(bytes), out _), Is.False);
            Assert.That(pair.Server.IsServerRecoveryPending(pair.ActiveServerId), Is.True);
        }

        [Test]
        public void MatchFinishingWithUnchangedRawState_InvalidatesOldAck()
        {
            var pair = Playing(0.1f);
            Reconnect(pair);
            SessionRecoveryCodec.TryReadSeed(SeedPacket(pair), out uint epoch, out var seed);
            RunningMatchFixture.Advance(pair.Server, 105);
            byte[] bytes = new byte[SessionRecoveryCodec.ReceiptPacketSize];
            SessionRecoveryCodec.TryWriteReceipt(NetworkMessageType.SessionRecoveryAck,
                seed.Attempt, epoch, seed.EntityId, new ArraySegment<byte>(bytes), out _);
            Assert.That(pair.Server.TryHandleData(NetworkTransportSide.Server, pair.ActiveServerId,
                new ArraySegment<byte>(bytes), out _), Is.False);
            FlushOwnPackets(pair);
            SessionRecoveryCodec.TryReadSeed(SeedPacket(pair), out uint refreshed, out _);
            Assert.That(SequenceMath.IsNewer(refreshed, epoch), Is.True);
        }

        private static void PredictUnsent(Pair pair, int count, uint shotSequence = 0)
        {
            for (uint tick = 1; tick <= count; tick++)
            {
                Assert.That(pair.Client.TryPredictAndSend(new PlayerInputCommand
                {
                    Tick = tick, MoveY = 1f, FirePressed = tick == 1 && shotSequence != 0,
                    ShotSequence = tick == 1 ? shotSequence : 0
                }, out _, out _), Is.True);
            }
            pair.ClientTransport.Pending.Clear();
        }

        private static void Reconnect(Pair pair)
        {
            pair.Server.HandleDisconnected(NetworkTransportSide.Server, pair.ActiveServerId);
            pair.Client.HandleDisconnected(NetworkTransportSide.Client, pair.ActiveClientId);
            ConnectReplacement(pair);
            FlushOwnPackets(pair);
        }

        private static void ConnectReplacement(Pair pair)
        {
            pair.ServerTransport.Pending.Clear();
            pair.ClientTransport.Pending.Clear();
            pair.ActiveServerId = new TransportConnectionId(30);
            pair.ActiveClientId = new TransportConnectionId(40);
            Assert.That(pair.Server.HandleConnected(NetworkTransportSide.Server, pair.ActiveServerId), Is.True);
            Assert.That(pair.Client.HandleConnected(NetworkTransportSide.Client, pair.ActiveClientId), Is.True);
        }

        private static void Confirm(Pair pair)
        {
            RunningMatchFixture.Advance(pair.Server, 101);
            FlushOwnPackets(pair);
            RunningMatchFixture.Advance(pair.Client, 101);
            FlushOwnPackets(pair);
        }

        private static void FlushOwnPackets(Pair pair)
        {
            for (int i = 0; i < 32 && (pair.ServerTransport.Pending.Count != 0 || pair.ClientTransport.Pending.Count != 0); i++)
            {
                while (pair.ClientTransport.Pending.Count != 0)
                {
                    pair.RouteClient();
                }
                while (pair.ServerTransport.Pending.Count != 0)
                {
                    var packet = pair.ServerTransport.Pending.Dequeue();
                    if (packet.Connection.Equals(pair.ActiveServerId))
                    {
                        pair.Client.TryHandleData(NetworkTransportSide.Client, pair.ActiveClientId, packet.Data, out _);
                    }
                }
            }
        }

        private static ArraySegment<byte> SeedPacket(Pair pair)
        {
            return pair.ServerTransport.Sent.FindLast(packet => packet.Type == NetworkMessageType.SessionRecoverySeed
                && packet.Connection.Equals(pair.ActiveServerId)).Data;
        }
    }
}
