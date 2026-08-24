using cowsins;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class StaminaBehaviour
{
    private PlayerMovementSettings playerSettings;

    private float currentStamina;
    private float lastStaminaValue = -1f; // Cache last Stamina Value to only update UI when necessary

    private MovementContext context;

    private InputManager inputManager;
    private IPlayerMovementStateProvider playerMovement;
    private IPlayerMovementEventsProvider playerEvents;
    private IPlayerStatsProvider playerStatsProvider;
    private IPlayerControlProvider playerControl;
    private Rigidbody rb;

    public StaminaBehaviour(MovementContext context)
    {
        this.playerSettings = context.Settings;
        this.context = context;

        ResetStamina();

        this.inputManager = context.InputManager;
        playerControl = context.Dependencies.PlayerControl;
        playerStatsProvider = context.Dependencies.PlayerStats;
        playerMovement = context.Dependencies.PlayerMovementState;
        playerEvents = context.Dependencies.PlayerMovementEvents;
        rb = context.Rigidbody;

        playerEvents.Events.OnSlideStart.AddListener(ConsumeSlideStamina);
        playerEvents.Events.OnDashStart.AddListener(ConsumeDashStamina);
        playerEvents.Events.OnJump.AddListener(ConsumeJumpStamina);
    }

    public void Dispose()
    {
        playerEvents.Events.OnSlideStart.RemoveListener(ConsumeSlideStamina);
        playerEvents.Events.OnDashStart.RemoveListener(ConsumeDashStamina);
        playerEvents.Events.OnJump.RemoveListener(ConsumeJumpStamina);
    }

    public void Tick()
    {
        if (!CanExecute()) return;

        float deltaTime = Time.deltaTime;

        float inputX = inputManager.X;
        float inputY = inputManager.Y;

        float oldStamina = currentStamina;

        // Check if stamina is above threshold
        if (currentStamina >= playerSettings.minStaminaRequiredToRun)
        {
            context.EnoughStaminaToRun = true;
            context.EnoughStaminaToJump = true;
        }

        // Regenerate stamina
        if (currentStamina < playerSettings.maxStamina)
        {
            bool isIdle = inputX == 0 && inputY == 0;
            bool allowRegen = playerMovement.CurrentSpeed <= playerMovement.WalkSpeed || playerMovement.CurrentSpeed < playerMovement.RunSpeed;

            if (allowRegen)
                currentStamina += deltaTime * playerSettings.staminaRegenMultiplier;
        }

        // Drain stamina while running
        float horizontalSpeed = new Vector3(rb.velocity.x, 0f, rb.velocity.z).magnitude;
        if (playerMovement.CurrentSpeed == playerMovement.RunSpeed && context.EnoughStaminaToRun && !playerMovement.IsWallRunning && horizontalSpeed > 0.1f)
        {
            ConsumeStamina(deltaTime);
        }

        // Clamp
        currentStamina = Mathf.Clamp(currentStamina, 0f, playerSettings.maxStamina);

        UnityEngine.UI.Slider slider = playerSettings.staminaSlider;
        if (slider == null) return;

        if(playerSettings.staminaSliderFadesOutIfFull) slider.gameObject.SetActive(oldStamina != currentStamina);

        // Update UI only if stamina value changes
        if (Mathf.Abs(currentStamina - lastStaminaValue) > 0.01f) // Small threshold
        {

            slider.maxValue = playerSettings.maxStamina;
            slider.value = currentStamina;
            lastStaminaValue = currentStamina;
        }
    }
    public bool CanExecute() => playerSettings.usesStamina && !playerStatsProvider.IsDead && playerControl.IsControllable;

    private void TryConsumeStamina(float? amount)
    {
        if(amount.HasValue) ConsumeStamina(amount.Value);
    }
    public void ConsumeStamina(float amount)
    {
        if (!CanExecute()) return;

        currentStamina = Mathf.Max(currentStamina - amount, 0f);

        if (currentStamina <= 0)
        {
            context.EnoughStaminaToRun = false;
            context.EnoughStaminaToJump = false;
            currentStamina = 0;
            playerEvents.Events.OnStaminaDepleted?.Invoke();
        }
    }

    public void ResetStamina()
    {
        this.currentStamina = playerSettings.maxStamina;
         // Reset to force UI update
        lastStaminaValue = -1f;
        
        context.EnoughStaminaToRun = true;
        context.EnoughStaminaToJump = true;
    }

    private void ConsumeSlideStamina() => ConsumeStamina(playerSettings.staminaLossOnSlide);
    private void ConsumeDashStamina() => ConsumeStamina(playerSettings.staminaLossOnDash);
    private void ConsumeJumpStamina() => ConsumeStamina(playerSettings.staminaLossOnJump);
}
