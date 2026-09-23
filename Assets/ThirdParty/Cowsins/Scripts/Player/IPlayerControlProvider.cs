namespace cowsins
{
    // Implemented by PlayerControl and required by PlayerDependencies
    public interface IPlayerControlProvider
    {
        bool IsControllable { get; }
        bool IsMovementControllable { get; }

        void GrantControl();
        void LoseControl();
        void CheckIfCanGrantControl();
    }
}