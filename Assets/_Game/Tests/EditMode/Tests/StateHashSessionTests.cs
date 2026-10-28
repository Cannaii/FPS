using System;
using AFPS.Math;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Metrics;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Transport;
using AFPS.Simulation.Characters;
using NUnit.Framework;
using static AFPS.Tests.EditMode.SimulationAdmissionTests;

namespace AFPS.Tests.EditMode
{
    public sealed class StateHashSessionTests
    {
        [Test]
        public void OriginalPredictionHash_RemainsDiagnosableAfterClientRollbackCorrectsState()
        {
            var pair = Connected();
            pair.Server.TryGetServerSession(ServerId, out var authority);
            authority.Teleport(authority.CurrentState.PositionFixed + FixVec3.Right);
            int driftEvents = 0;
            pair.Server.StateDriftDetected += _ => driftEvents++;
            PlayerState predicted = default;
            for (uint tick = 1; tick <= 25; tick++)
            {
                pair.Client.TryPredictAndSend(new PlayerInputCommand { Tick = tick }, out predicted, out _);
                while (pair.ClientTransport.Pending.Count != 0)
                {
                    pair.RouteClient();
                }
                pair.Server.AdvanceServerSessions(tick);
            }
            Assert.That(pair.Server.Metrics.StateHashReportsReceived, Is.EqualTo(1));
            Assert.That(pair.Server.Metrics.StateHashMismatches, Is.EqualTo(1));
            Assert.That(driftEvents, Is.EqualTo(1));
            Assert.That(pair.Server.LastStateDrift.Client.EntityId, Is.EqualTo(1));
            Assert.That(pair.Server.LastStateDrift.Client.InputTick, Is.EqualTo(25));
            Assert.That(pair.Server.LastStateDrift.Client.StateHash, Is.EqualTo(PlayerStateHash.Compute(predicted)));
            pair.Flush();
            Assert.That(pair.Client.Metrics.CorrectionsApplied, Is.GreaterThan(0));
            for (uint tick = 26; tick <= 50; tick++)
            {
                pair.Client.TryPredictAndSend(new PlayerInputCommand { Tick = tick }, out _, out _);
                while (pair.ClientTransport.Pending.Count != 0)
                {
                    pair.RouteClient();
                }
                pair.Server.AdvanceServerSessions(tick);
                pair.Flush();
            }
            Assert.That(pair.Server.LastStateHashComparison.Status, Is.EqualTo(StateHashComparisonStatus.Matched));
            Assert.That(pair.Server.LastStateDrift.Client.InputTick, Is.EqualTo(25));
            Assert.That(pair.Server.Metrics.StateHashMatches, Is.EqualTo(1));
        }

        [TestCase(26u)]
        [TestCase(51u)]
        public void FailedSend_RetriesOriginalSampleOrSupersedesWithinSingleBoundedSlot(uint lastTick)
        {
            var pair = Connected();
            ulong expectedHash = 0;
            uint expectedTick = lastTick == 26 ? 25u : 50u;
            for (uint tick = 1; tick <= lastTick; tick++)
            {
                if (tick >= 25 && tick < lastTick)
                {
                    pair.ClientTransport.FailType = AFPS.NetCode.Protocol.NetworkMessageType.ClientStateHashReport;
                }
                pair.Client.TryPredictAndSend(new PlayerInputCommand { Tick = tick, MoveY = 1f }, out var state, out _);
                if (tick == expectedTick)
                {
                    expectedHash = PlayerStateHash.Compute(state);
                }
            }
            var packet = pair.ClientTransport.Sent.FindLast(value => value.Type
                == AFPS.NetCode.Protocol.NetworkMessageType.ClientStateHashReport);
            Assert.That(packet, Is.Not.Null);
            Assert.That(packet.Delivery, Is.EqualTo(TransportDelivery.ReliableSequenced));
            Assert.That(ClientStateHashReportCodec.TryDeserialize(packet.Data, out uint sequence, out var report), Is.True);
            Assert.That(sequence, Is.EqualTo(1));
            Assert.That(report.InputTick, Is.EqualTo(expectedTick));
            Assert.That(report.StateHash, Is.EqualTo(expectedHash));
            Assert.That(pair.Client.Metrics.StateHashReportsSent, Is.EqualTo(1));
            Assert.That(pair.Client.Metrics.StateHashReportsSuperseded, Is.EqualTo(lastTick == 26 ? 0 : 1));
        }

        [Test]
        public void ReportCannotNameAnotherEntityOrUseAnUnadmittedConnection()
        {
            var pair = Connected();
            byte[] bytes = new byte[ClientStateHashReportCodec.PacketSize];
            ClientStateHashReportCodec.TrySerialize(new ClientStateHashReport(2, 25, 0, 0), 1,
                new ArraySegment<byte>(bytes), out _);
            Assert.That(pair.Server.TryHandleData(NetworkTransportSide.Server, ServerId,
                new ArraySegment<byte>(bytes), out _), Is.False);
            Assert.That(pair.Server.TryHandleData(NetworkTransportSide.Server, new TransportConnectionId(999),
                new ArraySegment<byte>(bytes), out _), Is.False);
            Assert.That(pair.Server.Metrics.StateHashReportsRejected, Is.EqualTo(1));
            Assert.That(pair.Server.Metrics.StateHashReportsReceived, Is.Zero);
        }

        private static Pair Connected()
        {
            var pair = new Pair();
            pair.Connect();
            pair.Flush();
            return pair;
        }
    }
}
