/// <summary>
/// This script belongs to cowsins� as a part of the cowsins� FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
using System;
using TMPro;
using UnityEngine.UI;
using System.Collections; 
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace cowsins
{
    /// <summary>
    /// Manage UI actions.
    /// This is still subject to change and optimize.
    /// </summary>
    public class UIController : MonoBehaviour
    {
        // REFERENCES
        [SerializeField] private PauseMenu pauseMenu;

        // HEALTH
        [Tooltip("Use image bars to display player statistics."), SerializeField] private bool barHealthDisplay;
        [Tooltip("Use text to display player statistics."), SerializeField] private bool numericHealthDisplay;
        [Tooltip("Slider that will display the health on screen"), SerializeField] private Slider healthSlider;
        [Tooltip("Slider that will display the shield on screen"), SerializeField] private Slider shieldSlider;
        [SerializeField, Tooltip("UI Element ( TMPro text ) that displays current and maximum health.")] private TextMeshProUGUI healthTextDisplay;
        [SerializeField, Tooltip("UI Element ( TMPro te�xt ) that displays current and maximum shield.")] private TextMeshProUGUI shieldTextDisplay;
        [Tooltip("This image shows damage and heal states visually on your screen, you can change the image" +
                "to any you like, but note that color will be overriden by the script"), SerializeField] private Image healthStatesEffect;
        [Tooltip(" Color of healthStatesEffect on different actions such as being hurt or healed"), SerializeField] private Color damageColor, healColor, coinCollectColor, xpCollectColor;
        [Tooltip("Time for the healthStatesEffect to fade out"), SerializeField] private float fadeOutTime;

        // INTERACTION
        [Tooltip("Attach the UI you want to use as your interaction UI"),  SerializeField] private GameObject interactUI;
        [SerializeField] private AudioClip allowedInteractionSFX;
        [Tooltip("Displays the current progress of your interaction"), SerializeField] private Image interactUIProgressDisplay;
        [SerializeField, Tooltip("UI that displays incompatible interactions.")] private GameObject forbiddenInteractionUI;
        [Tooltip("Inside the interact UI, this is the text that will display the object you want to interact with " +
           "or any custom method you would like." +
           "Do check Interactable.cs for that or, if you want, read our documentation or contact the cowsins support " +
           "in order to make custom interactions."), SerializeField] private TextMeshProUGUI interactText;

        // ATTACHMENTS
        [Tooltip("UI enabled when inspecting."), SerializeField] private CanvasGroup inspectionUI;
        [SerializeField] private float inspectionFadeDuration = 0.5f;
        [SerializeField, Tooltip("Text that displays the name of the current weapon when inspecting.")] private TextMeshProUGUI weaponDisplayText_AttachmentsUI;
        [SerializeField, Tooltip("Prefab of the UI element that represents an attachment on-screen when inspecting")] private GameObject attachmentDisplay_UIElement;
        [SerializeField, Tooltip("Group of attachments. Attachment UI elements are wrapped inside these.")]
        private GameObject
            barrels_AttachmentsGroup,
            scopes_AttachmentsGroup,
            stocks_AttachmentsGroup,
            grips_AttachmentsGroup,
            magazines_AttachmentsGroup,
            flashlights_AttachmentsGroup,
            lasers_AttachmentsGroup;
        [SerializeField, Tooltip("Color of an attachment UI element when it is equipped.")] private Color usingAttachmentColor;
        [SerializeField, Tooltip("Color of an attachment UI element when it is unequipped. This is the default color.")] private Color notUsingAttachmentColor;
        
        [Header("Attachment Layout Settings")]
        [SerializeField, Tooltip("Defines how attachment groups are displayed on screen when inspecting")] private AttachmentUILayoutMode attachmentLayoutMode = AttachmentUILayoutMode.WorldSpaceTracking;
        [SerializeField, Tooltip("Spacing between attachment groups")] private float attachmentGroupSpacing = 150f;
        [SerializeField, Tooltip("Starting position for attachment groups in the UI ( 0,0 = Top Left Corner )")] private Vector2 attachmentGroupStartPosition = Vector2.zero;
        [SerializeField, Tooltip("Spacing direction for Vertical layout. True = downwards, False = upwards")] private bool verticalSpacingDown = true;

        // WEAPON
        [Tooltip("Attach the appropriate UI here"), SerializeField] private TextMeshProUGUI bulletsUI, magazineUI, reloadUI, lowAmmoUI;
        [Tooltip("Image that represents heat levels of your overheating weapon"), SerializeField] private Image overheatUI;
        [Tooltip("Display an icon of your current weapon"), SerializeField] private Image currentWeaponDisplay;
        [Tooltip(" Attach the CanvasGroup that contains the inventory"), SerializeField] private CanvasGroup inventoryContainer;
        [SerializeField] private WeaponsInventoryUISlot inventoryUISlot;
        [SerializeField] private Crosshair crosshair;

        // DASHING
        [SerializeField, Tooltip("Contains dashUIElements in game.")] private Transform dashUIContainer;
        [SerializeField, Tooltip("Displays a dash slot in-game. This keeps stored at dashUIContainer during runtime.")] private Transform dashUIElement;

        // EXPERIENCE
        [SerializeField] private Image xpImage;
        [SerializeField] private TextMeshProUGUI currentLevel, nextLevel;
        [SerializeField] private float lerpXpSpeed;

        // OTHERS
        [SerializeField] private GameObject coinsUI;
        [SerializeField] private TextMeshProUGUI coinsText;
        [SerializeField] private Hitmarker hitmarker;

        // UI EVENTS
        [Tooltip("An object showing death events will be displayed on kill"), SerializeField] private bool displayEvents;
        [Tooltip("UI element which contains the killfeed. Where the kilfeed object will be instantiated and parented to"), SerializeField]
        private GameObject killfeedContainer;
        [Tooltip("Object to spawn"), SerializeField] private GameObject killfeedObject;
        [SerializeField] private string killfeedMessage;
        [Tooltip("Add a pop up showing the damage that has been dealt. Recommendation: use the already made pop up included in this package. "), SerializeField]
        private GameObject damagePopUp;
        [Tooltip("Horizontal randomness variation"), SerializeField] private float xVariation;

        private Coroutine inspectFadeRoutine;
        private Coroutine inventoryFadeCoroutine;
        private PlayerDependencies playerDependencies;
        private IPlayerMovementEventsProvider playerEvents;
        private IPlayerStatsProvider playerStats;
        private IPlayerStatsEventsProvider playerStatsEvents;
        private IWeaponReferenceProvider weaponController;
        private IWeaponEventsProvider weaponEvents;
        private IInteractEventsProvider interactEvents;

        // GETTERS
        public Crosshair Crosshair => crosshair;
        public CrosshairShape crosshairShape {  get; private set; }
        public WeaponsInventoryUISlot[] weaponsInventoryUISlots { get; private set; }

        private List<GameObject> dashElements; // Stores the UI Elements required to display the current dashes amount
        public Action<float, float> healthDisplayMethod;
        public static UIController Instance { get; set; }

        private void Awake()
        {
            Instance = this;
            crosshairShape = crosshair?.GetComponent<CrosshairShape>();
            DisableWeaponUI();
        }
        private void Start()
        {
            if (!CoinManager.Instance.useCoins && coinsUI != null) coinsUI.SetActive(false);
            if(ExperienceManager.Instance.useExperience) UpdateXP(false);
            StartFadingInventory();
            if(inspectionUI) inspectionUI.alpha = 0;

            // Register Pool
            if (damagePopUp) PoolManager.Instance.RegisterPool(damagePopUp, PoolManager.Instance.DamagePopUpsSize);

            LockMouse();
        }
 
        private void Update()
        {
            // Ensure the cursor is locked at the start of the game
            if (Time.timeSinceLevelLoad < 0.1f && !PauseMenu.isPaused) LockMouse();
        }

        // INVENTORY /////////////////////////////////////////////////////////////////////////////////////////
        private void StartFadingInventory()
        {
            if(inventoryContainer != null) inventoryContainer.alpha = 1f;

            if (inventoryFadeCoroutine != null) StopCoroutine(inventoryFadeCoroutine);
            inventoryFadeCoroutine = StartCoroutine(FadeInventory());
        }

        private IEnumerator FadeInventory()
        {
            if (inventoryContainer == null) yield break;

            while (inventoryContainer.alpha > 0)
            {
                inventoryContainer.alpha -= Time.deltaTime;
                yield return null;
            }
        }

        // HEALTH SYSTEM /////////////////////////////////////////////////////////////////////////////////////////
        private void UpdateHealthUI(float health, float shield, bool damaged)
        {
            healthDisplayMethod?.Invoke(health, shield);

            Color colorSelected = damaged ? damageColor : healColor;
            if (healthStatesEffect != null) healthStatesEffect.color = colorSelected;

            StartCoroutine(ReduceHealthStatesAlpha());
        }

        private IEnumerator ReduceHealthStatesAlpha()
        {
            if (healthStatesEffect == null) yield break;

            Color currentColor = healthStatesEffect.color;
            while (currentColor.a > 0)
            {
                healthStatesEffect.color -= new Color(0, 0, 0, Time.deltaTime * fadeOutTime);
                yield return null;
            }
        }
        private void InitializeHealthUI(IPlayerStatsProvider playerStats)
        {
            if (healthSlider != null)
            {
                healthSlider.maxValue = playerStats.MaxHealth;
            }
            if (shieldSlider != null)
            {
                shieldSlider.maxValue = playerStats.MaxShield;
            }

            healthDisplayMethod?.Invoke(playerStats.Health, playerStats.Shield);

            if (playerStats.Shield == 0) shieldSlider.gameObject.SetActive(false);
        }

        private void BarHealthDisplayMethod(float health, float shield)
        {
            if (healthSlider != null)
                healthSlider.value = health;

            if (shieldSlider != null)
                shieldSlider.value = shield;
        }
        private void NumericHealthDisplayMethod(float health, float shield)
        {
            if (healthTextDisplay != null)
            {
                healthTextDisplay.text = health > 0 && health <= 1 ? 1.ToString("F0") : health.ToString("F0");
            }

            if (shieldTextDisplay != null)
                shieldTextDisplay.text = shield.ToString("F0");
        }


        // COINS & XP /////////////////////////////////////////////////////////////////////////////////////////
        private void UpdateCoinsPanel()
        {
            if(healthStatesEffect != null) healthStatesEffect.color = coinCollectColor;
            StartCoroutine(ReduceHealthStatesAlpha());
        }

        private void UpdateXPPanel()
        {
            if (healthStatesEffect != null) healthStatesEffect.color = xpCollectColor;
            StartCoroutine(ReduceHealthStatesAlpha());
        }

        public void UpdateXP(bool updatePanel)
        {
            int playerLevel = ExperienceManager.Instance.playerLevel;

            if(currentLevel != null) currentLevel.text = (playerLevel + 1).ToString();
            if(nextLevel != null) nextLevel.text = (playerLevel + 2).ToString();

            // Stop the previous fill coroutine to prevent overlap
            StopCoroutine(FillExperienceBar());
            StartCoroutine(FillExperienceBar());

            if (updatePanel) UpdateXPPanel();
        }

        private void UpdateCoins(int amount, bool updateCoinsPanel)
        {
            if(coinsText != null) coinsText.text = CoinManager.Instance.coins.ToString();
            if (updateCoinsPanel) UpdateCoinsPanel();
        }

        private IEnumerator FillExperienceBar()
        {
            if (xpImage == null) yield break;

            float targetXp = ExperienceManager.Instance.GetCurrentExperience() / ExperienceManager.Instance.experienceRequirements[ExperienceManager.Instance.playerLevel];
            xpImage.fillAmount = 0; 
            while (xpImage.fillAmount < targetXp)
            {
                xpImage.fillAmount = Mathf.Lerp(xpImage.fillAmount, targetXp, lerpXpSpeed * Time.deltaTime);
                yield return null;
            }
        }

        // INTERACTION /////////////////////////////////////////////////////////////////////////////////////////
        private void AllowedInteraction(string displayText)
        {
            if (forbiddenInteractionUI != null) forbiddenInteractionUI.SetActive(false);

            if (interactUI != null)
            {
                interactUI.gameObject.SetActive(true);
                if(interactText != null) interactText.text = displayText;
                interactUI.GetComponent<Animation>().Play();

                // Adjust the width of the background based on the length of the displayText
                RectTransform imageRect = interactUI.GetComponentInChildren<Image>().GetComponent<RectTransform>();
                string plainText = Regex.Replace(displayText, "<.*?>", string.Empty);
                float textLength = plainText.Length;
                imageRect.sizeDelta = new Vector2(100 + textLength * 10, imageRect.sizeDelta.y);
            }

            SoundManager.Instance.PlaySound(allowedInteractionSFX, 0,0, false);
        }

        private void ForbiddenInteraction()
        {
            if (forbiddenInteractionUI != null) forbiddenInteractionUI.SetActive(true);
            if(interactUI != null) interactUI.gameObject.SetActive(false);
        }

        private void DisableInteractionUI()
        {
            if (forbiddenInteractionUI != null) forbiddenInteractionUI.SetActive(false);
            if (interactUI != null) interactUI.gameObject.SetActive(false);
        }
        private void InteractionProgressUpdate(float value)
        {
            if (interactUIProgressDisplay != null)
            {
                interactUIProgressDisplay.gameObject.SetActive(true);
                interactUIProgressDisplay.fillAmount = value;
            }
        }
        private void FinishInteraction()
        {
            if (interactUIProgressDisplay != null) interactUIProgressDisplay.gameObject.SetActive(false);
        }

        // UI EVENTS /////////////////////////////////////////////////////////////////////////////////////////
        public void AddKillfeed(string name)
        {
            if(killfeedContainer == null || killfeedObject == null) return;

            GameObject killfeed = PoolManager.Instance.GetFromPool(killfeedObject, transform.position, Quaternion.identity);
            killfeed.transform.SetParent(killfeedContainer.transform);
            killfeed.transform.GetChild(0).Find("Text").GetComponent<TextMeshProUGUI>().text = $"{killfeedMessage} {name}";
        }

        public void Hitmarker(bool headshot, bool damagePopUp, Vector3 position, float damage)
        {
            if (hitmarker == null) return;
            hitmarker.Play(headshot);
            if (damagePopUp) AddDamagePopUp(position, damage);
        }

        public void AddDamagePopUp(Vector3 position, float damage)
        {
            float xRand = UnityEngine.Random.Range(-xVariation, xVariation);
            Vector3 posculatedPos = position + new Vector3(xRand, 0, 0);
            GameObject popup = PoolManager.Instance.GetFromPool(damagePopUp, posculatedPos, Quaternion.identity, .4f);
            TMP_Text text = popup.transform.GetChild(0).GetComponent<TMP_Text>();
            if (damage / Mathf.FloorToInt(damage) == 1)
                text.text = damage.ToString("F0");
            else
                text.text = damage.ToString("F1");
        }

        // WEAPON INVENTORY UI /////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Procedurally generate the Inventory UI depending on your needs
        /// </summary>
        private void CreateInventoryUI(int inventorySize)
        {
            // Adjust the inventory size 
            weaponsInventoryUISlots = new WeaponsInventoryUISlot[inventorySize];

            if (inventoryContainer == null || inventoryUISlot == null) return;

            foreach (Transform child in inventoryContainer.transform) Destroy(child.gameObject);

            int i = 0; // Control variable
            while (i < inventorySize)
            {
                // Load the slot, instantiate it and set it to the slots array
                WeaponsInventoryUISlot slot = Instantiate(inventoryUISlot, Vector3.zero, Quaternion.identity, inventoryContainer.transform) as WeaponsInventoryUISlot;
                slot.Initialize(i);
                weaponsInventoryUISlots[i] = slot;
                i++;
            }
        }

        public void SelectInventoryUISlot()
        {
            int selectedIndex = weaponController.CurrentWeaponIndex;
            if (selectedIndex >= weaponsInventoryUISlots.Length) return;

            foreach (WeaponsInventoryUISlot slot in weaponsInventoryUISlots)
            {
                slot?.Deselect();
            }
            weaponsInventoryUISlots[selectedIndex]?.Select();
        }
        public void SetInventoryUISlotWeapon(int selectedIndex, Weapon_SO newWeapon)
        {
            weaponsInventoryUISlots[selectedIndex]?.SetWeapon(newWeapon);
            // update crosshair if the inventory slot being updated corresponds to the currently equipped weapon.
            if (weaponController != null && weaponController.CurrentWeaponIndex == selectedIndex)
            {
                crosshairShape?.SetCrosshair(newWeapon?.crosshairParts);
            }
        }

        // INSPECT   /////////////////////////////////////////////////////////////////////////////////////////
        private void StartRealtimeInspection(bool displayCurrentAttachmentsOnly)
        {
            UnlockMouse();
            GenerateInspectionUI(displayCurrentAttachmentsOnly);

            StartFadeCoroutine(1f);
        }

        private void StopInspection()
        {
            LockMouse();
            StartFadeCoroutine(0f);
        }

        private void StartFadeCoroutine(float targetAlpha)
        {
            if (inspectFadeRoutine != null)
                StopCoroutine(inspectFadeRoutine);

            inspectFadeRoutine = StartCoroutine(FadeInspectionUI(targetAlpha));
        }

        private IEnumerator FadeInspectionUI(float targetAlpha)
        {
            if(inspectionUI == null) yield break;

            inspectionUI.gameObject.SetActive(true);

            float startAlpha = inspectionUI.alpha;
            float elapsedTime = 0f;

            while (elapsedTime < inspectionFadeDuration)
            {
                elapsedTime += Time.deltaTime;
                inspectionUI.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / inspectionFadeDuration);
                yield return null;
            }

            inspectionUI.alpha = targetAlpha;

            if (Mathf.Approximately(targetAlpha, 0f))
                inspectionUI.gameObject.SetActive(false);
        }

        private void GenerateInspectionUI(bool displayCurrentAttachments)
        {
            IWeaponReferenceProvider wRef = playerDependencies.WeaponReference;
            WeaponIdentification weapon = wRef.Id;
            if(weaponDisplayText_AttachmentsUI != null) weaponDisplayText_AttachmentsUI.text = wRef.Weapon._name;

            CleanAttachmentGroup(barrels_AttachmentsGroup);
            CleanAttachmentGroup(scopes_AttachmentsGroup);
            CleanAttachmentGroup(stocks_AttachmentsGroup);
            CleanAttachmentGroup(grips_AttachmentsGroup);
            CleanAttachmentGroup(magazines_AttachmentsGroup);
            CleanAttachmentGroup(flashlights_AttachmentsGroup);
            CleanAttachmentGroup(lasers_AttachmentsGroup);

            WeaponIdentification wID = wRef.Id;
            AttachmentStateManager state = wID.AttachmentState;
            CompatibleAttachments compAttachments = weapon.compatibleAttachments;

            int visibleGroupIndex = 0;
            visibleGroupIndex = GenerateAttachmentGroup(displayCurrentAttachments, compAttachments.GetCompatible(AttachmentType.Barrel), barrels_AttachmentsGroup, state.GetCurrent(AttachmentType.Barrel), state.GetDefault(AttachmentType.Barrel), visibleGroupIndex);
            visibleGroupIndex = GenerateAttachmentGroup(displayCurrentAttachments, compAttachments.GetCompatible(AttachmentType.Scope), scopes_AttachmentsGroup, state.GetCurrent(AttachmentType.Scope), state.GetDefault(AttachmentType.Scope), visibleGroupIndex);
            visibleGroupIndex = GenerateAttachmentGroup(displayCurrentAttachments, compAttachments.GetCompatible(AttachmentType.Stock), stocks_AttachmentsGroup, state.GetCurrent(AttachmentType.Stock), state.GetDefault(AttachmentType.Stock), visibleGroupIndex);
            visibleGroupIndex = GenerateAttachmentGroup(displayCurrentAttachments, compAttachments.GetCompatible(AttachmentType.Grip), grips_AttachmentsGroup, state.GetCurrent(AttachmentType.Grip), state.GetDefault(AttachmentType.Grip), visibleGroupIndex);
            visibleGroupIndex = GenerateAttachmentGroup(displayCurrentAttachments, compAttachments.GetCompatible(AttachmentType.Magazine), magazines_AttachmentsGroup, state.GetCurrent(AttachmentType.Magazine), state.GetDefault(AttachmentType.Magazine), visibleGroupIndex);
            visibleGroupIndex = GenerateAttachmentGroup(displayCurrentAttachments, compAttachments.GetCompatible(AttachmentType.Flashlight), flashlights_AttachmentsGroup, state.GetCurrent(AttachmentType.Flashlight), state.GetDefault(AttachmentType.Flashlight), visibleGroupIndex);
            visibleGroupIndex = GenerateAttachmentGroup(displayCurrentAttachments, compAttachments.GetCompatible(AttachmentType.Laser), lasers_AttachmentsGroup, state.GetCurrent(AttachmentType.Laser), state.GetDefault(AttachmentType.Laser), visibleGroupIndex);
        }

        private int GenerateAttachmentGroup(bool displayCurrentAttachments, IReadOnlyList<Attachment> attachments, GameObject attachmentsGroup, Attachment atc, Attachment defaultAttachment, int groupIndex)
        {
            if (attachmentDisplay_UIElement == null) return groupIndex;

            if (attachments.Count == 0 || displayCurrentAttachments && atc == null)
            {
                attachmentsGroup.SetActive(false);
                return groupIndex;
            }
            AttachmentGroupUI atcG = attachmentsGroup.GetComponent<AttachmentGroupUI>();
            if (atc != null)
                atcG.target = atc.transform;
            else if (attachments[0] != null)
                atcG.target = attachments[0].transform;
            
            // Configure layout strategy
            atcG.groupIndex = groupIndex;
            atcG.SetLayoutStrategy(attachmentLayoutMode, attachmentGroupSpacing, attachmentGroupStartPosition, verticalSpacingDown);

            attachmentsGroup.SetActive(true);
            for (int i = 0; i < attachments.Count; i++)
            {
                if (attachments[i] == defaultAttachment || displayCurrentAttachments && attachments[i] != atc) continue; // Do not add default attachments to the UI 
                GameObject display = Instantiate(attachmentDisplay_UIElement, attachmentsGroup.transform);
                AttachmentUIElement disp = display.GetComponent<AttachmentUIElement>();

                if (attachments[i].attachmentIdentifier?.icon != null)
                    disp.SetIcon(attachments[i].attachmentIdentifier.icon);
                disp.assignedColor = usingAttachmentColor;
                disp.unAssignedColor = notUsingAttachmentColor;
                disp.DeselectAll(atc);
                if (attachments[i] == atc)
                    disp.SelectAsAssigned();
                disp.atc = attachments[i];
                disp.id = i;
                display.SetActive(false);
            }
            
            return groupIndex + 1;
        }

        private void CleanAttachmentGroup(GameObject attachmentsGroup)
        {
            if(attachmentsGroup == null) return;

            for (int i = 1; i < attachmentsGroup.transform.childCount; i++)
            {
                Destroy(attachmentsGroup.transform.GetChild(i).gameObject);
            }
        }
        // MOVEMENT    ////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Draws the dash UI 
        /// </summary>
        private void DrawDashUI(int amountOfDashes)
        {
            if (dashUIContainer == null || dashUIElement == null) return;   

            dashElements = new List<GameObject>(amountOfDashes);
            for (int i = 0; i < amountOfDashes; i++)
            {
                var uiElement = Instantiate(dashUIElement.gameObject, dashUIContainer);
                dashElements.Add(uiElement);
            }
        }

        private void GainDash(int dashIndex)
        {
            if (dashElements == null || dashElements.Count <= 0) return;

            // Adapt the index since lists begin at 0, not 1.
            dashIndex--;
            if (dashIndex >= 0 && dashIndex < dashElements.Count)
            {
                dashElements[dashIndex].SetActive(true);
            }
        }

        private void DashUsed(int dashIndex)
        {
            if(dashElements == null || dashElements.Count <= 0) return;

            if (dashIndex >= 0 && dashIndex < dashElements.Count)
            {
                dashElements[dashIndex].SetActive(false);
            }
        }

        // WEAPON    /////////////////////////////////////////////////////////////////////////////////////////

        private void ConfigureReloadUIVisibility(bool enable, bool useOverheat)
        {
            bulletsUI?.gameObject.SetActive(enable);
            magazineUI?.gameObject.SetActive(enable);
            overheatUI?.transform.parent.gameObject.SetActive(useOverheat);
        }

        private void UpdateHeatRatio(float heatRatio)
        {
            if(overheatUI != null) overheatUI.fillAmount = heatRatio;
        }
        private void UpdateBullets(int bullets, int mag, bool activeReloadUI, bool activeLowAmmoUI)
        {
            bulletsUI?.SetText("{0}", bullets);
            magazineUI?.SetText("{0}", mag);
            reloadUI?.gameObject.SetActive(activeReloadUI);
            lowAmmoUI?.gameObject.SetActive(activeLowAmmoUI);
        }

        private void UpdateWeaponReloadInfo(bool autoReload)
        {
            Weapon_SO weapon = weaponController.Weapon;
            WeaponIdentification id = weaponController.Id;

            if (weapon.reloadStyle == ReloadingStyle.defaultReload)
            {
                if (!weapon.infiniteBullets)
                {
                    bool activeReloadUI = id.bulletsLeftInMagazine == 0 && !autoReload && !weapon.infiniteBullets;
                    bool activeLowAmmoUI = id.bulletsLeftInMagazine < id.magazineSize / 3.5f && id.bulletsLeftInMagazine > 0;
                    // Set different display settings for each shoot style 
                    if (weapon.limitedMagazines)
                    {
                        UpdateBullets(id.bulletsLeftInMagazine, id.totalBullets, activeReloadUI, activeLowAmmoUI);
                    }
                    else
                    {
                        UpdateBullets(id.bulletsLeftInMagazine, id.magazineSize, activeReloadUI, activeLowAmmoUI);
                    }
                }
                else
                {
                    UpdateBullets(id.bulletsLeftInMagazine, id.totalBullets, false, false);
                }
            }
            else
            {
                UpdateHeatRatio(id.heatRatio);
            }
        }

        private void OnUnholster(bool autoReload, bool prop2)
        {
            Weapon_SO weapon = weaponController.Weapon;

            EnableDisplay();
            SetWeaponDisplay(weapon);
            UpdateWeaponReloadInfo(autoReload);
            crosshairShape?.SetCrosshair(weapon.crosshairParts);
        }

        private void DisableWeaponUI()
        {
            overheatUI?.transform.parent.gameObject.SetActive(false);
            bulletsUI?.gameObject.SetActive(false);
            magazineUI?.gameObject.SetActive(false);
            currentWeaponDisplay?.gameObject.SetActive(false);
            reloadUI?.gameObject.SetActive(false);
            lowAmmoUI?.gameObject.SetActive(false);
        }

        private void SetWeaponDisplay(Weapon_SO weapon) { if(currentWeaponDisplay != null) currentWeaponDisplay.sprite = weapon.icon; }

        private void EnableDisplay() => currentWeaponDisplay?.gameObject.SetActive(true);

        // MOUSE VISIBILITY

        public void UnlockMouse() => SetMouseLockState(false);

        public void LockMouse()
        {
            if (PauseMenu.isPaused) return;
            SetMouseLockState(true);
        }

        public void SetMouseLockState(bool isLocked)
        {
            Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !isLocked;
        }

        private void ResetCrosshairToDefault() => crosshairShape?.ResetCrosshairToDefault();

        private void OnEnable()
        {
            UIEvents.onExperienceCollected += UpdateXP;
            if (barHealthDisplay) healthDisplayMethod += BarHealthDisplayMethod;
            if (numericHealthDisplay) healthDisplayMethod += NumericHealthDisplayMethod;
            UIEvents.onEnemyHit += Hitmarker;
            UIEvents.onEnemyKilled += AddKillfeed;
            UIEvents.onCoinsChange += UpdateCoins;

            if(interactUI != null) interactUI.SetActive(false);

            pauseMenu.OnPause += UnlockMouse;
            pauseMenu.OnUnPause += LockMouse;
        }
        private void OnDisable()
        {
            healthDisplayMethod = null;
            UIEvents.onEnemyHit -= Hitmarker;
            UIEvents.onEnemyKilled -= AddKillfeed;
            UIEvents.onCoinsChange -= UpdateCoins;
            UIEvents.onExperienceCollected -= UpdateXP;

            pauseMenu.OnPause -= UnlockMouse;
            pauseMenu.OnUnPause -= LockMouse;
        }
        public void Initialize(PlayerDependencies dependencies)
        {
            this.playerDependencies = dependencies;
            this.playerEvents = playerDependencies.PlayerMovementEvents;
            this.playerStats = playerDependencies.PlayerStats;
            this.playerStatsEvents = playerDependencies.PlayerStatsEvents;
            this.weaponController = playerDependencies.WeaponReference; 
            this.weaponEvents = playerDependencies.WeaponEvents;
            this.interactEvents = playerDependencies.InteractEvents;

            // PLAYER EVENTS
            playerEvents.Events.OnInitializeDash.AddListener(DrawDashUI);
            playerEvents.Events.OnDashUsed.AddListener(DashUsed);
            playerEvents.Events.OnDashGained.AddListener(GainDash);

            // WEAPON EVENTS
            weaponEvents.Events.OnUnholster.AddListener(OnUnholster);
            weaponEvents.Events.OnSwitchingWeapon.AddListener(StartFadingInventory);
            weaponEvents.Events.OnUnselectingWeapon.AddListener(DisableWeaponUI);
            weaponEvents.Events.OnReleaseWeapon.AddListener(DisableWeaponUI);
            weaponEvents.Events.OnAmmoChanged.AddListener(UpdateWeaponReloadInfo);
            weaponEvents.Events.OnSelectWeapon.AddListener(SelectInventoryUISlot);
            weaponEvents.Events.OnWeaponInventoryChanged.AddListener(SetInventoryUISlotWeapon);
            weaponEvents.Events.OnInitializeWeaponSystem.AddListener(CreateInventoryUI);
            weaponEvents.Events.OnReloadUIChanged.AddListener(ConfigureReloadUIVisibility);

            // INTERACT EVENTS
            interactEvents.Events.OnDrop.AddListener(ResetCrosshairToDefault);
            interactEvents.Events.OnStartRealtimeInspection.AddListener(StartRealtimeInspection);
            interactEvents.Events.OnStopInspect.AddListener(StopInspection);
            interactEvents.Events.OnInteractionProgressChanged.AddListener(InteractionProgressUpdate);
            interactEvents.Events.OnFinishInteraction.AddListener(FinishInteraction);
            interactEvents.Events.OnFinishInteraction.AddListener(DisableInteractionUI);
            interactEvents.Events.OnDisableInteraction.AddListener(DisableInteractionUI);
            interactEvents.Events.OnForbiddenInteraction.AddListener(ForbiddenInteraction);
            interactEvents.Events.OnAllowedInteraction.AddListener(AllowedInteraction);
            interactEvents.Events.OnInspectionUIRefreshRequested.AddListener(GenerateInspectionUI);

            // HEALTH EVENTS
            playerStatsEvents.Events.OnHealthChanged.AddListener(UpdateHealthUI);
            playerStatsEvents.Events.OnInitializeHealth.AddListener(InitializeHealthUI);
        }
        private void OnDestroy()
        {
            // PLAYER EVENTS
            playerEvents.Events.OnInitializeDash.RemoveListener(DrawDashUI);
            playerEvents.Events.OnDashUsed.RemoveListener(DashUsed);
            playerEvents.Events.OnDashGained.RemoveListener(GainDash);

            // WEAPON EVENTS
            weaponEvents.Events.OnUnholster.RemoveListener(OnUnholster);
            weaponEvents.Events.OnSwitchingWeapon.RemoveListener(StartFadingInventory);
            weaponEvents.Events.OnUnselectingWeapon.RemoveListener(DisableWeaponUI);
            weaponEvents.Events.OnReleaseWeapon.RemoveListener(DisableWeaponUI);
            weaponEvents.Events.OnAmmoChanged.RemoveListener(UpdateWeaponReloadInfo);
            weaponEvents.Events.OnWeaponInventoryChanged.RemoveListener(SetInventoryUISlotWeapon);
            weaponEvents.Events.OnSelectWeapon.RemoveListener(SelectInventoryUISlot);
            weaponEvents.Events.OnInitializeWeaponSystem.RemoveListener(CreateInventoryUI);
            weaponEvents.Events.OnReloadUIChanged.RemoveListener(ConfigureReloadUIVisibility);

            // INTERACT EVENTS
            interactEvents.Events.OnDrop.RemoveListener(ResetCrosshairToDefault);
            interactEvents.Events.OnStartRealtimeInspection.RemoveListener(StartRealtimeInspection);
            interactEvents.Events.OnStopInspect.RemoveListener(StopInspection);
            interactEvents.Events.OnInteractionProgressChanged.RemoveListener(InteractionProgressUpdate);
            interactEvents.Events.OnFinishInteraction.RemoveListener(FinishInteraction);
            interactEvents.Events.OnFinishInteraction.RemoveListener(DisableInteractionUI);
            interactEvents.Events.OnDisableInteraction.RemoveListener(DisableInteractionUI);
            interactEvents.Events.OnForbiddenInteraction.RemoveListener(ForbiddenInteraction);
            interactEvents.Events.OnAllowedInteraction.RemoveListener(AllowedInteraction);
            interactEvents.Events.OnInspectionUIRefreshRequested.RemoveListener(GenerateInspectionUI);

            // HEALTH EVENTS
            playerStatsEvents.Events.OnHealthChanged.RemoveListener(UpdateHealthUI);
            playerStatsEvents.Events.OnInitializeHealth.RemoveListener(InitializeHealthUI);
        }
    }
}