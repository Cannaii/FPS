/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using TMPro;
using System.Text;
namespace cowsins
{
    public class GetGameInformation : MonoBehaviour
    {
        public bool showFPS;
        public bool showAverageFrameRate, showMaximumFrameRate;

        [SerializeField, Range(.01f, 1f)] private float fpsRefreshRate = 0.5f;
        [SerializeField] private TextMeshProUGUI fpsObject;

        [SerializeField] private Color appropriateValueColor, intermediateValueColor, badValueColor;

        private float fpsTimer;
        private float fps, avgFPS, maxFps;
        private int lastFrameIndex;
        private float[] frameDeltaTimeArray;

        private static readonly StringBuilder sb = new StringBuilder(128);

        private void Start()
        {
            if (showFPS)
                fpsTimer = fpsRefreshRate;
            else
                Destroy(fpsObject);

            frameDeltaTimeArray = new float[50];
        }

        private void Update()
        {
            if (!showFPS) return;

            frameDeltaTimeArray[lastFrameIndex] = Time.deltaTime;
            lastFrameIndex = (lastFrameIndex + 1) % frameDeltaTimeArray.Length;
            fps = CalculateFramerate();

            fpsTimer -= Time.deltaTime;

            if (fpsTimer <= 0)
            {
                avgFPS = Time.frameCount / Time.time;
                if (fps > maxFps) maxFps = fps;
                fpsTimer = fpsRefreshRate;

                sb.Clear();

                AppendFPS("Current FPS: ", fps);
                if (showAverageFrameRate) AppendFPS("\nAvg FPS: ", avgFPS);
                if (showMaximumFrameRate) AppendFPS("\nMax FPS: ", maxFps);

                fpsObject.text = sb.ToString();
            }
        }

        private float CalculateFramerate()
        {
            float total = 0;
            foreach (float deltaTime in frameDeltaTimeArray)
                total += deltaTime;
            return frameDeltaTimeArray.Length / total;
        }

        private void AppendFPS(string label, float fpsValue)
        {
            Color color = fpsValue < 15 ? badValueColor :
                          fpsValue < 45 ? intermediateValueColor :
                          appropriateValueColor;

            sb.Append(label)
              .Append("<color=#");
            AppendColorHex(sb, color);
            sb.Append(">")
              .Append(Mathf.RoundToInt(fpsValue))
              .Append("</color>");
        }

        private static void AppendColorHex(StringBuilder sb, Color color)
        {
            int r = (int)(color.r * 255f);
            int g = (int)(color.g * 255f);
            int b = (int)(color.b * 255f);

            AppendHexByte(sb, r);
            AppendHexByte(sb, g);
            AppendHexByte(sb, b);
        }

        private static void AppendHexByte(StringBuilder sb, int b)
        {
            const string hexDigits = "0123456789ABCDEF";
            sb.Append(hexDigits[(b >> 4) & 0xF]);
            sb.Append(hexDigits[b & 0xF]);
        }
    }

#if UNITY_EDITOR
    [System.Serializable]
    [CustomEditor(typeof(GetGameInformation))]
    public class GetGameInformatioEditor : Editor
    {

        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            GetGameInformation myScript = target as GetGameInformation;

            EditorGUILayout.LabelField("FPS", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("showFPS"));
            if (myScript.showFPS)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("fpsRefreshRate"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("fpsObject"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("showAverageFrameRate"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("showMaximumFrameRate"));
            }
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("COLOR", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("appropriateValueColor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("intermediateValueColor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("badValueColor"));

            serializedObject.ApplyModifiedProperties();

        }
    }
#endif
}