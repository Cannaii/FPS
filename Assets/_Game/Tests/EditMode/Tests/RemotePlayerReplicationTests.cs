using System;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.SnapshotInterpolation;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class RemotePlayerReplicationTests
    {
        [Test]
        public void DeltaSnapshot_RoundTripsChangedFieldsAndRejectsWrongBaseline()
        {
            RemotePlayerSnapshot baseline = new RemotePlayerSnapshot(7, 100, new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 10f, 0f), new Vector3(1f, 0f, 0f));
            RemotePlayerSnapshot current = new RemotePlayerSnapshot(7, 101, new Vector3(1.25f, 2f, 3f), Quaternion.Euler(-5f, 20f, 0f), new Vector3(1f, 0f, 0f), true);
            byte[] packet = new byte[RemotePlayerSnapshotDeltaCodec.MaximumPacketSize];

            Assert.That(RemotePlayerSnapshotDeltaCodec.TrySerialize(current, baseline, 9, new ArraySegment<byte>(packet), out int bytes), Is.True);
            Assert.That(bytes, Is.LessThan(RemotePlayerSnapshotCodec.PacketSize));
            Assert.That(RemotePlayerSnapshotDeltaCodec.TryDeserialize(new ArraySegment<byte>(packet, 0, bytes), baseline, out PacketHeader header, out RemotePlayerSnapshot decoded), Is.True);
            Assert.That(header.Sequence, Is.EqualTo(9));
            Assert.That(decoded.ServerTick, Is.EqualTo(101));
            Assert.That(Vector3.Distance(decoded.Position, current.Position), Is.LessThan(0.002f));
            Assert.That(decoded.Velocity, Is.EqualTo(baseline.Velocity));
            Assert.That(Quaternion.Angle(decoded.Rotation, current.Rotation), Is.LessThan(0.02f));
            Assert.That(decoded.IsTeleport, Is.True);

            RemotePlayerSnapshot wrongBaseline = new RemotePlayerSnapshot(7, 99, baseline.Position, baseline.Rotation, baseline.Velocity);
            Assert.That(RemotePlayerSnapshotDeltaCodec.TryDeserialize(new ArraySegment<byte>(packet, 0, bytes), wrongBaseline, out _, out _), Is.False);
        }

        [Test]
        public void DeltaSnapshot_DetectsChangesAcrossQuantizationBoundaryBelowOneResolution()
        {
            RemotePlayerSnapshot baseline = new RemotePlayerSnapshot(2, 10, Vector3.zero, Quaternion.identity, Vector3.zero);
            RemotePlayerSnapshot current = new RemotePlayerSnapshot(2, 11, new Vector3(0.0006f, 0f, 0f), Quaternion.identity, Vector3.zero);
            byte[] packet = new byte[RemotePlayerSnapshotDeltaCodec.MaximumPacketSize];
            Assert.That(RemotePlayerSnapshotDeltaCodec.TrySerialize(current, baseline, 1, new ArraySegment<byte>(packet), out int bytes), Is.True);
            Assert.That(RemotePlayerSnapshotDeltaCodec.TryDeserialize(new ArraySegment<byte>(packet, 0, bytes), baseline, out _, out RemotePlayerSnapshot decoded), Is.True);
            Assert.That(decoded.Position.x, Is.EqualTo(0.001f).Within(0.00001f));
        }

        [Test]
        public void SnapshotCodec_RoundTripsEntityTransformAndSequence()
        {
            RemotePlayerSnapshot source = new RemotePlayerSnapshot(7, 200, new Vector3(1.234f, 2.345f, -3.456f), Quaternion.Euler(-25.5f, 123.45f, 0f), new Vector3(4.56f, -7.89f, 0.12f), true);
            byte[] packet = new byte[RemotePlayerSnapshotCodec.PacketSize];

            Assert.That(RemotePlayerSnapshotCodec.TrySerialize(source, 99, new ArraySegment<byte>(packet), out int bytesWritten), Is.True);
            Assert.That(bytesWritten, Is.EqualTo(packet.Length));
            Assert.That(RemotePlayerSnapshotCodec.TryDeserialize(new ArraySegment<byte>(packet), out AFPS.NetCode.Protocol.PacketHeader header, out RemotePlayerSnapshot decoded), Is.True);
            Assert.That(header.Sequence, Is.EqualTo(99));
            Assert.That(decoded.EntityId, Is.EqualTo(source.EntityId));
            Assert.That(decoded.ServerTick, Is.EqualTo(source.ServerTick));
            Assert.That(Vector3.Distance(decoded.Position, source.Position), Is.LessThanOrEqualTo(AuthoritativePlayerStateCodec.MaximumPositionQuantizationError));
            Assert.That(Vector3.Distance(decoded.Velocity, source.Velocity), Is.LessThanOrEqualTo(AuthoritativePlayerStateCodec.MaximumVelocityQuantizationError));
            Assert.That(Quaternion.Angle(decoded.Rotation, source.Rotation), Is.LessThan(0.03f));
            Assert.That(decoded.IsTeleport, Is.True);
        }

        [Test]
        public void ReplicaSet_ExcludesLocalEntityRejectsOldSequenceAndDespawnsRemote()
        {
            RemotePlayerReplicaSet replicas = new RemotePlayerReplicaSet(8, 0.02f, 2.0, 2.0, 5f);
            Assert.That(replicas.TryApplyAssignment(CreateAssignmentPacket(3)), Is.True);
            Assert.That(replicas.LocalEntityId, Is.EqualTo(3));
            Assert.That(replicas.TryInsertSnapshot(CreateSnapshotPacket(3, 10, 1), out _), Is.False);
            Assert.That(replicas.TryInsertSnapshot(CreateSnapshotPacket(4, 10, 2), out _), Is.True);
            Assert.That(replicas.TryInsertSnapshot(CreateSnapshotPacket(4, 11, 1), out _), Is.False);
            Assert.That(replicas.Count, Is.EqualTo(1));
            Assert.That(replicas.TryApplyDespawn(CreateDespawnPacket(4), out uint entityId), Is.True);
            Assert.That(entityId, Is.EqualTo(4));
            Assert.That(replicas.Count, Is.Zero);
        }

        private static ArraySegment<byte> CreateAssignmentPacket(uint entityId)
        {
            byte[] packet = new byte[PlayerSessionAssignmentCodec.PacketSize];
            PlayerSessionAssignmentCodec.TrySerialize(new PlayerSessionAssignment(entityId), 1, new ArraySegment<byte>(packet), out _);
            return new ArraySegment<byte>(packet);
        }

        private static ArraySegment<byte> CreateSnapshotPacket(uint entityId, uint serverTick, uint sequence)
        {
            byte[] packet = new byte[RemotePlayerSnapshotCodec.PacketSize];
            RemotePlayerSnapshotCodec.TrySerialize(new RemotePlayerSnapshot(entityId, serverTick, Vector3.zero, Quaternion.identity, Vector3.zero), sequence, new ArraySegment<byte>(packet), out _);
            return new ArraySegment<byte>(packet);
        }

        private static ArraySegment<byte> CreateDespawnPacket(uint entityId)
        {
            byte[] packet = new byte[PlayerDespawnCodec.PacketSize];
            PlayerDespawnCodec.TrySerialize(entityId, 1, new ArraySegment<byte>(packet), out _);
            return new ArraySegment<byte>(packet);
        }
    }
}
