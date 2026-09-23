using System;
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// Handles events for the UI. These can be called anywhere.
    /// </summary>
    public class UIEvents
    {
        // Attachment UI Events
        public static Action<GameObject> onEnableAttachmentUI;
        public static Action<Attachment, bool> onAttachmentUIElementClicked;
        public static Action<Attachment> onAttachmentUIElementClickedNewAttachment;

        // Coins, Experience & Combat
        public static Action<int, bool> onCoinsChange;
        public static Action<bool> onExperienceCollected;
        public static Action<string> onEnemyKilled;
        public static Action<bool, bool, Vector3, float> onEnemyHit;
    }
}
