using UnityEngine;
using UnityEngine.EventSystems;

namespace cowsins
{

    /// <summary>
    /// Attachment Group UI groups all the attachment UI elements together.
    /// </summary>
    public class AttachmentGroupUI : MonoBehaviour, IPointerClickHandler
    {
        [HideInInspector] public Transform target;
        [HideInInspector] public int groupIndex;

        private bool active;
        private IAttachmentLayoutStrategy layoutStrategy;
        private RectTransform rectTransform;

        private void OnEnable()
        {
            // Subscribe to the method
            UIEvents.onEnableAttachmentUI += Disable;
            rectTransform = GetComponent<RectTransform>();
            
            // Default to world space tracking as the default layout strategy 
            if (layoutStrategy == null) layoutStrategy = new WorldSpaceLayoutStrategy();
        }

        private void OnDisable()
        {
            // Unsubscribe to the method
            UIEvents.onEnableAttachmentUI -= Disable;
        }

        private void Update()
        {
            // Return if no strategy at all is set.
            if (layoutStrategy == null || rectTransform == null) return;

            // Return if strategy requires target but target doesnt exist
            if (layoutStrategy.RequiresTarget && target == null) return;

            // Calculate and apply position
            Vector3 calculatedPosition = layoutStrategy.CalculatePosition(target, rectTransform, groupIndex);
            
            // Use anchoredPosition for fixed layouts and transform position for world space tracking since positions are calculated in different ways
            if (layoutStrategy.RequiresTarget)
                transform.position = calculatedPosition;
            else
                rectTransform.anchoredPosition = calculatedPosition;
        }

        /// <summary>
        /// Sets the layout strategy for this attachment group
        /// </summary>
        public void SetLayoutStrategy(AttachmentUILayoutMode mode, float spacing = 150f, Vector2? startPosition = null, bool spacingDown = true)
        {
            switch (mode)
            {
                case AttachmentUILayoutMode.WorldSpaceTracking:
                    layoutStrategy = new WorldSpaceLayoutStrategy();
                    break;
                case AttachmentUILayoutMode.VerticalLayout:
                    layoutStrategy = new VerticalLayoutStrategy(spacing, startPosition, spacingDown);
                    break;
                default:
                    layoutStrategy = new WorldSpaceLayoutStrategy();
                    break;
            }
        }

        // handle on mouse click ( only if hovering previously )
        public void OnPointerClick(PointerEventData eventData)
        {
            // Handle UI event
            UIEvents.onEnableAttachmentUI?.Invoke(this.gameObject);

            // If its currently active, disable it
            if (active)
            {
                Disable(null);
                return;
            }

            // Otherwise, enable it
            Enable();
        }

        private void Enable()
        {
            // Set to active
            active = true;

            // Enable each of the children
            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(true);
            }
        }

        private void Disable(GameObject go)
        {
            if (go != null && go == this.gameObject) return;

            // Set active to false
            active = false;

            //Deactivate each child
            for (int i = 1; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(false);
            }
        }
    }
}