/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using System.Collections;
using UnityEngine;
using TMPro;
namespace cowsins
{
    public class CheckPointView : MonoBehaviour
    {
        public enum MeasureType
        {
            metres, kilometres, inches, feet, yards, miles
        }
        #region variables

        [Tooltip("Attach the text where you want the distance to be displayed"), SerializeField]
        private TextMeshProUGUI text;

        [Tooltip("Select a measure unit among the following"), SerializeField]
        private MeasureType measureType;

        [Tooltip("number of decimals to display"), Range(0, 10), SerializeField]
        private int decimals;

        [Tooltip("How fast you want the text to display the new distance"), SerializeField]
        private float updatePeriod;

        private Transform playerTransform;
        #endregion

        private readonly float[] ConversionFactors =
        {
            1f,                  // Metres
            0.001f,              // Kilometres
            39.37f,              // Inches
            3.28084f,            // Feet
            1.09361f,            // Yards
            0.000621371192f      // Miles
        };

        private readonly string[] UnitLabels =
        {
            "m", "km", "inch", "feet", "yards", "miles"
        };

        private void Start()
        {
            playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
            StartCoroutine(UpdateValue());
        }

        /// <summary>
        /// Updates the displayed distance at the specified update period.
        /// </summary>
        private IEnumerator UpdateValue()
        {
            var wait = new WaitForSeconds(updatePeriod);

            while (true)
            {
                UpdateDistanceText();
                yield return wait;
            }
        }

        /// <summary>
        /// Calculates and updates the distance text.
        /// </summary>
        private void UpdateDistanceText()
        {
            float baseDistance = Vector3.Distance(transform.position, playerTransform.position);
            float convertedDistance = baseDistance * ConversionFactors[(int)measureType];
            string distanceText = convertedDistance.ToString($"F{decimals}") + UnitLabels[(int)measureType];

            if (text != null)
                text.text = distanceText;
        }
    }
}