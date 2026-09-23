using cowsins;
using UnityEngine;

public class CrouchSlideBehaviour
{
    private MovementContext context;
    private Rigidbody rb;
    private InputManager inputManager;
    private IPlayerMovementStateProvider playerMovement;
    private IPlayerMovementEventsProvider playerEvents;

    private PlayerMovementSettings playerSettings;

    private PlayerOrientation orientation => playerMovement.Orientation;
    private Vector3 playerScale;
    public Vector3 crouchScale { get; private set; } = new Vector3(1, 0.5f, 1);
    private bool canUnCrouch = false;
    private float slideTimer = 0f;
    private Vector3 slideDirection = Vector3.zero;
    private float slideBoostRemaining = 0f;
    private bool isBoosting = false;
    private float slideAirborneTimer = 0f; // how long we have been airborne mid slide
    private const float slideAirborneTolerance = 0.25f;

    public CrouchSlideBehaviour(MovementContext context)
    {
        this.context = context;
        this.rb = context.Rigidbody;
        this.inputManager = context.InputManager;

        this.playerMovement = context.Dependencies.PlayerMovementState;
        this.playerEvents = context.Dependencies.PlayerMovementEvents;

        this.playerSettings = context.Settings;
        this.playerScale = context.Transform.localScale;

        playerEvents.Events.AllowSlide += AllowSliding;
    }

    public void Dispose()
    {
        playerEvents.Events.AllowSlide -= AllowSliding;
    }
    public void Enter()
    {
        if (!playerSettings.allowCrouch) return;

        playerMovement.IsCrouching = true;

        playerSettings.events.OnCrouch.Invoke();
        playerEvents.Events.OnCrouchStart?.Invoke(); // Internal Event
        SoundManager.Instance.PlaySound(playerSettings.sounds.startCrouchSFX, 0, 0, false);

        // Start sliding when conditions match.
        if (rb.velocity.magnitude >= playerMovement.WalkSpeed && playerMovement.CurrentSpeed > playerMovement.WalkSpeed && playerMovement.Grounded && playerSettings.allowSliding && !context.HasJumped)
        {
            Vector3 horizontalVel = new Vector3(rb.velocity.x, 0, rb.velocity.z);
            if (horizontalVel.magnitude > 0.1f)
                slideDirection = horizontalVel.normalized;
            else
                slideDirection = Vector3.ProjectOnPlane(orientation.Forward, Vector3.up).normalized;

            slideBoostRemaining = Mathf.Max(0.0001f, playerSettings.slideBoostDuration);
            isBoosting = true;

            // Begin local boost and timer.
            slideTimer = playerSettings.slideDuration;

            playerSettings.events.OnSlideStart.Invoke();
            playerEvents.Events.OnSlideStart?.Invoke();
            SoundManager.Instance.PlaySound(playerSettings.sounds.slideSFX, 0, 0, false);
        }
    }

    public void Tick() 
    {
        Transform transform = context.Transform;
        // When not crouching, smoothly stand up
        if (!inputManager.Crouching)
        {
            playerMovement.IsCrouching = false;
            transform.localScale = Vector3.MoveTowards(transform.localScale, playerScale, Time.deltaTime * playerSettings.crouchTransitionSpeed);
            return;
        }

        // Maintain crouch scale while crouching or during the short boost window for the slide
        if (playerMovement.IsCrouching || isBoosting)
        {
            transform.localScale = Vector3.MoveTowards(transform.localScale, crouchScale, Time.deltaTime * playerSettings.crouchTransitionSpeed * 1.5f);
        }
    }

    public void FixedTick()
    {
        if (!playerMovement.IsSliding) return;

        // Allow the slide to survive brief airtime
        if (!playerMovement.Grounded)
        {
            slideAirborneTimer += Time.fixedDeltaTime;
            if (slideAirborneTimer > slideAirborneTolerance)
            {
                // the player is genuinely airborne, end the slide
                EndSlide();
            }

            return;
        }

        // Back on the ground
        slideAirborneTimer = 0f;

        // on a slope, project onto the surface so the slide follows it
        if (context.IsPlayerOnSlope && !context.HasJumped)
        {
            rb.velocity = Vector3.ProjectOnPlane(rb.velocity, context.SlopeHit.normal);
        }
        else if (rb.velocity.y > 0f)
        {
            Vector3 v = rb.velocity;
            v.y = 0f;
            rb.velocity = v;
        }

        // Apply initial boost
        if (isBoosting && slideBoostRemaining > 0f)
        {
            float dt = Time.fixedDeltaTime;
            // Distribute the configured slideForce across the boost duration as acceleration
            float boostAmount = playerSettings.slideForce * dt / Mathf.Max(playerSettings.slideBoostDuration, dt);
            rb.AddForce(slideDirection * boostAmount, ForceMode.Acceleration);
            slideBoostRemaining -= dt;
            if (slideBoostRemaining <= 0f) isBoosting = false;
        }

        // Steering while sliding
        Vector3 inputDir = (orientation.Forward * inputManager.Y + orientation.Right * inputManager.X);
        inputDir.y = 0;
        if (inputDir.sqrMagnitude > 0.0001f)
        {
            Vector3 steer = inputDir.normalized * playerSettings.acceleration * playerSettings.slideSteerMultiplier * Time.fixedDeltaTime;
            rb.AddForce(steer, ForceMode.Acceleration);
        }

        // Decrease timer and check for stop condition
        slideTimer -= Time.fixedDeltaTime;
        Vector3 horizontalVel = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        if (slideTimer <= 0 || horizontalVel.magnitude < playerSettings.slideStopSpeed)
        {
            EndSlide();
        }
    }

    public void Exit()
    {
        playerEvents.Events.OnCrouchStop?.Invoke();
        playerSettings.events.OnStopCrouch?.Invoke();
        SoundManager.Instance.PlaySound(playerSettings.sounds.stopCrouchSFX, 0, 0, false);
        EndSlide();
    }

    public bool CanExecute()
    {
        return inputManager.Crouching && !playerMovement.IsWallRunning && playerSettings.allowCrouch && (playerMovement.Grounded || !playerMovement.Grounded && playerSettings.allowCrouchWhileJumping);
    }


    public void HandleCrouch()
    {
        if (inputManager.Crouching)
        {
            context.Transform.localScale = Vector3.MoveTowards(context.Transform.localScale, crouchScale, Time.deltaTime * playerSettings.crouchTransitionSpeed * 1.5f);
        }

        playerMovement.IsCrouching = true;

        playerEvents.Events.OnCrouching?.Invoke();
    }

    public bool CheckUnCrouch()
    {
        if (!inputManager.Crouching) // Prevent from uncrouching when there�s a roof and we can get hit with it
        {
            RaycastHit hit;
            bool isObstacleAbove = Physics.Raycast(context.Transform.position, context.Transform.up, out hit, playerSettings.roofCheckDistance, context.WhatIsGround);

            canUnCrouch = !isObstacleAbove;

            if (canUnCrouch)
            {
                Tick();
                if (context.Transform.localScale == playerScale)
                    return true;
            }
        }

        return false;
    }

    private bool AllowSliding() => playerSettings.allowSliding;

    private void EndSlide()
    {
        if (!playerMovement.IsSliding) return;

        slideAirborneTimer = 0f;
        playerMovement.IsSliding = false;
        //playerEvents.Events.OnSlideEnd?.Invoke();
        playerSettings.events.OnStopCrouch?.Invoke();

        Vector3 vel = rb.velocity;
        Vector3 horizontal = new Vector3(vel.x, 0, vel.z);

        rb.velocity = new Vector3(horizontal.magnitude > 0 ? horizontal.normalized.x * Mathf.Max(0, horizontal.magnitude * 0.6f) : 0, vel.y, horizontal.magnitude > 0 ? horizontal.normalized.z * Mathf.Max(0, horizontal.magnitude * 0.6f) : 0);
    }
}
