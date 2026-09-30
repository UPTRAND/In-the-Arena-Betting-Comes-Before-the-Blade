using System;
using System.Collections.Generic;
using InTheArena.Save;
using UnityEngine;

namespace InTheArena.Quests
{
    public enum QuestKind
    {
        Daily,
        Emergency
    }

    public enum QuestTier
    {
        Tier1 = 1,
        Tier2 = 2,
        Tier3 = 3
    }

    public enum QuestObjectiveKind
    {
        DoubleBet,
        BigBettor,
        PreciseBet,
        ConsecutiveStageClear,
        WinningPayout
    }

    public enum QuestStatus
    {
        Active,
        ReadyToConfirm,
        Submitting,
        Delivered,
        Failed,
        Expired
    }

    public enum QuestCompletionResult
    {
        Success,
        AlreadyProcessed,
        GoalNotReached,
        Expired,
        FailedQuest,
        NotFound,
        NotInLobby,
        Unavailable,
        SaveFailed
    }

    public enum QuestRefreshResult
    {
        NoChange,
        Committed,
        Unavailable,
        InvalidCatalog,
        SaveFailed
    }

    [Serializable]
    public sealed class QuestTierData
    {
        [Min(1)] public int TargetAmount = 1;
        [Min(1)] public int RewardGold = 300;
    }

    /// <summary>
    /// Inspector에서 편집하는 퀘스트 콘텐츠 정의입니다. 런타임 진행 상태는 저장하지 않습니다.
    /// </summary>
    [CreateAssetMenu(menuName = "In The Arena/Quest/Definition")]
    public sealed class QuestDefinition : ScriptableObject
    {
        [SerializeField] private string m_DefinitionId;
        [SerializeField] private QuestKind m_Kind;
        [SerializeField] private string m_Title;
        [TextArea(2, 4)] [SerializeField] private string m_DescriptionTemplate;
        [SerializeField] private QuestObjectiveKind m_ObjectiveKind;
        [SerializeField] private QuestTierData[] m_Tiers = new QuestTierData[3];
        [Min(1)] [SerializeField] private int m_DurationSeconds = 1800;

        public string DefinitionId => m_DefinitionId;
        public QuestKind Kind => m_Kind;
        public string Title => m_Title;
        public QuestObjectiveKind ObjectiveKind => m_ObjectiveKind;
        public int DurationSeconds => m_DurationSeconds;

        /// <summary>
        /// 정의와 등급, 생성 파라미터를 저장 가능한 퀘스트 스냅샷으로 고정합니다.
        /// </summary>
        /// <summary>
        /// 정의와 선택된 등급·대상을 영속 퀘스트 스냅샷으로 생성합니다.
        /// </summary>
        public QuestInstancePayload CreateInstance(QuestTier tier, string parameter, DateTime createdAtUtc)
        {
            int tierIndex = Mathf.Clamp((int)tier - 1, 0, 2);
            QuestTierData tierData = m_Tiers[tierIndex];
            DateTime expiration = m_Kind == QuestKind.Daily
                ? createdAtUtc.Date.AddDays(1)
                : createdAtUtc.AddSeconds(Math.Max(1, m_DurationSeconds));

            string objectiveText = BuildObjectiveText(tierData.TargetAmount, parameter);
            return new QuestInstancePayload
            {
                instanceId = Guid.NewGuid().ToString("N"),
                definitionId = m_DefinitionId,
                kind = (int)m_Kind,
                tier = (int)tier,
                objectiveKind = (int)m_ObjectiveKind,
                title = m_Title,
                description = objectiveText,
                objectiveParameter = parameter,
                targetAmount = tierData.TargetAmount,
                progressAmount = 0,
                rewardGold = tierData.RewardGold,
                status = (int)QuestStatus.Active,
                createdAtUtcTicks = createdAtUtc.Ticks,
                expiresAtUtcTicks = expiration.Ticks,
                consumedEventIds = Array.Empty<string>()
            };
        }

        /// <summary>
        /// 정의 ID, 등급, 목표 수치 등 출시 콘텐츠의 필수 항목을 검증합니다.
        /// </summary>
        /// <summary>
        /// 정의 ID, 문구와 등급별 목표·보상이 사용 가능한지 검사합니다.
        /// </summary>
        public bool IsValid(out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(m_DefinitionId) || string.IsNullOrWhiteSpace(m_Title))
            {
                error = "Definition id and title are required.";
                return false;
            }

            if (m_Tiers == null || m_Tiers.Length != 3)
            {
                error = $"{m_DefinitionId}: Exactly three tiers are required.";
                return false;
            }

            for (int i = 0; i < m_Tiers.Length; i++)
            {
                if (m_Tiers[i] == null || m_Tiers[i].TargetAmount <= 0 || m_Tiers[i].RewardGold <= 0)
                {
                    error = $"{m_DefinitionId}: Tier {i + 1} is invalid.";
                    return false;
                }
            }

            if (m_Kind == QuestKind.Emergency && m_DurationSeconds <= 0)
            {
                error = $"{m_DefinitionId}: Emergency duration must be positive.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 확정 목표와 파라미터를 설명 템플릿에 대입합니다.
        /// </summary>
        private string BuildObjectiveText(int target, string parameter)
        {
            if (!string.IsNullOrWhiteSpace(m_DescriptionTemplate))
            {
                return m_DescriptionTemplate
                    .Replace("{target}", target.ToString())
                    .Replace("{parameter}", parameter ?? string.Empty);
            }

            switch (m_ObjectiveKind)
            {
                case QuestObjectiveKind.DoubleBet:
                    return $"{parameter} 팀에 베팅하여 {target}회 적중";
                case QuestObjectiveKind.BigBettor:
                    return $"확정 베팅액 {target} Col 누적";
                case QuestObjectiveKind.PreciseBet:
                    return $"{parameter} 베팅 {target}회 적중";
                case QuestObjectiveKind.ConsecutiveStageClear:
                    return $"스테이지 {target}회 연속 클리어";
                case QuestObjectiveKind.WinningPayout:
                    return $"승리 정산 지급액 {target} Col 획득";
                default:
                    return $"목표 {target} 달성";
            }
        }

        /// <summary>
        /// 콘텐츠 생성 도구가 정의 에셋을 일관된 값으로 구성합니다.
        /// </summary>
        /// <summary>
        /// 에디터 빌더와 테스트가 정의 필드를 한 번에 설정할 수 있게 합니다.
        /// </summary>
        public void ConfigureForEditor(
            string definitionId,
            QuestKind kind,
            string title,
            string descriptionTemplate,
            QuestObjectiveKind objectiveKind,
            int[] targets,
            int durationSeconds)
        {
            m_DefinitionId = definitionId;
            m_Kind = kind;
            m_Title = title;
            m_DescriptionTemplate = descriptionTemplate;
            m_ObjectiveKind = objectiveKind;
            m_DurationSeconds = durationSeconds;
            m_Tiers = new QuestTierData[3];

            for (int i = 0; i < m_Tiers.Length; i++)
            {
                m_Tiers[i] = new QuestTierData
                {
                    TargetAmount = targets[i],
                    RewardGold = 300 + i * 100
                };
            }
        }
    }

    /// <summary>
    /// 일일·긴급 정의 풀과 확률·시간 설정을 제공하는 콘텐츠 카탈로그입니다.
    /// </summary>
    [CreateAssetMenu(menuName = "In The Arena/Quest/Catalog")]
    public sealed class QuestCatalog : ScriptableObject
    {
        [SerializeField] private List<QuestDefinition> m_DailyDefinitions = new List<QuestDefinition>();
        [SerializeField] private List<QuestDefinition> m_EmergencyDefinitions = new List<QuestDefinition>();

        [Header("Schedule")]
        [Min(1)] [SerializeField] private int m_DailyCount = 3;
        [SerializeField] private Vector3Int m_TierWeights = new Vector3Int(50, 30, 20);
        [Range(0f, 1f)] [SerializeField] private float m_EmergencyChance = 0.5f;
        [Min(1)] [SerializeField] private int m_EmergencyDailyLimit = 3;

        public IReadOnlyList<QuestDefinition> DailyDefinitions => m_DailyDefinitions;
        public IReadOnlyList<QuestDefinition> EmergencyDefinitions => m_EmergencyDefinitions;
        public int DailyCount => m_DailyCount;
        public Vector3Int TierWeights => m_TierWeights;
        public float EmergencyChance => m_EmergencyChance;
        public int EmergencyDailyLimit => m_EmergencyDailyLimit;

        /// <summary>
        /// 풀 수량, 중복 ID, 정의 값과 스케줄 확률을 검사합니다.
        /// </summary>
        /// <summary>
        /// 일일·긴급 풀, 수량, 등급 가중치와 발생 정책을 검사합니다.
        /// </summary>
        public bool IsValid(out string error)
        {
            error = null;
            if (m_DailyCount != 3 || m_DailyDefinitions == null || m_DailyDefinitions.Count < m_DailyCount)
            {
                error = "At least three daily quest definitions are required.";
                return false;
            }

            if (m_EmergencyDefinitions == null || m_EmergencyDefinitions.Count == 0)
            {
                error = "At least one emergency quest definition is required.";
                return false;
            }

            if (m_TierWeights.x <= 0 || m_TierWeights.y <= 0 || m_TierWeights.z <= 0 || m_EmergencyDailyLimit <= 0)
            {
                error = "Schedule weights and limits must be positive.";
                return false;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            if (!ValidateDefinitions(m_DailyDefinitions, QuestKind.Daily, ids, out error))
            {
                return false;
            }

            return ValidateDefinitions(m_EmergencyDefinitions, QuestKind.Emergency, ids, out error);
        }

        /// <summary>
        /// 풀의 null·종류·정의 ID 중복을 검사합니다.
        /// </summary>
        private static bool ValidateDefinitions(
            List<QuestDefinition> definitions,
            QuestKind expectedKind,
            HashSet<string> ids,
            out string error)
        {
            error = null;
            for (int i = 0; i < definitions.Count; i++)
            {
                QuestDefinition definition = definitions[i];
                if (definition == null || definition.Kind != expectedKind || !definition.IsValid(out error))
                {
                    error = error ?? $"Invalid {expectedKind} definition at index {i}.";
                    return false;
                }

                if (!ids.Add(definition.DefinitionId))
                {
                    error = $"Duplicate quest definition id: {definition.DefinitionId}";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 콘텐츠 생성 도구가 실제 퀘스트 풀을 설정합니다.
        /// </summary>
        /// <summary>
        /// 에디터 빌더가 확정 일일·긴급 정의 풀을 설정할 수 있게 합니다.
        /// </summary>
        public void ConfigureForEditor(List<QuestDefinition> daily, List<QuestDefinition> emergency)
        {
            m_DailyDefinitions = daily;
            m_EmergencyDefinitions = emergency;
            m_DailyCount = 3;
            m_TierWeights = new Vector3Int(50, 30, 20);
            m_EmergencyChance = 0.5f;
            m_EmergencyDailyLimit = 3;
        }
    }

    public static class QuestContentFactory
    {
        /// <summary>
        /// Resources 에셋이 없을 때도 확정된 5종 콘텐츠로 기능을 유지하는 런타임 카탈로그를 만듭니다.
        /// </summary>
        /// <summary>
        /// 에셋이 누락된 개발 환경에서만 사용하는 확정 콘텐츠 카탈로그를 만듭니다.
        /// </summary>
        public static QuestCatalog CreateRuntimeDefaultCatalog()
        {
            List<QuestDefinition> daily = new List<QuestDefinition>();
            List<QuestDefinition> emergency = new List<QuestDefinition>();

            daily.Add(CreateDefinition(
                "daily_double_bet",
                QuestKind.Daily,
                "이중 배팅",
                "{parameter} 팀에 베팅하여 {target}회 적중",
                QuestObjectiveKind.DoubleBet,
                new[] { 3, 5, 7 },
                1800));
            daily.Add(CreateDefinition(
                "daily_big_bettor",
                QuestKind.Daily,
                "큰손 배팅",
                "확정 베팅액 {target} Col 누적",
                QuestObjectiveKind.BigBettor,
                new[] { 3000, 4000, 5000 },
                1800));
            daily.Add(CreateDefinition(
                "daily_precise_bet",
                QuestKind.Daily,
                "정밀 배팅",
                "{parameter} 베팅 {target}회 적중",
                QuestObjectiveKind.PreciseBet,
                new[] { 3, 5, 7 },
                1800));

            emergency.Add(CreateDefinition(
                "emergency_raise_stakes",
                QuestKind.Emergency,
                "판돈 올리기",
                "스테이지 {target}회 연속 클리어",
                QuestObjectiveKind.ConsecutiveStageClear,
                new[] { 3, 4, 5 },
                1800));
            emergency.Add(CreateDefinition(
                "emergency_wager",
                QuestKind.Emergency,
                "내기",
                "승리 정산 지급액 {target} Col 획득",
                QuestObjectiveKind.WinningPayout,
                new[] { 3500, 4500, 5500 },
                1800));

            QuestCatalog catalog = ScriptableObject.CreateInstance<QuestCatalog>();
            catalog.ConfigureForEditor(daily, emergency);
            return catalog;
        }

        /// <summary>
        /// 런타임 기본 카탈로그용 정의 ScriptableObject를 구성합니다.
        /// </summary>
        private static QuestDefinition CreateDefinition(
            string definitionId,
            QuestKind kind,
            string title,
            string description,
            QuestObjectiveKind objective,
            int[] targets,
            int durationSeconds)
        {
            QuestDefinition definition = ScriptableObject.CreateInstance<QuestDefinition>();
            definition.ConfigureForEditor(
                definitionId,
                kind,
                title,
                description,
                objective,
                targets,
                durationSeconds);
            return definition;
        }
    }

    public sealed class QuestView
    {
        public string InstanceId { get; }
        public string DefinitionId { get; }
        public QuestKind Kind { get; }
        public QuestTier Tier { get; }
        public string Parameter { get; }
        public string Title { get; }
        public string Description { get; }
        public int Progress { get; }
        public int Target { get; }
        public int RewardGold { get; }
        public QuestStatus Status { get; }
        public long ExpiresAtUtcTicks { get; }
        public string AssignedProfileId { get; }
        public string AssignedNpcId { get; }

        /// <summary>
        /// 저장된 인스턴스를 UI와 외부 호출용 읽기 모델로 복사합니다.
        /// </summary>
        public QuestView(QuestInstancePayload payload)
        {
            InstanceId = payload.instanceId;
            DefinitionId = payload.definitionId;
            Kind = (QuestKind)payload.kind;
            Tier = (QuestTier)payload.tier;
            Parameter = payload.objectiveParameter;
            Title = payload.title;
            Description = payload.description;
            Progress = payload.progressAmount;
            Target = payload.targetAmount;
            RewardGold = payload.rewardGold;
            Status = (QuestStatus)payload.status;
            ExpiresAtUtcTicks = payload.expiresAtUtcTicks;
            AssignedProfileId = payload.assignedProfileId;
            AssignedNpcId = payload.assignedNpcId;
        }
    }

    public interface IQuestRandom
    {
        int Range(int minimumInclusive, int maximumExclusive);
        double Value();
    }

    public sealed class SystemQuestRandom : IQuestRandom
    {
        private readonly System.Random m_Random = new System.Random();

        /// <summary>
        /// 지정 범위의 정수 난수를 반환합니다.
        /// </summary>
        public int Range(int minimumInclusive, int maximumExclusive)
        {
            return m_Random.Next(minimumInclusive, maximumExclusive);
        }

        /// <summary>
        /// 0 이상 1 미만의 실수 난수를 반환합니다.
        /// </summary>
        public double Value()
        {
            return m_Random.NextDouble();
        }
    }
}
