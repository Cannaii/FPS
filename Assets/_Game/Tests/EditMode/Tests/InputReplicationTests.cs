using System;
using AFPS.Core.Collections;
using AFPS.NetCode.InputReplication;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Transport;
using AFPS.Simulation.Characters;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class InputReplicationTests
    {
        [Test]
        public void Sender_PacksLatestContinuousHistoryInAscendingTickOrder()
        {
            TickBuffer<PlayerInputCommand> history = new TickBuffer<PlayerInputCommand>(16);
            for (uint tick = 10; tick <= 14; tick++)
            {
                PlayerInputCommand command = new PlayerInputCommand { Tick = tick, MoveX = tick / 20f };
                history.Store(tick, command);
            }

            FakeTransport transport = new FakeTransport();
            TransportConnectionId connectionId = new TransportConnectionId(9);
            ClientInputBatchSender sender = new ClientInputBatchSender(transport, connectionId, history, 3);
            Assert.That(sender.TrySendLatest(14, out InputBatchSendResult sendResult), Is.True);

            Assert.That(sendResult.FirstTick, Is.EqualTo(12));
            Assert.That(sendResult.LastTick, Is.EqualTo(14));
            Assert.That(sendResult.CommandCount, Is.EqualTo(3));
            Assert.That(sendResult.PacketSequence, Is.EqualTo(1));
            Assert.That(transport.LastConnectionId, Is.EqualTo(connectionId));
            Assert.That(transport.LastDelivery, Is.EqualTo(TransportDelivery.UnreliableSequenced));

            PlayerInputCommand[] decoded = new PlayerInputCommand[3];
            Assert.That(InputCommandBatchCodec.TryDeserialize(new ArraySegment<byte>(transport.LastPayload), decoded, 0, out _, out InputCommandBatch batch), Is.True);
            Assert.That(batch.FirstTick, Is.EqualTo(12));
            Assert.That(batch.LastTick, Is.EqualTo(14));
        }

        [Test]
        public void Sender_DoesNotCrossMissingHistoryAndOnlyAdvancesSequenceAfterSuccess()
        {
            TickBuffer<PlayerInputCommand> history = new TickBuffer<PlayerInputCommand>(16);
            PlayerInputCommand tick9 = new PlayerInputCommand { Tick = 9 };
            PlayerInputCommand tick11 = new PlayerInputCommand { Tick = 11 };
            history.Store(9, tick9);
            history.Store(11, tick11);

            FakeTransport transport = new FakeTransport { SendResult = TransportSendResult.TransportError };
            ClientInputBatchSender sender = new ClientInputBatchSender(transport, new TransportConnectionId(1), history, 4, 20);
            Assert.That(sender.TrySendLatest(11, out InputBatchSendResult failed), Is.False);
            Assert.That(failed.Status, Is.EqualTo(InputBatchSendStatus.TransportRejected));
            Assert.That(failed.FirstTick, Is.EqualTo(11));
            Assert.That(failed.CommandCount, Is.EqualTo(1));
            Assert.That(failed.PacketSequence, Is.EqualTo(20));

            transport.SendResult = TransportSendResult.Success;
            Assert.That(sender.TrySendLatest(11, out InputBatchSendResult succeeded), Is.True);
            Assert.That(succeeded.PacketSequence, Is.EqualTo(20));
            Assert.That(sender.TrySendLatest(11, out InputBatchSendResult next), Is.True);
            Assert.That(next.PacketSequence, Is.EqualTo(21));
        }

        [Test]
        public void Receiver_RecoversLostEarlierPacketFromLaterRedundantBatch()
        {
            ServerInputCommandReceiver receiver = new ServerInputCommandReceiver(1, 16);
            byte[] laterPacket = CreatePacket(2, Command(1), Command(2));

            Assert.That(receiver.TryReceivePacket(new ArraySegment<byte>(laterPacket), out InputBatchReceiveResult receiveResult), Is.True);
            Assert.That(receiveResult.AcceptedCommandCount, Is.EqualTo(2));
            Assert.That(receiver.TryDequeueNext(out PlayerInputCommand first), Is.True);
            Assert.That(first.Tick, Is.EqualTo(1));
            Assert.That(receiver.TryDequeueNext(out PlayerInputCommand second), Is.True);
            Assert.That(second.Tick, Is.EqualTo(2));
            Assert.That(receiver.TryDequeueNext(out _), Is.False);
        }

        [Test]
        public void Receiver_DeduplicatesOverlappingBatches()
        {
            ServerInputCommandReceiver receiver = new ServerInputCommandReceiver(1, 16);
            Assert.That(receiver.TryReceivePacket(new ArraySegment<byte>(CreatePacket(1, Command(1), Command(2), Command(3))), out InputBatchReceiveResult first), Is.True);
            Assert.That(first.AcceptedCommandCount, Is.EqualTo(3));

            Assert.That(receiver.TryReceivePacket(new ArraySegment<byte>(CreatePacket(2, Command(2), Command(3), Command(4))), out InputBatchReceiveResult second), Is.True);
            Assert.That(second.AcceptedCommandCount, Is.EqualTo(1));
            Assert.That(second.DuplicateCommandCount, Is.EqualTo(2));

            for (uint tick = 1; tick <= 4; tick++)
            {
                Assert.That(receiver.TryDequeueNext(out PlayerInputCommand command), Is.True);
                Assert.That(command.Tick, Is.EqualTo(tick));
            }
        }

        [Test]
        public void Receiver_WaitsAtGapAndRejectsCommandsOutsideFutureWindow()
        {
            ServerInputCommandReceiver receiver = new ServerInputCommandReceiver(1, 4);
            Assert.That(receiver.TryReceivePacket(new ArraySegment<byte>(CreatePacket(1, Command(2))), out _), Is.True);
            Assert.That(receiver.TryDequeueNext(out _), Is.False);

            Assert.That(receiver.TryReceivePacket(new ArraySegment<byte>(CreatePacket(2, Command(5))), out InputBatchReceiveResult farFuture), Is.True);
            Assert.That(farFuture.RejectedCommandCount, Is.EqualTo(1));

            Assert.That(receiver.TryReceivePacket(new ArraySegment<byte>(CreatePacket(3, Command(1))), out _), Is.True);
            Assert.That(receiver.TryDequeueNext(out PlayerInputCommand first), Is.True);
            Assert.That(first.Tick, Is.EqualTo(1));
            Assert.That(receiver.TryDequeueNext(out PlayerInputCommand second), Is.True);
            Assert.That(second.Tick, Is.EqualTo(2));
        }

        [Test]
        public void Receiver_HandlesTickWrapAround()
        {
            ServerInputCommandReceiver receiver = new ServerInputCommandReceiver(uint.MaxValue, 8);
            byte[] packet = CreatePacket(1, Command(uint.MaxValue), Command(0));
            Assert.That(receiver.TryReceivePacket(new ArraySegment<byte>(packet), out _), Is.True);
            Assert.That(receiver.TryDequeueNext(out PlayerInputCommand beforeWrap), Is.True);
            Assert.That(beforeWrap.Tick, Is.EqualTo(uint.MaxValue));
            Assert.That(receiver.TryDequeueNext(out PlayerInputCommand afterWrap), Is.True);
            Assert.That(afterWrap.Tick, Is.EqualTo(0));
        }

        [Test]
        public void Receiver_AdvancesPastMissingCommandAndRejectsItsLateArrival()
        {
            ServerInputCommandReceiver receiver = new ServerInputCommandReceiver(10, 8);
            Assert.That(receiver.TryAdvancePastMissingCommand(out uint missingTick), Is.True);
            Assert.That(missingTick, Is.EqualTo(10));
            Assert.That(receiver.NextExpectedTick, Is.EqualTo(11));

            Assert.That(receiver.TryReceivePacket(new ArraySegment<byte>(CreatePacket(1, Command(10))), out InputBatchReceiveResult result), Is.True);
            Assert.That(result.AcceptedCommandCount, Is.EqualTo(0));
            Assert.That(result.DuplicateCommandCount, Is.EqualTo(1));
            Assert.That(receiver.TryDequeueNext(out _), Is.False);
        }

        [Test]
        public void Receiver_DoesNotAdvancePastCommandThatAlreadyArrived()
        {
            ServerInputCommandReceiver receiver = new ServerInputCommandReceiver(10, 8);
            Assert.That(receiver.TryReceivePacket(new ArraySegment<byte>(CreatePacket(1, Command(10))), out _), Is.True);

            Assert.That(receiver.TryAdvancePastMissingCommand(out _), Is.False);
            Assert.That(receiver.NextExpectedTick, Is.EqualTo(10));
            Assert.That(receiver.TryDequeueNext(out PlayerInputCommand command), Is.True);
            Assert.That(command.Tick, Is.EqualTo(10));
        }

        private static PlayerInputCommand Command(uint tick) => new PlayerInputCommand { Tick = tick, MoveY = 1f };

        private static byte[] CreatePacket(uint sequence, params PlayerInputCommand[] commands)
        {
            byte[] packet = new byte[InputCommandBatchCodec.GetPacketSize(commands.Length)];
            InputCommandBatch batch = new InputCommandBatch(new ArraySegment<PlayerInputCommand>(commands));
            Assert.That(InputCommandBatchCodec.TrySerialize(batch, sequence, new ArraySegment<byte>(packet), out _), Is.True);
            return packet;
        }

        private sealed class FakeTransport : IGameTransport
        {
            public bool IsRunning => true;
            public TransportRole Role => TransportRole.Client;
            public TransportSendResult SendResult = TransportSendResult.Success;
            public TransportConnectionId LastConnectionId;
            public TransportDelivery LastDelivery;
            public byte[] LastPayload;

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

            public void Pump()
            {
            }

            public bool TryPollEvent(ArraySegment<byte> receiveBuffer, out GameTransportEvent transportEvent)
            {
                transportEvent = default;
                return false;
            }

            public TransportSendResult Send(TransportConnectionId connectionId, TransportDelivery delivery, ArraySegment<byte> payload)
            {
                LastConnectionId = connectionId;
                LastDelivery = delivery;
                LastPayload = new byte[payload.Count];
                Array.Copy(payload.Array, payload.Offset, LastPayload, 0, payload.Count);
                return SendResult;
            }

            public void Disconnect(TransportConnectionId connectionId)
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
