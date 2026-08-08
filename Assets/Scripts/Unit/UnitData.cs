#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;
using InTheArena.MainGame;

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
        [Tooltip("유닛 내부 식별자 (영문 ID)")]
        [SerializeField] private string m_UnitName;

        [Tooltip("유닛 표시 이름 (한글 등)")]
        [SerializeField] private string m_DisplayName;

        [Tooltip("유닛 설명 (도감용)")]
        [SerializeField, TextArea(1, 3)] private string m_Description;

        [Tooltip("유닛 공격 타입 (근거리/원거리)")]
        [SerializeField] private UnitAttackType m_AttackType;

        [Header("기본 공격")]
        [Tooltip("기본 공격의 전달 방식과 적중 효과")]
        [SerializeField] private BasicAttackData m_BasicAttackData;

        [Header("스탯")]
        [Tooltip("유닛 기본 스탯")]
        [SerializeField] private UnitStat m_BaseStat;

        [Header("프리팹 및 컴포넌트")]
        [Tooltip("유닛 프리팹 (씬에 생성될 오브젝트)")]
        [SerializeField] private GameObject m_UnitPrefab;

        [Header("UI Portraits")]
        [SerializeField] private Sprite m_RedPortrait;
        [SerializeField] private Sprite m_BluePortrait;

        [Tooltip("AI가 앞에서부터 사용 가능 여부를 검사하는 스킬 목록")]
        [SerializeField] private List<SkillData> m_SkillDatas = new List<SkillData>();

        [Tooltip("유닛 AI 데이터 (AIData ScriptableObject)")]
        [FormerlySerializedAs("m_AI")]
        [SerializeField] private AIData m_AIData;

        [Header("시작 상태효과")]
        [Tooltip("전투 시작 시 한 번 적용되는 Buff/Debuff 데이터")]
        [SerializeField] private List<StatusEffectData> m_StartingStatusEffects = new List<StatusEffectData>();

        [Header("전투 시각 기준")]
        [Tooltip("카메라 프레이밍과 soft separation에서 사용할 유닛 반경")]
        [SerializeField, Min(0.1f)] private float m_VisualRadius = 0.5f;

        /// <summary> 유닛 내부 식별자 이름 </summary>
        public string UnitName => m_UnitName;

        /// <summary> 유닛 표시 이름 (한글 등) </summary>
        public string DisplayName => string.IsNullOrEmpty(m_DisplayName) ? m_UnitName : m_DisplayName;

        /// <summary> 유닛 설명 (도감용) </summary>
        public string Description => m_Description;

        /// <summary> 공격 타입 </summary>
        public UnitAttackType AttackType => m_AttackType;

        public BasicAttackData BasicAttackData => m_BasicAttackData;

        /// <summary> 기본 스탯 </summary>
        public UnitStat BaseStat => m_BaseStat;

        /// <summary> 유닛 프리팹 </summary>
        public GameObject UnitPrefab => m_UnitPrefab;

        public Sprite GetPortrait(Team team) => team == Team.Red ? m_RedPortrait : m_BluePortrait;

        /// <summary> 스킬 데이터 </summary>
        public SkillData SkillData => m_SkillDatas != null && m_SkillDatas.Count > 0
            ? m_SkillDatas[0]
            : null;

        public IReadOnlyList<SkillData> SkillDatas => m_SkillDatas;

        /// <summary> AI 데이터 </summary>
        public AIData AIData => m_AIData;

        public IReadOnlyList<StatusEffectData> StartingStatusEffects => m_StartingStatusEffects;

        public float VisualRadius => Mathf.Max(0.1f, m_VisualRadius);

        /// <summary> AI 로직 (런타임용) </summary>
        public AIData AI => m_AIData;

        /// <summary>
        /// 런타임용 AI 인스턴스 생성 및 초기화
        /// </summary>
        public UnitDecisionAgent CreateRuntimeAI(Unit owner = null)
        {
            if (m_AIData == null) return null;

            var ai = new UnitDecisionAgent(m_AIData);
            if (owner != null) ai.Initialize(owner);
            return ai;
        }

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

            if (m_BasicAttackData == null)
            {
                Debug.LogError($"[UnitData] {name}: 기본 공격 데이터가 없습니다.", this);
                isValid = false;
            }
            else if (!m_BasicAttackData.IsValid())
            {
                isValid = false;
            }

            if (m_AttackType == UnitAttackType.Ranged &&
                !(m_BasicAttackData?.Delivery is HomingProjectileAttackDelivery))
            {
                Debug.LogError(
                    $"[UnitData] {name}: 원거리 유닛은 HomingProjectileAttackDelivery가 필요합니다.",
                    this);
                isValid = false;
            }

            if (m_SkillDatas != null)
            {
                if (m_SkillDatas.Count > 8)
                    Debug.LogWarning(
                        $"[UnitData] {name}: 권장 스킬 수 8개를 초과했습니다. Android 프로파일링이 필요합니다.",
                        this);
                for (int i = 0; i < m_SkillDatas.Count; i++)
                {
                    if (m_SkillDatas[i] != null && !m_SkillDatas[i].IsValid()) isValid = false;
                }
            }

            if (m_AIData != null)
            {
                if (!(m_AIData is AIData))
                {
                    Debug.LogError($"[UnitData] {name}: AI 데이터가 AIData를 상속받지 않았습니다.");
                    isValid = false;
                }
                else if (!m_AIData.IsValid())
                {
                    Debug.LogError($"[UnitData] {name}: AI 데이터가 유효하지 않습니다.");
                    isValid = false;
                }
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

            return PoolManager.Require().Units.Spawn(this, parent, team, Vector3.zero);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터에서 유효성 검사 수행
        /// </summary>
        private void OnValidate()
        {
            m_VisualRadius = Mathf.Max(0.1f, m_VisualRadius);
            IsValid();
        }
#endif
    }
}
#endif
