#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace InTheArena.Unit
{
    /// <summary>
    /// 스킬 데이터 ScriptableObject
    /// 스킬 설정(데미지, 쿨타임, 사거리 등)과 로직(Skill_Base 구현체)을 함께 관리
    /// </summary>
    [CreateAssetMenu(fileName = "SkillData_", menuName = "In The Arena/Unit/Skill/Skill Data", order = 0)]
    public class SkillData : ScriptableObject
    {
        [Header("스킬 기본 정보")]
                [Tooltip("스킬 이름")]
                [SerializeField] protected string m_SkillName;

                [Tooltip("스킬 설명")]
                [SerializeField] [TextArea(2, 4)] protected string m_Description;

                [Tooltip("스킬 타입 (액티브/패시브)")]
                [SerializeField] protected SkillType m_SkillType;

                [Tooltip("스킬 타겟 타입")]
                [SerializeField] protected SkillTargetType m_TargetType;

                [Header("스킬 수치")]
                [Tooltip("스킬 고유 데미지 (유닛 공격력과 별개)")]
                [SerializeField] protected float m_SkillDamage;

                [Tooltip("스킬 사거리")]
                [SerializeField] protected float m_SkillRange;

                [Header("액티브 스킬 전용")]
                [Tooltip("쿨타임 (초) - 액티브 스킬만 사용")]
                [SerializeField] protected float m_Cooldown;

                [Tooltip("시전 시간 (초) - 액티브 스킬만 사용")]
                [SerializeField] protected float m_CastTime;

        [Header("스킬 로직")]
        [Tooltip("실제 스킬 동작을 구현한 Skill_Base 상속 클래스")]
        [SerializeReference, SubclassSelector]
        protected Skill_Base m_SkillLogic;

        /// <summary> 스킬 이름 </summary>
        public string SkillName => m_SkillName;

        /// <summary> 스킬 설명 </summary>
        public string Description => m_Description;

        /// <summary> 스킬 타입 </summary>
        public SkillType SkillType => m_SkillType;

        /// <summary> 타겟 타입 </summary>
        public SkillTargetType TargetType => m_TargetType;

        /// <summary> 스킬 데미지 </summary>
        public float SkillDamage => m_SkillDamage;

        /// <summary> 스킬 사거리 </summary>
        public float SkillRange => m_SkillRange;

        /// <summary> 쿨타임 (액티브만) </summary>
        public float Cooldown => m_Cooldown;

        /// <summary> 시전 시간 (액티브만) </summary>
        public float CastTime => m_CastTime;

        /// <summary> 스킬 로직 </summary>
        public Skill_Base SkillLogic => m_SkillLogic;

        /// <summary>
        /// 데이터 유효성 검사 (에디터에서만 사용)
        /// </summary>
        public bool IsValid()
        {
            bool isValid = true;

            if (string.IsNullOrEmpty(m_SkillName))
            {
                Debug.LogError($"[SkillData] {name}: 스킬 이름이 비어있습니다.");
                isValid = false;
            }

            if (m_SkillLogic == null)
            {
                Debug.LogError($"[SkillData] {name}: 스킬 로직이 할당되지 않았습니다.");
                isValid = false;
            }

            m_SkillDamage = Mathf.Max(0f, m_SkillDamage);
            m_SkillRange = Mathf.Max(0f, m_SkillRange);
            m_Cooldown = Mathf.Max(0f, m_Cooldown);
            m_CastTime = Mathf.Max(0f, m_CastTime);

            return isValid;
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            IsValid();
        }
#endif
    }
}
#endif