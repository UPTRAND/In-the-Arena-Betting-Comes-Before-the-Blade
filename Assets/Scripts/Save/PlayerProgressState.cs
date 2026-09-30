using System;
using System.Collections.Generic;
using InTheArena.MainGame;

namespace InTheArena.Save
{
    /// <summary>
    /// 저장 트랜잭션에서 복제하여 사용하는 플레이어 영구 상태입니다.
    /// </summary>
    public sealed class PlayerProgressState
    {
        public int ClearedStageNumber { get; private set; }
        public int Gold { get; private set; }
        public int Hearts { get; private set; }
        public int Stars { get; private set; }
        public int SelectedStageDifficulty { get; private set; }
        public long LastHeartRecoveryUtcTicks { get; private set; }
        public long FreePassExpirationUtcTicks { get; private set; }

        public int CreatedWithSchemaVersion { get; private set; }
        public string DailyQuestDateUtc { get; private set; }
        public string HighestProcessedUtcDate { get; private set; }
        public string EmergencyGenerationDateUtc { get; private set; }
        public int EmergencyGenerationCount { get; private set; }
        public QuestInstancePayload EmergencyQuestInstance { get; private set; }
        public StageRunPayload ActiveStageRun { get; private set; }
        public StageReturnReceiptPayload PendingStageReturn { get; private set; }
        public bool EmergencyIntroPending { get; private set; }

        private readonly Dictionary<ItemType, int> m_ItemCounts = new Dictionary<ItemType, int>();
        private readonly List<MailEntryPayload> m_MailEntries = new List<MailEntryPayload>();
        private readonly HashSet<string> m_CompletedOneShotRuleIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<QuestInstancePayload> m_DailyQuestInstances = new List<QuestInstancePayload>();
        private readonly HashSet<string> m_ProcessedStageReturnIds = new HashSet<string>(StringComparer.Ordinal);

        public IReadOnlyList<MailEntryPayload> MailEntries => m_MailEntries;
        public IReadOnlyCollection<string> CompletedOneShotRuleIds => m_CompletedOneShotRuleIds;
        public IReadOnlyList<QuestInstancePayload> DailyQuestInstances => m_DailyQuestInstances;
        public IReadOnlyCollection<string> ProcessedStageReturnIds => m_ProcessedStageReturnIds;

        /// <summary>
        /// 빈 영구 진행 상태를 만듭니다.
        /// </summary>
        public PlayerProgressState()
        {
        }

        /// <summary>
        /// 다른 상태의 모든 값과 중첩 컬렉션을 깊게 복사합니다.
        /// </summary>
        private PlayerProgressState(PlayerProgressState other)
        {
            ClearedStageNumber = other.ClearedStageNumber;
            Gold = other.Gold;
            Hearts = other.Hearts;
            Stars = other.Stars;
            SelectedStageDifficulty = other.SelectedStageDifficulty;
            LastHeartRecoveryUtcTicks = other.LastHeartRecoveryUtcTicks;
            FreePassExpirationUtcTicks = other.FreePassExpirationUtcTicks;

            CreatedWithSchemaVersion = other.CreatedWithSchemaVersion;
            DailyQuestDateUtc = other.DailyQuestDateUtc;
            HighestProcessedUtcDate = other.HighestProcessedUtcDate;
            EmergencyGenerationDateUtc = other.EmergencyGenerationDateUtc;
            EmergencyGenerationCount = other.EmergencyGenerationCount;
            EmergencyQuestInstance = other.EmergencyQuestInstance?.DeepClone();
            ActiveStageRun = other.ActiveStageRun?.DeepClone();
            PendingStageReturn = other.PendingStageReturn?.DeepClone();
            EmergencyIntroPending = other.EmergencyIntroPending;

            foreach (KeyValuePair<ItemType, int> pair in other.m_ItemCounts)
            {
                m_ItemCounts[pair.Key] = pair.Value;
            }

            foreach (MailEntryPayload mail in other.m_MailEntries)
            {
                m_MailEntries.Add(mail?.DeepClone());
            }

            foreach (string ruleId in other.m_CompletedOneShotRuleIds)
            {
                m_CompletedOneShotRuleIds.Add(ruleId);
            }

            foreach (QuestInstancePayload quest in other.m_DailyQuestInstances)
            {
                m_DailyQuestInstances.Add(quest?.DeepClone());
            }

            foreach (string returnId in other.m_ProcessedStageReturnIds)
            {
                m_ProcessedStageReturnIds.Add(returnId);
            }
        }

        /// <summary>
        /// 모든 중첩 컬렉션을 포함한 저장 후보 복사본을 만듭니다.
        /// </summary>
        public PlayerProgressState DeepClone()
        {
            return new PlayerProgressState(this);
        }

        /// <summary>
        /// 직렬화 데이터에서 런타임 저장 상태를 복원합니다.
        /// </summary>
        public void CopyFromPayload(PlayerSavePayload payload)
        {
            if (payload == null)
            {
                return;
            }

            ClearedStageNumber = payload.clearedStageNumber;
            Gold = payload.gold;
            Hearts = payload.hearts;
            Stars = payload.stars;
            SelectedStageDifficulty = payload.selectedStageDifficulty;
            LastHeartRecoveryUtcTicks = payload.lastHeartRecoveryUtcTicks;
            FreePassExpirationUtcTicks = payload.freePassExpirationUtcTicks;

            CreatedWithSchemaVersion = payload.createdWithSchemaVersion;
            DailyQuestDateUtc = payload.dailyQuestDateUtc;
            HighestProcessedUtcDate = payload.highestProcessedUtcDate;
            EmergencyGenerationDateUtc = payload.emergencyGenerationDateUtc;
            EmergencyGenerationCount = payload.emergencyGenerationCount;
            EmergencyQuestInstance = payload.emergencyQuestInstance?.DeepClone();
            ActiveStageRun = payload.activeStageRun?.DeepClone();
            PendingStageReturn = payload.pendingStageReturn?.DeepClone();
            EmergencyIntroPending = payload.emergencyIntroPending;

            CopyItemCounts(payload.itemCounts);
            CopyMailEntries(payload.mailEntries);
            CopyStringSet(payload.completedOneShotRuleIds, m_CompletedOneShotRuleIds);
            CopyDailyQuests(payload.dailyQuestInstances);
            CopyStringSet(payload.processedStageReturnIds, m_ProcessedStageReturnIds);
        }

        /// <summary>
        /// 현재 상태를 스키마 6 직렬화 데이터로 변환합니다.
        /// </summary>
        public PlayerSavePayload ToPayload()
        {
            return new PlayerSavePayload
            {
                clearedStageNumber = ClearedStageNumber,
                gold = Gold,
                hearts = Hearts,
                stars = Stars,
                selectedStageDifficulty = SelectedStageDifficulty,
                lastHeartRecoveryUtcTicks = LastHeartRecoveryUtcTicks,
                freePassExpirationUtcTicks = FreePassExpirationUtcTicks,
                itemCounts = ToItemCountPayload(),
                createdWithSchemaVersion = CreatedWithSchemaVersion,
                mailEntries = CloneMailEntries(),
                completedOneShotRuleIds = ToStringArray(m_CompletedOneShotRuleIds),
                dailyQuestDateUtc = DailyQuestDateUtc,
                highestProcessedUtcDate = HighestProcessedUtcDate,
                dailyQuestInstances = CloneDailyQuests(),
                emergencyGenerationDateUtc = EmergencyGenerationDateUtc,
                emergencyGenerationCount = EmergencyGenerationCount,
                emergencyQuestInstance = EmergencyQuestInstance?.DeepClone(),
                processedStageReturnIds = ToStringArray(m_ProcessedStageReturnIds),
                activeStageRun = ActiveStageRun?.DeepClone(),
                pendingStageReturn = PendingStageReturn?.DeepClone(),
                emergencyIntroPending = EmergencyIntroPending
            };
        }

        /// <summary>클리어한 최고 스테이지 번호를 후보 상태에 설정합니다.</summary>
        public void SetClearedStageNumber(int value)
        {
            ClearedStageNumber = value;
        }

        /// <summary>영구 골드를 후보 상태에 설정합니다.</summary>
        public void SetGold(int value)
        {
            Gold = value;
        }

        /// <summary>입장권 보유량을 후보 상태에 설정합니다.</summary>
        public void SetHearts(int value)
        {
            Hearts = value;
        }

        /// <summary>별 보유량을 후보 상태에 설정합니다.</summary>
        public void SetStars(int value)
        {
            Stars = value;
        }

        /// <summary>선택한 스테이지 난이도를 후보 상태에 설정합니다.</summary>
        public void SetSelectedStageDifficulty(int value)
        {
            SelectedStageDifficulty = value;
        }

        /// <summary>입장권 자연 회복 기준 시각을 설정합니다.</summary>
        public void SetLastHeartRecoveryUtcTicks(long value)
        {
            LastHeartRecoveryUtcTicks = value;
        }

        /// <summary>자유 이용권 만료 시각을 설정합니다.</summary>
        public void SetFreePassExpirationUtcTicks(long value)
        {
            FreePassExpirationUtcTicks = value;
        }

        /// <summary>최초 생성에 사용된 저장 스키마를 기록합니다.</summary>
        public void SetCreatedWithSchemaVersion(int value)
        {
            CreatedWithSchemaVersion = value;
        }

        /// <summary>현재 일일 퀘스트의 UTC 날짜 키를 설정합니다.</summary>
        public void SetDailyQuestDate(string value)
        {
            DailyQuestDateUtc = value;
        }

        /// <summary>날짜 역행 재초기화를 막는 최고 처리 날짜를 설정합니다.</summary>
        public void SetHighestProcessedUtcDate(string value)
        {
            HighestProcessedUtcDate = value;
        }

        /// <summary>
        /// 현재 일일 퀘스트 목록을 깊은 복사본으로 교체합니다.
        /// </summary>
        public void ReplaceDailyQuests(IEnumerable<QuestInstancePayload> quests)
        {
            m_DailyQuestInstances.Clear();
            if (quests == null)
            {
                return;
            }

            foreach (QuestInstancePayload quest in quests)
            {
                if (quest != null)
                {
                    m_DailyQuestInstances.Add(quest.DeepClone());
                }
            }
        }

        /// <summary>
        /// UTC 날짜와 그 날짜에 실제 생성한 긴급 퀘스트 수를 함께 설정합니다.
        /// </summary>
        public void SetEmergencyGeneration(string dateUtc, int count)
        {
            EmergencyGenerationDateUtc = dateUtc;
            EmergencyGenerationCount = count;
        }

        /// <summary>현재 긴급 슬롯을 깊은 복사본으로 교체합니다.</summary>
        public void SetEmergencyQuest(QuestInstancePayload quest)
        {
            EmergencyQuestInstance = quest?.DeepClone();
        }

        /// <summary>현재 실행 중인 도전 표식을 깊은 복사본으로 교체합니다.</summary>
        public void SetActiveStageRun(StageRunPayload stageRun)
        {
            ActiveStageRun = stageRun?.DeepClone();
        }

        /// <summary>로비에서 처리할 클리어 복귀 영수증을 교체합니다.</summary>
        public void SetPendingStageReturn(StageReturnReceiptPayload receipt)
        {
            PendingStageReturn = receipt?.DeepClone();
        }

        /// <summary>긴급 퀘스트 최초 자동 표시 대기를 설정합니다.</summary>
        public void SetEmergencyIntroPending(bool value)
        {
            EmergencyIntroPending = value;
        }

        /// <summary>처리한 복귀 ID를 중복 방지 집합에 추가합니다.</summary>
        public void AddProcessedStageReturn(string returnId)
        {
            if (!string.IsNullOrWhiteSpace(returnId))
            {
                m_ProcessedStageReturnIds.Add(returnId);
            }
        }

        /// <summary>복귀 ID가 이미 추첨 처리되었는지 확인합니다.</summary>
        public bool HasProcessedStageReturn(string returnId)
        {
            return !string.IsNullOrWhiteSpace(returnId) && m_ProcessedStageReturnIds.Contains(returnId);
        }

        /// <summary>최초 1회 규칙 완료 ID를 기록합니다.</summary>
        public void AddCompletedOneShotRule(string ruleId)
        {
            if (!string.IsNullOrWhiteSpace(ruleId))
            {
                m_CompletedOneShotRuleIds.Add(ruleId);
            }
        }

        /// <summary>최초 1회 규칙이 이미 처리되었는지 확인합니다.</summary>
        public bool HasCompletedOneShotRule(string ruleId)
        {
            return !string.IsNullOrWhiteSpace(ruleId) && m_CompletedOneShotRuleIds.Contains(ruleId);
        }

        /// <summary>우편 DTO의 깊은 복사본을 저장 후보에 추가합니다.</summary>
        public void AddMail(MailEntryPayload mail)
        {
            if (mail != null)
            {
                m_MailEntries.Add(mail.DeepClone());
            }
        }

        /// <summary>MailId로 후보 상태의 우편을 찾습니다.</summary>
        public MailEntryPayload FindMail(string mailId)
        {
            for (int i = 0; i < m_MailEntries.Count; i++)
            {
                MailEntryPayload entry = m_MailEntries[i];
                if (entry != null && entry.mailId == mailId)
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>같은 발송 근거 키의 우편이 이미 존재하는지 확인합니다.</summary>
        public bool ContainsMailSource(string sourceKey)
        {
            for (int i = 0; i < m_MailEntries.Count; i++)
            {
                MailEntryPayload entry = m_MailEntries[i];
                if (entry != null && entry.sourceKey == sourceKey)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>일일 목록과 긴급 슬롯에서 인스턴스 ID를 찾습니다.</summary>
        public QuestInstancePayload FindQuest(string instanceId)
        {
            for (int i = 0; i < m_DailyQuestInstances.Count; i++)
            {
                QuestInstancePayload daily = m_DailyQuestInstances[i];
                if (daily != null && daily.instanceId == instanceId)
                {
                    return daily;
                }
            }

            if (EmergencyQuestInstance != null && EmergencyQuestInstance.instanceId == instanceId)
            {
                return EmergencyQuestInstance;
            }

            return null;
        }

        /// <summary>지정 아이템의 영구 보유량을 반환합니다.</summary>
        public int GetItemCount(ItemType type)
        {
            if (type == ItemType.None || !m_ItemCounts.TryGetValue(type, out int count))
            {
                return 0;
            }

            return count;
        }

        /// <summary>지정 아이템의 영구 보유량을 설정하거나 0이면 제거합니다.</summary>
        public void SetItemCount(ItemType type, int count)
        {
            if (type == ItemType.None)
            {
                return;
            }

            if (count <= 0)
            {
                m_ItemCounts.Remove(type);
                return;
            }

            m_ItemCounts[type] = count;
        }

        /// <summary>직렬화 아이템 배열을 정규화하여 내부 사전에 복사합니다.</summary>
        private void CopyItemCounts(ItemCountPayload[] entries)
        {
            m_ItemCounts.Clear();
            if (entries == null)
            {
                return;
            }

            foreach (ItemCountPayload entry in entries)
            {
                if (entry == null || !Enum.IsDefined(typeof(ItemType), entry.itemType))
                {
                    continue;
                }

                ItemType type = (ItemType)entry.itemType;
                if (type != ItemType.None && entry.count > 0)
                {
                    m_ItemCounts[type] = entry.count;
                }
            }
        }

        /// <summary>직렬화 우편 배열을 내부 목록에 깊게 복사합니다.</summary>
        private void CopyMailEntries(MailEntryPayload[] entries)
        {
            m_MailEntries.Clear();
            if (entries == null)
            {
                return;
            }

            foreach (MailEntryPayload entry in entries)
            {
                if (entry != null)
                {
                    m_MailEntries.Add(entry.DeepClone());
                }
            }
        }

        /// <summary>직렬화 일일 퀘스트 배열을 내부 목록에 깊게 복사합니다.</summary>
        private void CopyDailyQuests(QuestInstancePayload[] quests)
        {
            m_DailyQuestInstances.Clear();
            if (quests == null)
            {
                return;
            }

            foreach (QuestInstancePayload quest in quests)
            {
                if (quest != null)
                {
                    m_DailyQuestInstances.Add(quest.DeepClone());
                }
            }
        }

        /// <summary>비어 있지 않은 문자열만 중복 없는 집합으로 복사합니다.</summary>
        private static void CopyStringSet(string[] values, HashSet<string> target)
        {
            target.Clear();
            if (values == null)
            {
                return;
            }

            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    target.Add(value);
                }
            }
        }

        /// <summary>내부 아이템 사전을 직렬화 배열로 변환합니다.</summary>
        private ItemCountPayload[] ToItemCountPayload()
        {
            List<ItemCountPayload> entries = new List<ItemCountPayload>();
            foreach (KeyValuePair<ItemType, int> pair in m_ItemCounts)
            {
                if (pair.Value > 0)
                {
                    entries.Add(new ItemCountPayload { itemType = (int)pair.Key, count = pair.Value });
                }
            }

            return entries.ToArray();
        }

        /// <summary>모든 우편을 직렬화용 깊은 복사 배열로 만듭니다.</summary>
        private MailEntryPayload[] CloneMailEntries()
        {
            MailEntryPayload[] entries = new MailEntryPayload[m_MailEntries.Count];
            for (int i = 0; i < m_MailEntries.Count; i++)
            {
                entries[i] = m_MailEntries[i]?.DeepClone();
            }

            return entries;
        }

        /// <summary>모든 일일 퀘스트를 직렬화용 깊은 복사 배열로 만듭니다.</summary>
        private QuestInstancePayload[] CloneDailyQuests()
        {
            QuestInstancePayload[] entries = new QuestInstancePayload[m_DailyQuestInstances.Count];
            for (int i = 0; i < m_DailyQuestInstances.Count; i++)
            {
                entries[i] = m_DailyQuestInstances[i]?.DeepClone();
            }

            return entries;
        }

        /// <summary>문자열 집합을 체크섬이 안정적인 정렬 배열로 변환합니다.</summary>
        private static string[] ToStringArray(IEnumerable<string> values)
        {
            List<string> entries = new List<string>();
            if (values != null)
            {
                entries.AddRange(values);
            }

            entries.Sort(StringComparer.Ordinal);
            return entries.ToArray();
        }
    }
}
