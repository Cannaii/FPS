namespace AFPS.NetCode.Weapons
{
    public readonly struct WeaponFireResult
    {
        public readonly bool Accepted;
        public readonly WeaponFireRejectReason RejectReason;
        public readonly int RemainingAmmo;
        public readonly AuthoritativeShot Shot;

        public WeaponFireResult(bool accepted, WeaponFireRejectReason rejectReason, int remainingAmmo, in AuthoritativeShot shot)
        {
            Accepted = accepted;
            RejectReason = rejectReason;
            RemainingAmmo = remainingAmmo;
            Shot = shot;
        }
    }
}
