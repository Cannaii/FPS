using cowsins;
using UnityEngine;
using System.Collections;

public class DashBehaviour 
{
    private MovementContext context;
    private Rigidbody rb;
    private IPlayerMovementStateProvider playerMovement;
    private IPlayerMovementEventsProvider playerEvents;
    private IPlayerControlProvider playerControl;

    private PlayerOrientation orientation => playerMovement.Orientation;

    private PlayerMovementSettings playerSettings;
    public int currentDashes;

    private Vector2 input;
    private Transform playerCam;

    private MonoBehaviour CoroutineRunner;

    public DashBehaviour(MovementContext context)
    {
        this.context = context;
        this.rb = context.Rigidbody;
        this.playerMovement = context.Dependencies.PlayerMovementState;
        this.playerEvents = context.Dependencies.PlayerMovementEvents;
        this.playerControl = context.Dependencies.PlayerControl;  
        this.CoroutineRunner = context.Transform.GetComponent<MonoBehaviour>();

        this.playerSettings = context.Settings;
        this.playerCam = context.Camera;

        if (this.playerSettings.canDash && !this.playerSettings.infiniteDashes)
        {
            playerEvents.Events.OnInitializeDash?.Invoke(this.playerSettings.amountOfDashes);
            currentDashes = this.playerSettings.amountOfDashes;
        }
    }
    public void Enter(Vector2 input)
    {
        this.input = input;

        playerMovement.IsDashing = true;
        rb.useGravity = true;

        if (!playerSettings.infiniteDashes)
        {
            currentDashes--;
            playerEvents.Events.OnDashUsed?.Invoke(currentDashes);
            CallRegainDash();
        }

        playerEvents.Events.OnDashStart?.Invoke();
        SoundManager.Instance.PlaySound(playerSettings.sounds.dashSFX, 0, 0, false);
    }
    public void Tick() 
    {
        Vector3 dir = GetProperDirection();
        //player.HandleStairs(dir);
        rb.AddForce(dir * playerSettings.dashForce * Time.deltaTime, ForceMode.Impulse);

        // Remove not wanted Y velocity after slope
        Vector3 vel = rb.velocity;
        vel.y = 0;
        rb.velocity = vel;

        playerEvents.Events.OnDashing?.Invoke(); 
    }

    public void Exit()
    {
        playerMovement.IsDashing = false;
        rb.useGravity = true;
        playerSettings.events.OnEndDash?.Invoke();
    }

    public bool CanExecute()
    {
        return playerControl.IsControllable && playerSettings.canDash && (playerSettings.infiniteDashes || currentDashes > 0 && !playerSettings.infiniteDashes);
    }

    private Vector3 GetProperDirection()
    {
        Vector3 direction = Vector3.zero;
        switch (playerSettings.dashMethod)
        {
            case PlayerMovementSettings.DashMethod.ForwardAlways:
                direction = orientation.Forward;
                break;
            case PlayerMovementSettings.DashMethod.Free:
                direction = playerCam.forward;
                break;
            case PlayerMovementSettings.DashMethod.InputBased:
                direction = (input.x == 0 && input.y == 0) ? orientation.Forward : CameraBasedInput();
                break;
        }
        return direction;
    }


    private Vector3 CameraBasedInput()
    {
        Vector3 forward = playerCam.transform.forward;
        Vector3 right = playerCam.transform.right;

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 dir = forward * input.y + right * input.x;

        return dir;
    }
    public void CallRegainDash() => CoroutineRunner.StartCoroutine(RegainDashAfterDelay(playerSettings.dashCooldown));

    private IEnumerator RegainDashAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        RegainDash();
    }

    private void RegainDash()
    {
        // Gain a dash
        currentDashes += 1;
        playerEvents.Events.OnDashGained?.Invoke(currentDashes);
    }

    public void ResetDashes()
    {
        currentDashes = playerSettings.amountOfDashes;
    }
}
