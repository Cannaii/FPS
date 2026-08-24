using cowsins;
using UnityEngine;

public class JumpBehaviour
{
    private MovementContext context;
    private InputManager inputManager;
    private Rigidbody rb;
    private IPlayerMovementStateProvider playerMovement;
    private IPlayerMovementEventsProvider playerEvents;
    private IFallHeightProvider fallHeightProvider;
    private IPlayerControlProvider playerControl;
    private PlayerOrientation orientation => playerMovement.Orientation;

    private bool isJumpAvailable;

    private PlayerMovementSettings playerSettings;

    private bool wallOpposite = false; // Needs to be passed in the shared state, depends on wall jump directly.

    private float jumpCooldownTimer;
    private bool isCoyoteJumpAvailable = false;
    public int jumpCount {  get; private set; }

    public JumpBehaviour(MovementContext context)
    {
        this.context = context;
        this.rb = context.Rigidbody;
        this.inputManager = context.InputManager;

        this.playerSettings = context.Settings;

        this.playerMovement = context.Dependencies.PlayerMovementState;
        this.playerEvents = context.Dependencies.PlayerMovementEvents;
        this.fallHeightProvider = context.Dependencies.FallHeight;
        this.playerControl = context.Dependencies.PlayerControl;

        isJumpAvailable = true;
        jumpCount = playerSettings.maxJumps;

        playerEvents.Events.OnLand.AddListener(ResetJumpCount);
        playerEvents.Events.OnWallRunStart.AddListener(ResetJumpsOnWallJump);
        playerEvents.Events.OnWallBounceStart.AddListener(ResetJumpsOnWallBounce);
        playerEvents.Events.OnGrappleStart.AddListener(ResetJumpsOnGrapple);
    }

    public void Dispose()
    {
        playerEvents.Events.OnLand.RemoveListener(ResetJumpCount);
        playerEvents.Events.OnWallRunStart.RemoveListener(ResetJumpsOnWallJump);
        playerEvents.Events.OnWallBounceStart.RemoveListener(ResetJumpsOnWallBounce);
        playerEvents.Events.OnGrappleStart.RemoveListener(ResetJumpsOnGrapple);
    }
    public void Enter()
    {
        if (rb == null) return;

        jumpCooldownTimer = playerSettings.jumpCooldown;

        jumpCount--;

        isJumpAvailable = false;
        context.HasJumped = true;

        // Store velocity
        Vector3 velocity = rb.velocity;
        // Reset Y velocity before jumping
        velocity.y = 0f;
        rb.velocity = velocity;

        if (playerSettings.doubleJumpResetsFallDamage) fallHeightProvider?.SetFallHeight(context.Transform.position.y);

        //Add jump forces
        if (playerMovement.IsWallRunning) playerEvents.Events.OnWallJump?.Invoke();
        else Jump();

        SoundManager.Instance.PlaySound(playerSettings.sounds.jumpSFX, 0, 0, false);

        playerSettings.events.OnJump.Invoke();
        playerEvents.Events.OnJump?.Invoke(); 
    }

    public void Tick()
    {
        if (!isJumpAvailable)
        {
            jumpCooldownTimer -= Time.deltaTime;
            
            if (jumpCooldownTimer <= 0f)
            {
                isJumpAvailable = true;
            }
        }

        if (playerSettings.canCoyote)
        {
            if (playerMovement.Grounded)
            {
                context.CoyoteTimer = context.CoyoteJumpTime;
                context.HasJumped = false;
            }
            else
            {
                context.CoyoteTimer -= Time.deltaTime;
            }

            isCoyoteJumpAvailable = !playerMovement.Grounded && context.CoyoteTimer > 0 && isJumpAvailable;
        }

        if (playerMovement.Grounded && isJumpAvailable)
        {
            jumpCount = playerSettings.maxJumps;
        }
    }
    public bool CanExecute()
    {
        bool isCoyoteValid = playerSettings.canCoyote && isCoyoteJumpAvailable;
        bool hasStamina = context.EnoughStaminaToJump;
        bool hasJumpsLeft = jumpCount > 0;

        bool isValidJumpCondition =
            (hasStamina && (playerMovement.Grounded || isCoyoteValid || context.IsPlayerOnSlope)) ||
            playerMovement.IsWallRunning ||
            (hasJumpsLeft && playerSettings.maxJumps > 1 && hasStamina);

        return playerSettings.allowJump
            && hasJumpsLeft
            && playerControl.IsControllable
            && isJumpAvailable
            && isValidJumpCondition;
    }

    public bool CanExecuteDoubleJump()
    {
        bool hasStamina = context.EnoughStaminaToJump;
        bool hasJumpsLeft = jumpCount > 0;
        bool isWallRunning = playerMovement.IsWallRunning;

        bool isDoubleJumpAllowed =
            (hasJumpsLeft && playerSettings.maxJumps > 1 && hasStamina) ||
            (isWallRunning && hasStamina);

        return isJumpAvailable && isDoubleJumpAllowed;
    }


    private void Jump()
    {
        // Gather & Store Movement Input
        float inputX = inputManager.X;
        float inputY = inputManager.Y;

        rb.AddForce(Vector3.up * playerSettings.jumpForce, ForceMode.Impulse);

        // Handle directional jumping
        if (!playerMovement.Grounded && playerSettings.directionalJumpMethod != PlayerMovementSettings.DirectionalJumpMethod.None && playerSettings.maxJumps > 1 && !wallOpposite)
        {
            if (Vector3.Dot(rb.velocity, new Vector3(inputX, 0, inputY)) > .5f)
                rb.velocity /= 2f;

            if (playerSettings.directionalJumpMethod == PlayerMovementSettings.DirectionalJumpMethod.InputBased) // Input based method for directional jumping
            {
                rb.AddForce(orientation.Right * inputX * playerSettings.directionalJumpForce, ForceMode.Impulse);
                rb.AddForce(orientation.Forward * inputY * playerSettings.directionalJumpForce, ForceMode.Impulse);
            }
            if (playerSettings.directionalJumpMethod == PlayerMovementSettings.DirectionalJumpMethod.ForwardMovement) // Forward Movement method for directional jumping, dependant on orientation
                rb.AddForce(orientation.Forward * Mathf.Abs(inputY) * playerSettings.directionalJumpForce, ForceMode.VelocityChange);
        }
    }

    private void ResetJumpCount()
    {
        jumpCount = playerSettings.maxJumps;
        context.HasJumped = false;
    }
    private void ResetJumpsOnWallJump()
    {
        if (playerSettings.resetJumpsOnWallrun) jumpCount = playerSettings.maxJumps;
    }
    private void ResetJumpsOnWallBounce()
    {
        if (playerSettings.resetJumpsOnWallBounce) jumpCount = playerSettings.maxJumps - 1;
    }
    private void ResetJumpsOnGrapple()
    {
        if (playerSettings.resetJumpsOnGrapple) jumpCount = playerSettings.maxJumps;
    }
}