#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace InTheArena.MainGame
{
    public enum StageRegion
    {
        CentralCastle = 0,
        Arena = 1,
        Swamp = 2,
        Volcano = 3,
        FrozenPeak = 4,
        DarkTower = 5,
        SkyIsland = 6,
        Abyss = 7
    }

    [CreateAssetMenu(fileName = "StageData_", menuName = "In The Arena/MainGame/Stage Data", order = 0)]
    public class StageData : ScriptableObject
    {
        [Header("\uC2A4\uD14C\uC774\uC9C0 \uAE30\uBCF8 \uC815\uBCF4")]
        [SerializeField] private string m_StageName;
        [SerializeField] private StageRegion m_Region;
        [SerializeField] private int m_StageNum;
        [SerializeField] private Sprite m_BackgroundSprite;

        [Header("\uC2A4\uD14C\uC774\uC9C0 \uB09C\uC774\uB3C4 \uC124\uC815")]
        [FormerlySerializedAs("m_InitialCoin")]
        [SerializeField, Min(0)] private int m_InitialCall = 500;
        [SerializeField] private List<RoundData> m_RoundDatas = new List<RoundData>();
        [SerializeField, Min(0)] private int m_TargetCall = 1500;

        [Header("\uBCA0\uD305 \uC124\uC815")]
        [SerializeField] private bool m_EnableFactionBet = true;
        [SerializeField, HideInInspector] private List<SpecialBetType> m_SpecialBetTypes = new List<SpecialBetType>();

        public string StageName => m_StageName;
        public StageRegion Region => m_Region;
        public int StageNum => m_StageNum;
        public int StageId => (int)m_Region * 100 + m_StageNum;
        public Sprite BackgroundSprite => m_BackgroundSprite;
        public int InitialCall => Mathf.Max(0, m_InitialCall);
        public int TargetCall => Mathf.Max(0, m_TargetCall);
        public int TotalRounds => m_RoundDatas?.Count ?? 0;
        public List<RoundData> RoundDatas => m_RoundDatas ?? new List<RoundData>();
        public bool EnableFactionBet => m_EnableFactionBet;
        public IReadOnlyList<SpecialBetType> SpecialBetTypes => m_SpecialBetTypes;
        public string FullStageName => $"{m_StageName}-{m_StageNum}";

        public bool IsValid()
        {
            bool isValid = true;

            if (string.IsNullOrEmpty(m_StageName))
            {
                Debug.LogError($"[StageData] {name}: \uC2A4\uD14C\uC774\uC9C0 \uC774\uB984\uC774 \uBE44\uC5B4 \uC788\uC2B5\uB2C8\uB2E4.");
                isValid = false;
            }

            if (m_StageNum <= 0)
            {
                Debug.LogError($"[StageData] {name}: \uC2A4\uD14C\uC774\uC9C0 \uBC88\uD638\uB294 0\uBCF4\uB2E4 \uCEE4\uC57C \uD569\uB2C8\uB2E4.");
                isValid = false;
            }

            if (!m_EnableFactionBet)
            {
                Debug.LogError($"[StageData] {name}: \uC9C4\uC601 \uBCA0\uD305\uC774 \uBE44\uD65C\uC131\uD654\uB418\uC5B4 \uC788\uC2B5\uB2C8\uB2E4.");
                isValid = false;
            }

            if (m_TargetCall <= m_InitialCall)
            {
                Debug.LogError($"[StageData] {name}: \uBAA9\uD45C Call({m_TargetCall})\uC740 \uC2DC\uC791 Call({m_InitialCall})\uBCF4\uB2E4 \uCEE4\uC57C \uD569\uB2C8\uB2E4.");
                isValid = false;
            }

            if (m_RoundDatas == null || m_RoundDatas.Count == 0)
            {
                Debug.LogError($"[StageData] {name}: \uB77C\uC6B4\uB4DC \uB370\uC774\uD130 \uBAA9\uB85D\uC774 \uBE44\uC5B4 \uC788\uC2B5\uB2C8\uB2E4.");
                return false;
            }

            for (int i = 0; i < m_RoundDatas.Count; i++)
            {
                if (m_RoundDatas[i] == null)
                {
                    Debug.LogError($"[StageData] {name}: {i}\uBC88\uC9F8 \uB77C\uC6B4\uB4DC \uB370\uC774\uD130\uAC00 null\uC785\uB2C8\uB2E4.");
                    isValid = false;
                }
                else if (!m_RoundDatas[i].IsValid())
                {
                    Debug.LogError($"[StageData] {name}: {i}\uBC88\uC9F8 \uB77C\uC6B4\uB4DC \uB370\uC774\uD130\uAC00 \uC720\uD6A8\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.");
                    isValid = false;
                }
            }

            if (m_SpecialBetTypes != null)
            {
                var uniqueTypes = new HashSet<SpecialBetType>(m_SpecialBetTypes);
                if (uniqueTypes.Count != m_SpecialBetTypes.Count)
                {
                    Debug.LogError($"[StageData] {name}: \uD2B9\uC218 \uBCA0\uD305 \uC885\uB958\uAC00 \uC911\uBCF5\uB418\uC5C8\uC2B5\uB2C8\uB2E4.");
                    isValid = false;
                }
            }

            return isValid;
        }

        public bool HasSpecialBet(SpecialBetType type)
        {
            return m_SpecialBetTypes != null && m_SpecialBetTypes.Contains(type);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            m_InitialCall = Mathf.Max(0, m_InitialCall);
            m_TargetCall = Mathf.Max(0, m_TargetCall);
        }
#endif
    }
}
#endif
