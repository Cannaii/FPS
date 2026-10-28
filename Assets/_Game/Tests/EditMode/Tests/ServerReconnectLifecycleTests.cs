using System;
using AFPS.Math;
using AFPS.NetCode.Match;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Sessions;
using AFPS.NetCode.Transport;
using AFPS.Simulation.Characters;
using AFPS.Simulation.World;
using NUnit.Framework;
using static AFPS.Tests.EditMode.SimulationAdmissionTests;

namespace AFPS.Tests.EditMode
{
    public sealed class ServerReconnectLifecycleTests
    {
        private static readonly TransportConnectionId Replacement = new TransportConnectionId(30);

        [Test]
        public void FailedCredentialGrant_RetriesWithoutChangingEntityOrCredential()
        {
            var pair = new Pair();
            pair.ServerTransport.FailType = NetworkMessageType.SessionResumeGrant;
            pair.Connect();
            pair.Flush();
            Assert.That(pair.Client.RemotePlayers.LocalEntityId, Is.EqualTo(1));
            Assert.That(pair.Client.ClientResumeCredential.IsValid, Is.False);
            pair.Client.AdvanceServerSessions(2);
            pair.Flush();
            Assert.That(pair.Client.ClientResumeCredential.IsValid, Is.True);
            var original = pair.Client.ClientResumeCredential;
            pair.Server.TryHandleData(NetworkTransportSide.Server, ServerId,
                Handshake(NetworkMessageType.SimulationReady, 1, pair.Server.WorldContext.Identity), out _);
            pair.Flush();
            Assert.That(pair.Client.ClientResumeCredential.Equals(original), Is.True);
            Assert.That(pair.Server.ServerSessionCount, Is.EqualTo(1));
        }

        [Test]
        public void Disconnect_FreezesRawStateKeepsVisibleTargetAndExpiresAtExclusiveDeadline()
        {
            var pair = Connected();
            pair.Server.TryGetServerSession(ServerId, out var original);
            var state = original.CurrentState;
            pair.Server.AdvanceServerSessions(100);
            state = original.CurrentState;
            Assert.That(pair.Server.HandleDisconnected(NetworkTransportSide.Server, ServerId), Is.True);
            Assert.That(pair.Server.HandleDisconnected(NetworkTransportSide.Server, ServerId), Is.False);
            Assert.That(pair.Server.ServerSessionCount, Is.Zero);
            Assert.That(pair.Server.RetainedServerSessionCount, Is.EqualTo(1));
            Assert.That(original.IsRetainedOffline, Is.True);
            pair.Server.AdvanceServerSessions(1599);
            Assert.That(original.CurrentState.RawEquals(state), Is.True);
            Assert.That(pair.Server.TryGetServerHealth(1, out float health), Is.True);
            Assert.That(health, Is.EqualTo(100));
            pair.Server.AdvanceServerSessions(1600);
            Assert.That(pair.Server.RetainedServerSessionCount, Is.Zero);
            Assert.That(pair.Server.TryGetServerHealth(1, out _), Is.False);
            Assert.That(pair.Server.ServerDeathmatchPhase, Is.EqualTo(DeathmatchPhase.WaitingForPlayers));
            Assert.That(Resume(pair, Replacement, pair.Client.ClientResumeCredential), Is.False);
        }

        [Test]
        public void Resume_ReusesSessionRawStateAndEntity_RejectsOldConnectionAndAllInputUntilClientAck()
        {
            var pair = Connected();
            pair.Server.TryGetServerSession(ServerId, out var original);
            pair.Server.TryHandleData(NetworkTransportSide.Server, ServerId,
                InputPacket(new PlayerInputCommand { Tick = 1, MoveY = 1f }), out _);
            pair.Server.AdvanceServerSessions(100);
            var before = original.CurrentState;
            pair.Server.HandleDisconnected(NetworkTransportSide.Server, ServerId);
            Assert.That(Resume(pair, Replacement, pair.Client.ClientResumeCredential), Is.True);
            Assert.That(pair.Server.TryGetServerSession(Replacement, out var restored), Is.True);
            Assert.That(restored, Is.SameAs(original));
            Assert.That(restored.CurrentState.RawEquals(before), Is.True);
            Assert.That(pair.Server.RetainedServerSessionCount, Is.Zero);
            Assert.That(pair.Server.IsServerRecoveryPending(Replacement), Is.True);
            var input = InputPacket(new PlayerInputCommand { Tick = 2, MoveY = 1f });
            Assert.That(pair.Server.TryHandleData(NetworkTransportSide.Server, ServerId, input, out _), Is.False);
            Assert.That(pair.Server.TryHandleData(NetworkTransportSide.Server, Replacement, input, out _), Is.False);
            pair.Server.AdvanceServerSessions(101);
            Assert.That(restored.CurrentState.RawEquals(before), Is.True);
            foreach (var packet in pair.ServerTransport.Sent.FindAll(p => p.Connection.Equals(Replacement)))
            {
                if (packet.Type == NetworkMessageType.AuthoritativePlayerState
                    || packet.Type == NetworkMessageType.AuthoritativeWeaponState)
                {
                    Assert.That(packet.Delivery, Is.EqualTo(TransportDelivery.ReliableSequenced));
                }
            }
            Assert.That(pair.ServerTransport.Sent.Exists(p => p.Connection.Equals(Replacement)
                && p.Type == NetworkMessageType.AuthoritativePlayerState), Is.True);
            Assert.That(pair.ServerTransport.Sent.Exists(p => p.Connection.Equals(Replacement)
                && p.Type == NetworkMessageType.AuthoritativeWeaponState), Is.True);
            Admit(pair, new TransportConnectionId(40), 3);
            Assert.That(pair.Server.TryGetServerEntityId(new TransportConnectionId(40), out uint next), Is.True);
            Assert.That(next, Is.EqualTo(2));
        }

        [TestCase(NetworkMessageType.PlayerSessionAssignment)]
        [TestCase(NetworkMessageType.AuthoritativePlayerState)]
        [TestCase(NetworkMessageType.AuthoritativeWeaponState)]
        public void RecoverySeedQueueFailure_RetriesReliablyWithoutConsumingInput(NetworkMessageType failedType)
        {
            var pair = Connected();
            pair.Server.TryGetServerSession(ServerId, out var original);
            pair.Server.HandleDisconnected(NetworkTransportSide.Server, ServerId);
            pair.ServerTransport.FailType = failedType;
            Assert.That(Resume(pair, Replacement, pair.Client.ClientResumeCredential), Is.True);
            Assert.That(original.RecoverySnapshotPending, Is.True);
            var before = original.CurrentState;
            pair.Server.AdvanceServerSessions(1);
            Assert.That(original.RecoverySnapshotPending, Is.False);
            Assert.That(original.CurrentState.RawEquals(before), Is.True);
            Assert.That(pair.Server.IsServerRecoveryPending(Replacement), Is.True);
        }

        [Test]
        public void RepeatedReady_IsIdempotentAndDoesNotExtendRecoveryWait()
        {
            var pair = Connected();
            pair.Server.HandleDisconnected(NetworkTransportSide.Server, ServerId);
            Assert.That(Resume(pair, Replacement, pair.Client.ClientResumeCredential), Is.True);
            pair.Server.TryGetServerSession(Replacement, out var original);
            pair.Server.AdvanceServerSessions(249);
            Assert.That(Ready(pair, Replacement, 2), Is.True);
            Assert.That(pair.Server.TryGetServerSession(Replacement, out var same), Is.True);
            Assert.That(same, Is.SameAs(original));
            pair.Server.AdvanceServerSessions(250);
            Assert.That(pair.Server.ServerSessionCount, Is.Zero);
            Assert.That(pair.Server.RetainedServerSessionCount, Is.EqualTo(1));
            Assert.That(pair.ServerTransport.Disconnected, Does.Contain(Replacement));
            Assert.That(pair.Server.HandleDisconnected(NetworkTransportSide.Server, Replacement), Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Resume_WorldMismatchCannotConsumeLease(int part)
        {
            var pair = Connected();
            pair.Server.HandleDisconnected(NetworkTransportSide.Server, ServerId);
            var identity = pair.Server.WorldContext.Identity;
            var wrong = new SimulationWorldIdentity(identity.AlgorithmVersion + (part == 0 ? 1u : 0),
                identity.WorldHash + (part == 1 ? 1UL : 0), identity.ConfigurationHash + (part == 2 ? 1UL : 0));
            pair.Server.HandleConnected(NetworkTransportSide.Server, Replacement);
            Assert.That(Request(pair, Replacement, pair.Client.ClientResumeCredential, 2, wrong), Is.True);
            Assert.That(pair.Server.TryHandleData(NetworkTransportSide.Server, Replacement,
                Handshake(NetworkMessageType.SimulationReady, 2, wrong), out _), Is.False);
            Assert.That(pair.Server.RetainedServerSessionCount, Is.EqualTo(1));
            Assert.That(pair.Server.ServerSessionCount, Is.Zero);
            Assert.That(Resume(pair, new TransportConnectionId(31), pair.Client.ClientResumeCredential), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnknownOrOnlineCredential_CannotTakeOverOrAllocateFallbackPlayer(bool online)
        {
            var pair = Connected();
            var credential = online ? pair.Client.ClientResumeCredential : SessionResumeCredential.Create();
            Assert.That(Resume(pair, Replacement, credential), Is.False);
            Assert.That(pair.Server.ServerSessionCount, Is.EqualTo(1));
            Assert.That(pair.Server.TryGetServerEntityId(ServerId, out uint entity), Is.True);
            Assert.That(entity, Is.EqualTo(1));
            Assert.That(pair.ServerTransport.Disconnected, Does.Contain(Replacement));
            Assert.That(pair.Server.TryGetServerEntityId(Replacement, out _), Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void FirstHelloLocksAttemptCredentialAndAdmissionKind(int mutation)
        {
            var pair = Connected();
            pair.Server.HandleDisconnected(NetworkTransportSide.Server, ServerId);
            pair.Server.HandleConnected(NetworkTransportSide.Server, Replacement);
            Assert.That(Request(pair, Replacement, pair.Client.ClientResumeCredential, 2,
                pair.Server.WorldContext.Identity), Is.True);
            bool changed = mutation == 0
                ? Request(pair, Replacement, pair.Client.ClientResumeCredential, 3, pair.Server.WorldContext.Identity)
                : mutation == 1
                    ? Request(pair, Replacement, SessionResumeCredential.Create(), 2, pair.Server.WorldContext.Identity)
                    : pair.Server.TryHandleData(NetworkTransportSide.Server, Replacement,
                        Handshake(NetworkMessageType.SimulationHello, 2, pair.Server.WorldContext.Identity), out _);
            Assert.That(changed, Is.False);
            Assert.That(Ready(pair, Replacement, 2), Is.True);
            Assert.That(pair.Server.TryGetServerEntityId(Replacement, out uint entity), Is.True);
            Assert.That(entity, Is.EqualTo(1));
        }

        [Test]
        public void OfflinePlayer_RemainsVisibleTakesDamageAndRespawnsOnOriginalWorldDeadline()
        {
            var pair = Connected();
            var shooter = new TransportConnectionId(40);
            Admit(pair, shooter, 3);
            pair.Server.AdvanceServerSessions(100);
            pair.Server.HandleDisconnected(NetworkTransportSide.Server, ServerId);
            for (uint shot = 1; shot <= 5; shot++)
            {
                Assert.That(pair.Server.TryHandleData(NetworkTransportSide.Server, shooter, InputPacket(new PlayerInputCommand
                {
                    Tick = shot, LookYaw = -90f, FirePressed = true, ShotSequence = shot
                }), out _), Is.True);
                pair.Server.AdvanceServerSessions(100 + shot * 5);
            }
            Assert.That(pair.Server.TryGetServerDeathmatchPlayer(1, out var victim), Is.True);
            Assert.That(victim.Health, Is.Zero);
            Assert.That(victim.Deaths, Is.EqualTo(1));
            Assert.That(pair.Server.TryGetServerDeathmatchPlayer(2, out var attacker), Is.True);
            Assert.That(attacker.Kills, Is.EqualTo(1));
            Assert.That(pair.ServerTransport.Sent.Exists(p => p.Connection.Equals(shooter)
                && p.Type == NetworkMessageType.RemotePlayerSnapshot), Is.True);
            pair.Server.AdvanceServerSessions(274);
            Assert.That(pair.Server.TryGetServerHealth(1, out float health), Is.True);
            Assert.That(health, Is.Zero);
            pair.Server.AdvanceServerSessions(275);
            Assert.That(pair.Server.TryGetServerHealth(1, out health), Is.True);
            Assert.That(health, Is.EqualTo(100));
            Assert.That(Resume(pair, Replacement, pair.Client.ClientResumeCredential), Is.True);
            Assert.That(pair.Server.TryGetServerDeathmatchPlayer(1, out victim), Is.True);
            Assert.That(victim.Deaths, Is.EqualTo(1));
            Assert.That(victim.Health, Is.EqualTo(100));
        }

        [Test]
        public void AllDisconnected_PreservesPlayingMatchUntilLastLeaseExpires()
        {
            var pair = Connected();
            var second = new TransportConnectionId(40);
            Admit(pair, second, 3);
            pair.Server.AdvanceServerSessions(100);
            pair.Server.HandleDisconnected(NetworkTransportSide.Server, ServerId);
            pair.Server.HandleDisconnected(NetworkTransportSide.Server, second);
            Assert.That(pair.Server.ServerDeathmatchPhase, Is.EqualTo(DeathmatchPhase.Playing));
            pair.Server.AdvanceServerSessions(1599);
            Assert.That(pair.Server.ServerDeathmatchPhase, Is.EqualTo(DeathmatchPhase.Playing));
            pair.Server.AdvanceServerSessions(1600);
            Assert.That(pair.Server.ServerDeathmatchPhase, Is.EqualTo(DeathmatchPhase.WaitingForPlayers));
            Assert.That(pair.Server.RetainedServerSessionCount, Is.Zero);
        }

        [Test]
        public void RetentionDeadline_SupportsWorldTickWrap()
        {
            var pair = Connected();
            pair.Server.AdvanceServerSessions(uint.MaxValue - 5);
            pair.Server.HandleDisconnected(NetworkTransportSide.Server, ServerId);
            pair.Server.AdvanceServerSessions(1493);
            Assert.That(pair.Server.RetainedServerSessionCount, Is.EqualTo(1));
            pair.Server.AdvanceServerSessions(1494);
            Assert.That(pair.Server.RetainedServerSessionCount, Is.Zero);
        }

        private static Pair Connected()
        {
            var pair = new Pair();
            pair.Connect();
            pair.Flush();
            Assert.That(pair.Client.ClientResumeCredential.IsValid, Is.True);
            return pair;
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void StaleGrant_CannotReplaceCurrentClientCredential(int mutation)
        {
            var pair = Connected();
            var original = pair.Client.ClientResumeCredential;
            var source = mutation == 0 ? Replacement : ClientId;
            uint attempt = mutation == 1 ? 2u : 1u;
            uint entityId = mutation == 2 ? 2u : 1u;
            if (mutation == 3)
            {
                pair.Client.HandleDisconnected(NetworkTransportSide.Client, ClientId);
            }
            byte[] bytes = new byte[SessionResumeCodec.GrantPacketSize];
            Assert.That(SessionResumeCodec.TryWriteGrant(entityId, SessionResumeCredential.Create(), attempt,
                new ArraySegment<byte>(bytes), out _), Is.True);
            Assert.That(pair.Client.TryHandleData(NetworkTransportSide.Client, source,
                new ArraySegment<byte>(bytes), out _), Is.False);
            Assert.That(pair.Client.ClientResumeCredential.Equals(original), Is.True);
        }

        [Test]
        public void FullRetainedRoster_BlocksNewPlayerButAllowsAuthenticatedResume()
        {
            var pair = Connected();
            for (uint entity = 2; entity <= 64; entity++)
            {
                Admit(pair, new TransportConnectionId(100 + entity), entity);
            }
            pair.Server.HandleDisconnected(NetworkTransportSide.Server, ServerId);
            for (uint entity = 2; entity <= 64; entity++)
            {
                pair.Server.HandleDisconnected(NetworkTransportSide.Server, new TransportConnectionId(100 + entity));
            }
            Assert.That(pair.Server.RetainedServerSessionCount, Is.EqualTo(64));
            Assert.That(pair.Server.HandleConnected(NetworkTransportSide.Server, Replacement), Is.True);
            Assert.That(pair.Server.TryHandleData(NetworkTransportSide.Server, Replacement,
                Handshake(NetworkMessageType.SimulationHello, 65, pair.Server.WorldContext.Identity), out _), Is.True);
            Assert.That(Ready(pair, Replacement, 65), Is.False);
            Assert.That(pair.Server.ServerSessionCount, Is.Zero);
            Assert.That(pair.Server.RetainedServerSessionCount, Is.EqualTo(64));
            Assert.That(Resume(pair, new TransportConnectionId(31), pair.Client.ClientResumeCredential), Is.True);
            Assert.That(pair.Server.RetainedServerSessionCount, Is.EqualTo(63));
        }

        [Test]
        public void EmptyConnections_DoNotPauseMatchClockOrRestartFinishedMatchOnResume()
        {
            var pair = new Pair(durationSeconds: 0.1f);
            pair.Connect();
            pair.Flush();
            var second = new TransportConnectionId(40);
            Admit(pair, second, 3);
            pair.Server.AdvanceServerSessions(100);
            pair.Server.HandleDisconnected(NetworkTransportSide.Server, ServerId);
            pair.Server.HandleDisconnected(NetworkTransportSide.Server, second);
            pair.Server.AdvanceServerSessions(105);
            Assert.That(pair.Server.ServerDeathmatchPhase, Is.EqualTo(DeathmatchPhase.Finished));
            Assert.That(Resume(pair, Replacement, pair.Client.ClientResumeCredential), Is.True);
            Assert.That(pair.Server.ServerDeathmatchPhase, Is.EqualTo(DeathmatchPhase.Finished));
        }

        [Test]
        public void FailedExpiryDespawn_IsRetriedThroughVisibilityDifference()
        {
            var pair = Connected();
            var observer = new TransportConnectionId(40);
            Admit(pair, observer, 3);
            pair.Server.AdvanceServerSessions(100);
            pair.Server.HandleDisconnected(NetworkTransportSide.Server, ServerId);
            pair.ServerTransport.Sent.Clear();
            pair.ServerTransport.FailType = NetworkMessageType.PlayerDespawn;
            pair.Server.AdvanceServerSessions(1600);
            Assert.That(pair.Server.TryGetServerHealth(1, out _), Is.False);
            Assert.That(pair.ServerTransport.Sent.Exists(p => p.Connection.Equals(observer)
                && p.Type == NetworkMessageType.PlayerDespawn), Is.True);
        }

        private static void Admit(Pair pair, TransportConnectionId id, uint attempt)
        {
            Assert.That(pair.Server.HandleConnected(NetworkTransportSide.Server, id), Is.True);
            Assert.That(pair.Server.TryHandleData(NetworkTransportSide.Server, id,
                Handshake(NetworkMessageType.SimulationHello, attempt, pair.Server.WorldContext.Identity), out _), Is.True);
            Assert.That(Ready(pair, id, attempt), Is.True);
        }

        private static bool Resume(Pair pair, TransportConnectionId id, SessionResumeCredential credential)
        {
            Assert.That(pair.Server.HandleConnected(NetworkTransportSide.Server, id), Is.True);
            Assert.That(Request(pair, id, credential, 2, pair.Server.WorldContext.Identity), Is.True);
            return Ready(pair, id, 2);
        }

        private static bool Ready(Pair pair, TransportConnectionId id, uint attempt)
        {
            return pair.Server.TryHandleData(NetworkTransportSide.Server, id,
                Handshake(NetworkMessageType.SimulationReady, attempt, pair.Server.WorldContext.Identity), out _);
        }

        private static bool Request(Pair pair, TransportConnectionId id,
            SessionResumeCredential credential, uint attempt, SimulationWorldIdentity identity)
        {
            byte[] bytes = new byte[SessionResumeCodec.RequestPacketSize];
            Assert.That(SessionResumeCodec.TryWriteRequest(identity, credential, attempt,
                new ArraySegment<byte>(bytes), out int count), Is.True);
            return pair.Server.TryHandleData(NetworkTransportSide.Server, id,
                new ArraySegment<byte>(bytes, 0, count), out _);
        }
    }
}
