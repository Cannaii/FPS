using System;
using AFPS.Math;
using AFPS.NetCode.Messages;
using AFPS.Simulation.Characters;

namespace AFPS.NetCode.Prediction
{
    public static class ClientPredictionComparer
    {
        public static PredictionError Compare(in PlayerState predicted, in AuthoritativePlayerState authoritative)
        {
            if (authoritative.State.Tick != authoritative.LastProcessedInputTick || predicted.Tick != authoritative.LastProcessedInputTick) throw new ArgumentException("Only states at the same acknowledged input Tick may be compared.");
            PlayerState state = authoritative.State;
            long yawDelta = System.Math.Abs((long)predicted.YawFixedDegrees.Raw - state.YawFixedDegrees.Raw) % (360 * Fix.Scale);
            yawDelta = System.Math.Min(yawDelta, 360 * Fix.Scale - yawDelta);
            long pitchDelta = System.Math.Abs((long)predicted.PitchFixedDegrees.Raw - state.PitchFixedDegrees.Raw);
            // 浮点值仅用于诊断显示；校正决策读取 RawMismatch，绝不读取这些距离阈值。
            return new PredictionError(predicted.Tick, Distance(predicted.PositionFixed, state.PositionFixed), Distance(predicted.VelocityFixed, state.VelocityFixed), (float)(System.Math.Max(yawDelta, pitchDelta) / (double)Fix.Scale), predicted.IsGrounded != state.IsGrounded, !predicted.RawEquals(state));
        }
        private static float Distance(in FixVec3 a, in FixVec3 b)
        {
            double x = ((long)a.X.Raw - b.X.Raw) / (double)Fix.Scale, y = ((long)a.Y.Raw - b.Y.Raw) / (double)Fix.Scale, z = ((long)a.Z.Raw - b.Z.Raw) / (double)Fix.Scale;
            return (float)System.Math.Sqrt(x*x + y*y + z*z);
        }
    }
}
