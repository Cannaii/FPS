using System;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Prediction;
using AFPS.NetCode.Protocol;
using AFPS.Simulation.Characters;
using NUnit.Framework;
using UnityEngine;

namespace AFPS.Tests.EditMode
{
    public sealed class AuthoritativePlayerStateCodecTests
    {
        [Test]
        public void State_RoundTripsTicksFlagsAndQuantizedVectors()
        {
            PlayerState sourceState = new PlayerState
            {
                Tick = 120,
                Position = new Vector3(12.3454f, -0.0006f, 2048.7654f),
                Velocity = new Vector3(6.234f, -8.765f, 0.004f),
                IsGrounded = true
            };
            AuthoritativePlayerState source = new AuthoritativePlayerState(500, 120, sourceState);
            byte[] packet = new byte[AuthoritativePlayerStateCodec.PacketSize];

            Assert.That(AuthoritativePlayerStateCodec.TrySerialize(source, 77, new ArraySegment<byte>(packet), out int bytesWritten), Is.True);
            Assert.That(bytesWritten, Is.EqualTo(39));
            Assert.That(AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(packet), out PacketHeader header, out AuthoritativePlayerState decoded), Is.True);
            Assert.That(header.MessageType, Is.EqualTo(NetworkMessageType.AuthoritativePlayerState));
            Assert.That(header.Sequence, Is.EqualTo(77));
            Assert.That(decoded.ServerTick, Is.EqualTo(500));
            Assert.That(decoded.LastProcessedInputTick, Is.EqualTo(120));
            Assert.That(decoded.State.Tick, Is.EqualTo(120));
            Assert.That(decoded.State.IsGrounded, Is.True);
            Assert.That(Vector3.Distance(decoded.State.Position, sourceState.Position), Is.LessThanOrEqualTo(AuthoritativePlayerStateCodec.MaximumPositionQuantizationError));
            Assert.That(Vector3.Distance(decoded.State.Velocity, sourceState.Velocity), Is.LessThanOrEqualTo(AuthoritativePlayerStateCodec.MaximumVelocityQuantizationError));
        }

        [Test]
        public void State_WorksInsideArraySegmentWithNonZeroOffset()
        {
            PlayerState state = new PlayerState { Tick = 9, Position = new Vector3(1f, 2f, 3f), IsGrounded = false };
            AuthoritativePlayerState source = new AuthoritativePlayerState(20, 9, state);
            byte[] storage = new byte[AuthoritativePlayerStateCodec.PacketSize + 8];
            ArraySegment<byte> packet = new ArraySegment<byte>(storage, 4, AuthoritativePlayerStateCodec.PacketSize);

            Assert.That(AuthoritativePlayerStateCodec.TrySerialize(source, 3, packet, out int bytesWritten), Is.True);
            Assert.That(bytesWritten, Is.EqualTo(AuthoritativePlayerStateCodec.PacketSize));
            Assert.That(AuthoritativePlayerStateCodec.TryDeserialize(packet, out _, out AuthoritativePlayerState decoded), Is.True);
            Assert.That(Vector3.Distance(decoded.State.Position, state.Position), Is.LessThanOrEqualTo(AuthoritativePlayerStateCodec.MaximumPositionQuantizationError));
        }

        [Test]
        public void Serialize_RejectsMismatchedTickAndInvalidNumbers()
        {
            byte[] packet = new byte[AuthoritativePlayerStateCodec.PacketSize];
            PlayerState mismatched = new PlayerState { Tick = 4 };
            Assert.That(AuthoritativePlayerStateCodec.TrySerialize(new AuthoritativePlayerState(10, 5, mismatched), 1, new ArraySegment<byte>(packet), out _), Is.False);

            PlayerState invalidPosition = new PlayerState { Tick = 5, Position = new Vector3(float.NaN, 0f, 0f) };
            Assert.That(AuthoritativePlayerStateCodec.TrySerialize(new AuthoritativePlayerState(10, 5, invalidPosition), 1, new ArraySegment<byte>(packet), out _), Is.False);

            PlayerState excessiveVelocity = new PlayerState { Tick = 5, Velocity = new Vector3(400f, 0f, 0f) };
            Assert.That(AuthoritativePlayerStateCodec.TrySerialize(new AuthoritativePlayerState(10, 5, excessiveVelocity), 1, new ArraySegment<byte>(packet), out _), Is.False);
        }

        [Test]
        public void Deserialize_RejectsUnknownStateFlags()
        {
            PlayerState state = new PlayerState { Tick = 5 };
            byte[] packet = new byte[AuthoritativePlayerStateCodec.PacketSize];
            Assert.That(AuthoritativePlayerStateCodec.TrySerialize(new AuthoritativePlayerState(10, 5, state), 1, new ArraySegment<byte>(packet), out _), Is.True);

            packet[packet.Length - 1] = 1 << 1;
            Assert.That(AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(packet), out _, out _), Is.False);
        }

        [Test]
        public void RecommendedThresholds_IgnoreOnlyProtocolQuantizationError()
        {
            PlayerState predicted = new PlayerState
            {
                Tick = 30,
                Position = new Vector3(1.2344f, 2.3454f, 3.4564f),
                Velocity = new Vector3(1.234f, 2.345f, 3.456f),
                IsGrounded = true
            };
            byte[] packet = new byte[AuthoritativePlayerStateCodec.PacketSize];
            AuthoritativePlayerState original = new AuthoritativePlayerState(100, 30, predicted);
            Assert.That(AuthoritativePlayerStateCodec.TrySerialize(original, 1, new ArraySegment<byte>(packet), out _), Is.True);
            Assert.That(AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(packet), out _, out AuthoritativePlayerState decoded), Is.True);

            PredictionError error = ClientPredictionComparer.Compare(predicted, decoded);
            Assert.That(error.Exceeds(AuthoritativePlayerStateCodec.RecommendedPositionErrorThreshold, AuthoritativePlayerStateCodec.RecommendedVelocityErrorThreshold), Is.False);
        }
    }
}
