using cowsins;
using UnityEngine;

public class SpeedLinesBehaviour
{
    private Rigidbody rb;
    private IPlayerMovementStateProvider playerMovement;
    private IPlayerMovementEventsProvider playerEvents;

    private PlayerMovementSettings playerSettings;
    
    // Cache last velocity magnitude to avoid unnecessary checks
    private float lastVelocityMagnitude = -1f;
    private bool isSpeedLinesActive = false;

    public SpeedLinesBehaviour(MovementContext context)
    {
        this.rb = context.Rigidbody;
        this.playerMovement = context.Dependencies.PlayerMovementState;
        this.playerEvents = context.Dependencies.PlayerMovementEvents;

        this.playerSettings = context.Settings;

        playerEvents.Events.OnMoving.AddListener(Tick);
        playerEvents.Events.OnDashing.AddListener(Tick);
        playerEvents.Events.OnIdle.AddListener(StopSpeedlines);
    }

    public void Dispose()
    {
        playerEvents.Events.OnMoving.RemoveListener(Tick);
        playerEvents.Events.OnDashing.RemoveListener(Tick);
        playerEvents.Events.OnIdle.RemoveListener(StopSpeedlines);
    }

    public void Tick()
    {
        var speedLines = playerSettings.speedLines;
        if (!CanExecute()) return;
        
        // Check if we want to use speedlines. If false, stop and return.
        if (!playerSettings.useSpeedLines || PauseMenu.isPaused || playerMovement.IsClimbing)
        {
            if (isSpeedLinesActive)
            {
                speedLines.Stop();
                isSpeedLinesActive = false;
            }
            return;
        }

        float currentVelocityMagnitude = rb.velocity.magnitude;
        
        // Only check velocity if it has changed enough
        if (Mathf.Abs(currentVelocityMagnitude - lastVelocityMagnitude) > lastVelocityMagnitude * 0.05f)
        {
            lastVelocityMagnitude = currentVelocityMagnitude;
            
            bool shouldBeActive = currentVelocityMagnitude >= playerSettings.minSpeedToUseSpeedLines;
            
            if (shouldBeActive && !isSpeedLinesActive)
            {
                speedLines.Play();
                isSpeedLinesActive = true;
            }
            else if (!shouldBeActive && isSpeedLinesActive)
            {
                speedLines.Stop();
                isSpeedLinesActive = false;
            }

            // HandleEmission
            if (isSpeedLinesActive)
            {
                var emission = speedLines.emission;
                float emissionRate = (currentVelocityMagnitude > playerMovement.RunSpeed) ? 200 : 70;
                emission.rateOverTime = emissionRate * playerSettings.speedLinesAmount;
            }
        }
    }

    public bool CanExecute() => playerSettings.speedLines != null;

    public void StopSpeedlines() 
    {
        if (playerSettings.speedLines != null)
        {
            playerSettings.speedLines.Stop();
            isSpeedLinesActive = false;
            // Reset to force next check
            lastVelocityMagnitude = -1f;
        }
    }
}
