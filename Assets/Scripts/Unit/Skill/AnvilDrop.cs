#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using UnityEngine;

namespace InTheArena.Unit
{
    [DisallowMultipleComponent]
    public sealed class AnvilDrop : MonoBehaviour
    {
        private static ObjectPoolingFactory<AnvilDrop> s_Factory;
        private static readonly List<AnvilDrop> ActiveDrops = new List<AnvilDrop>();

        /// <summary>전투 수명에 묶인 프리팹별 제한 풀에서 낙하체를 대여합니다.</summary>
        public static bool TryRent(GameObject prefab, Vector3 position, out AnvilDrop drop)
        {
            drop = null;
            if (prefab == null || prefab.GetComponent<AnvilDrop>() == null)
            {
                return false;
            }
            if (s_Factory == null)
            {
                s_Factory = new ObjectPoolingFactory<AnvilDrop>(BattleSimulation.EnsureExists().transform);
            }
            if (!s_Factory.IsRegistered(prefab))
            {
                s_Factory.Register(prefab, new PoolPolicy(0, 108, PoolScope.Stage));
            }
            var spawn = new PoolSpawnContext(null, position, Quaternion.identity, false);
            return s_Factory.TryRent(prefab, spawn, out drop);
        }

        /// <summary>결과 확정 시 미도착 낙하체를 피해 없이 회수합니다.</summary>
        public static void CancelAll()
        {
            for (int i = ActiveDrops.Count - 1; i >= 0; i--)
            {
                if (ActiveDrops[i] != null)
                {
                    s_Factory?.Return(ActiveDrops[i]);
                }
            }
            ActiveDrops.Clear();
        }

        /// <summary>전투 씬 종료 시 풀과 정적 참조를 정리합니다.</summary>
        public static void ClearPool()
        {
            CancelAll();
            s_Factory?.Clear();
            s_Factory = null;
        }

        private UnitHandle m_Source;
        private Vector3 m_StartPosition;
        private Vector3 m_ImpactPosition;
        private Vector3 m_DamageCenter;
        private float m_ImpactRadius;
        private float m_Damage;
        private float m_FallDuration;
        private float m_BaseXRotationDegrees;
        private float m_StartZRotationDegrees;
        private float m_ZRotationDegrees;
        private float m_Elapsed;
        private float m_LastStep;
        private string m_ActionName;
        private GameObject m_ImpactVfxPrefab;
        private Vector3 m_ImpactVfxOffset;
        private float m_ImpactVfxScale;
        private float m_ImpactVfxDuration;

        public void Initialize(
            Unit source,
            Vector3 visualImpactPosition,
            Vector3 damageCenter,
            float impactRadius,
            float damage,
            string actionName,
            float spawnHeight,
            float spawnDepth,
            float fallDuration,
            float baseXRotationDegrees,
            float startZRotationDegrees,
            float zRotationDegrees,
            GameObject impactVfxPrefab,
            Vector3 impactVfxOffset,
            float impactVfxScale,
            float impactVfxDuration)
        {
            m_Source = new UnitHandle(source);
            m_ImpactPosition = visualImpactPosition;
            m_StartPosition = m_ImpactPosition +
                              Vector3.up * Mathf.Max(0.1f, spawnHeight) +
                              Vector3.forward * Mathf.Max(0f, spawnDepth);
            m_DamageCenter = damageCenter;
            m_ImpactRadius = Mathf.Max(0.1f, impactRadius);
            m_Damage = Mathf.Max(0f, damage);
            m_FallDuration = Mathf.Max(0.05f, fallDuration);
            m_BaseXRotationDegrees = baseXRotationDegrees;
            m_StartZRotationDegrees = startZRotationDegrees;
            m_ZRotationDegrees = zRotationDegrees;
            m_ImpactVfxPrefab = impactVfxPrefab;
            m_ImpactVfxOffset = impactVfxOffset;
            m_ImpactVfxScale = Mathf.Max(0f, impactVfxScale);
            m_ImpactVfxDuration = Mathf.Max(0f, impactVfxDuration);
            m_Elapsed = 0f;
            m_LastStep = 0f;
            m_ActionName = string.IsNullOrWhiteSpace(actionName) ? "스킬" : actionName;
            transform.position = m_StartPosition;
            transform.rotation = Quaternion.Euler(
                m_BaseXRotationDegrees,
                0f,
                m_StartZRotationDegrees);
            gameObject.SetActive(true);
        }

        /// <summary>유닛·투사체와 같은 논리 틱을 구독합니다.</summary>
        private void OnEnable()
        {
            BattleSimulation.SimulationStepped += AdvanceFall;
            ActiveDrops.Add(this);
        }

        /// <summary>풀 반환이나 파괴 시 틱 구독을 해제합니다.</summary>
        private void OnDisable()
        {
            BattleSimulation.SimulationStepped -= AdvanceFall;
            ActiveDrops.Remove(this);
        }

        /// <summary>전투가 받아들인 시간만큼 낙하를 진행합니다.</summary>
        private void AdvanceFall(float deltaTime)
        {
            m_LastStep = deltaTime;
            m_Elapsed += deltaTime;
            if (m_Elapsed < m_FallDuration)
            {
                return;
            }

            ApplyImpact();
            if (s_Factory == null || !s_Factory.Return(this))
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
        }

        /// <summary>낙하 판정과 분리하여 화면 위치·회전을 렌더 프레임마다 보간합니다.</summary>
        private void LateUpdate()
        {
            float elapsed = Mathf.Max(0f, m_Elapsed - m_LastStep + m_LastStep * BattleSimulation.InterpolationAlpha);
            float t = Mathf.Clamp01(elapsed / m_FallDuration);
            transform.position = Vector3.Lerp(m_StartPosition, m_ImpactPosition, t * t);
            transform.rotation = Quaternion.Euler(m_BaseXRotationDegrees, 0f, m_StartZRotationDegrees + m_ZRotationDegrees * t);
        }

        private void ApplyImpact()
        {
            Unit source = m_Source.Unit;
            if (source == null || source.IsDead || m_Damage <= 0f) return;

            Unit firstHitTarget = null;
            using (UnityEngine.Pool.ListPool<UnitHandle>.Get(out var targets))
            {
                UnitRegistry.CaptureEnemiesInRadius(source.Team, m_DamageCenter, m_ImpactRadius, targets);
                for (int i = 0; i < targets.Count; i++)
                {
                    Unit target = targets[i].Unit;
                    if (target == null || target.IsDead)
                    {
                        continue;
                    }

                    var damage = new DamageContext
                    {
                        Source = new UnitHandle(source),
                        Target = target,
                        Amount = m_Damage,
                        IgnoreDefense = true,
                        IsCritical = false,
                        IsSkill = true,
                        IsReaction = false
                    };
                    float actualDamage = target.ApplyDamage(in damage);
                    if (actualDamage <= 0f)
                    {
                        continue;
                    }
                    if (firstHitTarget == null)
                    {
                        firstHitTarget = target;
                    }
                    source.LogCombatAction(m_ActionName, target, actualDamage, "피해");
                }
            }

            SkillVfxUtility.TryRequest(
                m_ImpactVfxPrefab,
                SkillVfxSpawnPosition.GroundPosition,
                m_ImpactVfxOffset,
                source,
                null,
                firstHitTarget,
                m_ImpactPosition,
                m_ImpactVfxScale,
                m_ImpactVfxDuration);
        }
    }
}
#endif
