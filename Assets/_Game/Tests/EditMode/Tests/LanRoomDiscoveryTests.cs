using System;
using System.Collections.Generic;
using AFPS.NetCode.Match;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Runtime;
using AFPS.Simulation.World;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class LanRoomDiscoveryTests
    {
        private static readonly SimulationWorldIdentity Identity = new SimulationWorldIdentity(
            21, 0x2122232425262728UL, 0x3132333435363738UL);

        private static LanRoomAdvertisement Room(ushort port = 7777, byte online = 2, byte capacity = 32,
            ulong instance = 0x1112131415161718UL)
        {
            return new LanRoomAdvertisement(instance, PacketHeader.CurrentProtocolVersion, port, online,
                capacity, DeathmatchPhase.Playing, Identity);
        }

        [Test]
        public void AdvertisementHasStableLittleEndianGoldenAndHonorsSlices()
        {
            var bytes = new byte[56];
            bytes[0] = 123;
            bytes[55] = 234;
            var slice = new ArraySegment<byte>(bytes, 3, 50);
            Assert.That(LanRoomDiscoveryProtocol.TryWriteAdvertisement(0x0102030405060708UL, Room(), slice), Is.True);
            CollectionAssert.AreEqual(new byte[]
            {
                0x41, 0x46, 0x44, 0x52, 1, 2, 0, 0,
                8, 7, 6, 5, 4, 3, 2, 1,
                0x18, 0x17, 0x16, 0x15, 0x14, 0x13, 0x12, 0x11,
                27, 2, 32, 1, 0x61, 0x1e, 21, 0, 0, 0,
                0x28, 0x27, 0x26, 0x25, 0x24, 0x23, 0x22, 0x21,
                0x38, 0x37, 0x36, 0x35, 0x34, 0x33, 0x32, 0x31
            }, slice);
            Assert.That(bytes[0], Is.EqualTo(123));
            Assert.That(bytes[55], Is.EqualTo(234));
            Assert.That(LanRoomDiscoveryProtocol.TryReadAdvertisement(slice, out ulong nonce, out var room), Is.True);
            Assert.That(nonce, Is.EqualTo(0x0102030405060708UL));
            Assert.That(room.Identity.Equals(Identity), Is.True);
            Assert.That(room.IsCompatible(Identity), Is.True);
            Assert.That(room.Port, Is.EqualTo(7777));
        }

        [Test]
        public void QueryRejectsTruncationTrailingDataWrongKindAndZeroCorrelation()
        {
            var bytes = new byte[16];
            Assert.That(LanRoomDiscoveryProtocol.TryWriteQuery(ulong.MaxValue, new ArraySegment<byte>(bytes)), Is.True);
            for (int length = 0; length < 16; length++)
            {
                Assert.That(LanRoomDiscoveryProtocol.TryReadQuery(new ArraySegment<byte>(bytes, 0, length), out ulong nonce), Is.False);
                Assert.That(nonce, Is.Zero);
            }
            Assert.That(LanRoomDiscoveryProtocol.TryReadQuery(new ArraySegment<byte>(new byte[17]), out _), Is.False);
            Assert.That(LanRoomDiscoveryProtocol.TryWriteQuery(0, new ArraySegment<byte>(bytes)), Is.False);
            Assert.That(LanRoomDiscoveryProtocol.TryWriteQuery(1, default), Is.False);
            Assert.That(LanRoomDiscoveryProtocol.TryReadQuery(new ArraySegment<byte>(bytes), out ulong value), Is.True);
            Assert.That(value, Is.EqualTo(ulong.MaxValue));
            bytes[5] = 2;
            Assert.That(LanRoomDiscoveryProtocol.TryReadQuery(new ArraySegment<byte>(bytes), out _), Is.False);
        }

        [TestCase(0, 0)]
        [TestCase(4, 2)]
        [TestCase(5, 1)]
        [TestCase(6, 1)]
        [TestCase(24, 0)]
        [TestCase(25, 33)]
        [TestCase(26, 0)]
        [TestCase(26, 65)]
        [TestCase(27, 3)]
        public void MalformedAdvertisementReturnsNoPartialState(int offset, int invalid)
        {
            var bytes = new byte[50];
            LanRoomDiscoveryProtocol.TryWriteAdvertisement(1, Room(), new ArraySegment<byte>(bytes));
            bytes[offset] = (byte)invalid;
            Assert.That(LanRoomDiscoveryProtocol.TryReadAdvertisement(new ArraySegment<byte>(bytes),
                out ulong nonce, out var room), Is.False);
            Assert.That(nonce, Is.Zero);
            Assert.That(room.InstanceId, Is.Zero);
        }

        [Test]
        public void AdvertisementRejectsAllTruncationTrailingDataAndMissingRequiredFields()
        {
            var bytes = new byte[51];
            LanRoomDiscoveryProtocol.TryWriteAdvertisement(1, Room(), new ArraySegment<byte>(bytes));
            for (int length = 0; length <= 51; length++)
            {
                if (length != 50)
                {
                    Assert.That(LanRoomDiscoveryProtocol.TryReadAdvertisement(
                        new ArraySegment<byte>(bytes, 0, length), out _, out _), Is.False);
                }
            }
            foreach (var range in new[] { (8, 8), (16, 8), (28, 2), (30, 4) })
            {
                LanRoomDiscoveryProtocol.TryWriteAdvertisement(1, Room(), new ArraySegment<byte>(bytes));
                Array.Clear(bytes, range.Item1, range.Item2);
                Assert.That(LanRoomDiscoveryProtocol.TryReadAdvertisement(new ArraySegment<byte>(bytes, 0, 50),
                    out _, out _), Is.False);
            }
            Assert.That(LanRoomDiscoveryProtocol.TryWriteAdvertisement(0, Room(), new ArraySegment<byte>(bytes)), Is.False);
            Assert.That(LanRoomDiscoveryProtocol.TryWriteAdvertisement(1, Room(), default), Is.False);
            Assert.That(LanRoomDiscoveryProtocol.TryWriteAdvertisement(1, Room(0), new ArraySegment<byte>(bytes)), Is.False);
        }

        [Test]
        public void IncompatibleRoomsCanBeListedWithoutBeingJoinable()
        {
            var identity = new SimulationWorldIdentity(22, Identity.WorldHash, Identity.ConfigurationHash);
            Assert.That(Room().IsCompatible(identity), Is.False);
            Assert.That(Room().IsCompatible(new SimulationWorldIdentity(21, 1, Identity.ConfigurationHash)), Is.False);
            Assert.That(Room().IsCompatible(new SimulationWorldIdentity(21, Identity.WorldHash, 1)), Is.False);
            var old = new LanRoomAdvertisement(1, 25, 7777, 1, 32, DeathmatchPhase.Playing, Identity);
            var directory = new LanRoomDirectory();
            Assert.That(directory.Observe("127.0.0.1", old, 0), Is.True);
            Assert.That(old.IsCompatible(Identity), Is.False);
        }

        [Test]
        public void DirectoryRefreshesReplacesRestartedInstanceAndExpiresAtInclusiveDeadline()
        {
            var directory = new LanRoomDirectory();
            Assert.That(directory.Observe("127.0.0.1", Room(), 10), Is.True);
            Assert.That(directory.Observe("127.0.0.1", Room(online: 3, instance: 99), 12), Is.True);
            Assert.That(directory.Count, Is.EqualTo(1));
            var rows = new List<LanRoomEntry>();
            directory.CopyTo(rows);
            Assert.That(rows[0].Advertisement.InstanceId, Is.EqualTo(99));
            Assert.That(rows[0].Advertisement.OnlinePlayers, Is.EqualTo(3));
            rows.Clear();
            Assert.That(directory.Count, Is.EqualTo(1));
            directory.Advance(16.999);
            Assert.That(directory.Count, Is.EqualTo(1));
            directory.Advance(17);
            Assert.That(directory.Count, Is.Zero);
        }

        [Test]
        public void DirectoryIsBoundedAndAllowsUpdatesAtCapacityAndNewRoomsAfterExpiry()
        {
            var directory = new LanRoomDirectory();
            for (ushort port = 1; port <= 32; port++)
            {
                Assert.That(directory.Observe("127.0.0.1", Room(port), 0), Is.True);
            }
            Assert.That(directory.Observe("127.0.0.1", Room(33), 0), Is.False);
            Assert.That(directory.Observe("127.0.0.1", Room(1, online: 32), 1), Is.True);
            directory.Advance(5);
            Assert.That(directory.Count, Is.EqualTo(1));
            Assert.That(directory.Observe("127.0.0.1", Room(33), 5), Is.True);
            var rows = new List<LanRoomEntry>();
            directory.CopyTo(rows);
            Assert.That(rows[0].Advertisement.Port, Is.EqualTo(1));
            Assert.That(rows[1].Advertisement.Port, Is.EqualTo(33));
        }

        [Test]
        public void DirectoryRejectsInvalidEndpointsAndTimeAndResetsOnClockRollback()
        {
            var directory = new LanRoomDirectory();
            directory.Observe("127.0.0.1", Room(), 10);
            foreach (double time in new[] { double.NaN, double.PositiveInfinity, -1 })
            {
                Assert.That(directory.Observe("127.0.0.1", Room(), time), Is.False);
                Assert.That(directory.Count, Is.EqualTo(1));
            }
            Assert.That(directory.Observe("0.0.0.0", Room(), 10), Is.False);
            Assert.That(directory.Observe("224.0.0.1", Room(), 10), Is.False);
            Assert.That(directory.Observe("127.0.0.1", Room(instance: 0), 10), Is.False);
            directory.Advance(9);
            Assert.That(directory.Count, Is.Zero);
            Assert.Throws<ArgumentNullException>(() => directory.CopyTo(null));
        }
    }
}
