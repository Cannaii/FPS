using UnityEngine;
namespace cowsins
{
    public class PlayerStates : MonoBehaviour
    {
        PlayerBaseState _currentState;
        PlayerStateFactory _states;

        public PlayerBaseState CurrentState { get { return _currentState; } set { _currentState = value; } }
        public PlayerStateFactory _States { get { return _states; } set { _states = value; } }

        // Shared references stored in context to avoid repeated GetComponent calls across states.
        public IPlayerControlProvider PlayerControlProvider { get; private set; } // IPlayerControlProvider is implemented in PlayerControl.cs
        public IPlayerStatsProvider PlayerStatsProvider { get; private set; } // IPlayerStatsProvider is implemented in PlayerStats.cs
        public Rigidbody Rigidbody { get; private set; }
        public PlayerMovement PlayerMovement { get; private set; }
        public InputManager InputManager { get; private set; }

        private void Start()
        {
            GetContextReferences();

            _states = new PlayerStateFactory(this);
            _currentState = _states.Default();
            _currentState.EnterState();
        }

        private void Update()
        {
            _currentState.UpdateState();
        }

        private void FixedUpdate()
        {
            _currentState.FixedUpdateState();
        }

        /// <summary>
        /// Force to change a Player state by passing the desired new state.
        /// </summary>
        /// <param name="newState"></param>
        public void ForceChangeState(PlayerBaseState newState)
        {
            _currentState.ExitState();
            _currentState = newState;
            _currentState.EnterState();
        }

        public bool IsInState<T>() where T : PlayerBaseState
        {
            return _currentState is T;
        }

        private void GetContextReferences()
        {
            PlayerControlProvider = GetComponent<IPlayerControlProvider>();
            Rigidbody = GetComponent<Rigidbody>();
            PlayerMovement = GetComponent<PlayerMovement>();
            PlayerStatsProvider = GetComponent<IPlayerStatsProvider>();
            InputManager = GetComponent<PlayerDependencies>().InputManager;
        }
    }
}