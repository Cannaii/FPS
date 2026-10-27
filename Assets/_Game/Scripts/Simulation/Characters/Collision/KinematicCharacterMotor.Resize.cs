using System;
using AFPS.Math;

namespace AFPS.Simulation.Characters.Collision
{
    public static partial class KinematicCharacterMotor
    {
        /// <summary>
        /// 脚底不动时能否切换胶囊高度，单位米；调用前当前胶囊应处于有效位置。
        /// 缩小直接允许；增高要求顶端与障碍至少保留皮肤厚度，包含查询终点的接触也阻止站起。
        /// </summary>
        public static bool CanResizeAtFeet(
            FixVec3 feetPosition,
            in CharacterCollisionConfig current,
            Fix targetHeight,
            IFixedCharacterCollisionWorld world)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
            if ((long)targetHeight.Raw < 2L * current.RadiusFixed.Raw)
            {
                throw new ArgumentOutOfRangeException(nameof(targetHeight));
            }
            if (targetHeight <= current.HeightFixed)
            {
                return true;
            }

            // 固定半径的竖直胶囊沿Y扫过高度差，其并集恰好覆盖增高后的胶囊。
            // 不能用一根头顶射线代替，否则会漏掉帽沿和斜面；也不能平移脚底以假装站起。
            long distanceRaw = (long)targetHeight.Raw - current.HeightFixed.Raw + current.SkinWidthFixed.Raw;
            if (distanceRaw > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(targetHeight), "高度差与皮肤余量超出定点距离范围。");
            }
            return !world.CapsuleCast(feetPosition, FixVec3.Up, Fix.FromRaw((int)distanceRaw),
                current, out _);
        }
    }
}
