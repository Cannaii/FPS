/// <summary>
/// This script belongs to cowsinsT as a part of the cowsins FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
namespace cowsins
{
    public class DamageMultiplier : PowerUp
    {
        [Header("CUSTOM"), SerializeField]
        private float damageMultiplierAddition;

        public override void Interact(PlayerDependencies player)
        {
            base.Interact(player);
            player.PlayerMultipliers.DamageMultiplier.AddModifier(new StatModifier(damageMultiplierAddition, StatModifierType.Additive, this));
            Destroy(this.gameObject);
        }
    }
}
