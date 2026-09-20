#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

namespace InTheArena.Unit
{
    [Serializable]
    public sealed class SelfSkillTargeting : SkillTargetingDefinition
    {
        public override bool TryResolve(
            Unit owner,
            SkillData data,
            in SkillUseRequest request,
            SkillTargetSet result)
        {
            return owner != null && result.Add(owner);
        }

        public override bool Revalidate(Unit owner, SkillData data, SkillTargetSet targets)
            => owner != null && !owner.IsDead && targets != null &&
               targets.Count == 1 && targets[0].Unit == owner;
    }

    [Serializable]
    public sealed class SingleUnitSkillTargeting : SkillTargetingDefinition
    {
        [Tooltip("시전 완료 시 기존 대상이 유효하지 않으면 조건에 맞는 새 대상을 선택합니다.")]
        [SerializeField] private bool m_ReacquireOnTargetLost;
        [SerializeField] private SkillTargetRelation m_Relation = SkillTargetRelation.Enemy;
        [SerializeField] private TargetPriorityType m_Priority = TargetPriorityType.Nearest;
        [SerializeField] private bool m_IncludeSelf = true;

        public override bool TryResolve(
            Unit owner,
            SkillData data,
            in SkillUseRequest request,
            SkillTargetSet result)
        {
            Unit hint = request.TargetHint.Unit;
            if (IsCandidate(owner, hint, data.Range)) return result.Add(hint);

            Unit best = FindBest(owner, data.Range);
            return best != null && result.Add(best);
        }

        public override bool Revalidate(Unit owner, SkillData data, SkillTargetSet targets)
        {
            Unit target = targets != null && targets.Count == 1 ? targets[0].Unit : null;
            if (IsCandidate(owner, target, data.Range))
            {
                return true;
            }
            if (!m_ReacquireOnTargetLost || targets == null)
            {
                return false;
            }

            targets.Clear();
            Unit replacement = FindBest(owner, data.Range);
            return replacement != null && targets.Add(replacement);
        }

        /// <summary>양 팀의 후보를 관계 조건에 맞춰 비교하고 우선 대상 하나를 고릅니다.</summary>
        private Unit FindBest(Unit owner, float range)
        {
            if (owner == null)
            {
                return null;
            }

            if (m_Relation == SkillTargetRelation.Enemy)
            {
                return UnitRegistry.FindBestTarget(owner, m_Priority, range);
            }

            Unit best = null;
            float bestScore = float.MaxValue;
            FindBestInTeam(owner, range, UnitRegistry.RedTeam, ref best, ref bestScore);
            FindBestInTeam(owner, range, UnitRegistry.BlueTeam, ref best, ref bestScore);
            return best;
        }

        /// <summary>팀 목록에서 관계·사거리 조건을 만족하는 대상을 기존 후보와 비교합니다.</summary>
        private void FindBestInTeam(Unit owner, float range, IReadOnlyList<Unit> candidates,
            ref Unit best, ref float bestScore)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                Unit candidate = candidates[i];
                if (!IsCandidate(owner, candidate, range))
                {
                    continue;
                }

                Vector3 delta = candidate.GroundPosition - owner.GroundPosition;
                delta.y = 0f;
                float distanceSqr = delta.sqrMagnitude;
                float score = distanceSqr;
                if (m_Priority == TargetPriorityType.LowestHp)
                {
                    score = candidate.CurrentHp / Mathf.Max(1f, candidate.MaxHp) * 100000f + distanceSqr;
                }
                else if (m_Priority == TargetPriorityType.Random)
                {
                    score = UnityEngine.Random.value;
                }

                if (score >= bestScore)
                {
                    continue;
                }

                best = candidate;
                bestScore = score;
            }
        }

        private bool IsCandidate(Unit owner, Unit candidate, float range)
        {
            if (!m_IncludeSelf && candidate == owner) return false;
            return SkillTargetingUtility.MatchesRelation(owner, candidate, m_Relation) &&
                   SkillTargetingUtility.IsInRange(owner, candidate.GroundPosition, range);
        }
    }

    [Serializable]
    public sealed class LowestHealthAllySkillTargeting : SkillTargetingDefinition
    {
        [SerializeField] private bool m_IncludeSelf = true;
        [SerializeField, Range(0f, 1f)] private float m_MaxHealthRatio = 0.99f;

        public override bool TryResolve(
            Unit owner,
            SkillData data,
            in SkillUseRequest request,
            SkillTargetSet result)
        {
            if (owner == null) return false;
            IReadOnlyList<Unit> allies = owner.Team == 0 ? UnitRegistry.RedTeam : UnitRegistry.BlueTeam;
            Unit best = null;
            float bestRatio = Mathf.Clamp01(m_MaxHealthRatio);
            float bestDistance = float.MaxValue;
            for (int i = 0; i < allies.Count; i++)
            {
                Unit candidate = allies[i];
                if (candidate == null || candidate.IsDead || (!m_IncludeSelf && candidate == owner) ||
                    !candidate.gameObject.activeInHierarchy ||
                    !SkillTargetingUtility.IsInRange(owner, candidate.GroundPosition, data.Range))
                    continue;

                float ratio = candidate.CurrentHp / Mathf.Max(1f, candidate.MaxHp);
                if (ratio > bestRatio) continue;
                Vector3 delta = candidate.GroundPosition - owner.GroundPosition;
                delta.y = 0f;
                float distance = delta.sqrMagnitude;
                if (ratio > bestRatio - 0.0001f && distance >= bestDistance) continue;
                best = candidate;
                bestRatio = ratio;
                bestDistance = distance;
            }
            return best != null && result.Add(best);
        }

        public override bool Revalidate(Unit owner, SkillData data, SkillTargetSet targets)
        {
            Unit target = targets != null && targets.Count == 1 ? targets[0].Unit : null;
            return target != null && !target.IsDead && target.Team == owner.Team &&
                   target.CurrentHp < target.MaxHp &&
                   SkillTargetingUtility.IsInRange(owner, target.GroundPosition, data.Range);
        }
    }

    [Serializable]
    public sealed class GroundAtTargetSkillTargeting : SkillTargetingDefinition
    {
        [SerializeField] private SkillTargetRelation m_Relation = SkillTargetRelation.Enemy;

        public override bool TryResolve(
            Unit owner,
            SkillData data,
            in SkillUseRequest request,
            SkillTargetSet result)
        {
            Vector3 position;
            Unit hint = request.TargetHint.Unit;
            if (request.HasGroundPosition)
                position = request.GroundPosition;
            else if (SkillTargetingUtility.MatchesRelation(owner, hint, m_Relation))
                position = hint.GroundPosition;
            else
            {
                Unit target = m_Relation == SkillTargetRelation.Enemy
                    ? UnitRegistry.FindBestTarget(owner, TargetPriorityType.Nearest, data.Range)
                    : owner;
                if (target == null) return false;
                position = target.GroundPosition;
            }

            position.y = owner.GroundPosition.y;
            if (!SkillTargetingUtility.IsInRange(owner, position, data.Range)) return false;
            result.SetGroundPosition(position);
            return true;
        }

        public override bool Revalidate(Unit owner, SkillData data, SkillTargetSet targets)
            => owner != null && !owner.IsDead && targets != null && targets.HasGroundPosition &&
               SkillTargetingUtility.IsInRange(owner, targets.GroundPosition, data.Range);
    }

    [Serializable]
    public sealed class AreaSkillTargeting : SkillTargetingDefinition
    {
        [SerializeField] private SkillTargetRelation m_Relation = SkillTargetRelation.Enemy;
        [SerializeField, Min(0.1f)] private float m_Radius = 2f;
        [SerializeField] private bool m_CenterOnOwner;

        public override bool TryResolve(
            Unit owner,
            SkillData data,
            in SkillUseRequest request,
            SkillTargetSet result)
        {
            if (owner == null) return false;
            Vector3 center = ResolveCenter(owner, data, request);
            if (!m_CenterOnOwner && !SkillTargetingUtility.IsInRange(owner, center, data.Range))
                return false;

            result.SetGroundPosition(center);
            CollectTargets(owner, center, UnitRegistry.RedTeam, result);
            CollectTargets(owner, center, UnitRegistry.BlueTeam, result);
            return result.Count > 0;
        }

        /// <summary>관계 조건에 맞는 범위 내 대상을 수집하며 Any는 양 팀 모두 검사합니다.</summary>
        private void CollectTargets(Unit owner, Vector3 center, IReadOnlyList<Unit> candidates,
            SkillTargetSet result)
        {
            float radiusSqr = m_Radius * m_Radius;
            for (int i = 0; i < candidates.Count; i++)
            {
                Unit candidate = candidates[i];
                if (!SkillTargetingUtility.MatchesRelation(owner, candidate, m_Relation))
                {
                    continue;
                }

                Vector3 delta = candidate.GroundPosition - center;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSqr)
                {
                    result.Add(candidate);
                }
            }
        }

        /// <summary>시전 완료 시점의 위치로 범위 대상을 다시 계산합니다.</summary>
        public override bool Revalidate(Unit owner, SkillData data, SkillTargetSet targets)
        {
            if (owner == null || owner.IsDead || targets == null || !targets.HasGroundPosition)
            {
                return false;
            }

            Vector3 center = targets.GroundPosition;
            if (m_CenterOnOwner)
            {
                center = owner.GroundPosition;
            }

            if (!m_CenterOnOwner &&
                !SkillTargetingUtility.IsInRange(owner, center, data.Range))
            {
                return false;
            }

            targets.Clear();
            targets.SetGroundPosition(center);
            CollectTargets(owner, center, UnitRegistry.RedTeam, targets);
            CollectTargets(owner, center, UnitRegistry.BlueTeam, targets);
            return targets.Count > 0;
        }

        private Vector3 ResolveCenter(Unit owner, SkillData data, in SkillUseRequest request)
        {
            if (m_CenterOnOwner) return owner.GroundPosition;
            if (request.HasGroundPosition) return request.GroundPosition;
            Unit hint = request.TargetHint.Unit;
            if (hint != null) return hint.GroundPosition;
            Unit nearest = UnitRegistry.FindBestTarget(owner, TargetPriorityType.Nearest, data.Range);
            return nearest != null ? nearest.GroundPosition : owner.GroundPosition;
        }

    }
}
#endif
