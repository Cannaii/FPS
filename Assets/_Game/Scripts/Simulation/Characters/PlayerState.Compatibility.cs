using AFPS.Math;
using UnityEngine;

namespace AFPS.Simulation.Characters
{
    /// <summary>Unity/旧接口迁移边界；权威协议和数值模拟只访问 Fixed 字段。</summary>
    public partial struct PlayerState
    {
        public Vector3 Position { get => ToVector(PositionFixed); set => PositionFixed = ToFixed(value); }
        public Vector3 Velocity { get => ToVector(VelocityFixed); set => VelocityFixed = ToFixed(value); }
        public float Yaw { get => YawFixedDegrees.ToFloat(); set { ValidateAngle(value); YawFixedDegrees = InputAngleGrid.YawToFix(InputAngleGrid.YawFromFloat(value)); } }
        public float Pitch { get => PitchFixedDegrees.ToFloat(); set { ValidateAngle(value); PitchFixedDegrees = InputAngleGrid.PitchToFix(InputAngleGrid.PitchFromFloat(value)); } }
        private static void ValidateAngle(float value) { if (float.IsNaN(value) || float.IsInfinity(value)) throw new System.ArgumentOutOfRangeException(nameof(value)); }
        internal static FixVec3 ToFixed(Vector3 value) => new FixVec3(Fix.FromFloat(value.x), Fix.FromFloat(value.y), Fix.FromFloat(value.z));
        internal static Vector3 ToVector(FixVec3 value) => new Vector3(value.X.ToFloat(), value.Y.ToFloat(), value.Z.ToFloat());
    }
}
