#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace InTheArena.Unit
{
    /// <summary>
    /// 유닛 데이터 ScriptableObject
    /// 각 유닛의 기본 데이터(이름, 타입, 스탯, 프리팹, 스킬, AI)를 에디터에서 설정 가능하게 함
    /// </summary>
    [CreateAssetMenu(fileName = "UnitData_", menuName = "In The Arena/Unit/Unit Data", order = 0)]
    public class UnitData : ScriptableObject
    {
        [Header("기본 정보")]
        [Tooltip("유닛 이름")]
        [SerializeField] private string m_UnitName;

        [Tooltip("유닛 공격 타입 (근거리/원거리)")]
        [SerializeField] private UnitAttackType m_AttackType;

        [Header("스탯")]
        [Tooltip("유닛 기본 스탯")]
        [SerializeField] private UnitStat m_BaseStat;

        [Header("프리팹 및 컴포넌트")]
        [Tooltip("유닛 프리팹 (씬에 생성될 오브젝트)")]
        [SerializeField] private GameObject m_UnitPrefab;

        [Tooltip("유닛 스킬 데이터 (SkillData ScriptableObject)")]
        [SerializeField] private SkillData m_SkillData;

        [Tooltip("유닛 AI (UnitAI_Base 상속 클래스만 할당 가능)")]
        [SerializeField] private UnitAI_Base m_AI;

        /// <summary> 유닛 이름 </summary>
        public string UnitName => m_UnitName;

        /// <summary> 공격 타입 </summary>
        public UnitAttackType AttackType => m_AttackType;

        /// <summary> 기본 스탯 </summary>
        public UnitStat BaseStat => m_BaseStat;

        /// <summary> 유닛 프리팹 </summary>
        public GameObject UnitPrefab => m_UnitPrefab;

        /// <summary> 스킬 데이터 </summary>
        public SkillData SkillData => m_SkillData;

        /// <summary> AI </summary>
        public UnitAI_Base AI => m_AI;

        /// <summary>
        /// 데이터 유효성 검사 (에디터에서만 사용)
        /// </summary>
        public bool IsValid()
        {
            bool isValid = true;

            if (string.IsNullOrEmpty(m_UnitName))
            {
                Debug.LogError($"[UnitData] {name}: 유닛 이름이 비어있습니다.");
                isValid = false;
            }

            if (m_UnitPrefab == null)
            {
                Debug.LogError($"[UnitData] {name}: 유닛 프리팹이 할당되지 않았습니다.");
                isValid = false;
            }
            else if (m_UnitPrefab.GetComponent<Unit>() == null)
            {
                Debug.LogError($"[UnitData] {name}: 프리팹에 Unit 컴포넌트가 없습니다.");
                isValid = false;
            }

            if (!m_BaseStat.IsValid())
            {
                Debug.LogError($"[UnitData] {name}: 스탯이 유효하지 않습니다.");
                isValid = false;
            }

            if (m_SkillData != null)
            {
                if (!(m_SkillData is SkillData))
                {
                    Debug.LogError($"[UnitData] {name}: 스킬 데이터가 SkillData를 상속받지 않았습니다.");
                    isValid = false;
                }
                else if (!m_SkillData.IsValid())
                {
                    Debug.LogError($"[UnitData] {name}: 스킬 데이터가 유효하지 않습니다.");
                    isValid = false;
                }
            }

            if (m_AI != null && !(m_AI is UnitAI_Base))
            {
                Debug.LogError($"[UnitData] {name}: AI가 UnitAI_Base를 상속받지 않았습니다.");
                isValid = false;
            }

            return isValid;
        }

        /// <summary>
        /// 유닛 인스턴스 생성 및 초기화
        /// </summary>
        /// <param name="parent">부모 Transform (선택사항)</param>
        /// <param name="team">팀 구분 (0: 아군, 1: 적군 등)</param>
        /// <returns>생성된 Unit 컴포넌트</returns>
        public Unit CreateUnit(Transform parent = null, int team = 0)
        {
            if (m_UnitPrefab == null)
            {
                Debug.LogError($"[UnitData] {name}: 프리팹이 없어 유닛을 생성할 수 없습니다.");
                return null;
            }

            GameObject instance = Object.Instantiate(m_UnitPrefab, parent);
            Unit unit = instance.GetComponent<Unit>();

            if (unit == null)
            {
                Debug.LogError($"[UnitData] {name}: 프리팹에 Unit 컴포넌트가 없습니다.");
                Object.Destroy(instance);
                return null;
            }

            unit.Initialize(this, team);
            return unit;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터에서 유효성 검사 수행
        /// </summary>
        private void OnValidate()
        {
            IsValid();
        }
#endif
    }
}
#endif