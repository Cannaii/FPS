using System;
using System.Collections.Generic;
using AFPS.Math;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Sessions;
using AFPS.NetCode.Transport;
using AFPS.NetCode.Weapons;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;
using AFPS.Simulation.Weapons;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class ServerSessionRecoveryTests
    {
        [Test]
        public void Rebind_PreservesAllPlayerFieldsAndReliablySeedsOnlyNewConnection()
        {
            var old = new RecordingTransport();
            var replacement = new RecordingTransport();
            var initial = Initial();
            initial.Tick = 17;
            initial.PositionFixed = new FixVec3(Fix.FromInt(4), Fix.Zero, Fix.FromInt(3));
            initial.VelocityFixed = FixVec3.Right * Fix.FromInt(2);
            initial.IsCrouched = true;
            initial.StaminaSpentFixed = Fix.FromRatio(2, 5);
            initial.DashSpentCharges = 1;
            initial.DashRechargeRemainingFixed = Fix.Half;
            var session = Create(old, initial: initial);
            Assert.That(session.TryRebindConnection(replacement, new TransportConnectionId(9)), Is.True);
            Assert.That(session.CurrentState.RawEquals(initial), Is.True);
            Assert.That(session.NextExpectedInputTick, Is.EqualTo(18));
            Assert.That(session.TrySendRecoverySnapshot(100, out var sent), Is.True);
            Assert.That(sent.PacketSequence, Is.EqualTo(1));
            Assert.That(old.Packets, Is.Empty);
            Assert.That(replacement.Packets.Count, Is.EqualTo(2));
            foreach (var packet in replacement.Packets)
            {
                Assert.That(packet.Connection.Value, Is.EqualTo(9));
                Assert.That(packet.Delivery, Is.EqualTo(TransportDelivery.ReliableSequenced));
            }
            var owner = Owner(replacement);
            Assert.That(owner.State.RawEquals(initial), Is.True);
            Assert.That(owner.ServerTick, Is.EqualTo(100));
            Assert.That(owner.LastProcessedInputTick, Is.EqualTo(17));
            Assert.That(session.TrySendRecoverySnapshot(100, out var rejected), Is.False);
            Assert.That(rejected.Succeeded, Is.False);
        }

        [Test]
        public void OldQueuedInputAndHeldMovement_AreNotAppliedAfterRecovery()
        {
            var old = new RecordingTransport();
            var session = Create(old);
            Feed(session, new PlayerInputCommand { Tick = 1, MoveY = 1f }, 100);
            Assert.That(session.TryReceiveInputPacket(Packet(new PlayerInputCommand
            {
                Tick = 2, FirePressed = true, ShotSequence = 1, ShotServerTick = 101, MoveY = 1f
            }), out _), Is.True);
            var retained = session.CurrentState;
            var replacement = new RecordingTransport();
            Assert.That(session.TryRebindConnection(replacement, new TransportConnectionId(9)), Is.True);
            Assert.That(session.TryReceiveInputPacket(Packet(new PlayerInputCommand { Tick = 2 }), out _), Is.False);
            Assert.That(session.TryAdvance(101, out _, out _), Is.False);
            Assert.That(session.CurrentState.RawEquals(retained), Is.True);
            Assert.That(session.TrySendRecoverySnapshot(101, out _), Is.True);
            Assert.That(session.TryAdvance(102, out _, out _), Is.True);
            Assert.That(session.LastAppliedInput.MoveYFixed, Is.EqualTo(Fix.Zero));
            Assert.That(session.LastAppliedInput.FirePressed, Is.False);
            Assert.That(Weapon(replacement).Ammo, Is.EqualTo(30));
        }

        [Test]
        public void Reconnect_DoesNotResetFireCadenceOrGlobalShotSequence()
        {
            var old = new RecordingTransport();
            var session = Create(old);
            Fire(session, 1, 1, 100);
            var replacement = new RecordingTransport();
            session.TryRebindConnection(replacement, new TransportConnectionId(9));
            session.TrySendRecoverySnapshot(101, out _);
            Fire(session, 2, 2, 102);
            Assert.That(session.LastWeaponFireResult.RejectReason, Is.EqualTo(WeaponFireRejectReason.FireRateLimited));
            Assert.That(session.LastObservedShotSequence, Is.EqualTo(2));
            Fire(session, 3, 2, 110);
            Assert.That(session.LastWeaponFireResult.RejectReason, Is.EqualTo(WeaponFireRejectReason.DuplicateOrOldSequence));
            Fire(session, 4, 3, 111);
            Assert.That(session.LastWeaponFireResult.Accepted, Is.True);
            Assert.That(Weapon(replacement).Ammo, Is.EqualTo(28));
            foreach (var packet in replacement.Packets)
            {
                Assert.That(packet.Connection.Value, Is.EqualTo(9));
            }
        }

        [Test]
        public void ReloadContinuesOnWorldClock_InsteadOfRestartingOrRefillingOnRebind()
        {
            var session = Create(new RecordingTransport());
            Fire(session, 1, 1, 100);
            Feed(session, new PlayerInputCommand { Tick = 2, ReloadPressed = true }, 101);
            var replacement = new RecordingTransport();
            session.TryRebindConnection(replacement, new TransportConnectionId(9));
            Assert.That(session.TrySendRecoverySnapshot(110, out _), Is.True);
            Assert.That(Weapon(replacement).Ammo, Is.EqualTo(29));
            Assert.That(Weapon(replacement).ReloadTicksRemaining, Is.EqualTo(91));
            Feed(session, new PlayerInputCommand { Tick = 3 }, 201);
            Assert.That(Weapon(replacement).Ammo, Is.EqualTo(30));
            Assert.That(Weapon(replacement).IsReloading, Is.False);
        }

        [Test]
        public void BothMagazinesAndEquipCooldown_SurviveConnectionMigration()
        {
            var session = Create(new RecordingTransport());
            Fire(session, 1, 1, 100);
            Feed(session, new PlayerInputCommand { Tick = 2, SwitchWeaponPressed = true }, 110);
            var replacement = new RecordingTransport();
            session.TryRebindConnection(replacement, new TransportConnectionId(9));
            session.TrySendRecoverySnapshot(111, out _);
            Assert.That(Weapon(replacement).WeaponId, Is.EqualTo(2));
            Assert.That(Weapon(replacement).EquipTicksRemaining, Is.EqualTo(WeaponCatalog.EquipDurationTicks - 1));
            Fire(session, 3, 2, 150);
            Assert.That(Weapon(replacement).Ammo, Is.EqualTo(4));
            Feed(session, new PlayerInputCommand { Tick = 4, SwitchWeaponPressed = true }, 160);
            var next = new RecordingTransport();
            session.TryRebindConnection(next, new TransportConnectionId(11));
            session.TrySendRecoverySnapshot(161, out _);
            Assert.That(Weapon(next).WeaponId, Is.EqualTo(1));
            Assert.That(Weapon(next).Ammo, Is.EqualTo(29));
            Feed(session, new PlayerInputCommand { Tick = 5, SwitchWeaponPressed = true }, 200);
            Assert.That(Weapon(next).Ammo, Is.EqualTo(4));
        }

        [TestCase(NetworkMessageType.AuthoritativePlayerState)]
        [TestCase(NetworkMessageType.AuthoritativeWeaponState)]
        public void PartialSeedFailure_KeepsSimulationBlockedAndAllowsSameTickRetry(NetworkMessageType failedType)
        {
            var session = Create(new RecordingTransport());
            var state = session.CurrentState;
            var replacement = new RecordingTransport { FailNext = failedType };
            session.TryRebindConnection(replacement, new TransportConnectionId(9));
            Assert.That(session.TrySendRecoverySnapshot(100, out _), Is.False);
            Assert.That(session.RecoverySnapshotPending, Is.True);
            Assert.That(session.TrySendRecoverySnapshot(99, out var rejected), Is.False);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(session.CurrentState.RawEquals(state), Is.True);
            Assert.That(session.TryAdvance(101, out _, out _), Is.False);
            Assert.That(session.TrySendRecoverySnapshot(101, out _), Is.True);
            Assert.That(session.RecoverySnapshotPending, Is.False);
            Assert.That(session.TrySendRecoverySnapshot(100, out _), Is.False);
            Assert.Throws<ArgumentException>(() => session.TryAdvance(101, out _, out _));
            Feed(session, new PlayerInputCommand { Tick = 1 }, 102);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void InvalidRebind_IsAtomicAndOriginalConnectionStillWorks(int reason)
        {
            var old = new RecordingTransport();
            var session = Create(old);
            var replacement = new RecordingTransport
            {
                Running = reason != 1,
                CurrentRole = reason == 2 ? TransportRole.Client : TransportRole.Server
            };
            Assert.That(session.TryRebindConnection(reason == 0 ? null : replacement,
                new TransportConnectionId(reason == 3 ? 0u : 9u)), Is.False);
            Assert.That(session.RecoverySnapshotPending, Is.False);
            Feed(session, new PlayerInputCommand { Tick = 1 }, 100);
            Assert.That(old.Packets.Count, Is.EqualTo(2));
            Assert.That(replacement.Packets, Is.Empty);
        }

        [Test]
        public void InputWorldAndShotSequences_ContinueAcrossUintWrap()
        {
            var initial = Initial();
            initial.Tick = uint.MaxValue - 1;
            var session = Create(new RecordingTransport(), initial: initial);
            Fire(session, uint.MaxValue, uint.MaxValue, uint.MaxValue);
            var replacement = new RecordingTransport();
            session.TryRebindConnection(replacement, new TransportConnectionId(9));
            Assert.That(session.NextExpectedInputTick, Is.Zero);
            Assert.That(session.TrySendRecoverySnapshot(0, out _), Is.True);
            Assert.That(session.TrySendRecoverySnapshot(uint.MaxValue - 1, out _), Is.False);
            Fire(session, 0, 1, 10);
            Assert.That(session.LastWeaponFireResult.Accepted, Is.True);
            Assert.That(Weapon(replacement).Ammo, Is.EqualTo(28));
        }

        [Test]
        public void OldFailedRejection_IsNotDeliveredToReplacementConnection()
        {
            var old = new RecordingTransport();
            var session = Create(old);
            Fire(session, 1, 1, 100);
            old.FailNext = NetworkMessageType.ShotRejection;
            Fire(session, 2, 1, 110);
            var replacement = new RecordingTransport();
            session.TryRebindConnection(replacement, new TransportConnectionId(9));
            session.TrySendRecoverySnapshot(111, out _);
            Feed(session, new PlayerInputCommand { Tick = 3 }, 112);
            foreach (var packet in replacement.Packets)
            {
                PacketHeaderCodec.TryRead(packet.Bytes, out var header);
                Assert.That(header.MessageType, Is.Not.EqualTo(NetworkMessageType.ShotRejection));
            }
        }

        [TestCase(100u, 104u, true)]
        [TestCase(100u, 105u, false)]
        [TestCase(uint.MaxValue - 2, 1u, true)]
        [TestCase(uint.MaxValue - 2, 2u, false)]
        public void RetentionDeadline_IsExclusiveAndWrapSafe(uint start, uint restore, bool allowed)
        {
            var session = Create(new RecordingTransport());
            var store = new RetainedServerSessionStore(1, 5);
            Assert.That(store.TryRetain(session, start), Is.True);
            Assert.That(session.IsRetainedOffline, Is.True);
            Assert.That(session.TryAdvance(start, out _, out _), Is.False);
            Assert.That(session.TryReceiveInputPacket(Packet(new PlayerInputCommand { Tick = 1 }), out _), Is.False);
            Assert.That(store.TryRestore(1, restore, new RecordingTransport(), new TransportConnectionId(9), out var restored), Is.EqualTo(allowed));
            Assert.That(store.Count, Is.Zero);
            Assert.That(restored, allowed ? Is.SameAs(session) : Is.Null);
            if (allowed)
            {
                Assert.That(restored.IsRetainedOffline, Is.False);
                Assert.That(restored.TrySendRecoverySnapshot(restore, out _), Is.True);
                Assert.That(store.TryRestore(1, restore, new RecordingTransport(), new TransportConnectionId(10), out _), Is.False);
            }
        }

        [Test]
        public void CapacityDuplicateAndInvalidEndpoint_DoNotEvictOrExtendOtherSessions()
        {
            var store = new RetainedServerSessionStore(1, 5);
            var first = Create(new RecordingTransport());
            var second = Create(new RecordingTransport(), 2);
            Assert.That(store.TryRetain(first, 100), Is.True);
            Assert.That(store.TryRetain(second, 101), Is.False);
            Assert.That(second.IsRetainedOffline, Is.False);
            Assert.That(store.TryRetain(first, 104), Is.False);
            Assert.That(store.TryRestore(1, 104, null, new TransportConnectionId(9), out var restored), Is.False);
            Assert.That(restored, Is.Null);
            Assert.That(store.Count, Is.EqualTo(1));
            Assert.That(store.Expire(105), Is.EqualTo(1));
            Assert.That(store.TryRetain(second, 105), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NullOrUnassignedSessions_CannotOccupyRetentionCapacity(bool unassigned)
        {
            var store = new RetainedServerSessionStore();
            var session = unassigned ? Create(new RecordingTransport(), 0) : null;
            Assert.That(store.TryRetain(session, 100), Is.False);
            Assert.That(store.Count, Is.Zero);
            Assert.That(store.TryRestore(0, 100, new RecordingTransport(), new TransportConnectionId(9), out var restored), Is.False);
            Assert.That(restored, Is.Null);
        }

        [Test]
        public void ClockRollbackIsRejected_AndClearResetsStoreClock()
        {
            var store = new RetainedServerSessionStore();
            store.TryRetain(Create(new RecordingTransport()), 100);
            Assert.Throws<ArgumentOutOfRangeException>(() => store.Expire(99));
            Assert.That(store.Count, Is.EqualTo(1));
            store.Clear();
            Assert.That(store.Count, Is.Zero);
            Assert.That(store.TryRetain(Create(new RecordingTransport(), 2), 0), Is.True);
        }

        [TestCase(0, 1u)]
        [TestCase(-1, 1u)]
        [TestCase(1025, 1u)]
        [TestCase(1, 0u)]
        [TestCase(1, 2147483648u)]
        public void RetentionConfiguration_RejectsUnboundedOrAmbiguousWindows(int capacity, uint ticks)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RetainedServerSessionStore(capacity, ticks));
        }

        private static PlayerState Initial() => new PlayerState { IsGrounded = true };

        private static ServerAuthoritativeMovementSession Create(RecordingTransport transport, uint entity = 1,
            PlayerState? initial = null) => new ServerAuthoritativeMovementSession(
                transport, new TransportConnectionId(1), initial ?? Initial(),
                new PlayerSimulationConfig(Fix.FromInt(6), Fix.FromInt(20), Fix.FromInt(20), Fix.FromInt(8)),
                0.02f, 16, maxMissingInputWaitTicks: 0, entityId: entity,
                weaponConfig: WeaponSimulationConfig.DefaultRifle, fixedCollisionWorld: FixedFlatGroundCollisionWorld.Instance);

        private static void Fire(ServerAuthoritativeMovementSession session, uint tick, uint sequence, uint worldTick)
        {
            Feed(session, new PlayerInputCommand
            {
                Tick = tick, FirePressed = true, ShotSequence = sequence, ShotServerTick = worldTick
            }, worldTick);
        }

        private static void Feed(ServerAuthoritativeMovementSession session, PlayerInputCommand input, uint worldTick)
        {
            Assert.That(session.TryReceiveInputPacket(Packet(input), out _), Is.True);
            Assert.That(session.TryAdvance(worldTick, out _, out _), Is.True);
        }

        private static ArraySegment<byte> Packet(PlayerInputCommand input)
        {
            var bytes = new byte[InputCommandBatchCodec.GetPacketSize(1)];
            Assert.That(InputCommandBatchCodec.TrySerialize(new InputCommandBatch(
                new ArraySegment<PlayerInputCommand>(new[] { input })), 1, new ArraySegment<byte>(bytes), out _), Is.True);
            return new ArraySegment<byte>(bytes);
        }

        private static AuthoritativeWeaponState Weapon(RecordingTransport transport)
        {
            for (int i = transport.Packets.Count - 1; i >= 0; i--)
            {
                if (AuthoritativeWeaponStateCodec.TryDeserialize(transport.Packets[i].Bytes, out _, out var state))
                {
                    return state;
                }
            }
            Assert.Fail("没有武器快照");
            return default;
        }

        private static AuthoritativePlayerState Owner(RecordingTransport transport)
        {
            foreach (var packet in transport.Packets)
            {
                if (AuthoritativePlayerStateCodec.TryDeserialize(packet.Bytes, out _, out var state))
                {
                    return state;
                }
            }
            Assert.Fail("没有拥有者快照");
            return default;
        }

        private readonly struct SentPacket
        {
            public readonly ArraySegment<byte> Bytes;
            public readonly TransportConnectionId Connection;
            public readonly TransportDelivery Delivery;

            public SentPacket(ArraySegment<byte> bytes, TransportConnectionId connection, TransportDelivery delivery)
            {
                Bytes = bytes;
                Connection = connection;
                Delivery = delivery;
            }
        }

        private sealed class RecordingTransport : IGameTransport
        {
            public readonly List<SentPacket> Packets = new List<SentPacket>();
            public bool Running = true;
            public TransportRole CurrentRole = TransportRole.Server;
            public NetworkMessageType FailNext;
            public bool IsRunning => Running;
            public TransportRole Role => CurrentRole;

            public TransportSendResult Send(TransportConnectionId connection, TransportDelivery delivery, ArraySegment<byte> payload)
            {
                PacketHeaderCodec.TryRead(payload, out var header);
                if (header.MessageType == FailNext)
                {
                    FailNext = NetworkMessageType.Unknown;
                    return TransportSendResult.TransportError;
                }
                var copy = new byte[payload.Count];
                Buffer.BlockCopy(payload.Array, payload.Offset, copy, 0, payload.Count);
                Packets.Add(new SentPacket(new ArraySegment<byte>(copy), connection, delivery));
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

            public void Pump()
            {
            }

            public void Disconnect(TransportConnectionId connection)
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
