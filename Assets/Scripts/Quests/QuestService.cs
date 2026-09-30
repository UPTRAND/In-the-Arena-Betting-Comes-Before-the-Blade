using System;
using System.Collections.Generic;
using InTheArena.Events.Core;
using InTheArena.Mail;
using InTheArena.MainGame;
using InTheArena.Save;

namespace InTheArena.Quests
{
    /// <summary>
    /// 일일·긴급 퀘스트의 생성, 진행, 만료, NPC 배정과 완료 확인을 담당합니다.
    /// </summary>
    public sealed class QuestService
    {
        private const string WelcomeRuleId = "welcome:first-launch:v1";
        private const int AutomaticDeliveryRetryLimit = 3;

        private readonly SaveManager m_SaveManager;
        private readonly QuestCatalog m_Catalog;
        private readonly IQuestRandom m_Random;
        private readonly ILobbyStateProvider m_LobbyState;
        private readonly Dictionary<string, PendingCompletion> m_PendingCompletions = new Dictionary<string, PendingCompletion>(StringComparer.Ordinal);
        private readonly Dictionary<string, EmergencyDecision> m_EmergencyDecisions = new Dictionary<string, EmergencyDecision>(StringComparer.Ordinal);

        public event Action Changed;

        public QuestService(
            SaveManager saveManager,
            QuestCatalog catalog,
            IQuestRandom random,
            ILobbyStateProvider lobbyState)
        {
            m_SaveManager = saveManager;
            m_Catalog = catalog;
            m_Random = random;
            m_LobbyState = lobbyState;
        }

        /// <summary>
        /// 저장 복원 직후 시간 경계, 신규 환영 우편, 일일 목록과 중단 도전을 한 저장으로 준비합니다.
        /// </summary>
        public QuestRefreshResult Initialize()
        {
            if (!ValidateDependencies(out _))
            {
                return QuestRefreshResult.InvalidCatalog;
            }

            PlayerProgressState candidate = m_SaveManager.CreateSnapshot();
            if (candidate == null)
            {
                return QuestRefreshResult.Unavailable;
            }

            DateTime now = m_SaveManager.UtcNow;
            bool changed = RefreshTimeBoundaries(candidate, now);
            changed |= EnsureWelcomeMail(candidate, now);
            changed |= RecoverInterruptedStage(candidate, now);

            if (!changed)
            {
                return QuestRefreshResult.NoChange;
            }

            if (!m_SaveManager.TryCommitCandidate(candidate, out _))
            {
                return QuestRefreshResult.SaveFailed;
            }

            Changed?.Invoke();
            return QuestRefreshResult.Committed;
        }

        /// <summary>
        /// UTC 날짜와 퀘스트 만료 경계를 최신 저장 상태에 반영합니다.
        /// </summary>
        public QuestRefreshResult RefreshTimeBoundaries(DateTime utcNow)
        {
            PlayerProgressState candidate = m_SaveManager?.CreateSnapshot();
            if (candidate == null)
            {
                return QuestRefreshResult.Unavailable;
            }

            if (!RefreshTimeBoundaries(candidate, utcNow))
            {
                return QuestRefreshResult.NoChange;
            }

            if (!m_SaveManager.TryCommitCandidate(candidate, out _))
            {
                return QuestRefreshResult.SaveFailed;
            }

            Changed?.Invoke();
            return QuestRefreshResult.Committed;
        }

        /// <summary>
        /// 현재 날짜의 완료 이력을 포함한 일일 퀘스트 읽기 모델을 반환합니다.
        /// </summary>
        public IReadOnlyList<QuestView> GetDailyQuests()
        {
            List<QuestView> views = new List<QuestView>();
            PlayerProgressState state = m_SaveManager?.CreateSnapshot();
            if (state == null)
            {
                return views.AsReadOnly();
            }

            for (int i = 0; i < state.DailyQuestInstances.Count; i++)
            {
                QuestInstancePayload quest = state.DailyQuestInstances[i];
                if (quest != null)
                {
                    views.Add(CreateView(quest));
                }
            }

            return views.AsReadOnly();
        }

        /// <summary>
        /// 현재 슬롯을 점유하거나 확인 가능한 긴급 퀘스트를 반환합니다.
        /// </summary>
        public QuestView GetEmergencyQuest()
        {
            PlayerProgressState state = m_SaveManager?.CreateSnapshot();
            QuestInstancePayload quest = state?.EmergencyQuestInstance;
            if (quest == null || IsTerminal((QuestStatus)quest.status))
            {
                return null;
            }

            return CreateView(quest);
        }

        /// <summary>
        /// 인스턴스 ID로 일일 또는 긴급 퀘스트를 조회합니다.
        /// </summary>
        public QuestView GetQuest(string instanceId)
        {
            PlayerProgressState state = m_SaveManager?.CreateSnapshot();
            QuestInstancePayload quest = state?.FindQuest(instanceId);
            if (quest == null)
            {
                return null;
            }

            return CreateView(quest);
        }

        /// <summary>
        /// 확정 베팅액 이벤트를 모든 활성 퀘스트에 반영합니다.
        /// </summary>
        public bool ProcessBetPlaced(BetPlacedEvent eventData)
        {
            if (eventData == null)
            {
                return false;
            }

            return ProcessProgressEvent(eventData, ApplyBetPlaced);
        }

        /// <summary>
        /// 항목별 적중과 지급액 이벤트를 모든 활성 퀘스트에 반영합니다.
        /// </summary>
        public bool ProcessBetSettled(BetSettledEvent eventData)
        {
            if (eventData == null || eventData.Settlement == null)
            {
                return false;
            }

            return ProcessProgressEvent(eventData, ApplyBetSettled);
        }

        /// <summary>
        /// 도전 시작 표식을 저장하여 다음 실행의 중단 복구 근거를 만듭니다.
        /// </summary>
        public bool ProcessStageStarted(StageStartedEvent eventData)
        {
            if (eventData == null)
            {
                return false;
            }

            PlayerProgressState candidate = m_SaveManager?.CreateSnapshot();
            if (candidate == null)
            {
                return false;
            }

            RefreshTimeBoundaries(candidate, new DateTime(eventData.OccurredAtUtcTicks, DateTimeKind.Utc));
            candidate.SetActiveStageRun(new StageRunPayload
            {
                stageRunId = eventData.StageRunId,
                stageNumber = eventData.StageNumber,
                startedAtUtcTicks = eventData.OccurredAtUtcTicks
            });

            if (!m_SaveManager.TryCommitCandidate(candidate, out _))
            {
                return false;
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 스테이지 클리어 저장 후보에 연속 클리어 진행과 로비 복귀 영수증을 함께 적용합니다.
        /// </summary>
        public void ApplyStageClearedToCandidate(PlayerProgressState candidate, StageClearedEvent eventData)
        {
            if (candidate == null || eventData == null)
            {
                return;
            }

            DateTime occurredAt = new DateTime(eventData.OccurredAtUtcTicks, DateTimeKind.Utc);
            RefreshTimeBoundaries(candidate, occurredAt);
            ApplyToActiveQuests(candidate, eventData, ApplyStageCleared);
            candidate.SetActiveStageRun(null);
            candidate.SetPendingStageReturn(new StageReturnReceiptPayload
            {
                returnId = Guid.NewGuid().ToString("N"),
                stageRunId = eventData.StageRunId,
                stageNumber = eventData.StageNumber,
                clearedAtUtcTicks = eventData.OccurredAtUtcTicks
            });
        }

        /// <summary>
        /// 패배·이탈·오류 종료를 저장하고 연속 클리어 긴급 퀘스트의 실패 정책을 적용합니다.
        /// </summary>
        public bool ProcessStageEnded(StageEndedEvent eventData)
        {
            if (eventData == null)
            {
                return false;
            }

            PlayerProgressState candidate = m_SaveManager?.CreateSnapshot();
            if (candidate == null)
            {
                return false;
            }

            DateTime occurredAt = new DateTime(eventData.OccurredAtUtcTicks, DateTimeKind.Utc);
            bool changed = RefreshTimeBoundaries(candidate, occurredAt);
            changed |= ApplyStageFailure(candidate, eventData);

            if (candidate.ActiveStageRun != null)
            {
                candidate.SetActiveStageRun(null);
                changed = true;
            }

            if (!changed)
            {
                return true;
            }

            if (!m_SaveManager.TryCommitCandidate(candidate, out _))
            {
                return false;
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 저장된 클리어 복귀 영수증을 한 번만 추첨하여 긴급 퀘스트 생성 여부를 확정합니다.
        /// </summary>
        public bool ProcessPendingStageReturn()
        {
            PlayerProgressState candidate = m_SaveManager?.CreateSnapshot();
            StageReturnReceiptPayload receipt = candidate?.PendingStageReturn;
            if (candidate == null || receipt == null || string.IsNullOrWhiteSpace(receipt.returnId))
            {
                return false;
            }

            if (candidate.HasProcessedStageReturn(receipt.returnId))
            {
                candidate.SetPendingStageReturn(null);
                return m_SaveManager.TryCommitCandidate(candidate, out _);
            }

            DateTime now = m_SaveManager.UtcNow;
            RefreshTimeBoundaries(candidate, now);
            ResetEmergencyCountIfNeeded(candidate, now);

            EmergencyDecision decision = GetEmergencyDecision(receipt.returnId);
            bool canCreate = !HasOccupiedEmergencySlot(candidate)
                && candidate.EmergencyGenerationCount < m_Catalog.EmergencyDailyLimit;

            if (canCreate && decision.ShouldCreate)
            {
                QuestDefinition definition = m_Catalog.EmergencyDefinitions[decision.DefinitionIndex];
                QuestTier tier = SelectTier(decision.TierRoll);
                string parameter = SelectParameter(definition.ObjectiveKind, decision.ParameterRoll);
                QuestInstancePayload quest = definition.CreateInstance(tier, parameter, now);
                candidate.SetEmergencyQuest(quest);
                candidate.SetEmergencyGeneration(FormatDate(now), candidate.EmergencyGenerationCount + 1);
                candidate.SetEmergencyIntroPending(true);
            }

            candidate.AddProcessedStageReturn(receipt.returnId);
            candidate.SetPendingStageReturn(null);

            if (!m_SaveManager.TryCommitCandidate(candidate, out _))
            {
                return false;
            }

            m_EmergencyDecisions.Remove(receipt.returnId);
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 완료 확인 요청을 검증하고 보상 우편과 Delivered 상태를 한 저장으로 확정합니다.
        /// </summary>
        public QuestCompletionResult TryConfirmCompletion(string instanceId)
        {
            if (m_LobbyState == null || !m_LobbyState.IsInLobby)
            {
                return QuestCompletionResult.NotInLobby;
            }

            PlayerProgressState state = m_SaveManager?.CreateSnapshot();
            if (state == null)
            {
                return QuestCompletionResult.Unavailable;
            }

            QuestInstancePayload quest = state.FindQuest(instanceId);
            if (quest == null)
            {
                return QuestCompletionResult.NotFound;
            }

            DateTime requestedAt = m_SaveManager.UtcNow;
            if (requestedAt.Ticks >= quest.expiresAtUtcTicks)
            {
                RefreshTimeBoundaries(requestedAt);
                return QuestCompletionResult.Expired;
            }

            QuestStatus status = (QuestStatus)quest.status;
            if (status == QuestStatus.Delivered || m_PendingCompletions.ContainsKey(instanceId))
            {
                return QuestCompletionResult.AlreadyProcessed;
            }

            if (status == QuestStatus.Failed)
            {
                return QuestCompletionResult.FailedQuest;
            }

            if (status != QuestStatus.ReadyToConfirm)
            {
                return QuestCompletionResult.GoalNotReached;
            }

            PendingCompletion pending = new PendingCompletion(quest, requestedAt);
            if (TryDeliverCompletion(pending))
            {
                return QuestCompletionResult.Success;
            }

            m_PendingCompletions[instanceId] = pending;
            Changed?.Invoke();
            return QuestCompletionResult.SaveFailed;
        }

        /// <summary>
        /// 저장 실패한 완료 확인을 1초 간격, 최대 3회 자동 재시도합니다.
        /// </summary>
        public void RetryPendingDeliveries(bool manualRetry)
        {
            if (m_PendingCompletions.Count == 0)
            {
                return;
            }

            DateTime now = m_SaveManager.UtcNow;
            List<string> completed = new List<string>();
            List<PendingCompletion> pendingItems = new List<PendingCompletion>(m_PendingCompletions.Values);

            for (int i = 0; i < pendingItems.Count; i++)
            {
                PendingCompletion pending = pendingItems[i];
                if (!manualRetry)
                {
                    if (pending.RetryCount >= AutomaticDeliveryRetryLimit || now < pending.NextRetryAtUtc)
                    {
                        continue;
                    }
                }

                pending.RetryCount++;
                pending.NextRetryAtUtc = now.AddSeconds(1);
                if (TryDeliverCompletion(pending))
                {
                    completed.Add(pending.InstanceId);
                }
            }

            for (int i = 0; i < completed.Count; i++)
            {
                m_PendingCompletions.Remove(completed[i]);
            }

            if (completed.Count > 0)
            {
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// 현재 광장의 실제 NPC 목록에 표시 대상 퀘스트를 1:1로 복원하거나 배정합니다.
        /// </summary>
        public bool AssignLobbyNpcs(string profileId, IReadOnlyList<string> npcIds)
        {
            if (string.IsNullOrWhiteSpace(profileId) || npcIds == null)
            {
                return false;
            }

            PlayerProgressState candidate = m_SaveManager?.CreateSnapshot();
            if (candidate == null)
            {
                return false;
            }

            List<QuestInstancePayload> quests = GetAssignableQuests(candidate);
            HashSet<string> available = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < npcIds.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(npcIds[i]))
                {
                    available.Add(npcIds[i]);
                }
            }

            HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
            bool changed = false;

            for (int i = 0; i < quests.Count; i++)
            {
                QuestInstancePayload quest = quests[i];
                bool validExisting = quest.assignedProfileId == profileId
                    && available.Contains(quest.assignedNpcId)
                    && used.Add(quest.assignedNpcId);

                if (!validExisting && (!string.IsNullOrEmpty(quest.assignedNpcId) || !string.IsNullOrEmpty(quest.assignedProfileId)))
                {
                    quest.assignedNpcId = null;
                    quest.assignedProfileId = null;
                    changed = true;
                }
            }

            List<string> emptyNpcIds = new List<string>();
            foreach (string npcId in available)
            {
                if (!used.Contains(npcId))
                {
                    emptyNpcIds.Add(npcId);
                }
            }

            emptyNpcIds.Sort(StringComparer.Ordinal);
            for (int i = 0; i < quests.Count && emptyNpcIds.Count > 0; i++)
            {
                QuestInstancePayload quest = quests[i];
                if (!string.IsNullOrWhiteSpace(quest.assignedNpcId))
                {
                    continue;
                }

                int index = m_Random.Range(0, emptyNpcIds.Count);
                quest.assignedProfileId = profileId;
                quest.assignedNpcId = emptyNpcIds[index];
                emptyNpcIds.RemoveAt(index);
                changed = true;
            }

            if (!changed)
            {
                return true;
            }

            if (!m_SaveManager.TryCommitCandidate(candidate, out _))
            {
                return false;
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 새 긴급 퀘스트의 최초 자동 표시 대기를 한 번만 소비합니다.
        /// </summary>
        public string ConsumeEmergencyIntro()
        {
            PlayerProgressState candidate = m_SaveManager?.CreateSnapshot();
            if (candidate == null || !candidate.EmergencyIntroPending || candidate.EmergencyQuestInstance == null)
            {
                return null;
            }

            string instanceId = candidate.EmergencyQuestInstance.instanceId;
            candidate.SetEmergencyIntroPending(false);
            if (!m_SaveManager.TryCommitCandidate(candidate, out _))
            {
                return instanceId;
            }

            Changed?.Invoke();
            return instanceId;
        }

        public int PendingDeliveryCount => m_PendingCompletions.Count;

        public bool HasEmergencyIntroPending
        {
            get
            {
                PlayerProgressState state = m_SaveManager?.CreateSnapshot();
                return state != null && state.EmergencyIntroPending;
            }
        }

        public bool HasPendingStageReturn
        {
            get
            {
                PlayerProgressState state = m_SaveManager?.CreateSnapshot();
                return state != null && state.PendingStageReturn != null;
            }
        }

        public float EmergencyChance => m_Catalog.EmergencyChance;
        public int EmergencyDailyLimit => m_Catalog.EmergencyDailyLimit;

#if UNITY_EDITOR
        /// <summary>
        /// 개발 창이 표시할 수 있도록 긴급 풀의 정의 ID 목록을 복사해 반환합니다.
        /// </summary>
        public IReadOnlyList<string> GetEmergencyDefinitionIds()
        {
            List<string> definitionIds = new List<string>();
            for (int i = 0; i < m_Catalog.EmergencyDefinitions.Count; i++)
            {
                QuestDefinition definition = m_Catalog.EmergencyDefinitions[i];
                if (definition != null)
                {
                    definitionIds.Add(definition.DefinitionId);
                }
            }

            return definitionIds.AsReadOnly();
        }

        /// <summary>
        /// 개발 창에서 지정 정의 또는 랜덤 풀을 호출하되 실제 슬롯과 일일 상한은 우회하지 않습니다.
        /// </summary>
        public bool TryCreateEmergencyForDebug(string definitionId, out string message)
        {
            PlayerProgressState candidate = m_SaveManager?.CreateSnapshot();
            if (candidate == null)
            {
                message = "저장 상태를 사용할 수 없습니다.";
                return false;
            }

            DateTime now = m_SaveManager.UtcNow;
            RefreshTimeBoundaries(candidate, now);
            ResetEmergencyCountIfNeeded(candidate, now);

            if (HasOccupiedEmergencySlot(candidate))
            {
                message = "긴급 퀘스트 슬롯이 이미 점유되어 있습니다.";
                return false;
            }

            if (candidate.EmergencyGenerationCount >= m_Catalog.EmergencyDailyLimit)
            {
                message = "오늘의 긴급 퀘스트 생성 상한에 도달했습니다.";
                return false;
            }

            QuestDefinition selected = null;
            if (!string.IsNullOrWhiteSpace(definitionId))
            {
                for (int i = 0; i < m_Catalog.EmergencyDefinitions.Count; i++)
                {
                    QuestDefinition definition = m_Catalog.EmergencyDefinitions[i];
                    if (definition != null && definition.DefinitionId == definitionId)
                    {
                        selected = definition;
                        break;
                    }
                }
            }
            else if (m_Catalog.EmergencyDefinitions.Count > 0)
            {
                int selectedIndex = m_Random.Range(0, m_Catalog.EmergencyDefinitions.Count);
                selected = m_Catalog.EmergencyDefinitions[selectedIndex];
            }

            if (selected == null)
            {
                message = "요청한 긴급 퀘스트 정의를 찾을 수 없습니다.";
                return false;
            }

            QuestTier tier = SelectTier(m_Random.Value());
            string parameter = SelectParameter(selected.ObjectiveKind, m_Random.Value());
            QuestInstancePayload quest = selected.CreateInstance(tier, parameter, now);
            candidate.SetEmergencyQuest(quest);
            candidate.SetEmergencyGeneration(FormatDate(now), candidate.EmergencyGenerationCount + 1);
            candidate.SetEmergencyIntroPending(false);

            if (!m_SaveManager.TryCommitCandidate(candidate, out string error))
            {
                message = string.IsNullOrWhiteSpace(error) ? "저장에 실패했습니다." : error;
                return false;
            }

            Changed?.Invoke();
            message = $"{selected.DefinitionId} 생성 완료";
            return true;
        }
#endif

        /// <summary>
        /// 시간 경계와 활성 퀘스트 진행을 한 저장 후보에 적용합니다.
        /// </summary>
        private bool ProcessProgressEvent<TEvent>(TEvent eventData, QuestEventApplier<TEvent> applier)
            where TEvent : IGameEvent
        {
            PlayerProgressState candidate = m_SaveManager?.CreateSnapshot();
            if (candidate == null)
            {
                return false;
            }

            DateTime occurredAt = new DateTime(eventData.OccurredAtUtcTicks, DateTimeKind.Utc);
            bool changed = RefreshTimeBoundaries(candidate, occurredAt);
            changed |= ApplyToActiveQuests(candidate, eventData, applier);

            if (!changed)
            {
                return true;
            }

            if (!m_SaveManager.TryCommitCandidate(candidate, out _))
            {
                return false;
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 일일 목록과 긴급 슬롯의 활성 인스턴스에 같은 사실 이벤트를 전달합니다.
        /// </summary>
        private bool ApplyToActiveQuests<TEvent>(
            PlayerProgressState candidate,
            TEvent eventData,
            QuestEventApplier<TEvent> applier)
            where TEvent : IGameEvent
        {
            bool changed = false;
            for (int i = 0; i < candidate.DailyQuestInstances.Count; i++)
            {
                changed |= ApplyToQuest(candidate.DailyQuestInstances[i], eventData, applier);
            }

            changed |= ApplyToQuest(candidate.EmergencyQuestInstance, eventData, applier);
            return changed;
        }

        /// <summary>
        /// 유효 시간·상태·EventId 중복을 확인한 뒤 퀘스트 하나의 진행도를 갱신합니다.
        /// </summary>
        private static bool ApplyToQuest<TEvent>(
            QuestInstancePayload quest,
            TEvent eventData,
            QuestEventApplier<TEvent> applier)
            where TEvent : IGameEvent
        {
            if (quest == null || (QuestStatus)quest.status != QuestStatus.Active)
            {
                return false;
            }

            if (eventData.OccurredAtUtcTicks < quest.createdAtUtcTicks || eventData.OccurredAtUtcTicks >= quest.expiresAtUtcTicks)
            {
                return false;
            }

            if (HasConsumed(quest, eventData.EventId))
            {
                return false;
            }

            int delta = applier(quest, eventData);
            if (delta <= 0)
            {
                return false;
            }

            quest.progressAmount = Math.Min(quest.targetAmount, checked(quest.progressAmount + delta));
            AddConsumed(quest, eventData.EventId);
            if (quest.progressAmount >= quest.targetAmount)
            {
                quest.status = (int)QuestStatus.ReadyToConfirm;
            }

            return true;
        }

        /// <summary>
        /// 확정 원금을 큰손 배팅 목표에 누적합니다.
        /// </summary>
        private static int ApplyBetPlaced(QuestInstancePayload quest, BetPlacedEvent eventData)
        {
            if ((QuestObjectiveKind)quest.objectiveKind == QuestObjectiveKind.BigBettor)
            {
                return Math.Max(0, eventData.WagerCall);
            }

            return 0;
        }

        /// <summary>
        /// 정산 결과를 이중·정밀·내기 목표 규칙에 맞춰 수치로 변환합니다.
        /// </summary>
        private static int ApplyBetSettled(QuestInstancePayload quest, BetSettledEvent eventData)
        {
            QuestObjectiveKind objective = (QuestObjectiveKind)quest.objectiveKind;
            if (objective == QuestObjectiveKind.WinningPayout)
            {
                return eventData.Settlement.IsWin ? Math.Max(0, eventData.Settlement.PayoutCall) : 0;
            }

            if (objective == QuestObjectiveKind.DoubleBet)
            {
                bool targetRed = string.Equals(quest.objectiveParameter, "레드", StringComparison.Ordinal);
                bool targetBlue = string.Equals(quest.objectiveParameter, "블루", StringComparison.Ordinal);
                bool selectedTarget = targetRed && eventData.SelectedFaction == FactionPrediction.Red;
                selectedTarget |= targetBlue && eventData.SelectedFaction == FactionPrediction.Blue;
                bool targetWon = targetRed && eventData.Winner == Team.Red;
                targetWon |= targetBlue && eventData.Winner == Team.Blue;
                return selectedTarget && targetWon ? 1 : 0;
            }

            if (objective != QuestObjectiveKind.PreciseBet)
            {
                return 0;
            }

            bool mainOnly = string.Equals(quest.objectiveParameter, "메인", StringComparison.Ordinal);
            int matchedCount = 0;
            IReadOnlyList<BetOutcome> outcomes = eventData.Settlement.Outcomes;
            for (int i = 0; i < outcomes.Count; i++)
            {
                BetOutcome outcome = outcomes[i];
                if (!outcome.IsSelected || !outcome.IsMatched)
                {
                    continue;
                }

                if (mainOnly && outcome.Category == BetCategory.Faction)
                {
                    matchedCount++;
                }
                else if (!mainOnly && outcome.Category != BetCategory.Faction)
                {
                    matchedCount++;
                }
            }

            return matchedCount;
        }

        /// <summary>
        /// 클리어 사실을 판돈 올리기의 연속 성공 횟수로 반영합니다.
        /// </summary>
        private static int ApplyStageCleared(QuestInstancePayload quest, StageClearedEvent eventData)
        {
            if ((QuestObjectiveKind)quest.objectiveKind == QuestObjectiveKind.ConsecutiveStageClear)
            {
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// 실패 종료일 때 활성 판돈 올리기 퀘스트만 실패 상태로 전이합니다.
        /// </summary>
        private bool ApplyStageFailure(PlayerProgressState candidate, StageEndedEvent eventData)
        {
            bool failureReason = eventData.Reason == StageEndReason.Defeated
                || eventData.Reason == StageEndReason.UserExit
                || eventData.Reason == StageEndReason.InterruptedRecovery;

            if (!failureReason)
            {
                return false;
            }

            QuestInstancePayload emergency = candidate.EmergencyQuestInstance;
            if (emergency == null || (QuestStatus)emergency.status != QuestStatus.Active)
            {
                return false;
            }

            if ((QuestObjectiveKind)emergency.objectiveKind != QuestObjectiveKind.ConsecutiveStageClear)
            {
                return false;
            }

            emergency.status = (int)QuestStatus.Failed;
            emergency.assignedProfileId = null;
            emergency.assignedNpcId = null;
            return true;
        }

        /// <summary>
        /// 만료 처리와 UTC 일일 목록 교체를 저장 후보에 적용합니다.
        /// </summary>
        private bool RefreshTimeBoundaries(PlayerProgressState candidate, DateTime utcNow)
        {
            bool changed = ExpireQuests(candidate, utcNow);
            string currentDate = FormatDate(utcNow);
            string effectiveDate = currentDate;

            if (!string.IsNullOrWhiteSpace(candidate.HighestProcessedUtcDate)
                && string.CompareOrdinal(currentDate, candidate.HighestProcessedUtcDate) < 0)
            {
                effectiveDate = candidate.HighestProcessedUtcDate;
            }

            if (candidate.DailyQuestDateUtc == effectiveDate && candidate.DailyQuestInstances.Count > 0)
            {
                return changed;
            }

            List<QuestInstancePayload> quests = CreateDailyQuests(utcNow);
            if (quests.Count != m_Catalog.DailyCount)
            {
                return changed;
            }

            candidate.SetDailyQuestDate(effectiveDate);
            candidate.SetHighestProcessedUtcDate(effectiveDate);
            candidate.ReplaceDailyQuests(quests);
            changed = true;
            return changed;
        }

        /// <summary>
        /// 모든 일일·긴급 인스턴스의 기한 도달 여부를 검사합니다.
        /// </summary>
        private bool ExpireQuests(PlayerProgressState candidate, DateTime utcNow)
        {
            bool changed = false;
            for (int i = 0; i < candidate.DailyQuestInstances.Count; i++)
            {
                changed |= ExpireQuest(candidate.DailyQuestInstances[i], utcNow);
            }

            changed |= ExpireQuest(candidate.EmergencyQuestInstance, utcNow);
            return changed;
        }

        /// <summary>
        /// 미발송 비종료 인스턴스가 기한에 도달하면 만료시키고 배정을 해제합니다.
        /// </summary>
        private static bool ExpireQuest(QuestInstancePayload quest, DateTime utcNow)
        {
            if (quest == null || utcNow.Ticks < quest.expiresAtUtcTicks)
            {
                return false;
            }

            QuestStatus status = (QuestStatus)quest.status;
            if (status != QuestStatus.Active && status != QuestStatus.ReadyToConfirm)
            {
                return false;
            }

            quest.status = (int)QuestStatus.Expired;
            quest.assignedProfileId = null;
            quest.assignedNpcId = null;
            return true;
        }

        /// <summary>
        /// 정의 ID 중복 없이 현재 UTC 날짜의 일일 인스턴스를 생성합니다.
        /// </summary>
        private List<QuestInstancePayload> CreateDailyQuests(DateTime utcNow)
        {
            List<QuestDefinition> pool = new List<QuestDefinition>();
            for (int i = 0; i < m_Catalog.DailyDefinitions.Count; i++)
            {
                pool.Add(m_Catalog.DailyDefinitions[i]);
            }

            List<QuestInstancePayload> quests = new List<QuestInstancePayload>();
            while (quests.Count < m_Catalog.DailyCount && pool.Count > 0)
            {
                int selectedIndex = m_Random.Range(0, pool.Count);
                QuestDefinition definition = pool[selectedIndex];
                pool.RemoveAt(selectedIndex);

                QuestTier tier = SelectTier(m_Random.Value());
                string parameter = SelectParameter(definition.ObjectiveKind, m_Random.Value());
                quests.Add(definition.CreateInstance(tier, parameter, utcNow));
            }

            return quests;
        }

        /// <summary>
        /// 카탈로그의 50·30·20 가중치와 난수로 등급을 선택합니다.
        /// </summary>
        private QuestTier SelectTier(double roll)
        {
            int total = m_Catalog.TierWeights.x + m_Catalog.TierWeights.y + m_Catalog.TierWeights.z;
            double value = roll * total;
            if (value < m_Catalog.TierWeights.x)
            {
                return QuestTier.Tier1;
            }

            if (value < m_Catalog.TierWeights.x + m_Catalog.TierWeights.y)
            {
                return QuestTier.Tier2;
            }

            return QuestTier.Tier3;
        }

        /// <summary>
        /// 이중·정밀 배팅 목표의 확정 대상 파라미터를 선택합니다.
        /// </summary>
        private static string SelectParameter(QuestObjectiveKind objective, double roll)
        {
            if (objective == QuestObjectiveKind.DoubleBet)
            {
                return roll < 0.5d ? "레드" : "블루";
            }

            if (objective == QuestObjectiveKind.PreciseBet)
            {
                return roll < 0.5d ? "메인" : "서브";
            }

            return string.Empty;
        }

        /// <summary>
        /// 스키마 6에서 새로 생성된 저장에만 최초 환영 우편을 추가합니다.
        /// </summary>
        private bool EnsureWelcomeMail(PlayerProgressState candidate, DateTime now)
        {
            if (candidate.CreatedWithSchemaVersion < 6
                || candidate.HasCompletedOneShotRule(WelcomeRuleId)
                || candidate.ContainsMailSource(WelcomeRuleId))
            {
                return false;
            }

            RewardEntryPayload reward = new RewardEntryPayload
            {
                kind = (int)MailRewardKind.Item,
                itemType = (int)ItemType.FreePass,
                amount = 1
            };
            MailGrantRequest request = new MailGrantRequest(
                WelcomeRuleId,
                "첫 방문을 환영합니다",
                "자유 이용권 1개를 드립니다. 우편에서 수령한 뒤 사용할 수 있습니다.",
                new[] { reward });

            candidate.AddMail(MailboxService.CreateMail(request, now));
            candidate.AddCompletedOneShotRule(WelcomeRuleId);
            return true;
        }

        /// <summary>
        /// 저장된 미완료 도전을 중단 종료로 복구하고 연속 클리어 실패를 적용합니다.
        /// </summary>
        private bool RecoverInterruptedStage(PlayerProgressState candidate, DateTime now)
        {
            if (candidate.ActiveStageRun == null || candidate.PendingStageReturn != null)
            {
                return false;
            }

            StageEndedEvent interrupted = new StageEndedEvent(
                Guid.NewGuid().ToString("N"),
                now.Ticks,
                candidate.ActiveStageRun.stageRunId,
                StageEndReason.InterruptedRecovery);
            ApplyStageFailure(candidate, interrupted);
            candidate.SetActiveStageRun(null);
            return true;
        }

        /// <summary>
        /// 완료 상태와 보상 우편을 같은 저장 후보로 확정합니다.
        /// </summary>
        private bool TryDeliverCompletion(PendingCompletion pending)
        {
            PlayerProgressState candidate = m_SaveManager?.CreateSnapshot();
            if (candidate == null)
            {
                return false;
            }

            if (candidate.ContainsMailSource(pending.SourceKey))
            {
                return true;
            }

            QuestInstancePayload quest = candidate.FindQuest(pending.InstanceId);
            if (quest != null)
            {
                quest.status = (int)QuestStatus.Delivered;
                quest.completionRequestedAtUtcTicks = pending.RequestedAtUtc.Ticks;
                quest.assignedProfileId = null;
                quest.assignedNpcId = null;
            }

            candidate.AddMail(MailboxService.CreateMail(pending.CreateMailRequest(), pending.RequestedAtUtc));
            if (!m_SaveManager.TryCommitCandidate(candidate, out _))
            {
                return false;
            }

            m_PendingCompletions.Remove(pending.InstanceId);
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 저장 재시도에서 같은 결과를 쓰도록 복귀 ID별 긴급 추첨 결정을 캐시합니다.
        /// </summary>
        private EmergencyDecision GetEmergencyDecision(string returnId)
        {
            if (m_EmergencyDecisions.TryGetValue(returnId, out EmergencyDecision decision))
            {
                return decision;
            }

            decision = new EmergencyDecision
            {
                ShouldCreate = m_Random.Value() < m_Catalog.EmergencyChance,
                DefinitionIndex = m_Random.Range(0, m_Catalog.EmergencyDefinitions.Count),
                TierRoll = m_Random.Value(),
                ParameterRoll = m_Random.Value()
            };
            m_EmergencyDecisions.Add(returnId, decision);
            return decision;
        }

        /// <summary>
        /// UTC 날짜가 전진했을 때만 긴급 실제 생성 횟수를 초기화합니다.
        /// </summary>
        private void ResetEmergencyCountIfNeeded(PlayerProgressState candidate, DateTime now)
        {
            string currentDate = FormatDate(now);
            if (candidate.EmergencyGenerationDateUtc == currentDate)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(candidate.EmergencyGenerationDateUtc)
                && string.CompareOrdinal(currentDate, candidate.EmergencyGenerationDateUtc) < 0)
            {
                return;
            }

            candidate.SetEmergencyGeneration(currentDate, 0);
        }

        /// <summary>
        /// 활성·확인 대기·발송 중 긴급 인스턴스가 슬롯을 점유하는지 판정합니다.
        /// </summary>
        private static bool HasOccupiedEmergencySlot(PlayerProgressState candidate)
        {
            QuestInstancePayload quest = candidate.EmergencyQuestInstance;
            if (quest == null)
            {
                return false;
            }

            QuestStatus status = (QuestStatus)quest.status;
            return status == QuestStatus.Active
                || status == QuestStatus.ReadyToConfirm
                || status == QuestStatus.Submitting;
        }

        /// <summary>
        /// NPC 말풍선을 유지할 일일·긴급 인스턴스 목록을 구성합니다.
        /// </summary>
        private static List<QuestInstancePayload> GetAssignableQuests(PlayerProgressState state)
        {
            List<QuestInstancePayload> quests = new List<QuestInstancePayload>();
            for (int i = 0; i < state.DailyQuestInstances.Count; i++)
            {
                QuestInstancePayload quest = state.DailyQuestInstances[i];
                if (IsAssignable(quest))
                {
                    quests.Add(quest);
                }
            }

            if (IsAssignable(state.EmergencyQuestInstance))
            {
                quests.Add(state.EmergencyQuestInstance);
            }

            return quests;
        }

        /// <summary>
        /// 진행·확인 대기·발송 중 상태인지 확인합니다.
        /// </summary>
        private static bool IsAssignable(QuestInstancePayload quest)
        {
            if (quest == null)
            {
                return false;
            }

            QuestStatus status = (QuestStatus)quest.status;
            return status == QuestStatus.Active
                || status == QuestStatus.ReadyToConfirm
                || status == QuestStatus.Submitting;
        }

        /// <summary>
        /// 저장 DTO를 외부에서 수정할 수 없는 읽기 모델로 변환합니다.
        /// </summary>
        private QuestView CreateView(QuestInstancePayload quest)
        {
            QuestInstancePayload viewPayload = quest.DeepClone();
            if (m_PendingCompletions.ContainsKey(quest.instanceId))
            {
                viewPayload.status = (int)QuestStatus.Submitting;
            }

            return new QuestView(viewPayload);
        }

        /// <summary>
        /// 저장 관리자와 콘텐츠 카탈로그가 초기화 가능한 상태인지 검사합니다.
        /// </summary>
        private bool ValidateDependencies(out string error)
        {
            error = null;
            if (m_SaveManager == null || m_Random == null)
            {
                error = "Save manager or random source is unavailable.";
                return false;
            }

            if (m_Catalog == null || !m_Catalog.IsValid(out error))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 인스턴스가 같은 사실 EventId를 이미 반영했는지 검사합니다.
        /// </summary>
        private static bool HasConsumed(QuestInstancePayload quest, string eventId)
        {
            if (quest.consumedEventIds == null)
            {
                return false;
            }

            for (int i = 0; i < quest.consumedEventIds.Length; i++)
            {
                if (quest.consumedEventIds[i] == eventId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 진행도와 함께 저장할 소비 EventId 기록을 추가합니다.
        /// </summary>
        private static void AddConsumed(QuestInstancePayload quest, string eventId)
        {
            int oldLength = quest.consumedEventIds?.Length ?? 0;
            string[] eventIds = new string[oldLength + 1];
            if (oldLength > 0)
            {
                Array.Copy(quest.consumedEventIds, eventIds, oldLength);
            }

            eventIds[oldLength] = eventId;
            quest.consumedEventIds = eventIds;
        }

        /// <summary>
        /// 더 이상 슬롯이나 NPC 배정을 점유하지 않는 종료 상태인지 판정합니다.
        /// </summary>
        private static bool IsTerminal(QuestStatus status)
        {
            return status == QuestStatus.Delivered
                || status == QuestStatus.Failed
                || status == QuestStatus.Expired;
        }

        /// <summary>
        /// UTC 날짜를 저장용 고정 형식 키로 변환합니다.
        /// </summary>
        private static string FormatDate(DateTime utcNow)
        {
            return utcNow.ToString("yyyy-MM-dd");
        }

        private delegate int QuestEventApplier<TEvent>(QuestInstancePayload quest, TEvent eventData)
            where TEvent : IGameEvent;

        private sealed class PendingCompletion
        {
            public string InstanceId { get; }
            public string SourceKey { get; }
            public string Title { get; }
            public string Body { get; }
            public int RewardGold { get; }
            public DateTime RequestedAtUtc { get; }
            public DateTime NextRetryAtUtc { get; set; }
            public int RetryCount { get; set; }

            public PendingCompletion(QuestInstancePayload quest, DateTime requestedAtUtc)
            {
                InstanceId = quest.instanceId;
                SourceKey = $"quest:{quest.instanceId}";
                Title = $"{quest.title} 완료 보상";
                Body = $"{quest.description} 목표를 완료했습니다.";
                RewardGold = quest.rewardGold;
                RequestedAtUtc = requestedAtUtc;
                NextRetryAtUtc = requestedAtUtc.AddSeconds(1);
            }

            /// <summary>
            /// 저장 재시도마다 동일한 보상 스냅샷의 우편 요청을 만듭니다.
            /// </summary>
            public MailGrantRequest CreateMailRequest()
            {
                RewardEntryPayload reward = new RewardEntryPayload
                {
                    kind = (int)MailRewardKind.Gold,
                    amount = RewardGold
                };
                return new MailGrantRequest(SourceKey, Title, Body, new[] { reward });
            }
        }

        private sealed class EmergencyDecision
        {
            public bool ShouldCreate;
            public int DefinitionIndex;
            public double TierRoll;
            public double ParameterRoll;
        }
    }
}
