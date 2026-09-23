#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace cowsins
{
    [InitializeOnLoad]
    public static class ExternalButtonRegister
    {
        static ExternalButtonRegister()
        {
            DraggableButtonInSceneView.OnAddExternalButtons += AddCustomButtons;
        }

        private static void AddCustomButtons(Rect menuRect)
        {
            float itemWidth = menuRect.width / 3;
            float itemHeight = menuRect.height;


            /// OVERRIDE BUTTON COUNT ( NECESSARY ) 
            //DraggableButtonInSceneView.buttonCount = 5;

            /// ADD 5TH BUTTON
            /*
            Texture2D customButtonImage = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CustomButton.png");
            // Add custom button
            if (GUI.Button(new Rect(DraggableButtonInSceneView.GetButtonPosition(menuRect, 5), menuRect.y, itemWidth, itemHeight), customButtonImage))
            {
                Debug.Log("Custom Button Clicked");
            }
            */

            /// ADD 6TH BUTTON
            /*
            Texture2D customSecondButtonImage = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CustomButton.png");
            if (GUI.Button(new Rect(DraggableButtonInSceneView.GetButtonPosition(menuRect, 6), menuRect.y, itemWidth, itemHeight), customSecondButtonImage))
            {
                Debug.Log("Custom Button Clicked");
            }
            */

        }

    }

}
#endif