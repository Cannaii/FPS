using UnityEngine;

namespace AFPS.Simulation.Characters.Collision
{
    public static partial class KinematicCharacterMotor
    {
        public static void Move(
            Vector3 startPosition,
            Vector3 startVelocity,
            float seconds,
            in CharacterCollisionConfig config,
            ICharacterCollisionWorld world,
            out Vector3 position,
            out Vector3 velocity,
            out bool grounded)
        {
            if (world == null)
            {
                throw new System.ArgumentNullException(nameof(world));
            }

            Move(
                PlayerMovementState.ToFixed(startPosition),
                PlayerMovementState.ToFixed(startVelocity),
                SimulationStep.FromSeconds(seconds),
                config,
                LegacyCharacterCollisionWorldAdapter.Adapt(world),
                out var fixedPosition,
                out var fixedVelocity,
                out grounded);
            position = PlayerMovementState.ToVector(fixedPosition);
            velocity = PlayerMovementState.ToVector(fixedVelocity);
        }
    }
}
