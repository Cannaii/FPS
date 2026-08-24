using cowsins;
using UnityEngine;
using static cowsins.PlayerMovementSettings;

public class ClimbLadderBehaviour
{
    private MovementContext context;
    private Rigidbody rb;
    private InputManager inputManager;
    private IPlayerMovementStateProvider playerMovement;
    private IPlayerMovementEventsProvider playerEvents;
    private IWeaponReferenceProvider weaponReference;
    private PlayerOrientation orientation => playerMovement.Orientation;
    private PlayerMovementSettings playerSettings;
    private LayerMask ladderMask;

    public ClimbLadderBehaviour(MovementContext context)
    {
        this.context = context;
        this.rb = context.Rigidbody;
        this.inputManager = context.InputManager;
        this.playerMovement = context.Dependencies.PlayerMovementState;
        this.playerEvents = context.Dependencies.PlayerMovementEvents;
        this.weaponReference = context.Dependencies.WeaponReference;
        this.playerSettings = context.Settings;
        this.ladderMask = LayerMask.GetMask("Ladder");
    }

    public void Enter()
    {
        rb.useGravity = false;
        playerMovement.IsClimbing = true;
        rb.velocity = Vector3.zero;
    }

    public void Tick() => HandleClimbMovement();

    public void Exit()
    {
        playerMovement.IsClimbing = false;
        rb.useGravity = true;
        playerEvents.Events.OnClimbStop?.Invoke(playerSettings.hideWeaponWhileClimbing);
    }

    public bool CanExecute()
    {
        return DetectLadders();
    }

    public bool DetectLadders()
    {
        if (!playerSettings.canClimb) return false;

        bool isGrounded = playerMovement.Grounded;

        // Check forward direction for ladder
        bool ladderAhead = Physics.Raycast(context.Transform.position, orientation.Forward,
            playerSettings.maxLadderDetectionDistance, ladderMask);

        // Only attach to climb ladder state when pressing W if player is grounded
        if (isGrounded && ladderAhead)
        {
            // Only if moving forward
            if (inputManager.Y > 0.01f)
            {
                if (playerSettings.ladderMovementMode != LadderMovementMode.WSOnly)
                {
                    float cameraPitch = GetCameraPitch();
                    if (cameraPitch > 40f) return false;
                }
                playerEvents.Events.OnClimbStart?.Invoke(playerSettings.hideWeaponWhileClimbing);
                return true;
            }
            return false;
        }

        // If not grounded: Attach when in air
        if (!isGrounded)
        {
            // Backwards: Always attach regardless of look direction
            if (inputManager.Y < -0.01f && ladderAhead)
            {
                playerEvents.Events.OnClimbStart?.Invoke(playerSettings.hideWeaponWhileClimbing);
                return true;
            }

            // forward: Only when looking down
            if (inputManager.Y > 0.01f && IsLookingDown())
            {
                if (ladderAhead)
                {
                    playerEvents.Events.OnClimbStart?.Invoke(playerSettings.hideWeaponWhileClimbing);
                    return true;
                }
            }

            // Strafe: Only when looking down
            if (Mathf.Abs(inputManager.X) > 0.01f && IsLookingDown())
            {
                Vector3 moveDir = GetMovementDirection();
                if (Physics.Raycast(context.Transform.position, moveDir,
                    playerSettings.maxLadderDetectionDistance * 1.5f, ladderMask))
                {
                    playerEvents.Events.OnClimbStart?.Invoke(playerSettings.hideWeaponWhileClimbing);
                    return true;
                }
            }
        }

        return false;
    }

    public bool DetectTopLadder()
    {
        if (!playerSettings.canClimb) return false;

        // Check if ladder no longer detected ahead
        bool noLadderAhead = !Physics.Raycast(context.Transform.position, orientation.Forward,
            playerSettings.maxLadderDetectionDistance, ladderMask);

        // Also check slightly above current position to ensure we are actually at the top of the ladder
        bool isAtTop = !Physics.Raycast(context.Transform.position + Vector3.up * 0.5f,
            orientation.Forward, playerSettings.maxLadderDetectionDistance, ladderMask);

        return noLadderAhead && isAtTop;
    }

    public bool DetectBottomLadder()
    {
        return Physics.Raycast(context.Transform.position, Vector3.down, 0.6f, context.WhatIsGround);
    }

    public void ApplyForcesOnTopReached()
    {
        // Clear velocity
        rb.velocity = Vector3.zero;

        // Apply impulse
        rb.AddForce(context.Transform.up * playerSettings.topReachedUpperForce, ForceMode.Impulse);
        rb.AddForce(orientation.Forward * playerSettings.topReachedForwardForce, ForceMode.Impulse);
    }

    public void HandleClimbMovement()
    {
        if (PauseMenu.isPaused) return;

        float verticalInput = inputManager.Y;

        // Apply movement mode
        switch (playerSettings.ladderMovementMode)
        {
            case LadderMovementMode.WSOnly:
                break;
            case LadderMovementMode.LookBased:
                verticalInput = GetLookBasedClimbInput(verticalInput);
                break;
            case LadderMovementMode.Combined:
                if (verticalInput > 0f)
                    verticalInput = GetLookBasedClimbInput(verticalInput);
                break;
        }

        // Prevent climbing into ceiling
        bool isObstacleAbove = Physics.Raycast(context.Transform.position, context.Transform.up,
            2f, context.WhatIsGround);
        if (isObstacleAbove && verticalInput > 0f)
            verticalInput = 0;

        // Prevent climbing below ground
        if (DetectBottomLadder() && verticalInput < 0f)
            verticalInput = 0;

        // Apply movement
        if (Mathf.Abs(verticalInput) > 0.01f)
        {
            Vector3 targetPosition = context.Transform.position +
                context.Transform.up * verticalInput * playerSettings.climbSpeed * Time.deltaTime;
            rb.MovePosition(Vector3.Lerp(context.Transform.position, targetPosition, 0.5f));
            rb.velocity = Vector3.zero;
        }
        else
        {
            // Keep velocity zero when not moving to prevent sliding
            rb.velocity = Vector3.zero;
        }
    }

    private float GetLookBasedClimbInput(float currentInput)
    {
        if (currentInput <= 0.01f) return 0f;

        float cameraPitch = GetCameraPitch();

        if (cameraPitch > 40f) return -1f;  // Look down = climb down
        if (cameraPitch < -40f) return 1f;  // Look up = climb up

        return 1f;  // Default to climbing up
    }

    private bool IsMovingTowardLadder()
    {
        // Check if any movement input is active
        return Mathf.Abs(inputManager.X) > 0.01f || Mathf.Abs(inputManager.Y) > 0.01f;
    }

    private bool IsLookingDown()
    {
        float cameraPitch = GetCameraPitch();
        // Looking down threshold
        return cameraPitch > 25f;
    }

    private Vector3 GetMovementDirection()
    {
        // Calculate movement direction based on input
        Vector3 direction = orientation.Forward * inputManager.Y + orientation.Right * inputManager.X;
        return direction.normalized;
    }

    private float GetCameraPitch()
    {
        float cameraPitch = weaponReference.MainCamera.transform.eulerAngles.x;
        if (cameraPitch > 180f) cameraPitch -= 360f;
        return cameraPitch;
    }
}