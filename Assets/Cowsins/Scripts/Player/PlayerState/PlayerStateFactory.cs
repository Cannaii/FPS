namespace cowsins
{
    public class PlayerStateFactory
    {
        private readonly PlayerStates _context;

        // Cache Player States
        // DashState is not cached because it depends on runtime input (direction vector)
        private readonly PlayerDefaultState _defaultState;
        private readonly PlayerJumpState _jumpState;
        private readonly PlayerCrouchState _crouchState;
        private readonly PlayerDeadState _deadState;
        private readonly PlayerClimbState _climbState;


        // When PlayerStateFactory is created, retrieve all PlayerStates and store them.
        public PlayerStateFactory(PlayerStates currentContext)
        {
            _context = currentContext;

            _defaultState = new PlayerDefaultState(_context, this);
            _jumpState = new PlayerJumpState(_context, this);
            _crouchState = new PlayerCrouchState(_context, this);
            _deadState = new PlayerDeadState(_context, this);
            _climbState = new PlayerClimbState(_context, this);
        }

        // Access Player States
        public PlayerBaseState Default() => _defaultState;
        public PlayerBaseState Jump() => _jumpState;
        public PlayerBaseState Crouch() => _crouchState;
        public PlayerBaseState Die() => _deadState;
        public PlayerBaseState Climb() => _climbState;

        // Dash state is created dynamically since it depends on current input direction
        public PlayerBaseState Dash() => new PlayerDashState(_context, this, new UnityEngine.Vector2(_context.InputManager.X, _context.InputManager.Y));
    }
}
