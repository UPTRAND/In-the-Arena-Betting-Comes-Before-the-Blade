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

        public static IReadOnlyList<Unit> RedTeam => RedUnits;
        public static IReadOnlyList<Unit> BlueTeam => BlueUnits;
        public static int RedAliveCount { get; private set; }
        public static int BlueAliveCount { get; private set; }

        public static void Register(Unit unit)
        {
            if (unit == null) return;

            List<Unit> team = unit.Team == 0 ? RedUnits : BlueUnits;
            if (team.Contains(unit)) return;

            team.Add(unit);
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

            if (!unit.IsDead)
            {
                if (unit.Team == 0) RedAliveCount = Mathf.Max(0, RedAliveCount - 1);
                else BlueAliveCount = Mathf.Max(0, BlueAliveCount - 1);
            }
        }

        public static void NotifyDeath(Unit unit)
        {
            if (unit == null) return;
            if (unit.Team == 0) RedAliveCount = Mathf.Max(0, RedAliveCount - 1);
            else BlueAliveCount = Mathf.Max(0, BlueAliveCount - 1);
        }

        public static Unit FindBestTarget(
            Unit owner,
            TargetPriorityType priority,
            float maxDistance)
        {
            if (owner == null) return null;

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

            List<Unit> team = owner.Team == 0 ? RedUnits : BlueUnits;
            Vector3 result = Vector3.zero;
            Vector3 origin = owner.transform.position;
            float radiusSqr = radius * radius;

            for (int i = 0; i < team.Count; i++)
            {
                Unit other = team[i];
                if (other == null || other == owner || other.IsDead || !other.gameObject.activeInHierarchy)
                    continue;

                Vector3 delta = origin - other.transform.position;
                delta.y = 0f;
                float sqr = delta.sqrMagnitude;
                if (sqr <= 0.0001f || sqr >= radiusSqr) continue;

                float distance = Mathf.Sqrt(sqr);
                result += delta / distance * (1f - distance / radius);
            }

            return Vector3.ClampMagnitude(result, 1f);
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
        }
    }

    /// <summary>
    /// 유닛별 Update를 대체하는 단일 전투 스케줄러입니다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class UnitSimulationSystem : MonoBehaviour
    {
        private const float SimulationStep = 1f / 20f;
        private const int InitialCapacity = 108;
        private static UnitSimulationSystem s_Instance;
        private readonly List<Unit> m_Units = new List<Unit>(InitialCapacity);
        private readonly SkillTriggerContext[] m_EventQueue = new SkillTriggerContext[256];
        private int m_EventCount;
        private float m_Accumulator;

        public static UnitSimulationSystem EnsureExists()
        {
            if (s_Instance != null) return s_Instance;

            var gameObject = new GameObject("[UnitRuntime]");
            s_Instance = gameObject.AddComponent<UnitSimulationSystem>();
            return s_Instance;
        }

        public static void Register(Unit unit)
        {
            UnitSimulationSystem system = EnsureExists();
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
            UnitSimulationSystem system = EnsureExists();
            if (system.m_EventCount >= system.m_EventQueue.Length)
            {
                Debug.LogWarning("[UnitSimulationSystem] 스킬 이벤트 큐가 가득 차 이벤트를 폐기했습니다.");
                return;
            }
            system.m_EventQueue[system.m_EventCount++] = context;
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
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            m_Accumulator = Mathf.Min(m_Accumulator + deltaTime, SimulationStep * 4f);

            while (m_Accumulator >= SimulationStep)
            {
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
                    unit.SimulationFrame(deltaTime);
            }

            PoolManager.Instance?.Projectiles?.SimulationFrame(deltaTime);
        }

        private void DrainSkillEvents()
        {
            int index = 0;
            while (index < m_EventCount)
            {
                SkillTriggerContext context = m_EventQueue[index];
                m_EventQueue[index++] = default;
                Unit receiver = context.Receiver.Unit;
                receiver?.DispatchSkillTrigger(context);
            }
            m_EventCount = 0;
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
                m_EventCount = 0;
                UnitRegistry.Clear();
                PoolManager.Instance?.ClearStage();
            }
        }
    }
}
#endif
