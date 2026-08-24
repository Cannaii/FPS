using UnityEngine;

namespace cowsins
{
    public class TitleAttribute : PropertyAttribute
    {
        // Title Label 
        public string title;
        // Enables a divider, true by default
        public bool divider;
        //  Separation Between the Title and the Property
        public float upMargin;
        public TitleAttribute(string title, bool divider = true, float upMargin = 0f)
        {
            this.title = title;
            this.divider = divider;
            this.upMargin = upMargin;
        }
    }
}