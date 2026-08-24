using cowsins;
using UnityEngine;

public class VelocityHandlerBehaviour 
{
    private MovementContext context;
    private Rigidbody rb;
    private InputManager inputManager;
    private IPlayerMovementStateProvider playerMovement;
    private IPlayerMovementEventsProvider playerMovementEvents;
    private IWeaponReferenceProvider weaponReference;
    private IWeaponBehaviourProvider weaponBehaviour;
    private IWeaponBehaviourProvider weaponController;
    private IWeaponEventsProvider weaponEvents;
    private IPlayerControlProvider playerControl;
   
    private PlayerOrientation orientation => playerMovement?.Orientation;

    private PlayerMovementSettings playerSettings;

    public VelocityHandlerBehaviour(MovementContext context)
    {
        this.context = context;
        this.rb = context.Rigidbody;
        this.inputManager = context.InputManager;

        this.playerMovement = context.Dependencies.PlayerMovementState;
        this.playerMovementEvents = context.Dependencies.PlayerMovementEvents;
        this.weaponReference = context.Dependencies.WeaponReference;
        this.weaponBehaviour = context.Dependencies.WeaponBehaviour;
        this.weaponController = context.Dependencies.WeaponBehaviour;
        this.weaponEvents = context.Dependencies.WeaponEvents;
        this.playerControl = context.Dependencies.PlayerControl;

        this.playerSettings = context.Settings;

        playerMovementEvents.Events.OnMovingToIdle.AddListener(HandleIdle);
        playerMovementEvents.Events.OnIdleToMove.AddListener(HandleMoving);
        playerMovementEvents.Events.OnCrouching.AddListener(HandleCrouching);
        playerMovementEvents.Events.OnCrouchStop.AddListener(HandleMoving);
        inputManager.OnSprintPressed += HandleSprintStart;
        inputManager.OnSprintReleased += HandleIdle;
        inputManager.OnMoveInputChanged += HandleMoving;
        inputManager.OnShoot += HandleShootStart;
        inputManager.OnStopShoot += HandleShootStop;
        playerMovementEvents.Events.OnStaminaDepleted.AddListener(HandleStaminaDepleted);
        weaponEvents.Events.OnAimStart.AddListener(HandleAimStart);
        weaponEvents.Events.OnAimStop.AddListener(HandleAimStop);
    }

    public void Dispose()
    {
        playerMovementEvents.Events.OnMovingToIdle.RemoveListener(HandleIdle);
        playerMovementEvents.Events.OnIdleToMove.RemoveListener(HandleMoving);
        playerMovementEvents.Events.OnCrouching.RemoveListener(HandleCrouching);
        playerMovementEvents.Events.OnCrouchStop.RemoveListener(HandleMoving);
        inputManager.OnSprintPressed -= HandleSprintStart;
        inputManager.OnSprintReleased -= HandleIdle;
        inputManager.OnMoveInputChanged -= HandleMoving;
        inputManager.OnShoot -= HandleShootStart;
        inputManager.OnStopShoot -= HandleShootStop;
        playerMovementEvents.Events.OnStaminaDepleted.RemoveListener(HandleStaminaDepleted);
        weaponEvents.Events.OnAimStart.RemoveListener(HandleAimStart);
        weaponEvents.Events.OnAimStop.RemoveListener(HandleAimStop);
    }

    private void HandleAimStart(float prop)
    {
        if (weaponReference.Weapon == null) return;

        if (weaponReference.Weapon.setMovementSpeedWhileAiming)
            playerMovement.CurrentSpeed = weaponReference.Weapon.movementSpeedWhileAiming;
        else playerMovement.CurrentSpeed = playerSettings.autoRun ? playerMovement.RunSpeed : playerMovement.WalkSpeed;
    }
    private void HandleAimStop()
    {
        if(inputManager.Sprinting) HandleSprintStart();
        else HandleIdle();
    }
    private void HandleIdle()
    {
        if(playerMovement.IsCrouching && playerMovement.CurrentSpeed > playerMovement.CrouchSpeed) return;

        playerMovement.CurrentSpeed = playerMovement.WalkSpeed;
        
        SetFOVToNormal();
    }

    private void HandleMoving()
    {
        if(weaponBehaviour.IsAiming || playerMovement.IsCrouching) return;

        if (inputManager.Sprinting || playerSettings.autoRun) HandleSprintStart();
        else HandleIdle();
    }
    private void HandleSprintStart()
    {
        if (playerSettings.alternateCrouch && playerMovement.IsCrouching) inputManager.ForceUncrouch();

        if (!context.EnoughStaminaToRun || weaponBehaviour.IsAiming || playerMovement.IsCrouching) return;

        if (CanSprint())
        {
            playerMovement.CurrentSpeed = playerMovement.RunSpeed;
            SetFOVToSprint();
            playerSettings.events.OnSprint?.Invoke();
        }
        else HandleIdle();
    }

    // Called when the shoot input fires — only triggers sprint-break when a weapon is held.
    private void HandleShootStart()
    {
        if (weaponReference.Weapon == null) return;
        HandleSprintStart();
    }

    // Called when the shoot input is released — only recalculates movement when a weapon is held.
    private void HandleShootStop()
    {
        if (weaponReference.Weapon == null) return;
        HandleMoving();
    }

    private void HandleCrouching()
    {
        if (inputManager.Crouching)
        {
            playerMovement.CurrentSpeed = Mathf.MoveTowards(playerMovement.CurrentSpeed, playerMovement.CrouchSpeed, Time.deltaTime * playerSettings.crouchTransitionSpeed);
        }
    }
    private void HandleStaminaDepleted()
    {
        if (playerMovement.CurrentSpeed >= playerMovement.RunSpeed)
            HandleIdle();
    }

    private bool CanSprint()
    {
        bool movingForward = inputManager.Y > 0;
        bool movingBackward = inputManager.Y < 0;
        bool movingSideways = inputManager.X != 0;
        bool shooting = inputManager.Shooting && weaponReference.Weapon != null;

        if (shooting && !playerSettings.canRunWhileShooting && playerMovement.Grounded) return false;
        if (movingBackward && !playerSettings.canRunBackwards) return false;
        if (movingSideways && !movingForward && !playerSettings.canRunSideways) return false;

        return (movingForward || movingSideways || movingBackward) && playerControl.IsControllable;
    }

    private void SetFOVToNormal() => context.Dependencies.CameraFOVManager?.SetFOVToNormal();

    private void SetFOVToSprint() => context.Dependencies.CameraFOVManager?.SetFOVToRun();
}
