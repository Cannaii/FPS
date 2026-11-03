using System.Reflection;
using AFPS.Bootstrap;
using AFPS.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AFPS.Tests.EditMode
{
    public sealed class CrosshairSettingsTests
    {
        [Test]
        public void Appearance_ClampsInvalidDataAndRoundTripsVersionedSettings()
        {
            var value = CrosshairAppearance.Default;
            value.Size = float.NaN;
            value.Opacity = 5f;
            value.Color = new Color(-1f, 2f, float.PositiveInfinity);
            value.Style = (CrosshairStyle)88;
            value = value.Validated();
            Assert.That(value.Size, Is.EqualTo(24f));
            Assert.That(value.Opacity, Is.EqualTo(1f));
            Assert.That(value.Color.r, Is.Zero);
            Assert.That(value.Color.g, Is.EqualTo(1f));
            Assert.That(value.Style, Is.EqualTo(CrosshairStyle.Cross));
            Assert.That(CrosshairPreferences.TryDecode(CrosshairPreferences.Encode(value), out var decoded), Is.True);
            Assert.That(decoded.Color, Is.EqualTo(value.Color));
            Assert.That(CrosshairPreferences.TryDecode("{}", out _), Is.False);
            Assert.That(CrosshairPreferences.TryDecode("{\"Version\":9}", out _), Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Window_PreviewsCancelsAndRestoresInput(bool enabledBefore)
        {
            var root = new GameObject("Crosshair window test");
            GameObject events = null;
            var original = CrosshairPreferences.Current;
            var originalLock = Cursor.lockState;
            bool originalCursor = Cursor.visible;
            var controller = root.AddComponent<CrosshairSettingsController>();
            try
            {
                if (EventSystem.current == null)
                {
                    events = new GameObject("Crosshair test events", typeof(EventSystem));
                }
                var input = root.AddComponent<LocalPlayerInputCollector>();
                input.enabled = enabledBefore;
                controller.Open();
                var view = root.GetComponentInChildren<CrosshairSettingsView>(true);
                Assert.That(view.IsConfigured, Is.True);
                Assert.That(view.Preview.GetComponent<CanvasRenderer>(), Is.Not.Null);
                Assert.That(input.enabled, Is.False);
                view.Sliders[0].value = 48f;
                view.Sliders[3].value = 0.4f;
                view.Style.onClick.Invoke();
                Assert.That(CrosshairPreferences.Current.Size, Is.EqualTo(48f));
                Assert.That(CrosshairPreferences.Current.Opacity, Is.EqualTo(0.4f).Within(0.001f));
                Assert.That(view.Preview.Appearance.Style, Is.Not.EqualTo(original.Style));
                var gameplay = root.AddComponent<UnityNetworkGameplayController>();
                Assert.That((bool)typeof(UnityNetworkGameplayController).GetMethod("IsLocalGameplayActive",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(gameplay, null), Is.False);
                view.Cancel.onClick.Invoke();
                Assert.That(controller.IsVisible, Is.False);
                Assert.That(input.enabled, Is.EqualTo(enabledBefore));
                Assert.That(CrosshairPreferences.Current.Size, Is.EqualTo(original.Size));
                Assert.That(CrosshairPreferences.Current.Style, Is.EqualTo(original.Style));
            }
            finally
            {
                controller.Close(false);
                CrosshairPreferences.Apply(original, false);
                Object.DestroyImmediate(root);
                if (events != null)
                {
                    Object.DestroyImmediate(events);
                }
                Cursor.lockState = originalLock;
                Cursor.visible = originalCursor;
            }
        }

        [Test]
        public void CrosshairPrefab_IsCenteredAndDoesNotInterceptClicks()
        {
            var graphic = Resources.Load<CrosshairGraphic>("UI/Crosshair");
            Assert.That(graphic, Is.Not.Null);
            Assert.That(graphic.GetComponent<CanvasRenderer>(), Is.Not.Null);
            Assert.That(graphic.rectTransform.anchorMin, Is.EqualTo(Vector2.one * 0.5f));
            Assert.That(graphic.rectTransform.anchorMax, Is.EqualTo(Vector2.one * 0.5f));
            Assert.That(graphic.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(graphic.raycastTarget, Is.False);
        }
    }
}
