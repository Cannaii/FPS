using AFPS.Math;
using AFPS.Simulation.Weapons;
using UnityEngine;

namespace AFPS.NetCode.Weapons
{
    /// <summary>尚未迁移的命中/弹道/结果协议兼容边界；权威射击生成不使用此浮点入口。</summary>
    public readonly partial struct AuthoritativeShot
    {
        public Vector3 Origin => ToUnity(OriginFixed);
        public Vector3 Direction => ToUnity(DirectionFixed);
        public float Damage => DamageFixed.ToFloat();
        public float Range => RangeFixed.ToFloat();
        public float ProjectileSpeed => ProjectileSpeedFixed.ToFloat();

        public AuthoritativeShot(
            uint entityId,
            uint shotSequence,
            uint serverTick,
            in WeaponSimulationConfig config,
            Vector3 origin,
            Vector3 direction,
            uint rewindTick = 0)
            : this(entityId, shotSequence, serverTick, config,
                ToFixed(origin), ToFixed(direction), rewindTick)
        {
        }

        public AuthoritativeShot(
            uint entityId,
            uint shotSequence,
            uint serverTick,
            ushort weaponId,
            WeaponFireMode fireMode,
            Vector3 origin,
            Vector3 direction,
            float damage,
            float range,
            float projectileSpeed,
            uint rewindTick = 0)
            : this(entityId, shotSequence, serverTick, weaponId, fireMode,
                ToFixed(origin), ToFixed(direction), Fix.FromFloat(damage),
                Fix.FromFloat(range), Fix.FromFloat(projectileSpeed), rewindTick)
        {
        }

        // 只在边界量化一次；非有限/超出 Q16.16 域按 Fix 的溢出策略显式失败。
        private static FixVec3 ToFixed(Vector3 value) => new FixVec3(
            Fix.FromFloat(value.x), Fix.FromFloat(value.y), Fix.FromFloat(value.z));

        private static Vector3 ToUnity(FixVec3 value) => new Vector3(
            value.X.ToFloat(), value.Y.ToFloat(), value.Z.ToFloat());
    }
}
