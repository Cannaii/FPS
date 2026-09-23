using System;
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// Defines actions & Callbacks of different Shoot Styles.
    /// By Default, HitscanShootStyle, ProjectileShootStyle & MeleeShootStyle implement IShootStyle. These are called in WeaponController´s Shoot() method.
    /// </summary>
    public interface IShootStyle
    {
        void Shoot(float spread, float damageMultiplier, float shakeMultiplier);
        event Action onShoot;
    }
}