using System;
using System.Collections.Generic;
using InTheArena.MainGame;

namespace InTheArena.Save
{
    public interface IClock
    {
        DateTime UtcNow { get; }
    }

    public class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    public static class PlayerSaveValidator
    {
        public const int CurrentSchemaVersion = 6;

        public static bool ValidateAndNormalize(PlayerSaveEnvelope envelope, IClock clock)
        {
            if (envelope == null) return false;

            if (envelope.schemaVersion > CurrentSchemaVersion)
            {
                UnityEngine.Debug.LogError($"[PlayerSaveValidator] 지원하지 않는 미래 스키마 버전({envelope.schemaVersion})입니다. 현재 지원 버전: {CurrentSchemaVersion}");
                return false;
            }

            if (envelope.payload == null)
            {
                envelope.payload = new PlayerSavePayload();
            }

            var payload = envelope.payload;
            payload.clearedStageNumber = Math.Max(0, payload.clearedStageNumber);
            payload.gold = Math.Max(0, payload.gold);
            // 스키마 6부터 보상으로 자연 회복 상한을 초과할 수 있습니다.
            if (envelope.schemaVersion < 6)
            {
                payload.hearts = Math.Clamp(payload.hearts, 0, 5);
            }
            else
            {
                payload.hearts = Math.Max(0, payload.hearts);
            }
            payload.stars = Math.Max(0, payload.stars);
            payload.selectedStageDifficulty = Math.Clamp(payload.selectedStageDifficulty, 0, 2);
            var normalizedItems = new Dictionary<ItemType, int>();
            if (payload.itemCounts != null)
            {
                foreach (ItemCountPayload entry in payload.itemCounts)
                {
                    if (entry == null || !Enum.IsDefined(typeof(ItemType), entry.itemType)) continue;
                    ItemType type = (ItemType)entry.itemType;
                    if (type == ItemType.None || entry.count <= 0) continue;
                    normalizedItems[type] = Math.Max(0, normalizedItems.TryGetValue(type, out int oldCount) ? oldCount + entry.count : entry.count);
                }
            }
            var itemEntries = new List<ItemCountPayload>();
            foreach (var pair in normalizedItems) itemEntries.Add(new ItemCountPayload { itemType = (int)pair.Key, count = pair.Value });
            payload.itemCounts = itemEntries.ToArray();

            long nowTicks = clock.UtcNow.Ticks;
            if (payload.lastHeartRecoveryUtcTicks > nowTicks)
            {
                TimeSpan diff = TimeSpan.FromTicks(payload.lastHeartRecoveryUtcTicks - nowTicks);
                if (diff.TotalMinutes > 5)
                {
                    UnityEngine.Debug.LogWarning($"[PlayerSaveValidator] 저장된 하트 회복 시간이 현재 시간보다 5분 이상 미래입니다. 차이: {diff.TotalMinutes}분. 현재 시간으로 리셋합니다.");
                }
                // 정책: 어떠한 미래 시간이든 항상 현재 시간으로 클램핑 (5분은 로깅 임계값일 뿐)
                payload.lastHeartRecoveryUtcTicks = nowTicks;
            }
            else if (payload.lastHeartRecoveryUtcTicks <= 0)
            {
                payload.lastHeartRecoveryUtcTicks = nowTicks;
            }

            // 자유 이용권은 스키마 5부터 지원한다. 이전 스키마에 임의로 들어간 값은 사용하지 않는다.
            if (envelope.schemaVersion < 5)
            {
                payload.freePassExpirationUtcTicks = 0;
            }
            else
            {
                payload.freePassExpirationUtcTicks = Math.Clamp(
                    payload.freePassExpirationUtcTicks,
                    0,
                    DateTime.MaxValue.Ticks);

                if (payload.freePassExpirationUtcTicks <= nowTicks)
                {
                    payload.freePassExpirationUtcTicks = 0;
                }
            }

            if (envelope.schemaVersion < 6)
            {
                payload.createdWithSchemaVersion = 0;
                payload.mailEntries = Array.Empty<MailEntryPayload>();
                payload.completedOneShotRuleIds = Array.Empty<string>();
                payload.dailyQuestInstances = Array.Empty<QuestInstancePayload>();
                payload.processedStageReturnIds = Array.Empty<string>();
                payload.emergencyQuestInstance = null;
                payload.activeStageRun = null;
                payload.pendingStageReturn = null;
                payload.emergencyIntroPending = false;
            }

            payload.mailEntries = payload.mailEntries ?? Array.Empty<MailEntryPayload>();
            payload.completedOneShotRuleIds = payload.completedOneShotRuleIds ?? Array.Empty<string>();
            payload.dailyQuestInstances = payload.dailyQuestInstances ?? Array.Empty<QuestInstancePayload>();
            payload.processedStageReturnIds = payload.processedStageReturnIds ?? Array.Empty<string>();
            payload.emergencyGenerationCount = Math.Max(0, payload.emergencyGenerationCount);

            return true;
        }
    }
}
