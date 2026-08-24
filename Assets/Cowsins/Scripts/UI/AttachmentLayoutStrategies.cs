using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// Interface that defines a strategy for calculating attachment group UI positions / different layout modes
    /// </summary>
    public interface IAttachmentLayoutStrategy
    {
        /// <summary>
        /// Calculates the position for the attachment group UI element.
        /// </summary>
        /// <param name="target">The 3D target attachment in world space</param>
        /// <param name="rectTransform">RectTransform of the UI element</param>
        /// <param name="groupIndex">The index of this attachment group</param>
        Vector3 CalculatePosition(Transform target, RectTransform rectTransform, int groupIndex);

        bool RequiresTarget { get; }
    }

    /// <summary>
    /// Captures the 3D attachment position and converts to screen space
    /// </summary>
    public class WorldSpaceLayoutStrategy : IAttachmentLayoutStrategy
    {
        public bool RequiresTarget => true;

        public Vector3 CalculatePosition(Transform target, RectTransform rectTransform, int groupIndex)
        {
            if (target == null || Camera.main == null) return rectTransform.position;

            Vector3 objectPosition = target.position;
            Vector3 screenPosition = Camera.main.WorldToScreenPoint(objectPosition);
            return screenPosition;
        }
    }

    /// <summary>
    /// Displays attachment groups in a column
    /// </summary>
    public class VerticalLayoutStrategy : IAttachmentLayoutStrategy
    {
        private readonly float spacing;
        private readonly Vector2 startPosition;
        private readonly bool spacingDown;

        public bool RequiresTarget => false;

        public VerticalLayoutStrategy(float spacing = 150f, Vector2? startPosition = null, bool spacingDown = true)
        {
            this.spacing = spacing;
            this.startPosition = startPosition ?? Vector2.zero;
            this.spacingDown = spacingDown;
        }

        public Vector3 CalculatePosition(Transform target, RectTransform rectTransform, int groupIndex)
        {
            float yPosition = spacingDown 
                ? startPosition.y - (groupIndex * spacing)
                : startPosition.y + (groupIndex * spacing);
            return new Vector2(startPosition.x, yPosition);
        }
    }
}
