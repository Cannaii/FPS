using AFPS.Math;
using AFPS.Simulation.Weapons;

namespace AFPS.NetCode.Weapons
{
    /// <summary>权威射击的定点描述；坐标/射程为米，弹速为米/秒，伤害为生命值。</summary>
    public readonly partial struct AuthoritativeShot
    {
        public readonly uint EntityId;
        public readonly uint ShotSequence;
        public readonly uint ServerTick;
        public readonly uint RewindTick;
        public readonly ushort WeaponId;
        public readonly WeaponFireMode FireMode;
        public readonly FixVec3 OriginFixed;
        /// <summary>世界空间归一化方向；生成端负责归一化，DTO 不重复舍入。</summary>
        public readonly FixVec3 DirectionFixed;
        public readonly Fix DamageFixed;
        public readonly Fix RangeFixed;
        public readonly Fix ProjectileSpeedFixed;

        public AuthoritativeShot(
            uint entityId,
            uint shotSequence,
            uint serverTick,
            in WeaponSimulationConfig config,
            FixVec3 origin,
            FixVec3 direction,
            uint rewindTick = 0)
            : this(entityId, shotSequence, serverTick, config.WeaponId, config.FireMode,
                origin, direction, config.DamageFixed, config.RangeFixed,
                config.ProjectileSpeedFixed, rewindTick)
        {
        }

        public AuthoritativeShot(
            uint entityId,
            uint shotSequence,
            uint serverTick,
            ushort weaponId,
            WeaponFireMode fireMode,
            FixVec3 origin,
            FixVec3 direction,
            Fix damage,
            Fix range,
            Fix projectileSpeed,
            uint rewindTick = 0)
        {
            EntityId = entityId;
            ShotSequence = shotSequence;
            ServerTick = serverTick;
            // 保留旧请求语义：零表示使用接收请求的服务器 Tick；非零由回溯窗口另行验证。
            RewindTick = rewindTick == 0 ? serverTick : rewindTick;
            WeaponId = weaponId;
            FireMode = fireMode;
            OriginFixed = origin;
            DirectionFixed = direction;
            DamageFixed = damage;
            RangeFixed = range;
            ProjectileSpeedFixed = projectileSpeed;
        }
    }
}
