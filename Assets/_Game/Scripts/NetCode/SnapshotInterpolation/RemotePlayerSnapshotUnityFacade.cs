using AFPS.Math;
using UnityEngine;

namespace AFPS.NetCode.SnapshotInterpolation
{
    /// <summary>Unity 表现/兼容边界；协议、基线和服务器生成路径只使用定点字段。</summary>
    public readonly partial struct RemotePlayerSnapshot
    {
        public Vector3 Position => new Vector3(
            PositionFixed.X.ToFloat(), PositionFixed.Y.ToFloat(), PositionFixed.Z.ToFloat());
        public Vector3 Velocity => new Vector3(
            VelocityFixed.X.ToFloat(), VelocityFixed.Y.ToFloat(), VelocityFixed.Z.ToFloat());
        public Quaternion Rotation => Quaternion.Euler(
            PitchFixedDegrees.ToFloat(), YawFixedDegrees.ToFloat(), 0f);

        public RemotePlayerSnapshot(
            uint entityId,
            uint serverTick,
            Vector3 position,
            Quaternion rotation,
            Vector3 velocity,
            bool isTeleport = false)
        {
            EntityId = entityId;
            ServerTick = serverTick;
            PositionFixed = new FixVec3(
                Fix.FromFloat(position.x), Fix.FromFloat(position.y), Fix.FromFloat(position.z));
            VelocityFixed = new FixVec3(
                Fix.FromFloat(velocity.x), Fix.FromFloat(velocity.y), Fix.FromFloat(velocity.z));
            Vector3 euler = rotation.eulerAngles;
            YawFixedDegrees = Fix.FromRaw(Fix.FromFloat(Mathf.Repeat(euler.y, 360f)).Raw % FullTurnRaw);
            float pitch = euler.x > 180f ? euler.x - 360f : euler.x;
            int pitchRaw = Fix.FromFloat(Mathf.Clamp(pitch, -89.9f, 89.9f)).Raw;
            // 浮点 89.9 转 Raw 可能高一格；在表现输入边界钳到协议的规范角域。
            PitchFixedDegrees = Fix.FromRaw(System.Math.Max(-MaximumPitchRaw,
                System.Math.Min(MaximumPitchRaw, pitchRaw)));
            IsTeleport = isTeleport;
        }
    }
}