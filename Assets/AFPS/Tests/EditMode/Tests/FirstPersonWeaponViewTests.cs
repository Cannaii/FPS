using AFPS.Presentation.Weapons;
using NUnit.Framework;
using UnityEngine;

public sealed class FirstPersonWeaponViewTests
{
    [Test]
    public void PlayPredictedShot_WithOptionalEffectsUnassigned_DoesNotThrow()
    {
        GameObject gameObject = new GameObject("FirstPersonWeaponViewTest");
        try
        {
            FirstPersonWeaponView view = gameObject.AddComponent<FirstPersonWeaponView>();

            Assert.DoesNotThrow(view.PlayPredictedShot);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }
}
