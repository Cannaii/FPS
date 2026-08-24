/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
#if UNITY_EDITOR
using UnityEditor;
#endif 
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
namespace cowsins
{
    /// <summary>
    /// Basic Point Capture script
    /// This is the core of the system.
    /// </summary>
    public class PointCapture : Trigger
    {
        [System.Serializable]
        public class PointCaptureEvents
        {
            public UnityEvent OnCapture;
        }
        public PointCaptureEvents captureEvents; // custom events

        [Tooltip(" how fast the point will be captured "), SerializeField]
        private float captureSpeed;

        [Tooltip(" If true, progress will gradually be lost when player leaves the point ")]
        public bool loseProgressIfNotCapturing;

        [Tooltip(" Speed of progress loss "), SerializeField]
        private float losingProgressCaptureSpeed;

        private bool beingCaptured;

        private bool captured;

        [SaveField] private float progress;

        private GameObject ui;

        private void Start()
        {
            // Initial stuff
            progress = 0;
            captured = false;
        }
        
        void Update()
        {
            // if player is not inside and we wanna lose progress then lose it
            if (!beingCaptured && progress > 0 && loseProgressIfNotCapturing) progress -= Time.deltaTime * losingProgressCaptureSpeed;
        }
        public override void TriggerStay(Collider other)
        {
            // If we are player and point is not captured, try to cap
            if (!captured)
            {
                if (!beingCaptured && ui == null) ui = Instantiate(Resources.Load("PointCaptureUI")) as GameObject; // Instantiate cool UI
                beingCaptured = true;
                progress += Time.deltaTime * captureSpeed;

                // Check if we already captured
                if (progress >= 100)
                {
                    captured = true; 
                    OnCapture();
                }

#if SAVE_LOAD_ADD_ON
                SaveTrigger();
#endif
            }

            // Handle UI, we do not wanna do this if there is no UI currently (We are not inside the point)
            if (ui == null) return;

            // It will show progress while you are in
            Slider slider = ui.transform.Find("Progress").GetComponent<Slider>();
            slider.value = progress;
        }
        // Stop capping
        public override void TriggerExit(Collider other)
        {
            if (!captured)
            {
                beingCaptured = false;
                Destroy(ui.gameObject);
            }
        }

        /// <summary>
        /// This is called whenever you capture the point
        /// </summary>
        public virtual void OnCapture()
        {
            captureEvents.OnCapture.Invoke(); // Call our custom event 

            /*Since it is a public virtual void, you can override this method on a new script that inherits from PointCapture. 
            However, do not forget to use base.OnCapture(); if
            you still want to perform these actions on that new script.

             EXAMPLE: on a new class (public class MyNewPointCapture : PointCapture) we will write the following structure
            public override void OnCapture() { // Write here whatever you want }

             (You can always edit this one if you do not plan to add more different kinds of capture points, just to avoid unncessary scripts)*/

            Debug.Log("You captured the point!");

            Destroy(ui);
            Destroy(this.gameObject);
        }

#if SAVE_LOAD_ADD_ON
        // If the point is captured after loading the progress, Destroy it.
        public override void LoadedState()
        {
            if (progress >= 100)
            {
                Destroy(ui);
                Destroy(this.gameObject);
            }
        }
#endif
    }
#if UNITY_EDITOR
    [System.Serializable]
    [CustomEditor(typeof(PointCapture))]
    public class PointCaptureEditor : Editor
    {

        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            PointCapture myScript = target as PointCapture;

            EditorGUILayout.LabelField("SETTINGS", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("captureSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("loseProgressIfNotCapturing"));
            if (myScript.loseProgressIfNotCapturing)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("losingProgressCaptureSpeed"));
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("captureEvents"));
            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}