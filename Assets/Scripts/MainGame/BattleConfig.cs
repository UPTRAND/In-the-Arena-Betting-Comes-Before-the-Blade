#if UNITY_6000_0_OR_NEWER
using InTheArena.Unit;
using UnityEngine;

namespace InTheArena.MainGame
{
    public readonly struct SpawnPlan
    {
        public readonly UnitData UnitData;
        public readonly Team Team;
        public readonly int CellIndex;
        public readonly Vector3 Position;

        public SpawnPlan(UnitData unitData, Team team, int cellIndex, Vector3 position)
        {
            UnitData = unitData;
            Team = team;
            CellIndex = cellIndex;
            Position = position;
        }
    }

    /// <summary>
    /// Immutable hand-off from betting and round data to the combat runtime.
    /// </summary>
    public sealed class BattleConfig
    {
        private readonly SpawnPlan[] m_SpawnPlans;

        public BattleConfig(SpawnPlan[] spawnPlans, RoundRule roundRule)
        {
            m_SpawnPlans = spawnPlans != null
                ? (SpawnPlan[])spawnPlans.Clone()
                : System.Array.Empty<SpawnPlan>();
            RoundRule = roundRule;
        }

        public System.ReadOnlySpan<SpawnPlan> SpawnPlans => m_SpawnPlans;
        public RoundRule RoundRule { get; }
    }
}
#endif
