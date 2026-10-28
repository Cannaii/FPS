using AFPS.NetCode.Protocol;

namespace AFPS.NetCode.TimeSync
{
    /// <summary>用服务器自己的发送/ACK接收时间测量闭环RTT，包含客户端收包至发ACK的处理时间。</summary>
    public sealed class ServerRoundTripTracker
    {
        private struct Pending
        {
            public uint Sequence;
            public ulong SentAt;
            public bool Valid;
        }

        private readonly Pending[] pending = new Pending[8];
        private uint lastSequence;
        private ulong lastSampleAt;
        private bool hasSample;
        public double RoundTripMilliseconds { get; private set; }
        public double JitterMilliseconds { get; private set; }

        // 仅接受1秒内的闭环样本，3秒未更新失效；均为单调微秒，不跨客户端/服务器时钟相减。
        public bool HasFreshSample(ulong now) => hasSample && now >= lastSampleAt && now - lastSampleAt <= 3000000;

        public void RecordSent(uint sequence, ulong sentAt)
        {
            pending[sequence % (uint)pending.Length] = new Pending { Sequence = sequence, SentAt = sentAt, Valid = true };
        }

        public bool TryAcknowledge(uint sequence, ulong now)
        {
            int index = (int)(sequence % (uint)pending.Length);
            Pending entry = pending[index];
            if (!entry.Valid || entry.Sequence != sequence || now < entry.SentAt
                || now - entry.SentAt > 1000000 || (hasSample && !SequenceMath.IsNewer(sequence, lastSequence)))
            {
                return false;
            }
            pending[index].Valid = false;
            double measured = (now - entry.SentAt) / 1000d;
            JitterMilliseconds = HasFreshSample(now)
                ? JitterMilliseconds + (System.Math.Abs(measured - RoundTripMilliseconds) - JitterMilliseconds) * 0.2d
                : 0d;
            RoundTripMilliseconds = measured;
            lastSampleAt = now;
            lastSequence = sequence;
            hasSample = true;
            return true;
        }
    }
}
