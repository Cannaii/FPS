using System.Collections.Generic;
using AFPS.NetCode.Weapons;
using UnityEngine;

namespace AFPS.Presentation.Weapons
{
    public sealed partial class ShotPresentationController
    {
        private const int MaximumRememberedMuzzles = 256;
        private readonly Dictionary<(uint Entity, uint Shot), Vector3> localMuzzles =
            new Dictionary<(uint, uint), Vector3>();
        private readonly Queue<(uint Entity, uint Shot)> muzzleOrder = new Queue<(uint, uint)>();

        /// <summary>记录开火瞬间的表现枪口（世界米）；延迟确认或切枪不能把旧弹接到新枪上。</summary>
        public void RememberLocalMuzzle(uint entityId, uint sequence, Vector3 position)
        {
            var key = (entityId, sequence);
            if (entityId == 0 || sequence == 0 || localMuzzles.ContainsKey(key))
            {
                return;
            }
            localMuzzles.Add(key, position);
            muzzleOrder.Enqueue(key);
            while (muzzleOrder.Count > MaximumRememberedMuzzles)
            {
                localMuzzles.Remove(muzzleOrder.Dequeue());
            }
        }

        private Vector3 VisualOrigin(in AuthoritativeShot shot) =>
            localMuzzles.TryGetValue((shot.EntityId, shot.ShotSequence), out var muzzle) ? muzzle : shot.Origin;
    }
}
