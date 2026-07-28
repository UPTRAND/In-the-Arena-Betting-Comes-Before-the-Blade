#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using System.Collections.Generic;
using InTheArena.Unit;
using UnitType = InTheArena.Unit.Unit;

namespace InTheArena.MainGame
{
    /// <summary>
    /// 스테이지 지역 열거형
    /// </summary>
    public enum StageRegion
    {
        CentralCastle = 0,    // 센트럴 캐슬
        Arena = 1,            // 투기장
        Swamp = 2,            // 늪지대
        Volcano = 3,          // 화산
        FrozenPeak = 4,       // 얼음 봉우리
        DarkTower = 5,        // 어둠의 탑
        SkyIsland = 6,        // 하늘 섬
        Abyss = 7             // 심연
    }

    /// <summary>
    /// 스테이지 데이터 ScriptableObject
    /// 스테이지별 설정(지역, 번호, 시작 코인, 라운드 리스트)을 관리
    /// </summary>
    [CreateAssetMenu(fileName = "StageData_", menuName = "In The Arena/MainGame/Stage Data", order = 0)]
    public class StageData : ScriptableObject
    {
        [Header("스테이지 기본 정보")]
        [SerializeField] private string m_StageName;
        [SerializeField] private StageRegion m_Region;
        [SerializeField] private int m_StageNum;

        [Header("초기 설정")]
        [SerializeField] private int m_InitialCoin = 100;

        [Header("라운드 데이터")]
        [SerializeField] private List<RoundData> m_RoundDatas = new List<RoundData>();

        [Header("목표 설정")]
        [SerializeField] private int m_TargetCall = 200;

        // Properties
        public string StageName => m_StageName;
        public StageRegion Region => m_Region;
        public int StageNum => m_StageNum;
        public int StageId => (int)m_Region * 100 + m_StageNum; // 고유 ID: 지역*100 + 번호
        public int InitialCoin => m_InitialCoin;
        public int TargetCall => m_TargetCall;
        public int TotalRounds => m_RoundDatas.Count;
        public List<RoundData> RoundDatas => m_RoundDatas;

        /// <summary>
        /// 표시용 전체 스테이지 이름 (예: "센트럴 캐슬-1")
        /// </summary>
        public string FullStageName => $"{GetRegionName(m_Region)}-{m_StageNum}";

        /// <summary>
        /// 데이터 유효성 검사
        /// </summary>
        public bool IsValid()
        {
            bool isValid = true;

            if (string.IsNullOrEmpty(m_StageName))
            {
                Debug.LogError($"[StageData] {name}: 스테이지 이름이 비어있습니다.");
                isValid = false;
            }

            if (m_StageNum <= 0)
            {
                Debug.LogError($"[StageData] {name}: 스테이지 번호가 0 이하입니다.");
                isValid = false;
            }

            if (m_InitialCoin <= 0)
            {
                Debug.LogError($"[StageData] {name}: 초기 코인이 0 이하입니다.");
                isValid = false;
            }

            if (m_TargetCall <= m_InitialCoin)
            {
                Debug.LogError($"[StageData] {name}: 목표 콜({m_TargetCall})이 초기 콜({m_InitialCoin}) 이하입니다.");
                isValid = false;
            }

            if (m_RoundDatas == null || m_RoundDatas.Count == 0)
            {
                Debug.LogError($"[StageData] {name}: 라운드 데이터가 없습니다.");
                isValid = false;
            }
            else
            {
                for (int i = 0; i < m_RoundDatas.Count; i++)
                {
                    if (m_RoundDatas[i] == null)
                    {
                        Debug.LogError($"[StageData] {name}: {i}번째 라운드 데이터가 null입니다.");
                        isValid = false;
                    }
                    else if (!m_RoundDatas[i].IsValid())
                    {
                        Debug.LogError($"[StageData] {name}: {i}번째 라운드 데이터가 유효하지 않습니다.");
                        isValid = false;
                    }
                }
            }

            return isValid;
        }

        /// <summary>
        /// 지역 이름 반환 (한글)
        /// </summary>
        private string GetRegionName(StageRegion region)
        {
            return region switch
            {
                StageRegion.CentralCastle => "센트럴 캐슬",
                StageRegion.Arena => "투기장",
                StageRegion.Swamp => "늪지대",
                StageRegion.Volcano => "화산",
                StageRegion.FrozenPeak => "얼음 봉우리",
                StageRegion.DarkTower => "어둠의 탑",
                StageRegion.SkyIsland => "하늘 섬",
                StageRegion.Abyss => "심연",
                _ => "미정"
            };
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            IsValid();
        }
#endif
    }
}
#endif