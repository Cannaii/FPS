using System;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Prediction;
using AFPS.NetCode.Protocol;
using AFPS.Simulation.Characters;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class AuthoritativeMovementStateCodecTests
    {
        [Test]
        public void State_RoundTripsTicksFlagsAndRawVectors()
        {
            PlayerMovementState sourceState = new PlayerMovementState
            {
                Tick = 120,
                Position = new Vector3(12.3454f, -0.0006f, 2048.7654f),
                Velocity = new Vector3(6.234f, -8.765f, 0.004f),
                Yaw = 271.234f,
                Pitch = -35.678f,
                IsGrounded = true
            };
            AuthoritativeMovementState source = new AuthoritativeMovementState(500, 120, sourceState);
            byte[] packet = new byte[AuthoritativeMovementStateCodec.PacketSize];
            Assert.That(AuthoritativeMovementStateCodec.TrySerialize(source, 77, new ArraySegment<byte>(packet), out int bytesWritten), Is.True);
            Assert.That(bytesWritten, Is.EqualTo(AuthoritativeMovementStateCodec.PacketSize));
            Assert.That(AuthoritativeMovementStateCodec.TryDeserialize(new ArraySegment<byte>(packet), out PacketHeader header, out AuthoritativeMovementState decoded), Is.True);
            Assert.That(header.MessageType, Is.EqualTo(NetworkMessageType.AuthoritativeMovementState));
            Assert.That(header.Sequence, Is.EqualTo(77));
            Assert.That(decoded.ServerTick, Is.EqualTo(500));
            Assert.That(decoded.LastProcessedInputTick, Is.EqualTo(120));
            Assert.That(decoded.State.Tick, Is.EqualTo(120));
            Assert.That(decoded.State.IsGrounded, Is.True);
            Assert.That(Vector3.Distance(decoded.State.Position, sourceState.Position), Is.LessThanOrEqualTo(AuthoritativeMovementStateCodec.MaximumPositionQuantizationError));
            Assert.That(Vector3.Distance(decoded.State.Velocity, sourceState.Velocity), Is.LessThanOrEqualTo(AuthoritativeMovementStateCodec.MaximumVelocityQuantizationError));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(decoded.State.Yaw, sourceState.Yaw)), Is.LessThanOrEqualTo(AuthoritativeMovementStateCodec.LookAngleResolution));
            Assert.That(decoded.State.Pitch, Is.EqualTo(sourceState.Pitch).Within(AuthoritativeMovementStateCodec.LookAngleResolution));
        }

        [Test]
        public void State_WorksInsideArraySegmentWithNonZeroOffset()
        {
            PlayerMovementState state = new PlayerMovementState
            {
                Tick = 9,
                Position = new Vector3(1f, 2f, 3f),
                IsGrounded = false
            };
            AuthoritativeMovementState source = new AuthoritativeMovementState(20, 9, state);
            byte[] storage = new byte[AuthoritativeMovementStateCodec.PacketSize + 8];
            ArraySegment<byte> packet = new ArraySegment<byte>(storage, 4, AuthoritativeMovementStateCodec.PacketSize);
            Assert.That(AuthoritativeMovementStateCodec.TrySerialize(source, 3, packet, out int bytesWritten), Is.True);
            Assert.That(bytesWritten, Is.EqualTo(AuthoritativeMovementStateCodec.PacketSize));
            Assert.That(AuthoritativeMovementStateCodec.TryDeserialize(packet, out _, out AuthoritativeMovementState decoded), Is.True);
            Assert.That(Vector3.Distance(decoded.State.Position, state.Position), Is.LessThanOrEqualTo(AuthoritativeMovementStateCodec.MaximumPositionQuantizationError));
        }

        [Test]
        public void Serialize_RejectsMismatchedTickAndInvalidNumbers()
        {
            byte[] packet = new byte[AuthoritativeMovementStateCodec.PacketSize];
            PlayerMovementState mismatched = new PlayerMovementState
            {
                Tick = 4
            };
            Assert.That(AuthoritativeMovementStateCodec.TrySerialize(new AuthoritativeMovementState(10, 5, mismatched), 1, new ArraySegment<byte>(packet), out _), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerMovementState { Tick = 5, Position = new Vector3(float.NaN, 0f, 0f) });
            PlayerMovementState excessiveVelocity = new PlayerMovementState
            {
                Tick = 5,
                Velocity = new Vector3(400f, 0f, 0f)
            };
            Assert.That(AuthoritativeMovementStateCodec.TrySerialize(new AuthoritativeMovementState(10, 5, excessiveVelocity), 1, new ArraySegment<byte>(packet), out _), Is.True);
        }

        [Test]
        public void Deserialize_RejectsUnknownStateFlags()
        {
            PlayerMovementState state = new PlayerMovementState
            {
                Tick = 5
            };
            byte[] packet = new byte[AuthoritativeMovementStateCodec.PacketSize];
            Assert.That(AuthoritativeMovementStateCodec.TrySerialize(new AuthoritativeMovementState(10, 5, state), 1, new ArraySegment<byte>(packet), out _), Is.True);
            packet[PacketHeader.Size + 36] = 1 << 7; // bit6已用于攀爬，bit7仍为未知位；不能依赖包尾偏移。
            Assert.That(AuthoritativeMovementStateCodec.TryDeserialize(new ArraySegment<byte>(packet), out _, out _), Is.False);
        }

        [Test]
        public void RecommendedThresholds_IgnoreOnlyProtocolQuantizationError()
        {
            PlayerMovementState predicted = new PlayerMovementState
            {
                Tick = 30,
                Position = new Vector3(1.2344f, 2.3454f, 3.4564f),
                Velocity = new Vector3(1.234f, 2.345f, 3.456f),
                IsGrounded = true
            };
            byte[] packet = new byte[AuthoritativeMovementStateCodec.PacketSize];
            AuthoritativeMovementState original = new AuthoritativeMovementState(100, 30, predicted);
            Assert.That(AuthoritativeMovementStateCodec.TrySerialize(original, 1, new ArraySegment<byte>(packet), out _), Is.True);
            Assert.That(AuthoritativeMovementStateCodec.TryDeserialize(new ArraySegment<byte>(packet), out _, out AuthoritativeMovementState decoded), Is.True);
            MovementPredictionError error = MovementPredictionComparer.Compare(predicted, decoded);
            Assert.That(error.Exceeds(AuthoritativeMovementStateCodec.RecommendedPositionErrorThreshold, AuthoritativeMovementStateCodec.RecommendedVelocityErrorThreshold), Is.False);
        }
    }
}
