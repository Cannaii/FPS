#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;

namespace cowsins
{
    [CustomEditor(typeof(Turret))]
    public class TurretEditor : Editor
    {
        SerializedProperty targetType;
        SerializedProperty displayGizmos;
        SerializedProperty animator;
        SerializedProperty turretHead;
        SerializedProperty rangeShape;
        SerializedProperty detectionRadius;
        SerializedProperty cubeBounds;
        SerializedProperty coneRange;
        SerializedProperty coneAngle;
        SerializedProperty coneStaticDirection;
        SerializedProperty coneHorizontalAngle;
        SerializedProperty coneVerticalAngle;
        SerializedProperty allowVerticalMovement;
        SerializedProperty lerpSpeed;
        SerializedProperty requireLineOfSight;
        SerializedProperty obstacleLayers;
        SerializedProperty projectilePrefab;
        SerializedProperty projectileSpeed;
        SerializedProperty projectileDamage;
        SerializedProperty projectileDuration;
        SerializedProperty firePoint;
        SerializedProperty muzzleFlash;
        SerializedProperty fireRate;
        SerializedProperty projectilePoolSize;

        private void OnEnable()
        {
            targetType = serializedObject.FindProperty("targetType");
            displayGizmos = serializedObject.FindProperty("displayGizmos");
            animator = serializedObject.FindProperty("animator");
            turretHead = serializedObject.FindProperty("turretHead");
            rangeShape = serializedObject.FindProperty("rangeShape");
            detectionRadius = serializedObject.FindProperty("detectionRadius");
            cubeBounds = serializedObject.FindProperty("cubeBounds");
            coneRange = serializedObject.FindProperty("coneRange");
            coneAngle = serializedObject.FindProperty("coneAngle");
            coneStaticDirection = serializedObject.FindProperty("coneStaticDirection");
            coneHorizontalAngle = serializedObject.FindProperty("coneHorizontalAngle");
            coneVerticalAngle = serializedObject.FindProperty("coneVerticalAngle");
            allowVerticalMovement = serializedObject.FindProperty("allowVerticalMovement");
            lerpSpeed = serializedObject.FindProperty("lerpSpeed");
            requireLineOfSight = serializedObject.FindProperty("requireLineOfSight");
            obstacleLayers = serializedObject.FindProperty("obstacleLayers");
            projectilePrefab = serializedObject.FindProperty("projectilePrefab");
            projectileSpeed = serializedObject.FindProperty("projectileSpeed");
            projectileDamage = serializedObject.FindProperty("projectileDamage");
            projectileDuration = serializedObject.FindProperty("projectileDuration");
            firePoint = serializedObject.FindProperty("firePoint");
            muzzleFlash = serializedObject.FindProperty("muzzleFlash");
            fireRate = serializedObject.FindProperty("fireRate");
            projectilePoolSize = serializedObject.FindProperty("projectilePoolSize");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Target Settings
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(targetType);
            EditorGUILayout.Space(10);

            // References
            EditorGUILayout.PropertyField(animator);
            EditorGUILayout.PropertyField(turretHead);
            EditorGUILayout.Space(10);

            // Range Shape
            EditorGUILayout.PropertyField(rangeShape);
            EditorGUILayout.PropertyField(displayGizmos);

            // Shape specific settings
            Turret.RangeShape currentShape = (Turret.RangeShape)rangeShape.enumValueIndex;

            switch (currentShape)
            {
                case Turret.RangeShape.Sphere:
                    EditorGUILayout.PropertyField(detectionRadius, new GUIContent("Detection Radius"));
                    break;

                case Turret.RangeShape.Cube:
                    EditorGUILayout.PropertyField(cubeBounds, new GUIContent("Cube Bounds"));
                    break;

                case Turret.RangeShape.Cone:
                    EditorGUILayout.PropertyField(coneRange, new GUIContent("Cone Range"));
                    EditorGUILayout.PropertyField(coneAngle, new GUIContent("Cone Angle"));
                    EditorGUILayout.PropertyField(coneStaticDirection, new GUIContent("Static Direction"));

                    if (coneStaticDirection.boolValue)
                    {
                        EditorGUILayout.PropertyField(coneHorizontalAngle, new GUIContent("Horizontal Angle"));
                        EditorGUILayout.PropertyField(coneVerticalAngle, new GUIContent("Vertical Angle"));
                    }
                    break;
            }

            EditorGUILayout.Space(10);

            // Basic Settings
            EditorGUILayout.PropertyField(allowVerticalMovement);
            EditorGUILayout.PropertyField(lerpSpeed);
            EditorGUILayout.PropertyField(requireLineOfSight);

            EditorGUILayout.PropertyField(obstacleLayers, new GUIContent("Obstacle Layers"));

            EditorGUILayout.Space(10);

            // Projectile Settings
            EditorGUILayout.PropertyField(projectilePrefab);
            EditorGUILayout.PropertyField(projectileSpeed);
            EditorGUILayout.PropertyField(projectileDamage);
            EditorGUILayout.PropertyField(projectileDuration);
            EditorGUILayout.PropertyField(firePoint);
            EditorGUILayout.PropertyField(muzzleFlash);
            EditorGUILayout.Space(10);

            // Shooting
            EditorGUILayout.PropertyField(fireRate);
            EditorGUILayout.Space(10);

            // Pool Settings
            EditorGUILayout.PropertyField(projectilePoolSize);

            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif