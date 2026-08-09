#if UNITY_6000_0_OR_NEWER
using System;
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

    [Serializable]
    public sealed class StageDifficultyConfig
    {
        [SerializeField] private StageDifficulty m_Difficulty = StageDifficulty.Easy;
        [FormerlySerializedAs("m_InitialCoin")]
        [SerializeField, Min(0)] private int m_InitialCall = 500;
        [SerializeField, Min(0)] private int m_TargetCall = 1500;
        [SerializeField, Min(1)] private int m_RoundCount = 3;

        public StageDifficulty Difficulty => m_Difficulty;
        public int InitialCall => m_InitialCall;
        public int TargetCall => m_TargetCall;
        public int RoundCount => Mathf.Max(1, m_RoundCount);

#if UNITY_EDITOR
        public void ConfigureForEditor(StageDifficulty difficulty, int initialCall, int targetCall, int roundCount)
        {
            m_Difficulty = difficulty;
            m_InitialCall = Mathf.Max(0, initialCall);
            m_TargetCall = Mathf.Max(0, targetCall);
            m_RoundCount = Mathf.Max(1, roundCount);
        }
#endif
    }

    [CreateAssetMenu(fileName = "StageData_", menuName = "In The Arena/MainGame/Stage Data", order = 0)]
    public class StageData : ScriptableObject
    {
        [Header("스테이지 기본 정보")]
        [SerializeField] private string m_StageName;
        [SerializeField] private StageRegion m_Region;
        [SerializeField] private int m_StageNum;
        [SerializeField] private StageDifficulty m_Difficulty = StageDifficulty.Easy;
        [SerializeField] private Sprite m_BackgroundSprite;

        [Header("기본 실행 설정")]
        [FormerlySerializedAs("m_InitialCoin")]
        [SerializeField, Min(0)] private int m_InitialCall = 500;
        [SerializeField] private List<RoundData> m_RoundDatas = new List<RoundData>();
        [SerializeField, Min(0)] private int m_TargetCall = 1500;

        [Header("난이도별 실행 설정")]
        [SerializeField] private List<StageDifficultyConfig> m_DifficultyConfigs = new List<StageDifficultyConfig>();

        [Header("베팅 설정")]
        [SerializeField] private bool m_EnableFactionBet = true;
        [SerializeField, HideInInspector] private List<SpecialBetType> m_SpecialBetTypes = new List<SpecialBetType>();

        public string StageName => m_StageName;
        public StageRegion Region => m_Region;
        public int StageNum => m_StageNum;
        public int StageId => (int)m_Region * 100 + m_StageNum;
        public StageDifficulty Difficulty => ActiveDifficulty;
        public StageDifficulty DefaultDifficulty => m_Difficulty;
        public Sprite BackgroundSprite => m_BackgroundSprite;
        public int InitialCall => GetInitialCall(ActiveDifficulty);
        public int TargetCall => GetTargetCall(ActiveDifficulty);
        public int TotalRounds => GetRoundCount(ActiveDifficulty);
        public List<RoundData> RoundDatas => GetRoundDatas(ActiveDifficulty);
        public bool EnableFactionBet => m_EnableFactionBet;
        public IReadOnlyList<SpecialBetType> SpecialBetTypes => m_SpecialBetTypes;
        public string FullStageName => $"{m_StageName}-{m_StageNum}";

        private StageDifficulty ActiveDifficulty
        {
            get
            {
                if (Application.isPlaying && global::SaveManager.Instance != null)
                {
                    return global::SaveManager.Instance.SelectedStageDifficulty;
                }

                return m_Difficulty;
            }
        }

        public int GetInitialCall(StageDifficulty difficulty)
        {
            StageDifficultyConfig config = FindDifficultyConfig(difficulty);
            return config != null ? config.InitialCall : Mathf.Max(0, m_InitialCall);
        }

        public int GetTargetCall(StageDifficulty difficulty)
        {
            StageDifficultyConfig config = FindDifficultyConfig(difficulty);
            return config != null ? config.TargetCall : Mathf.Max(0, m_TargetCall);
        }

        public int GetRoundCount(StageDifficulty difficulty)
        {
            StageDifficultyConfig config = FindDifficultyConfig(difficulty);
            return config != null ? config.RoundCount : GetPresetRoundCount(difficulty);
        }

        public List<RoundData> GetRoundDatas(StageDifficulty difficulty)
        {
            int roundCount = GetRoundCount(difficulty);
            var rounds = new List<RoundData>(roundCount);
            if (m_RoundDatas == null)
            {
                return rounds;
            }

            int count = Mathf.Min(roundCount, m_RoundDatas.Count);
            for (int i = 0; i < count; i++)
            {
                rounds.Add(m_RoundDatas[i]);
            }

            return rounds;
        }

        public bool IsValid()
        {
            bool isValid = true;

            if (string.IsNullOrEmpty(m_StageName))
            {
                Debug.LogError($"[StageData] {name}: 스테이지 이름이 비어 있습니다.");
                isValid = false;
            }

            if (m_StageNum <= 0)
            {
                Debug.LogError($"[StageData] {name}: 스테이지 번호는 0보다 커야 합니다.");
                isValid = false;
            }

            if (!m_EnableFactionBet)
            {
                Debug.LogError($"[StageData] {name}: 승리 팀 베팅이 비활성화되어 있습니다.");
                isValid = false;
            }

            if (m_SpecialBetTypes != null)
            {
                var uniqueTypes = new HashSet<SpecialBetType>(m_SpecialBetTypes);
                if (uniqueTypes.Count != m_SpecialBetTypes.Count)
                {
                    Debug.LogError($"[StageData] {name}: 특수 베팅 종류가 중복되었습니다.");
                    isValid = false;
                }
            }

            isValid &= ValidateDifficulty(ActiveDifficulty);
            return isValid;
        }

        public bool HasSpecialBet(SpecialBetType type)
        {
            return m_SpecialBetTypes != null && m_SpecialBetTypes.Contains(type);
        }

        public void ApplyDifficultyPreset()
        {
            int targetCall = GetPresetTargetCall(m_Difficulty);
            int roundCount = GetPresetRoundCount(m_Difficulty);
            StageDifficultyConfig config = FindDifficultyConfig(m_Difficulty);
            if (config != null)
            {
#if UNITY_EDITOR
                config.ConfigureForEditor(m_Difficulty, GetInitialCall(m_Difficulty), targetCall, roundCount);
#endif
            }
            else
            {
                m_TargetCall = targetCall;
            }
        }

        public int PresetRoundCount => GetPresetRoundCount(m_Difficulty);

        public static int GetPresetRoundCount(StageDifficulty difficulty) => difficulty switch
        {
            StageDifficulty.Easy => 3,
            StageDifficulty.Normal => 5,
            StageDifficulty.Hard => 7,
            _ => 5
        };

        public static int GetPresetTargetCall(StageDifficulty difficulty) => difficulty switch
        {
            StageDifficulty.Easy => 1500,
            StageDifficulty.Normal => 3000,
            StageDifficulty.Hard => 4000,
            _ => 1500
        };

        private StageDifficultyConfig FindDifficultyConfig(StageDifficulty difficulty)
        {
            if (m_DifficultyConfigs == null)
            {
                return null;
            }

            return m_DifficultyConfigs.Find(config => config != null && config.Difficulty == difficulty);
        }

        private bool ValidateDifficulty(StageDifficulty difficulty)
        {
            bool isValid = true;
            int initialCall = GetInitialCall(difficulty);
            int targetCall = GetTargetCall(difficulty);
            int roundCount = GetRoundCount(difficulty);
            List<RoundData> roundDatas = GetRoundDatas(difficulty);

            if (initialCall < 0)
            {
                Debug.LogError($"[StageData] {name}: {difficulty} 시작 Call은 음수일 수 없습니다.");
                isValid = false;
            }

            if (targetCall <= initialCall)
            {
                Debug.LogError($"[StageData] {name}: {difficulty} 목표 Call({targetCall})은 시작 Call({initialCall})보다 커야 합니다.");
                isValid = false;
            }

            if (roundDatas == null || roundDatas.Count < roundCount)
            {
                Debug.LogError($"[StageData] {name}: {difficulty} 난이도에 필요한 라운드 수({roundCount})보다 라운드 데이터가 부족합니다.");
                return false;
            }

            for (int i = 0; i < roundDatas.Count; i++)
            {
                if (roundDatas[i] == null)
                {
                    Debug.LogError($"[StageData] {name}: {difficulty} {i}번째 라운드 데이터가 null입니다.");
                    isValid = false;
                }
                else if (!roundDatas[i].IsValid())
                {
                    Debug.LogError($"[StageData] {name}: {difficulty} {i}번째 라운드 데이터가 유효하지 않습니다.");
                    isValid = false;
                }
            }

            return isValid;
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
