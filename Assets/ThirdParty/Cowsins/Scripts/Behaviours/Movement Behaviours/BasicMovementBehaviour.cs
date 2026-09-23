using cowsins;
using UnityEngine;
using System; 

public class BasicMovementBehaviour
{
    private MovementContext context;
    private Rigidbody rb;
    private InputManager inputManager;
    private IPlayerMovementStateProvider playerMovement;
    private IPlayerMovementEventsProvider playerEvents;
    private IPlayerControlProvider playerControl;
    private IWeaponReferenceProvider weaponReference;
    private IWeaponBehaviourProvider weaponController;
    private IPlayerMultipliers playerMultipliers;

    private PlayerOrientation orientation => playerMovement?.Orientation;

    private Vector3 moveDirection;
    private PlayerMovementSettings playerSettings;

    private CapsuleCollider playerCapsuleCollider;

    private const float frictionThreshold = 0.1f;
    private const float slopeGravityMultiplier = 150;
    private const float extraGravityMultiplier = 10f;
    private bool wasMovingLastFrame;
    public BasicMovementBehaviour(MovementContext context)
    {
        this.context = context;
        this.rb = context.Rigidbody;
        this.inputManager = context.InputManager;

        this.playerMovement = context.Dependencies.PlayerMovementState;
        this.playerEvents = context.Dependencies.PlayerMovementEvents;
        this.playerControl = context.Dependencies.PlayerControl;
        this.weaponReference = context.Dependencies.WeaponReference;
        this.weaponController = context.Dependencies.WeaponBehaviour;
        this.playerMultipliers = context.Dependencies.PlayerMultipliers;

        this.playerSettings = context.Settings;

        this.playerCapsuleCollider = context.Capsule;
    }

    /// <summary>
    /// Handle all the basics related to the movement of the player.
    /// </summary>
    public void Movement()
    {
        if(!playerControl.IsMovementControllable) return;

        // Extra gravity should be skipped on slope
        if (!context.IsPlayerOnSlope)
            rb.AddForce(Vector3.down * Time.fixedDeltaTime * extraGravityMultiplier);

        //Find actual velocity relative to where player is looking
        Vector2 relativeVelocity = FindVelRelativeToLook();

        // Counteract sliding and sloppy movement.
        FrictionForce(inputManager.X, inputManager.Y, relativeVelocity);
        //If speed is larger than maxspeed, clamp the velocity so you don't go over max speed
        LimitDiagonalVelocity();

        if (!playerControl.IsControllable)
        {
            if (playerMovement.Grounded) rb.velocity = Vector3.zero;
            return;
        }

        Vector3 horizontalVel = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        bool isCrouchSliding = playerMovement.IsCrouching && horizontalVel.magnitude >= playerMovement.CrouchSpeed;

        if (isCrouchSliding && !playerSettings.allowMoveWhileSliding) return;

        float airborneMultiplier = !playerMovement.Grounded ? playerSettings.controlAirborne : 1;
        float movementMultipliers = playerSettings.acceleration * Time.deltaTime * airborneMultiplier;

        // Reduce movement influence while sliding if sliding is active and movement while sliding isn't allowed
        if (isCrouchSliding && !playerSettings.allowMoveWhileSliding)
            movementMultipliers *= 0f;

        CalculateMoveDirection();
        CallEvents();

        movementMultipliers *= context.IsPlayerOnSlope ? 2f : 1;
        // If crouch-sliding, respect steering multiplier and don't add full movement force
        movementMultipliers *= isCrouchSliding ? playerSettings.slideSteerMultiplier : 1;

        ApplyAirCameraInfluence();

        // Calculate the velocity change
        float velocityChange = (movementMultipliers / rb.mass) * Time.fixedDeltaTime;
            
        // Apply to current velocity to preserve momentum and add new input
        Vector3 targetVelocity = rb.velocity + moveDirection * velocityChange;

        // Clamp Horizontal Scale
        Vector3 horizontalTarget = new Vector3(targetVelocity.x, 0, targetVelocity.z);
        float maxSpeed = playerMovement.CurrentSpeed * playerMultipliers.WeightMultiplier.Value;
            
        if (horizontalTarget.magnitude > maxSpeed)
        {
            horizontalTarget = horizontalTarget.normalized * maxSpeed;
            targetVelocity.x = horizontalTarget.x;
            targetVelocity.z = horizontalTarget.z;
        }
            
        // project current velocity onto the slope plane, then add input force.
        if (context.IsPlayerOnSlope && !context.HasJumped)
        {
            Vector3 slopeVelocity = Vector3.ProjectOnPlane(rb.velocity, context.SlopeHit.normal)
                                    + moveDirection * velocityChange;
            if (slopeVelocity.magnitude > maxSpeed)
                slopeVelocity = slopeVelocity.normalized * maxSpeed;
            rb.velocity = slopeVelocity;
        }
        else
        {
            // rely on the standard physics gravity
            float yVel = (playerMovement.Grounded && !context.HasJumped && rb.velocity.y <= 0f)
                ? Mathf.Min(rb.velocity.y, 0f)
                : rb.velocity.y;
            rb.velocity = new Vector3(targetVelocity.x, yVel, targetVelocity.z);
        }
    }

    private void CalculateMoveDirection()
    {
        if (context.IsPlayerOnSlope)
        {
            moveDirection = GetSlopeDirection();
            
            if (moveDirection.magnitude == 0 && !context.HasJumped) rb.velocity = Vector3.zero;
        }
        else
        {
            moveDirection = (orientation.Forward * inputManager.Y + orientation.Right * inputManager.X).normalized;
        }
    }

    private void ApplyAirCameraInfluence()
    {
        if(!playerSettings.allowAirCameraInfluence) return;

        if (!playerMovement.Grounded && inputManager.Y > 0.1f)
        {
            Vector3 wishdir;

            if (context.Camera != null)
            {
                Vector3 camForward = Vector3.ProjectOnPlane(context.Camera.forward, Vector3.up).normalized;
                Vector3 camRight = Vector3.ProjectOnPlane(context.Camera.right, Vector3.up).normalized;

                Vector3 cameraDir = camForward * inputManager.Y + camRight * inputManager.X;
                wishdir = cameraDir.normalized;
            }
            else
            {
                Vector3 orientDir = orientation.Forward * inputManager.Y + orientation.Right * inputManager.X;
                if (orientDir.sqrMagnitude < 0.0001f) orientDir = orientation.Forward;
                wishdir = orientDir.normalized;
            }

            float wishspeed = playerMovement.RunSpeed;
            AirAccelerate(wishdir, wishspeed, playerSettings.airCameraInfluenceAmount);
        }
    }

    private void AirAccelerate(Vector3 dir, float speed, float accel)
    {
        Vector3 vel = rb.velocity;
        Vector3 velHoriz = new Vector3(vel.x, 0f, vel.z);

        float currentSpeed = Vector3.Dot(velHoriz, dir);
        float addSpeed = speed - currentSpeed;
        if (addSpeed <= 0f) return;

        float accelSpeed = accel * Time.fixedDeltaTime * speed;
        if (accelSpeed > addSpeed) accelSpeed = addSpeed;

        Vector3 newVel = vel + dir * accelSpeed;
        rb.velocity = new Vector3(newVel.x, vel.y, newVel.z);
    }

    private void CallEvents()
    {
        bool isMoving = moveDirection.magnitude > .1f;

        if (isMoving && !wasMovingLastFrame)
        {
            // Transition Idle -> Moving
            playerEvents.Events.OnIdleToMove?.Invoke();
        }
        else if (!isMoving && wasMovingLastFrame)
        {
            // Transition Moving -> Idle
            playerEvents.Events.OnMovingToIdle?.Invoke();
        }


        if (moveDirection.magnitude > .1f)
        {
            playerEvents.Events.OnMoving?.Invoke();
            playerSettings.events.OnMoving?.Invoke();
        }
        else
        {
            playerEvents.Events.OnIdle?.Invoke();
            playerSettings.events.OnIdle?.Invoke();
        }
        wasMovingLastFrame = isMoving;
    }


    /// <summary>
    /// Limits diagonal velocity
    /// </summary>
    private void LimitDiagonalVelocity()
    {
        Vector3 horizontalVelocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        float currentWeightedSpeed = playerMovement.CurrentSpeed * playerMultipliers.WeightMultiplier.Value;
        if (horizontalVelocity.magnitude > currentWeightedSpeed)
        {
            horizontalVelocity = horizontalVelocity.normalized * currentWeightedSpeed;
            rb.velocity = new Vector3(horizontalVelocity.x, rb.velocity.y, horizontalVelocity.z);
        }
    }

    /// <summary>
    /// Find the velocity relative to where the player is looking
    /// Useful for vectors calculations regarding movement and limiting movement
    /// </summary>
    /// <returns></returns>
    private Vector2 FindVelRelativeToLook()
    {
        // Convert velocity to local space relative to the player's look direction
        Vector3 localVel = Quaternion.Euler(0, -orientation.Yaw, 0) * rb.velocity;
        return new Vector2(localVel.x, localVel.z);
    }

    /// <summary>
    /// Get the direction of movement in a slope
    /// </summary>
    /// <returns></returns>
    private Vector3 GetSlopeDirection()
    {
        // Use the ground raycast normal recorded in the shared context
        return Vector3.ProjectOnPlane(orientation.Forward * inputManager.Y + orientation.Right * inputManager.X, context.SlopeHit.normal).normalized;
    }

    /// <summary>
    /// Add friction force to the player when it�s not airborne
    /// Please note that it counters movement, since it goes in the opposite direction to velocity
    /// </summary>
    private void FrictionForce(float x, float y, Vector2 mag)
    {
        // Prevent from adding friction on an airborne body
        if (!playerMovement.Grounded || inputManager.Jumping || context.HasJumped) return;

        Vector3 horizontalVel = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        bool isCrouchSliding = playerMovement.IsCrouching && horizontalVel.magnitude >= playerMovement.CrouchSpeed;

        // If crouch-sliding and sliding friction is disabled, skip friction here. Sliding behaviour handles deceleration.
        if (isCrouchSliding && !playerSettings.applyFrictionForceOnSliding) return;

        float friction = isCrouchSliding ? playerSettings.slideFrictionForceAmount : playerSettings.controlsResponsiveness;

        // Counter movement ( Friction while moving )
        float frictionDeltaVScale = playerSettings.acceleration * Time.fixedDeltaTime * friction / rb.mass;

        if (Math.Abs(mag.x) > frictionThreshold && Math.Abs(x) < 0.5f || (mag.x < -frictionThreshold && x > 0) || (mag.x > frictionThreshold && x < 0))
        {
            float rawDeltaV = frictionDeltaVScale * Time.fixedDeltaTime * -mag.x;
            // friction can stop velocity but must not reverse its sign
            rawDeltaV = Mathf.Clamp(rawDeltaV, -Mathf.Abs(mag.x), Mathf.Abs(mag.x));
            rb.velocity += orientation.Right * rawDeltaV;
        }
        if (Math.Abs(mag.y) > frictionThreshold && Math.Abs(y) < 0.05f || (mag.y < -frictionThreshold && y > 0) || (mag.y > frictionThreshold && y < 0))
        {
            float rawDeltaV = frictionDeltaVScale * Time.fixedDeltaTime * -mag.y;
            rawDeltaV = Mathf.Clamp(rawDeltaV, -Mathf.Abs(mag.y), Mathf.Abs(mag.y));
            rb.velocity += orientation.Forward * rawDeltaV;
        }
    }
}

