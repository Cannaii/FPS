using System;
using System.Collections.Generic;
using AFPS.Core.Collections;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Protocol;
using AFPS.Simulation.Characters;

namespace AFPS.NetCode.Metrics
{
    public enum MovementHashComparisonStatus
    {
        Matched,
        StateMismatch,
        DifferentAppliedInput,
        HistoryUnavailable
    }

    public readonly struct MovementHashComparison
    {
        public readonly ClientMovementHashReport Client;
        public readonly ulong ServerStateHash;
        public readonly ulong ServerInputHash;
        public readonly MovementHashComparisonStatus Status;

        public MovementHashComparison(ClientMovementHashReport client, ulong stateHash, ulong inputHash,
            MovementHashComparisonStatus status)
        {
            Client = client;
            ServerStateHash = stateHash;
            ServerInputHash = inputHash;
            Status = status;
        }
    }

    /// <summary>单实体有界诊断窗口；可靠hash可能先于不可靠输入到达，先挂起，待同Tick权威样本出现后比较。</summary>
    public sealed class ServerMovementDriftTracker
    {
        public const int HistoryCapacity = 256;
        public const int MaximumPendingReports = 8;
        public const uint MaximumFutureTicks = 64;
        private readonly TickBuffer<Sample> history = new TickBuffer<Sample>(HistoryCapacity);
        private readonly Dictionary<uint, ClientMovementHashReport> pending = new Dictionary<uint, ClientMovementHashReport>();
        private readonly List<uint> ready = new List<uint>();
        private uint latestInputTick;
        private uint lastSequence;
        private uint lastReportTick;
        private bool hasReport;
        private bool hasSample;

        private readonly struct Sample
        {
            internal readonly ulong State;
            internal readonly ulong Input;

            internal Sample(ulong state, ulong input)
            {
                State = state;
                Input = input;
            }
        }

        public int PendingCount => pending.Count;
        public ulong AcceptedCount { get; private set; }
        public ulong RejectedCount { get; private set; }
        public ulong DiscardedOnReset { get; private set; }
        public event Action<MovementHashComparison> Compared;

        public void Reset(uint initialInputTick)
        {
            DiscardedOnReset += (ulong)pending.Count;
            history.Clear();
            pending.Clear();
            ready.Clear();
            latestInputTick = initialInputTick;
            hasReport = false;
            hasSample = false;
        }

        public bool TryReceive(in ClientMovementHashReport report, uint sequence)
        {
            bool future = SequenceMath.IsNewer(report.InputTick, latestInputTick);
            uint distance = future ? unchecked(report.InputTick - latestInputTick) : unchecked(latestInputTick - report.InputTick);
            if (report.EntityId == 0 || (hasReport && (!SequenceMath.IsNewer(sequence, lastSequence)
                    || !SequenceMath.IsNewer(report.InputTick, lastReportTick)))
                || (future && (distance > MaximumFutureTicks || pending.Count >= MaximumPendingReports))
                || (!future && distance >= HistoryCapacity))
            {
                RejectedCount++;
                return false;
            }
            hasReport = true;
            lastSequence = sequence;
            lastReportTick = report.InputTick;
            AcceptedCount++;
            if (future)
            {
                pending.Add(report.InputTick, report);
            }
            else
            {
                Compare(report);
            }
            return true;
        }

        public void Record(in PlayerMovementState state, in PlayerInputCommand appliedInput)
        {
            if (state.Tick != appliedInput.Tick || SequenceMath.IsOlder(state.Tick, latestInputTick)
                || (hasSample && state.Tick == latestInputTick))
            {
                throw new ArgumentException("漂移诊断样本必须按输入Tick递增并与实际输入对应。");
            }
            latestInputTick = state.Tick;
            hasSample = true;
            history.Store(state.Tick, new Sample(PlayerMovementStateHash.Compute(state), MovementInputHash.Compute(appliedInput)));
            ready.Clear();
            foreach (uint tick in pending.Keys)
            {
                if (!SequenceMath.IsNewer(tick, latestInputTick))
                {
                    ready.Add(tick);
                }
            }
            foreach (uint tick in ready)
            {
                var report = pending[tick];
                pending.Remove(tick);
                Compare(report);
            }
        }

        private void Compare(in ClientMovementHashReport report)
        {
            if (!history.TryGet(report.InputTick, out var sample))
            {
                Compared?.Invoke(new MovementHashComparison(report, 0, 0, MovementHashComparisonStatus.HistoryUnavailable));
                return;
            }
            // 不同替代输入、死亡/终局的输入钳制单独分类；不能据此声称数学确定性失效。
            var status = report.InputHash != sample.Input ? MovementHashComparisonStatus.DifferentAppliedInput
                : report.StateHash == sample.State ? MovementHashComparisonStatus.Matched : MovementHashComparisonStatus.StateMismatch;
            Compared?.Invoke(new MovementHashComparison(report, sample.State, sample.Input, status));
        }
    }
}
