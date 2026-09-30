using System;

namespace InTheArena.Save
{
    /// <summary>
    /// 플레이어 영구 상태를 JSON으로 직렬화하는 스키마 6 데이터입니다.
    /// </summary>
    [Serializable]
    public sealed class PlayerSavePayload
    {
        public int clearedStageNumber;
        public int gold;
        public int hearts;
        public int stars;
        public int selectedStageDifficulty;
        public long lastHeartRecoveryUtcTicks;
        public long freePassExpirationUtcTicks;
        public ItemCountPayload[] itemCounts;

        public int createdWithSchemaVersion;
        public MailEntryPayload[] mailEntries;
        public string[] completedOneShotRuleIds;

        public string dailyQuestDateUtc;
        public string highestProcessedUtcDate;
        public QuestInstancePayload[] dailyQuestInstances;

        public string emergencyGenerationDateUtc;
        public int emergencyGenerationCount;
        public QuestInstancePayload emergencyQuestInstance;
        public string[] processedStageReturnIds;
        public StageRunPayload activeStageRun;
        public StageReturnReceiptPayload pendingStageReturn;
        public bool emergencyIntroPending;
    }

    [Serializable]
    public sealed class ItemCountPayload
    {
        public int itemType;
        public int count;
    }

    [Serializable]
    public sealed class RewardEntryPayload
    {
        public int kind;
        public int itemType;
        public int amount;

        /// <summary>
        /// 우편 첨부 보상을 독립된 객체로 복사합니다.
        /// </summary>
        public RewardEntryPayload DeepClone()
        {
            return new RewardEntryPayload
            {
                kind = kind,
                itemType = itemType,
                amount = amount
            };
        }
    }

    [Serializable]
    public sealed class MailEntryPayload
    {
        public string mailId;
        public string sourceKey;
        public string title;
        public string body;
        public long createdAtUtcTicks;
        public RewardEntryPayload[] rewards;
        public bool isClaimed;
        public long claimedAtUtcTicks;

        /// <summary>
        /// 우편과 모든 첨부를 깊게 복사합니다.
        /// </summary>
        public MailEntryPayload DeepClone()
        {
            RewardEntryPayload[] rewardCopies = Array.Empty<RewardEntryPayload>();
            if (rewards != null)
            {
                rewardCopies = new RewardEntryPayload[rewards.Length];
                for (int i = 0; i < rewards.Length; i++)
                {
                    rewardCopies[i] = rewards[i]?.DeepClone();
                }
            }

            return new MailEntryPayload
            {
                mailId = mailId,
                sourceKey = sourceKey,
                title = title,
                body = body,
                createdAtUtcTicks = createdAtUtcTicks,
                rewards = rewardCopies,
                isClaimed = isClaimed,
                claimedAtUtcTicks = claimedAtUtcTicks
            };
        }
    }

    [Serializable]
    public sealed class QuestInstancePayload
    {
        public string instanceId;
        public string definitionId;
        public int kind;
        public int tier;
        public int objectiveKind;
        public string title;
        public string description;
        public string objectiveParameter;
        public int targetAmount;
        public int progressAmount;
        public int rewardGold;
        public int status;
        public long createdAtUtcTicks;
        public long expiresAtUtcTicks;
        public long completionRequestedAtUtcTicks;
        public string assignedProfileId;
        public string assignedNpcId;
        public string[] consumedEventIds;

        /// <summary>
        /// 퀘스트 스냅샷과 이벤트 소비 기록을 깊게 복사합니다.
        /// </summary>
        public QuestInstancePayload DeepClone()
        {
            return new QuestInstancePayload
            {
                instanceId = instanceId,
                definitionId = definitionId,
                kind = kind,
                tier = tier,
                objectiveKind = objectiveKind,
                title = title,
                description = description,
                objectiveParameter = objectiveParameter,
                targetAmount = targetAmount,
                progressAmount = progressAmount,
                rewardGold = rewardGold,
                status = status,
                createdAtUtcTicks = createdAtUtcTicks,
                expiresAtUtcTicks = expiresAtUtcTicks,
                completionRequestedAtUtcTicks = completionRequestedAtUtcTicks,
                assignedProfileId = assignedProfileId,
                assignedNpcId = assignedNpcId,
                consumedEventIds = consumedEventIds == null
                    ? Array.Empty<string>()
                    : (string[])consumedEventIds.Clone()
            };
        }
    }

    [Serializable]
    public sealed class StageRunPayload
    {
        public string stageRunId;
        public int stageNumber;
        public long startedAtUtcTicks;

        /// <summary>
        /// 진행 중 도전 표식을 복사합니다.
        /// </summary>
        public StageRunPayload DeepClone()
        {
            return new StageRunPayload
            {
                stageRunId = stageRunId,
                stageNumber = stageNumber,
                startedAtUtcTicks = startedAtUtcTicks
            };
        }
    }

    [Serializable]
    public sealed class StageReturnReceiptPayload
    {
        public string returnId;
        public string stageRunId;
        public int stageNumber;
        public long clearedAtUtcTicks;

        /// <summary>
        /// 로비 복귀 영수증을 복사합니다.
        /// </summary>
        public StageReturnReceiptPayload DeepClone()
        {
            return new StageReturnReceiptPayload
            {
                returnId = returnId,
                stageRunId = stageRunId,
                stageNumber = stageNumber,
                clearedAtUtcTicks = clearedAtUtcTicks
            };
        }
    }
}
