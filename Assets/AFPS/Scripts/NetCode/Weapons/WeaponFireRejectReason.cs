namespace AFPS.NetCode.Weapons
{
    public enum WeaponFireRejectReason : byte
    {
        None = 0,
        NotRequested = 1,
        InvalidSequence = 2,
        DuplicateOrOldSequence = 3,
        WeaponUnavailable = 4,
        FireRateLimited = 5,
        EmptyMagazine = 6,
        InvalidAim = 7
    }
}
