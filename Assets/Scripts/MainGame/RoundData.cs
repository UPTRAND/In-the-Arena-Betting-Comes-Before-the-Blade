#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using System.Collections.Generic;
using InTheArena.Unit;
using UnitType = InTheArena.Unit.Unit;

namespace InTheArena.MainGame
{
    /// <summary>
    /// 라운드별 데이터 ScriptableObject
    /// 팀 A/B의 유닛 배치(고정/가변 칸), 기본 베팅 비율, 특별 규칙 포함
    /// 인라인 직렬화 + Custom Editor로 2x3 그리드 시각화 지원
    /// </summary>
    [CreateAssetMenu(fileName = "RoundData_", menuName = "In The Arena/MainGame/Round Data", order = 1)]
    public class RoundData : ScriptableObject
    {
        [Header("라운드 기본 정보")]
        [SerializeField] private int m_RoundNumber;

        [Header("팀 A 유닛 배치 (좌측 2x3 그리드)")]
        [SerializeField] private GridCellData[] m_TeamAGrid = new GridCellData[6]; // 2x3 = 6칸

        [Header("팀 B 유닛 배치 (우측 2x3 그리드)")]
        [SerializeField] private GridCellData[] m_TeamBGrid = new GridCellData[6]; // 2x3 = 6칸

        [Header("베팅 설정")]
        [SerializeField] [Range(0f, 100f)] private float m_DefaultBetRatioA = 50f;

        [Header("특별 규칙 (Test)")]
        [SerializeField] private RoundRule m_SpecialRule = RoundRule.None;

        // Properties
        public int RoundNumber => m_RoundNumber;
        public GridCellData[] TeamAGrid => m_TeamAGrid;
        public GridCellData[] TeamBGrid => m_TeamBGrid;
        public float DefaultBetRatioA => m_DefaultBetRatioA;
        public float DefaultBetRatioB => 100f - m_DefaultBetRatioA;
        public RoundRule SpecialRule => m_SpecialRule;

        /// <summary>
        /// 그리드 데이터를 평탄화된 유닛 리스트로 변환 (런타임용)
        /// </summary>
        public List<UnitData> GetTeamAUnits()
        {
            var units = new List<UnitData>();
            foreach (var cell in m_TeamAGrid)
            {
                if (cell != null && cell.IsValid())
                {
                    // 고정 유닛
                    if (cell.FixedUnit != null && cell.FixedCount > 0)
                    {
                        for (int i = 0; i < cell.FixedCount; i++)
                            units.Add(cell.FixedUnit);
                    }
                    // 가변 유닛은 런타임에 랜덤 선택
                }
            }
            return units;
        }

        public List<UnitData> GetTeamBUnits()
        {
            var units = new List<UnitData>();
            foreach (var cell in m_TeamBGrid)
            {
                if (cell != null && cell.IsValid())
                {
                    if (cell.FixedUnit != null && cell.FixedCount > 0)
                    {
                        for (int i = 0; i < cell.FixedCount; i++)
                            units.Add(cell.FixedUnit);
                    }
                }
            }
            return units;
        }

        /// <summary>
        /// 유효성 검사
        /// </summary>
        public bool IsValid()
        {
            bool isValid = true;

            if (m_RoundNumber <= 0)
            {
                Debug.LogError($"[RoundData] {name}: 라운드 번호가 0 이하입니다.");
                isValid = false;
            }

            // 팀 A 그리드 검사
            if (m_TeamAGrid == null || m_TeamAGrid.Length != 6)
            {
                Debug.LogError($"[RoundData] {name}: 팀 A 그리드가 6칸이 아닙니다.");
                isValid = false;
            }
            else
            {
                for (int i = 0; i < m_TeamAGrid.Length; i++)
                {
                    if (m_TeamAGrid[i] != null && !m_TeamAGrid[i].IsValid())
                    {
                        Debug.LogError($"[RoundData] {name}: 팀 A {i}번 칸 데이터가 유효하지 않습니다.");
                        isValid = false;
                    }
                }
            }

            // 팀 B 그리드 검사
            if (m_TeamBGrid == null || m_TeamBGrid.Length != 6)
            {
                Debug.LogError($"[RoundData] {name}: 팀 B 그리드가 6칸이 아닙니다.");
                isValid = false;
            }
            else
            {
                for (int i = 0; i < m_TeamBGrid.Length; i++)
                {
                    if (m_TeamBGrid[i] != null && !m_TeamBGrid[i].IsValid())
                    {
                        Debug.LogError($"[RoundData] {name}: 팀 B {i}번 칸 데이터가 유효하지 않습니다.");
                        isValid = false;
                    }
                }
            }

            if (m_DefaultBetRatioA < 0f || m_DefaultBetRatioA > 100f)
            {
                Debug.LogError($"[RoundData] {name}: 기본 베팅 비율이 0~100 범위를 벗어났습니다.");
                isValid = false;
            }

            return isValid;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 배열 크기 보정
            if (m_TeamAGrid == null || m_TeamAGrid.Length != 6)
            {
                var newGrid = new GridCellData[6];
                if (m_TeamAGrid != null)
                {
                    for (int i = 0; i < Mathf.Min(m_TeamAGrid.Length, 6); i++)
                        newGrid[i] = m_TeamAGrid[i];
                }
                m_TeamAGrid = newGrid;
            }

            if (m_TeamBGrid == null || m_TeamBGrid.Length != 6)
            {
                var newGrid = new GridCellData[6];
                if (m_TeamBGrid != null)
                {
                    for (int i = 0; i < Mathf.Min(m_TeamBGrid.Length, 6); i++)
                        newGrid[i] = m_TeamBGrid[i];
                }
                m_TeamBGrid = newGrid;
            }

            IsValid();
        }
#endif
    }

    /// <summary>
    /// 그리드 한 칸의 데이터 (고정/가변 설정)
    /// SerializeReference로 인터페이스/추상 클래스 직렬화 지원
    /// </summary>
    [System.Serializable]
    public class GridCellData
    {
        [Tooltip("이 칸이 고정 배치인지 여부 (false면 가변/랜덤)")]
        [SerializeField] private bool m_IsFixed = true;

        [Tooltip("고정 유닛 (고정 배치일 때만 사용)")]
        [SerializeReference] private UnitData m_FixedUnit;

        [Tooltip("고정 유닛 수 (1~9)")]
        [SerializeField] [Range(1, 9)] private int m_FixedCount = 1;

        [Tooltip("가변 시 기본 유닛 풀 (가변 배치일 때 랜덤 선택)")]
        [SerializeReference] private List<UnitData> m_VariableUnitPool = new List<UnitData>();

        [Tooltip("가변 시 추가 유닛 수 범위 (0~2)")]
        [SerializeField] [Range(0, 2)] private int m_ExtraCountRange = 1;

        [Tooltip("유닛 생성 확률 (0~1, 기본 0.3 = 30%)")]
        [SerializeField] [Range(0f, 1f)] private float m_SpawnProbability = 0.3f;

        // Properties
        public bool IsFixed => m_IsFixed;
        public UnitData FixedUnit => m_FixedUnit;
        public int FixedCount => m_FixedCount;
        public List<UnitData> VariableUnitPool => m_VariableUnitPool;
        public int ExtraCountRange => m_ExtraCountRange;
        public float SpawnProbability => m_SpawnProbability;

        /// <summary>
        /// 데이터 유효성 검사
        /// </summary>
        public bool IsValid()
        {
            if (m_IsFixed)
            {
                return m_FixedUnit != null && m_FixedUnit.IsValid() && m_FixedCount > 0;
            }
            else
            {
                return m_VariableUnitPool != null && m_VariableUnitPool.Count > 0 && 
                       m_VariableUnitPool.Exists(u => u != null && u.IsValid());
            }
        }

        /// <summary>
        /// 런타임용 유닛 리스트 생성 (가변인 경우 랜덤 선택)
        /// </summary>
        public List<UnitData> GenerateRuntimeUnits()
        {
            var units = new List<UnitData>();

            // 생성 확률 체크
            if (UnityEngine.Random.value > m_SpawnProbability)
            {
                return units; // 빈 리스트 반환 (생성 안 함)
            }

            if (m_IsFixed)
            {
                if (m_FixedUnit != null && m_FixedCount > 0)
                {
                    for (int i = 0; i < m_FixedCount; i++)
                        units.Add(m_FixedUnit);
                }
            }
            else
            {
                // 가변: 풀에서 랜덤 선택 + 기본 1개 + 추가 0~2개
                if (m_VariableUnitPool != null && m_VariableUnitPool.Count > 0)
                {
                    var validPool = m_VariableUnitPool.FindAll(u => u != null && u.IsValid());
                    if (validPool.Count > 0)
                    {
                        int baseCount = 1;
                        int extraCount = UnityEngine.Random.Range(0, m_ExtraCountRange + 1);
                        int totalCount = baseCount + extraCount;

                        for (int i = 0; i < totalCount; i++)
                        {
                            units.Add(validPool[UnityEngine.Random.Range(0, validPool.Count)]);
                        }
                    }
                }
            }

            return units;
        }
    }

    /// <summary>
    /// 라운드 특별 규칙
    /// </summary>
    public enum RoundRule
    {
        None = 0,           // 일반
        DoubleDamage = 1,   // 데미지 2배
        HalfHeal = 2,       // 회복량 50% 감소
        NoSkills = 3,       // 스킬 사용 불가
        SpeedUp = 4,        // 공격/이동 속도 2배
        SuddenDeath = 5     // 한 방에 사망 (체력 1)
    }
}
#endif