/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
namespace cowsins
{
    public class Healthpack : PowerUp
    {
        [Tooltip("Amount of health to be restored")] [Range(.1f, 1000), SerializeField] private float healAmount;
        public override void Interact(PlayerDependencies player)
        {
            IPlayerStatsProvider playerStatsProvider = player.GetComponent<IPlayerStatsProvider>();
            if (playerStatsProvider.IsFullyHealed()) return;
            used = true;
            timer = reappearTime;
            playerStatsProvider.Heal(healAmount);
        }
    }
}