/// <summary>
/// This script belongs to cowsinsT as a part of the cowsins FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
namespace cowsins
{
    public class HealMultiplierPowerUp : PowerUp
    {
        [Header("CUSTOM"), SerializeField]
        private float healMultiplierAddition;

        public override void Interact(PlayerDependencies player)
        {
            base.Interact(player);
            player.PlayerMultipliers.HealMultiplier.AddModifier(new StatModifier(healMultiplierAddition, StatModifierType.Additive, this));
            Destroy(this.gameObject);
        }
    }
}
