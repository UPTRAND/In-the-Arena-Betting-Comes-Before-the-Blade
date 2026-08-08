#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using UnityEngine;

namespace InTheArena.Unit
{
    /// <summary>
    /// 활성 유닛을 팀별로 보관하는 무할당 런타임 레지스트리입니다.
    /// 대상 탐색, 생존 판정, 카메라 프레이밍이 동일한 목록을 공유합니다.
    /// </summary>
    public static class UnitRegistry
    {
        private const int InitialCapacity = 108;
        private static readonly List<Unit> RedUnits = new List<Unit>(InitialCapacity);
        private static readonly List<Unit> BlueUnits = new List<Unit>(InitialCapacity);
        private static readonly UnitSpatialIndex SpatialIndex = new UnitSpatialIndex();
        private static bool s_SpatialIndexReady;

        public static IReadOnlyList<Unit> RedTeam => RedUnits;
        public static IReadOnlyList<Unit> BlueTeam => BlueUnits;
        public static int RedAliveCount { get; private set; }
        public static int BlueAliveCount { get; private set; }

        public static void RebuildSpatialIndex()
        {
            SpatialIndex.Rebuild(RedUnits, BlueUnits);
            s_SpatialIndexReady = true;
        }

        public static void Register(Unit unit)
        {
            if (unit == null) return;

            List<Unit> team = unit.Team == 0 ? RedUnits : BlueUnits;
            if (team.Contains(unit)) return;

            team.Add(unit);
            s_SpatialIndexReady = false;
            if (!unit.IsDead)
            {
                if (unit.Team == 0) RedAliveCount++;
                else BlueAliveCount++;
            }
        }

        public static void Unregister(Unit unit)
        {
            if (unit == null) return;

            List<Unit> team = unit.Team == 0 ? RedUnits : BlueUnits;
            if (!team.Remove(unit)) return;
            s_SpatialIndexReady = false;
            EngagementSlotSystem.Release(unit);
            EngagementSlotSystem.ReleaseTarget(unit);

            if (!unit.IsDead)
            {
                if (unit.Team == 0) RedAliveCount = Mathf.Max(0, RedAliveCount - 1);
                else BlueAliveCount = Mathf.Max(0, BlueAliveCount - 1);
            }
        }

        public static void NotifyDeath(Unit unit)
        {
            if (unit == null) return;
            EngagementSlotSystem.Release(unit);
            EngagementSlotSystem.ReleaseTarget(unit);
            if (unit.Team == 0) RedAliveCount = Mathf.Max(0, RedAliveCount - 1);
            else BlueAliveCount = Mathf.Max(0, BlueAliveCount - 1);
        }

        public static Unit FindBestTarget(
            Unit owner,
            TargetPriorityType priority,
            float maxDistance)
        {
            if (owner == null) return null;
            if (!s_SpatialIndexReady) RebuildSpatialIndex();

            if (priority == TargetPriorityType.Nearest ||
                priority == TargetPriorityType.HighestThreat)
            {
                Unit spatialTarget = SpatialIndex.FindNearestEnemy(owner, maxDistance);
                if (spatialTarget != null) return spatialTarget;
            }

            List<Unit> enemies = owner.Team == 0 ? BlueUnits : RedUnits;
            Unit best = null;
            float bestScore = float.MaxValue;
            float maxDistanceSqr = maxDistance > 0f ? maxDistance * maxDistance : float.MaxValue;
            Vector3 ownerPosition = owner.transform.position;

            for (int i = 0; i < enemies.Count; i++)
            {
                Unit candidate = enemies[i];
                if (candidate == null || candidate.IsDead || !candidate.gameObject.activeInHierarchy)
                    continue;

                Vector3 offset = candidate.transform.position - ownerPosition;
                offset.y = 0f;
                float distanceSqr = offset.sqrMagnitude;
                if (distanceSqr > maxDistanceSqr) continue;

                float score;
                switch (priority)
                {
                    case TargetPriorityType.LowestHp:
                        score = candidate.CurrentHp / Mathf.Max(1f, candidate.MaxHp) * 100000f + distanceSqr;
                        break;
                    case TargetPriorityType.Random:
                        score = Random.value * 100000f;
                        break;
                    case TargetPriorityType.HighestThreat:
                    case TargetPriorityType.Nearest:
                    default:
                        score = distanceSqr;
                        break;
                }

                if (score >= bestScore) continue;
                bestScore = score;
                best = candidate;
            }

            return best;
        }

        /// <summary>
        /// 작은 전투 규모에 맞춘 무할당 soft-separation 계산입니다.
        /// 물리 Contact 대신 같은 팀 유닛끼리의 겹침만 완화합니다.
        /// </summary>
        public static Vector3 CalculateSeparation(Unit owner, float radius)
        {
            if (owner == null || radius <= 0f) return Vector3.zero;
            if (!s_SpatialIndexReady) RebuildSpatialIndex();
            return SpatialIndex.CalculateSeparation(owner, radius);
        }

        public static Vector3 GetEngagementPosition(Unit owner, Unit target)
            => EngagementSlotSystem.GetPosition(owner, target);

        public static int CollectEnemiesInRadius(
            int sourceTeam,
            Vector3 position,
            float radius,
            Unit[] output)
        {
            if (!s_SpatialIndexReady) RebuildSpatialIndex();
            return SpatialIndex.CollectEnemiesInRadius(sourceTeam, position, radius, output);
        }

        public static bool TryCalculateLivingBounds(float visualRadius, out Bounds bounds)
        {
            bool initialized = false;
            bounds = default;
            EncapsulateLiving(RedUnits, visualRadius, ref initialized, ref bounds);
            EncapsulateLiving(BlueUnits, visualRadius, ref initialized, ref bounds);
            return initialized;
        }

        private static void EncapsulateLiving(
            List<Unit> units,
            float visualRadius,
            ref bool initialized,
            ref Bounds bounds)
        {
            Vector3 size = Vector3.one * Mathf.Max(0.1f, visualRadius * 2f);
            for (int i = 0; i < units.Count; i++)
            {
                Unit unit = units[i];
                if (unit == null || unit.IsDead || !unit.gameObject.activeInHierarchy) continue;

                Bounds unitBounds = new Bounds(unit.GroundPosition, size);
                unitBounds.Encapsulate(unit.HitPosition);
                if (!initialized)
                {
                    bounds = unitBounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(unitBounds);
                }
            }
        }

        public static void Clear()
        {
            RedUnits.Clear();
            BlueUnits.Clear();
            RedAliveCount = 0;
            BlueAliveCount = 0;
            SpatialIndex.Rebuild(RedUnits, BlueUnits);
            s_SpatialIndexReady = true;
            EngagementSlotSystem.Clear();
        }
    }

    /// <summary>
    /// 유닛별 Update를 대체하는 단일 전투 스케줄러입니다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class BattleSimulation : MonoBehaviour
    {
        private const float SimulationStep = 1f / 20f;
        private const int InitialCapacity = 108;
        private const int EventCapacity = 2048;
        private static BattleSimulation s_Instance;
        private static int s_NextUnitId;
        private readonly List<Unit> m_Units = new List<Unit>(InitialCapacity);
        private readonly SkillTriggerContext[] m_EventQueue = new SkillTriggerContext[EventCapacity];
        private int m_EventHead;
        private int m_EventTail;
        private int m_EventCount;
        private float m_Accumulator;

        public static int PeakEventCount { get; private set; }
        public static int EventOverflowCount { get; private set; }

        public static BattleSimulation EnsureExists()
        {
            if (s_Instance != null) return s_Instance;

            var gameObject = new GameObject("[BattleSimulation]");
            s_Instance = gameObject.AddComponent<BattleSimulation>();
            return s_Instance;
        }

        public static void Register(Unit unit)
        {
            BattleSimulation system = EnsureExists();
            unit.AssignSimulationId(++s_NextUnitId);
            if (!system.m_Units.Contains(unit)) system.m_Units.Add(unit);
            UnitRegistry.Register(unit);
        }

        public static void Unregister(Unit unit)
        {
            if (s_Instance != null) s_Instance.m_Units.Remove(unit);
            UnitRegistry.Unregister(unit);
        }

        public static void EnqueueSkillEvent(in SkillTriggerContext context)
        {
            BattleSimulation system = EnsureExists();
            if (system.m_EventCount >= system.m_EventQueue.Length)
            {
                EventOverflowCount++;
                Unit receiver = context.Receiver.Unit;
                receiver?.DispatchSkillTrigger(context);
                return;
            }
            system.m_EventQueue[system.m_EventTail] = context;
            system.m_EventTail = (system.m_EventTail + 1) % system.m_EventQueue.Length;
            system.m_EventCount++;
            PeakEventCount = Mathf.Max(PeakEventCount, system.m_EventCount);
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
            StatusEffectRuntimePool.Prewarm(256);
            UnitHpBarPresenter.EnsureExists(transform);
            SkillVfxPresenter.EnsureExists(transform);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            m_Accumulator = Mathf.Min(m_Accumulator + deltaTime, SimulationStep * 4f);

            while (m_Accumulator >= SimulationStep)
            {
                UnitRegistry.RebuildSpatialIndex();
                for (int i = m_Units.Count - 1; i >= 0; i--)
                {
                    Unit unit = m_Units[i];
                    if (unit == null)
                    {
                        m_Units.RemoveAt(i);
                        continue;
                    }

                    if (unit.gameObject.activeInHierarchy) unit.SimulationTick(SimulationStep);
                }
                DrainSkillEvents();
                m_Accumulator -= SimulationStep;
            }

            for (int i = 0; i < m_Units.Count; i++)
            {
                Unit unit = m_Units[i];
                if (unit != null && unit.gameObject.activeInHierarchy)
                    unit.SimulationFrame(deltaTime, m_Accumulator / SimulationStep);
            }

            PoolManager.Instance?.Projectiles?.SimulationFrame(deltaTime);
        }

        private void DrainSkillEvents()
        {
            while (m_EventCount > 0)
            {
                SkillTriggerContext context = m_EventQueue[m_EventHead];
                m_EventQueue[m_EventHead] = default;
                m_EventHead = (m_EventHead + 1) % m_EventQueue.Length;
                m_EventCount--;
                Unit receiver = context.Receiver.Unit;
                receiver?.DispatchSkillTrigger(context);
            }
        }

        private void LateUpdate()
        {
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null) return;

            Quaternion cameraRotation = camera.transform.rotation;
            for (int i = 0; i < m_Units.Count; i++)
            {
                Unit unit = m_Units[i];
                if (unit != null && unit.gameObject.activeInHierarchy)
                    unit.ApplyBillboard(cameraRotation);
            }

            UnitHpBarPresenter.Instance?.Refresh(camera);
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
                m_EventHead = 0;
                m_EventTail = 0;
                m_EventCount = 0;
                PeakEventCount = 0;
                EventOverflowCount = 0;
                s_NextUnitId = 0;
                UnitRegistry.Clear();
                PoolManager.Instance?.ClearStage();
            }
        }
    }
}
#endif
