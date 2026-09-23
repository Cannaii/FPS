#if UNITY_EDITOR
/// <summary>
/// This script belongs to cowsins� as a part of the cowsins� FPS Engine. All rights reserved. 
/// </summary>

using UnityEngine;
using UnityEditor;

namespace cowsins
{
    [System.Serializable]
    [CustomEditor(typeof(PlayerMovement))]
    public class PlayerMovementEditor : Editor
    {
        private string[] tabs = { "Assignables", "Movement", "Camera", "Sliding", "Jumping", "Aim assist", "Stamina", "Advanced Movement","Footsteps", "Others" };
        private int currentTab = 0;

        private bool showAirCameraInfluence, showWallRun, showWallBounce, showDashing, showGrapplingHook, showClimbing;

        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            PlayerMovement myScript = target as PlayerMovement;

            Texture2D myTexture = Resources.Load<Texture2D>("CustomEditor/playerMovement_CustomEditor") as Texture2D;
            GUILayout.Label(myTexture);

            SerializedProperty playerSettings = serializedObject.FindProperty("playerSettings");
            SerializedProperty allowCrouchProp = playerSettings.FindPropertyRelative("allowCrouch");
            bool allowCrouch = allowCrouchProp.boolValue;

            SerializedProperty maxJumpsProp = playerSettings.FindPropertyRelative("maxJumps");
            int maxJumps = maxJumpsProp.intValue;

            SerializedProperty allowSlideProp = playerSettings.FindPropertyRelative("allowSliding");
            bool allowSlide = allowSlideProp.boolValue;

            SerializedProperty canWallRunProp = playerSettings.FindPropertyRelative("canWallRun");
            bool canWallRun = canWallRunProp.boolValue;

            SerializedProperty canWallBounceProp = playerSettings.FindPropertyRelative("canWallBounce");
            bool canWallBounce = canWallBounceProp.boolValue;

            bool canDash = playerSettings.FindPropertyRelative("canDash").boolValue;
            bool infiniteDashes = playerSettings.FindPropertyRelative("infiniteDashes").boolValue;

            SerializedProperty allowGrappleProp = playerSettings.FindPropertyRelative("allowGrapple");
            bool allowGrapple = allowGrappleProp.boolValue;


            EditorGUILayout.BeginVertical();
            currentTab = GUILayout.SelectionGrid(currentTab, tabs, 6);
            EditorGUILayout.Space(10f);
            EditorGUILayout.EndVertical();
            #region variables

            if (currentTab >= 0 || currentTab < tabs.Length)
            {
                switch (tabs[currentTab])
                {
                    case "Assignables":
                        EditorGUILayout.LabelField("ASSIGNABLES", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("playerCam"));
                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("cameraFOVManager"));
                        SerializedProperty useSpeedLinesProp = playerSettings.FindPropertyRelative("useSpeedLines");
                        EditorGUILayout.PropertyField(useSpeedLinesProp);
                        if (useSpeedLinesProp != null && useSpeedLinesProp.boolValue)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("speedLines"));
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("minSpeedToUseSpeedLines"));
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("speedLinesAmount"));
                            EditorGUI.indentLevel--;
                        }
                        break;
                    case "Camera":
                        EditorGUILayout.LabelField("CAMERA LOOK", EditorStyles.boldLabel);
                        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(8) });

                        SerializedProperty maxCameraAngleProp = playerSettings.FindPropertyRelative("maxCameraAngle");
                        if (maxCameraAngleProp.floatValue != 89.7f) EditorGUILayout.LabelField("WARNING: The maximum camera angle is highly recommended to be set to the maximum value, 89.7", EditorStyles.helpBox);

                        EditorGUILayout.PropertyField(maxCameraAngleProp);

                        EditorGUILayout.Space(10f);
                        EditorGUILayout.HelpBox("WARNING: The Player Sensitivity (Mouse & Controller) values will be overridden if you open the scene from the Main Menu. Check GameSettingsManager.cs for more information.", MessageType.Warning);
                        EditorGUILayout.Space(5f);

                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("sensitivityX"));
                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("sensitivityY"));
                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("invertYSensitivty"));
                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("controllerSensitivityX"));
                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("controllerSensitivityY"));
                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("invertYControllerSensitivty"));
                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("aimingSensitivityMultiplier"));

                        GUILayout.Space(10);
                        EditorGUILayout.LabelField("CAMERA", EditorStyles.boldLabel);
                        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(8) });

                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("normalFOV"));
                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("runningFOV"));

                        if (canWallRun)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("wallrunningFOV"));
                            EditorGUI.indentLevel--;
                        }
                        if (canDash)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("fovToAddOnDash"));
                            EditorGUI.indentLevel--;
                        }
                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("fadeFOVAmount"));
                        if (allowSlide)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("slidingCameraTiltAmount"));
                            EditorGUI.indentLevel--;
                        }
                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("cameraTiltTransitionSpeed"));
                        break;
                    case "Movement":
                        EditorGUILayout.LabelField("BASIC MOVEMENT INPUT SETTINGS", EditorStyles.boldLabel);
                        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(8) });

                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("autoRun"));
                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("alternateSprint"));
                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("alternateCrouch"));

                        SerializedProperty canRunBackwardsProp = playerSettings.FindPropertyRelative("canRunBackwards");
                        EditorGUILayout.PropertyField(canRunBackwardsProp);
                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("canRunSideways"));
                        SerializedProperty canRunWhileShootingProp = playerSettings.FindPropertyRelative("canRunWhileShooting");
                        EditorGUILayout.PropertyField(canRunWhileShootingProp);

                        if (!canRunBackwardsProp.boolValue || !canRunWhileShootingProp.boolValue)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("loseSpeedDeceleration"));
                            EditorGUI.indentLevel--;
                        }
                        GUILayout.Space(15);
                        EditorGUILayout.LabelField("BASIC MOVEMENT", EditorStyles.boldLabel);
                        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(8) });
                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("acceleration"));
                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("runSpeed"));
                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("walkSpeed"));

                        EditorGUILayout.PropertyField(allowCrouchProp);
                        if (allowCrouch)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("crouchSpeed"));
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("crouchTransitionSpeed"));
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("roofCheckDistance"));
                            EditorGUI.indentLevel--;
                        }
                        SerializedProperty maxSpeedAllowedProp = playerSettings.FindPropertyRelative("maxSpeedAllowed");
                        EditorGUILayout.PropertyField(maxSpeedAllowedProp);
                        if (maxSpeedAllowedProp.floatValue < myScript.RunSpeed) maxSpeedAllowedProp.floatValue = myScript.RunSpeed;


                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("whatIsGround"));
                        EditorGUILayout.BeginHorizontal();

                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("groundCheckDistance"));

                        if (GUILayout.Button(myScript.showCapsuleGroundCheckDebugInfo ? "Hide Ground Check Debug Info" : "Show Ground Check Debug Info"))
                        {
                            myScript.showCapsuleGroundCheckDebugInfo = !myScript.showCapsuleGroundCheckDebugInfo;
                            EditorUtility.SetDirty(myScript);
                        }

                        EditorGUILayout.EndHorizontal();
                        GUILayout.Space(5);

                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("controlsResponsiveness"));                        

                        break;
                    case "Sliding":
                        EditorGUILayout.LabelField("SLIDING", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(allowSlideProp);
                        if (allowSlide)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.LabelField("A new customizable variable has been unlocked in `CAMERA`.", EditorStyles.helpBox);
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("slideForce"));
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("allowMoveWhileSliding"));

                            // New slide tuning fields
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("slideDuration"));
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("slideBoostDuration"));
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("slideStopSpeed"));
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("slideSteerMultiplier"));

                            SerializedProperty applyFrictionForceOnSlidingProp = playerSettings.FindPropertyRelative("applyFrictionForceOnSliding");
                            bool applyFrictionForceOnSliding = applyFrictionForceOnSlidingProp.boolValue;

                            EditorGUILayout.PropertyField(applyFrictionForceOnSlidingProp);
                            if(applyFrictionForceOnSliding)
                            {
                                EditorGUI.indentLevel++;
                                EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("slideFrictionForceAmount"));
                                EditorGUI.indentLevel--;
                            }
                            EditorGUI.indentLevel--;
                        }

                        break;
                    case "Jumping":

                        EditorGUILayout.LabelField("JUMPING", EditorStyles.boldLabel);
                        
                        SerializedProperty allowJumpProp = playerSettings.FindPropertyRelative("allowJump");
                        EditorGUILayout.PropertyField(allowJumpProp);
                        if (allowJumpProp.boolValue)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(maxJumpsProp);
                            if(canWallRun)
                            {
                                EditorGUI.indentLevel++;
                                EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("resetJumpsOnWallrun"));
                                EditorGUI.indentLevel--;
                            }
                            if (canWallBounce)
                            {
                                EditorGUI.indentLevel++;
                                EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("resetJumpsOnWallBounce"));
                                EditorGUI.indentLevel--;
                            }
                            if (allowGrapple)
                            {
                                EditorGUI.indentLevel++;
                                EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("resetJumpsOnGrapple"));
                                EditorGUI.indentLevel--;
                            }
                            if (maxJumps > 1)
                            {
                                var doubleJumpResetsFallDamageProp = playerSettings.FindPropertyRelative("doubleJumpResetsFallDamage");

                                if (myScript.GetComponent<PlayerStats>().TakesFallDamage)
                                {
                                    EditorGUI.indentLevel++;
                                    EditorGUILayout.PropertyField(doubleJumpResetsFallDamageProp);
                                    EditorGUI.indentLevel--;
                                }
                                else
                                {
                                    doubleJumpResetsFallDamageProp.boolValue = false;
                                }

                                EditorGUI.indentLevel--;
                                SerializedProperty directionalJumpMethodProp = playerSettings.FindPropertyRelative("directionalJumpMethod");
                                EditorGUILayout.PropertyField(directionalJumpMethodProp);

                                if ((PlayerMovementSettings.DirectionalJumpMethod)directionalJumpMethodProp.enumValueIndex != PlayerMovementSettings.DirectionalJumpMethod.None)
                                {
                                    EditorGUI.indentLevel++;
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("directionalJumpForce"));
                                    EditorGUI.indentLevel--;
                                }
                            }
                            
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("jumpForce"));
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("controlAirborne"));
                            if (allowCrouch)
                            {
                                EditorGUI.indentLevel++;
                                EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("allowCrouchWhileJumping"));
                                EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("canJumpWhileCrouching"));
                                EditorGUI.indentLevel--;
                            }
                            
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("jumpCooldown"));
                            var coyoteJumpTimeProp = playerSettings.FindPropertyRelative("coyoteJumpTime");
                            if (coyoteJumpTimeProp.floatValue == 0f)
                                EditorGUILayout.LabelField("Coyote Jump won�t be applied since the value is equal to 0", EditorStyles.helpBox);

                            EditorGUILayout.PropertyField(coyoteJumpTimeProp);
                            EditorGUI.indentLevel--;
                        }
                        break;

                    case "Aim assist":

                        EditorGUILayout.LabelField("AIM ASSIST", EditorStyles.boldLabel);
                        SerializedProperty applyAimAssistProp = playerSettings.FindPropertyRelative("applyAimAssist");
                        EditorGUILayout.PropertyField(applyAimAssistProp);
                        if (applyAimAssistProp.boolValue)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("maximumDistanceToAssistAim"));
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("aimAssistSpeed"));
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("aimAssistActivationAngle"));
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("targetLockDuration"));
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("assistOnlyWhenAiming"));
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("assistOnlyWithWeapons"));
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("aimAssistFalloffCurve"));
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("aimAssistHeightMultiplier"));
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("aimAssistDefaultHeightOffset"));
                            EditorGUI.indentLevel--;
                        }

                        break;
                    case "Stamina":
                        EditorGUILayout.LabelField("STAMINA", EditorStyles.boldLabel);

                        SerializedProperty usesStaminaProp = playerSettings.FindPropertyRelative("usesStamina");
                        EditorGUILayout.PropertyField(usesStaminaProp);

                        if (usesStaminaProp.boolValue)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("minStaminaRequiredToRun"));
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("maxStamina"));
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("staminaRegenMultiplier"));
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("staminaLossOnJump"));
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("staminaLossOnSlide"));
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("staminaLossOnDash"));
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("staminaSlider"));
                            EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("staminaSliderFadesOutIfFull"));
                            EditorGUI.indentLevel--;
                        }
                        break;
                    case "Advanced Movement":
                        EditorGUILayout.LabelField("ADVANCED MOVEMENT", EditorStyles.boldLabel);
                        EditorGUILayout.Space(5);

                        EditorGUI.indentLevel++;
                        EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
                        {
                            // Air Camera Influence foldout
                            showAirCameraInfluence = EditorGUILayout.Foldout(showAirCameraInfluence, "AIR CAMERA INFLUENCE (SOURCE-LIKE)", true);
                            if (showAirCameraInfluence)
                            {
                                EditorGUI.indentLevel++;
                                SerializedProperty allowAirCameraInfluenceProp = playerSettings.FindPropertyRelative("allowAirCameraInfluence");
                                bool allowAirCameraInfluence = allowAirCameraInfluenceProp.boolValue;
                                EditorGUILayout.PropertyField(allowAirCameraInfluenceProp);
                                if (allowAirCameraInfluence)
                                {
                                    EditorGUI.indentLevel++;
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("airCameraInfluenceAmount"));
                                    EditorGUI.indentLevel--;
                                }
                                EditorGUI.indentLevel--;
                            }
                        }
                        EditorGUILayout.EndVertical();

                        EditorGUILayout.Space(5);
                        EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
                        {
                            // Wall Run foldout
                            showWallRun = EditorGUILayout.Foldout(showWallRun, "WALL RUN", true);
                            if (showWallRun)
                            {
                                EditorGUI.indentLevel++;
                                EditorGUILayout.PropertyField(canWallRunProp);
                                if (canWallRun)
                                {
                                    EditorGUI.indentLevel++;
                                    EditorGUILayout.LabelField("NEW FEATURE AVAILABLE UNDER �CAMERA� SETTINGS", EditorStyles.helpBox);
                                    
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("whatIsWallRunWall"));
                                    SerializedProperty useGravityProp = playerSettings.FindPropertyRelative("useGravity");
                                    EditorGUILayout.PropertyField(useGravityProp);
                                    if (useGravityProp.boolValue)
                                    {
                                        EditorGUI.indentLevel++;
                                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("wallrunGravityCounterForce"));
                                        EditorGUI.indentLevel--;
                                    }
                                    
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("maxWallRunSpeed"));
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("normalWallJumpForce"));
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("upwardsWallJumpForce"));
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("stopWallRunningImpulse"));
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("wallMinimumHeight"));
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("wallrunCameraTiltAmount"));

                                    var cancelWallRunMethod = playerSettings.FindPropertyRelative("cancelWallRunMethod");
                                    PlayerMovementSettings.CancelWallRunMethod method = (PlayerMovementSettings.CancelWallRunMethod)cancelWallRunMethod.enumValueIndex;

                                    EditorGUILayout.PropertyField(cancelWallRunMethod);

                                    if (method == PlayerMovementSettings.CancelWallRunMethod.Timer)
                                    {
                                        EditorGUI.indentLevel++;
                                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("wallRunDuration"));
                                        EditorGUI.indentLevel--;
                                    }

                                    EditorGUI.indentLevel--;
                                }
                                EditorGUI.indentLevel--;
                            }
                        }
                        EditorGUILayout.EndVertical();

                        EditorGUILayout.Space(5);
                        EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
                        {
                            // Wall Bounce foldout
                            showWallBounce = EditorGUILayout.Foldout(showWallBounce, "WALL BOUNCE", true);
                            if (showWallBounce)
                            {
                                EditorGUI.indentLevel++;
                                EditorGUILayout.PropertyField(canWallBounceProp);
                                if (canWallBounce)
                                {
                                    EditorGUI.indentLevel++;
                                    if (maxJumps > 1) EditorGUILayout.LabelField("NEW FEATURE AVAILABLE UNDER `Jumping` SETTINGS ", EditorStyles.helpBox);
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("wallBounceForce"));
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("wallBounceUpwardsForce"));
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("oppositeWallDetectionDistance"));
                                    EditorGUI.indentLevel--;
                                }
                                EditorGUI.indentLevel--;
                            }
                        }
                        EditorGUILayout.EndVertical();

                        EditorGUILayout.Space(5);
                        EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
                        {
                            // Dashing foldout
                            showDashing = EditorGUILayout.Foldout(showDashing, "DASHING", true);
                            if (showDashing)
                            {
                                EditorGUI.indentLevel++;
                                if (canDash && !infiniteDashes) EditorGUILayout.LabelField("NEW FEATURE AVAILABLE UNDER �ASSIGNABLES� SETTINGS", EditorStyles.helpBox);

                                EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("canDash"));
                                
                                if (canDash)
                                {
                                    EditorGUI.indentLevel++;
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("dashMethod"));
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("infiniteDashes"));
                                    if (!infiniteDashes)
                                    {
                                        EditorGUI.indentLevel++;
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("amountOfDashes"));
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("dashCooldown"));    
                                        EditorGUI.indentLevel--;
                                    }
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("damageProtectionWhileDashing"));
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("dashForce"));
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("dashDuration"));
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("canShootWhileDashing"));
                                    EditorGUI.indentLevel--;
                                }

                                EditorGUI.indentLevel--;
                            }
                        }
                        EditorGUILayout.EndVertical();

                        EditorGUILayout.Space(5);
                        EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
                        {
                            // Grappling Hook foldout
                            showGrapplingHook = EditorGUILayout.Foldout(showGrapplingHook, "GRAPPLING HOOK", true);
                            if (showGrapplingHook)
                            {
                                EditorGUI.indentLevel++;
                                EditorGUILayout.PropertyField(allowGrappleProp);
                                if (allowGrappleProp.boolValue)
                                {
                                    EditorGUI.indentLevel++; 
                                    EditorGUILayout.LabelField("NEW SOUNDS AVAILABLE UNDER �Others� & �Jumping� SETTINGS", EditorStyles.helpBox);

                                    var grapplingHookMethod = playerSettings.FindPropertyRelative("grapplingHookMethod");
                                    PlayerMovementSettings.GrapplingHookMethod method = (PlayerMovementSettings.GrapplingHookMethod)grapplingHookMethod.enumValueIndex;

                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("maxGrappleDistance"));
                                    EditorGUILayout.PropertyField(grapplingHookMethod);

                                    if (method == PlayerMovementSettings.GrapplingHookMethod.Linear)
                                        EditorGUILayout.HelpBox("SUGGESTED VALUES: grappleForce = 100", MessageType.Info);
                                    else if (method == PlayerMovementSettings.GrapplingHookMethod.Swing)
                                        EditorGUILayout.HelpBox("SUGGESTED VALUES: grappleSpringForce = 4.5 | grappleDamper = 7", MessageType.Info);
                                    else // Combined
                                        EditorGUILayout.HelpBox("SUGGESTED VALUES: combinedGrapplePullForce = 20 | cameraInfluence = 10 | combinedGrappleMaxSpeed = 40 | combinedGrappleReelSpeed = 6", MessageType.Info);

                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("grappleRopeLength"));
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("grappleCooldown"));
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("distanceToBreakGrapple"));

                                    if(method != PlayerMovementSettings.GrapplingHookMethod.Swing)
                                    {
                                        EditorGUI.indentLevel++;
                                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("grappleForce"));
                                        EditorGUI.indentLevel--;
                                    }
                                    if (method == PlayerMovementSettings.GrapplingHookMethod.Combined)
                                    {
                                        EditorGUI.indentLevel++;
                                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("combinedGrapplePullForce"));
                                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("cameraInfluence"));
                                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("combinedGrappleMaxSpeed"));
                                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("combinedGrappleReelSpeed"));
                                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("combinedGrappleMinSwingSpeed"));
                                        EditorGUI.indentLevel--;
                                    }

                                    
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("grappleSpringForce"));
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("grappleDamper"));
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("drawDuration"));
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("ropeResolution"));
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("waveAmplitude"));
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("waveAmplitudeMitigation"));

                                    EditorGUI.indentLevel--;
                                }

                                EditorGUI.indentLevel--;
                            }
                        }
                        EditorGUILayout.EndVertical();


                        EditorGUILayout.Space(5);
                        EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
                        {
                            // Climbing foldout
                            showClimbing = EditorGUILayout.Foldout(showClimbing, "CLIMBING LADDERS", true);
                            if (showClimbing)
                            {
                                EditorGUI.indentLevel++;

                                SerializedProperty canClimbProp = playerSettings.FindPropertyRelative("canClimb");
                                EditorGUILayout.PropertyField(canClimbProp);

                                if (canClimbProp.boolValue)
                                {
                                    EditorGUI.indentLevel++;

                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("ladderMovementMode"));
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("maxLadderDetectionDistance"));
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("climbSpeed"));
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("topReachedUpperForce"));
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("topReachedForwardForce"));
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("allowVerticalLookWhileClimbing"));
                                    EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("hideWeaponWhileClimbing"));
                                    EditorGUI.indentLevel--;
                                }
                                EditorGUI.indentLevel--;
                            }
                        }
                        EditorGUILayout.EndVertical();
                        EditorGUI.indentLevel--;

                        break;
                    case "Footsteps":

                        EditorGUILayout.LabelField("FOOTSTEPS", EditorStyles.boldLabel);
                        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(8) });
                        SerializedProperty footstepsSettings = serializedObject.FindProperty("footstepsSettings");
                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("footstepVolume"));
                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("footstepSpeed"));
                        EditorGUILayout.Space(10);

                        EditorGUILayout.LabelField("FOOTSTEPS SOUNDS", EditorStyles.boldLabel);
                        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(8) });
                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("footstepSounds"));
                        break;

                    case "Others":
                        EditorGUILayout.LabelField("SOUNDS", EditorStyles.boldLabel);
                        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(8) });
                        
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("sounds"));
                        EditorGUI.indentLevel--;

                        GUILayout.Space(10);

                        EditorGUILayout.LabelField("EVENTS", EditorStyles.boldLabel);
                        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(8) });

                        EditorGUILayout.PropertyField(playerSettings.FindPropertyRelative("events"));
                        break;

                }
            }

            #endregion
            EditorGUILayout.Space(10f);
            serializedObject.ApplyModifiedProperties();

        }
    }
}
#endif