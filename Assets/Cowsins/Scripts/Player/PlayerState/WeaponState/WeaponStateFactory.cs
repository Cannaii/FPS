namespace cowsins
{
    public class WeaponStateFactory
    {
        private readonly WeaponStates _context;

        // Cache Weapon States
        private readonly WeaponDefaultState _defaultState;
        private readonly WeaponReloadState _reloadState;
        private readonly WeaponShootingState _shootingState;
        private readonly MeleeState _meleeState;
        private readonly WeaponInspectState _inspectState;
        private readonly WeaponUnholsterState _unholsterState;
        private readonly WeaponHiddenState _hiddenState;

        // When WeaponStateFactory is created, retrieve all WeaponStates and store them.
        public WeaponStateFactory(WeaponStates currentContext)
        {
            _context = currentContext;

            _defaultState = new WeaponDefaultState(_context, this);
            _reloadState = new WeaponReloadState(_context, this);
            _shootingState = new WeaponShootingState(_context, this);
            _meleeState = new MeleeState(_context, this);
            _inspectState = new WeaponInspectState(_context, this);
            _unholsterState = new WeaponUnholsterState(_context, this);
            _hiddenState = new WeaponHiddenState(_context, this);
        }

        // Access Weapon States
        public WeaponBaseState Default() => _defaultState;
        public WeaponBaseState Reload() => _reloadState;
        public WeaponBaseState Shoot() => _shootingState;
        public WeaponBaseState Melee() => _meleeState;
        public WeaponBaseState Inspect() => _inspectState;
        public WeaponBaseState Unholster() => _unholsterState;
        public WeaponBaseState Hidden() => _hiddenState;
    }
}
