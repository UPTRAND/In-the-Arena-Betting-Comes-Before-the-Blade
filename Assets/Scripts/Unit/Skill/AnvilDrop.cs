#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using UnityEngine;

namespace InTheArena.Unit
{
    [DisallowMultipleComponent]
    public sealed class AnvilDrop : MonoBehaviour
    {
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
        private string m_ActionName;

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
            float zRotationDegrees)
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
            m_Elapsed = 0f;
            m_ActionName = string.IsNullOrWhiteSpace(actionName) ? "스킬" : actionName;
            transform.position = m_StartPosition;
            transform.rotation = Quaternion.Euler(
                m_BaseXRotationDegrees,
                0f,
                m_StartZRotationDegrees);
        }

        private void Update()
        {
            m_Elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(m_Elapsed / m_FallDuration);
            float easedT = t * t;

            transform.position = Vector3.Lerp(m_StartPosition, m_ImpactPosition, easedT);
            transform.rotation = Quaternion.Euler(
                m_BaseXRotationDegrees,
                0f,
                m_StartZRotationDegrees + m_ZRotationDegrees * t);

            if (t < 1f) return;

            ApplyImpact();
            Destroy(gameObject);
        }

        private void ApplyImpact()
        {
            Unit source = m_Source.Unit;
            if (source == null || source.IsDead || m_Damage <= 0f) return;

            IReadOnlyList<Unit> enemies = source.Team == 0
                ? UnitRegistry.BlueTeam
                : UnitRegistry.RedTeam;
            float radiusSqr = m_ImpactRadius * m_ImpactRadius;
            for (int i = 0; i < enemies.Count; i++)
            {
                Unit target = enemies[i];
                if (target == null || target.IsDead) continue;

                Vector3 delta = target.GroundPosition - m_DamageCenter;
                delta.y = 0f;
                if (delta.sqrMagnitude > radiusSqr) continue;

                var damage = new DamageContext
                {
                    Source = new UnitHandle(source),
                    Target = target,
                    Amount = m_Damage + target.CurrentDefense,
                    IsCritical = false,
                    IsSkill = true,
                    IsReaction = false
                };
                float actualDamage = target.ApplyDamage(in damage);
                if (actualDamage <= 0f) continue;
                source.LogCombatAction(m_ActionName, target, actualDamage, "피해");
            }
        }
    }
}
#endif
