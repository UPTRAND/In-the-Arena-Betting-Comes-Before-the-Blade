#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace InTheArena.Unit
{
    /// <summary>
    /// Allocation-free simulation state view. It deliberately contains no presentation references.
    /// </summary>
    public readonly struct UnitRuntime
    {
        public readonly int UnitId;
        public readonly int SpawnVersion;
        public readonly int Team;
        public readonly float Hp;
        public readonly UnitStat Stats;
        public readonly Vector3 Position;
        public readonly Vector3 Velocity;
        public readonly UnitHandle Target;
        public readonly UnitActionState ActionState;
        public readonly float BasicAttackCooldown;

        public UnitRuntime(
            int unitId,
            int spawnVersion,
            int team,
            float hp,
            in UnitStat stats,
            Vector3 position,
            Vector3 velocity,
            UnitHandle target,
            UnitActionState actionState,
            float basicAttackCooldown)
        {
            UnitId = unitId;
            SpawnVersion = spawnVersion;
            Team = team;
            Hp = hp;
            Stats = stats;
            Position = position;
            Velocity = velocity;
            Target = target;
            ActionState = actionState;
            BasicAttackCooldown = basicAttackCooldown;
        }
    }

    public enum UnitIntentType
    {
        Hold = 0,
        Move = 1,
        BasicAttack = 2,
        CastSkill = 3,
        AcquireTarget = 4
    }

    public readonly struct UnitIntent
    {
        public readonly UnitIntentType Type;
        public readonly UnitHandle Target;
        public readonly Vector3 Destination;

        public UnitIntent(UnitIntentType type, Unit target = null, Vector3 destination = default)
        {
            Type = type;
            Target = new UnitHandle(target);
            Destination = destination;
        }
    }

    public static class DecisionSystem
    {
        public static UnitIntent Decide(Unit owner, Unit target, float attackRangeRatio)
        {
            if (owner == null || owner.IsDead || owner.IsStunned)
                return new UnitIntent(UnitIntentType.Hold);
            if (target == null || target.IsDead || !target.gameObject.activeInHierarchy)
                return new UnitIntent(UnitIntentType.AcquireTarget);

            Vector3 delta = target.GroundPosition - owner.GroundPosition;
            delta.y = 0f;
            bool ranged = owner.UnitData?.BasicAttackData?.Delivery
                is HomingProjectileAttackDelivery;
            float configuredStopRange =
                owner.CurrentAttackRange * Mathf.Clamp01(attackRangeRatio);
            float attackRange = ranged
                ? owner.CurrentAttackRange
                : Mathf.Max(
                    configuredStopRange,
                    EngagementSlotSystem.GetContactDistance(owner, target) +
                    EngagementSlotSystem.ArrivalTolerance +
                    EngagementSlotSystem.DistanceEpsilon);
            if (delta.sqrMagnitude > attackRange * attackRange)
            {
                return new UnitIntent(
                    UnitIntentType.Move,
                    target,
                    UnitRegistry.GetEngagementPosition(owner, target));
            }

            if (owner.IsAttacking || owner.IsCastingSkill)
                return new UnitIntent(UnitIntentType.Hold, target);

            for (int i = 0; i < owner.Skills.Count; i++)
            {
                SkillRuntime skill = owner.Skills[i];
                if (skill != null && skill.Data.SkillType == SkillType.Active && skill.CanUse)
                    return new UnitIntent(UnitIntentType.CastSkill, target);
            }
            return new UnitIntent(UnitIntentType.BasicAttack, target);
        }
    }
}
#endif
