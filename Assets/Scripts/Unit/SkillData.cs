#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using UnityEngine;

namespace InTheArena.Unit
{
    [CreateAssetMenu(fileName = "SkillData_", menuName = "In The Arena/Unit/Skill/Skill Data", order = 0)]
    public sealed class SkillData : ScriptableObject
    {
        [Header("기본 정보")]
        [SerializeField] private string m_SkillName;
        [SerializeField, TextArea(2, 4)] private string m_Description;
        [SerializeField] private SkillType m_SkillType;

        [Header("UI")]
        [SerializeField] private Sprite m_Icon;

        [Header("시전")]
        [SerializeField, Min(0f)] private float m_Range = 3f;
        [SerializeField, Min(0f)] private float m_Cooldown = 3f;
        [SerializeField, Min(0f)] private float m_CastTime;
        [SerializeField, Min(0.05f)] private float m_FailureRetryDelay = 0.25f;

        [Header("구성")]
        [SerializeField] private SkillExecutionMode m_ExecutionMode = SkillExecutionMode.EffectsOnly;
        [SerializeReference, SubclassSelector] private SkillTargetingDefinition m_Targeting;
        [SerializeReference, SubclassSelector] private List<SkillEffectDefinition> m_Effects =
            new List<SkillEffectDefinition>();
        [SerializeReference, SubclassSelector] private SkillBehaviorDefinition m_Behavior;

        public string SkillName => m_SkillName;
        public string Description => m_Description;
        public Sprite Icon => m_Icon;
        public SkillType SkillType => m_SkillType;
        public float Range => Mathf.Max(0f, m_Range);
        public float Cooldown => Mathf.Max(0f, m_Cooldown);
        public float CastTime => Mathf.Max(0f, m_CastTime);
        public float FailureRetryDelay => Mathf.Max(0.05f, m_FailureRetryDelay);
        public SkillExecutionMode ExecutionMode => m_ExecutionMode;
        public SkillTargetingDefinition Targeting => m_Targeting;
        public IReadOnlyList<SkillEffectDefinition> Effects => m_Effects;
        public SkillBehaviorDefinition Behavior => m_Behavior;

        /// <summary>공유 설정에서 유닛 전용 스킬 상태를 생성합니다.</summary>
        public SkillRuntime CreateRuntime(Unit owner)
        {
            return new SkillRuntime(this, owner);
        }

        /// <summary>현재 실행 모드가 사용하는 효과와 행동의 투사체를 수집합니다.</summary>
        public void CollectProjectilePrefabs(List<GameObject> output)
        {
            if (output == null)
            {
                return;
            }

            if (m_ExecutionMode != SkillExecutionMode.EffectsOnly)
            {
                m_Behavior?.CollectProjectilePrefabs(output);
            }

            if (m_ExecutionMode != SkillExecutionMode.BehaviorOnly && m_Effects != null)
            {
                for (int i = 0; i < m_Effects.Count; i++)
                {
                    m_Effects[i]?.CollectProjectilePrefabs(output);
                }
            }
        }

        /// <summary>실행 모드와 직접 타기팅 여부에 맞는 필수 설정을 검사합니다.</summary>
        public bool IsValid()
        {
            bool valid = true;
            if (m_SkillType == SkillType.Passive &&
                (m_ExecutionMode == SkillExecutionMode.EffectsOnly || m_Behavior == null))
            {
                Debug.LogError($"[SkillData] {name}: 패시브는 이벤트를 처리할 Behavior가 필요합니다.", this);
                valid = false;
            }
            if (string.IsNullOrWhiteSpace(m_SkillName))
            {
                Debug.LogError($"[SkillData] {name}: 스킬 이름이 비어 있습니다.", this);
                valid = false;
            }
            bool customTargeting = m_ExecutionMode != SkillExecutionMode.EffectsOnly &&
                m_Behavior != null && m_Behavior.UsesCustomTargeting;

            if (m_Targeting == null && !customTargeting)
            {
                Debug.LogError($"[SkillData] {name}: Targeting이 필요합니다.", this);
                valid = false;
            }
            if (m_ExecutionMode != SkillExecutionMode.BehaviorOnly &&
                (m_Effects == null || m_Effects.Count == 0))
            {
                Debug.LogError($"[SkillData] {name}: 실행할 Effect가 없습니다.", this);
                valid = false;
            }
            if (m_ExecutionMode != SkillExecutionMode.EffectsOnly && m_Behavior == null)
            {
                Debug.LogError($"[SkillData] {name}: 실행 모드에 필요한 Behavior가 없습니다.", this);
                valid = false;
            }
            return valid;
        }

#if UNITY_EDITOR
        /// <summary>Inspector 입력의 시간과 거리를 유효 범위로 보정합니다.</summary>
        private void OnValidate()
        {
            m_Range = Mathf.Max(0f, m_Range);
            m_Cooldown = Mathf.Max(0f, m_Cooldown);
            m_CastTime = Mathf.Max(0f, m_CastTime);
            m_FailureRetryDelay = Mathf.Max(0.05f, m_FailureRetryDelay);
        }
#endif
    }
}
#endif
