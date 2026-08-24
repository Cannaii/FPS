/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
namespace cowsins
{
    /// <summary>
    /// Used for Player and enemies, which can be hit
    /// </summary>
    public interface IDamageable { void Damage(float damage, bool isHeadshot); }
}
