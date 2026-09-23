using cowsins;
using UnityEngine;

/// <summary>
/// Bundles all player contexts and states, so we can only pass
/// the interfaces or states that specific behaviours need.
/// </summary>
public class MovementContext
{
    // ---------------- CORE REFS ----------------
    public Transform Transform { get; set; }
    public Rigidbody Rigidbody { get; set; }
    public CapsuleCollider Capsule { get; set; }
    public Transform Camera { get; set; }
    public LayerMask WhatIsGround { get; set; }

    // ---------------- DEPENDENCIES / CONFIG ----------------
    public InputManager InputManager { get; set; }
    public PlayerMovementSettings Settings { get; set; }
    public PlayerDependencies Dependencies { get; set; }

    // ---------------- GLOBAL STATE ----------------
    public bool IsPlayerOnSlope;

    // Last ground hit info
    public RaycastHit SlopeHit;

    public bool HasJumped;
    public float CoyoteTimer;
    public float CoyoteJumpTime;

    public bool WallLeft;

    public bool EnoughStaminaToRun = true;
    public bool EnoughStaminaToJump = true;
}