using cowsins;
using UnityEngine;

public class WallBounceBehaviour
{
    private MovementContext context;
    private Rigidbody rb;
    private InputManager inputManager;
    private IPlayerMovementStateProvider playerMovement;
    private IPlayerMovementEventsProvider playerEvents;
    private IPlayerControlProvider playerControl;

    public bool wallOpposite { get; private set; }

    private PlayerOrientation orientation => playerMovement.Orientation;

    private PlayerMovementSettings playerSettings;

    private RaycastHit wallOppositeHit;

    public WallBounceBehaviour(MovementContext context)
    {
        this.context = context;
        this.rb = context.Rigidbody;
        this.inputManager = context.InputManager;
        this.playerMovement = context.Dependencies.PlayerMovementState;
        this.playerEvents = context.Dependencies.PlayerMovementEvents;
        this.playerControl = context.Dependencies.PlayerControl;

        this.playerSettings = context.Settings;
    }
    public void Enter() 
    {
        playerEvents.Events.OnWallBounceStart?.Invoke();

        rb.velocity = Vector3.zero;

        Vector3 direction = Vector3.Reflect(orientation.Forward, wallOppositeHit.normal);
        rb.AddForce(direction * playerSettings.wallBounceForce, ForceMode.VelocityChange);
        rb.AddForce(context.Transform.up * playerSettings.wallBounceUpwardsForce, ForceMode.Impulse);
    }

    public void Tick() 
    {
        if(playerSettings.canWallBounce)
            wallOpposite = Physics.Raycast(context.Transform.position, orientation.Forward, out wallOppositeHit, playerSettings.oppositeWallDetectionDistance, context.WhatIsGround);
    }

    public bool CanExecute() => inputManager.Jumping && wallOpposite && playerSettings.canWallBounce && playerControl.IsControllable && playerEvents.Events.InvokeCanWallBounce();
}
