using System;
using UnityEngine;
using UnityEngine.Events;

namespace cowsins
{
    public class PlayerMovementEvents
    {
        public UnityEvent<Vector3, Quaternion, bool, bool> OnRespawn = new UnityEvent<Vector3, Quaternion, bool, bool>();

        public UnityEvent OnStaminaDepleted = new UnityEvent();
        public UnityEvent OnMoving = new UnityEvent();
        public UnityEvent OnIdle = new UnityEvent();
        public UnityEvent OnIdleToMove = new UnityEvent();
        public UnityEvent OnMovingToIdle = new UnityEvent();

        public UnityEvent OnCrouchStart = new UnityEvent();
        public UnityEvent OnCrouching = new UnityEvent();
        public UnityEvent OnCrouchStop = new UnityEvent();
        public UnityEvent OnSlideStart = new UnityEvent();
        public UnityEvent OnSlideEnd = new UnityEvent();
        public Func<bool> AllowSlide;

        public UnityEvent OnJump = new UnityEvent();
        public UnityEvent OnLand = new UnityEvent();
        public UnityEvent OnWallJump = new UnityEvent();

        public UnityEvent<int> OnInitializeDash = new UnityEvent<int>();
        public UnityEvent<int> OnDashUsed = new UnityEvent<int>();
        public UnityEvent<int> OnDashGained = new UnityEvent<int>();
        public UnityEvent OnDashStart = new UnityEvent();
        public UnityEvent OnDashing = new UnityEvent();
        public UnityEvent OnDashStop = new UnityEvent();

        public UnityEvent<bool?> OnClimbStart = new UnityEvent<bool?>();
        public UnityEvent<bool?> OnClimbStop = new UnityEvent<bool?>();

        public UnityEvent OnWallRunStart = new UnityEvent();
        public UnityEvent OnWallRunStop = new UnityEvent();

        public Func<bool> CanWallBounce;
        public UnityEvent OnWallBounceStart = new UnityEvent();

        public UnityEvent OnGrappleStart = new UnityEvent();

        public bool InvokeAllowSlide() => CowsinsUtilities.InvokeFunc(AllowSlide, false);
        public bool InvokeCanWallBounce() => CowsinsUtilities.InvokeFunc(CanWallBounce, false);
    }

}