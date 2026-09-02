using UnityEngine;
using Verse;

namespace VanillaCombatOverhaul
{
    public enum AttackFacing
    {
        Front,
        Left,
        Right,
        Rear
    }

    /// <summary>
    /// Which side of a target an attack arrives from. Shared by parrying and directional
    /// damage so both agree on what "flanked" means.
    /// </summary>
    public static class FacingUtility
    {
        /// <summary>Facing of an attack travelling from <paramref name="origin"/> onto <paramref name="target"/>.</summary>
        public static AttackFacing Relative(IntVec3 origin, Thing target)
        {
            // A downed pawn has no meaningful facing and cannot answer anything.
            if (target is Pawn p && p.Downed)
            {
                return AttackFacing.Front;
            }

            var offset = (target.Position - origin).ToVector3().Yto0();
            if (offset == Vector3.zero)
            {
                return AttackFacing.Front;
            }
            return FromTravelAngle(Quaternion.LookRotation(offset).eulerAngles.y, target);
        }

        /// <summary>
        /// Facing derived from a projectile's direction of travel, for damage whose instigator
        /// is gone or unspawned by the time it resolves.
        /// </summary>
        public static AttackFacing FromTravelAngle(float travelAngle, Thing target)
        {
            if (target is Pawn p && p.Downed)
            {
                return AttackFacing.Front;
            }
            return FromTravelAngleFor(travelAngle, target.Rotation);
        }

        /// <summary>
        /// The facing calculation itself, taking a bare rotation rather than a Thing so it can
        /// be exercised directly by the test harness without spawning anything.
        /// </summary>
        public static AttackFacing FromTravelAngleFor(float travelAngle, Rot4 targetRotation)
        {
            var incoming = Rot4.FromAngleFlat(travelAngle);
            switch (Rot4.GetRelativeRotation(incoming, targetRotation))
            {
                case RotationDirection.Opposite:
                    return AttackFacing.Front;
                case RotationDirection.Clockwise:
                    return AttackFacing.Right;
                case RotationDirection.Counterclockwise:
                    return AttackFacing.Left;
                default:
                    return AttackFacing.Rear;
            }
        }

        public static bool IsFlank(this AttackFacing facing) =>
            facing == AttackFacing.Left || facing == AttackFacing.Right;
    }
}
