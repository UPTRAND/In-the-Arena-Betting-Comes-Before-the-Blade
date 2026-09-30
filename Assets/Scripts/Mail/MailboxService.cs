using System;
using System.Collections.Generic;
using InTheArena.MainGame;
using InTheArena.Save;

namespace InTheArena.Mail
{
    public enum MailRewardKind
    {
        Item,
        Gold,
        EntranceTicket
    }

    public enum MailSendResult
    {
        Success,
        AlreadySent,
        InvalidReward,
        Overflow,
        Unavailable,
        SaveFailed
    }

    public enum MailClaimResult
    {
        Claimed,
        AlreadyClaimed,
        NotFound,
        NotInLobby,
        Unavailable,
        InvalidReward,
        Overflow,
        SaveFailed
    }

    public interface ILobbyStateProvider
    {
        bool IsInLobby { get; }
    }

    public sealed class MailGrantRequest
    {
        public string SourceKey { get; }
        public string Title { get; }
        public string Body { get; }
        public IReadOnlyList<RewardEntryPayload> Rewards { get; }

        /// <summary>
        /// 중복 방지 키와 변경 불가능한 첨부 스냅샷으로 발송 요청을 만듭니다.
        /// </summary>
        public MailGrantRequest(string sourceKey, string title, string body, IReadOnlyList<RewardEntryPayload> rewards)
        {
            SourceKey = sourceKey;
            Title = title;
            Body = body;
            Rewards = rewards;
        }
    }

    public sealed class MailView
    {
        public string MailId { get; }
        public string SourceKey { get; }
        public string Title { get; }
        public string Body { get; }
        public long CreatedAtUtcTicks { get; }
        public IReadOnlyList<RewardEntryPayload> Rewards { get; }
        public bool IsClaimed { get; }

        /// <summary>
        /// 저장 DTO를 우편함 UI용 읽기 모델로 복사합니다.
        /// </summary>
        public MailView(MailEntryPayload payload)
        {
            MailId = payload.mailId;
            SourceKey = payload.sourceKey;
            Title = payload.title;
            Body = payload.body;
            CreatedAtUtcTicks = payload.createdAtUtcTicks;
            IsClaimed = payload.isClaimed;

            List<RewardEntryPayload> rewards = new List<RewardEntryPayload>();
            if (payload.rewards != null)
            {
                for (int i = 0; i < payload.rewards.Length; i++)
                {
                    if (payload.rewards[i] != null)
                    {
                        rewards.Add(payload.rewards[i].DeepClone());
                    }
                }
            }

            Rewards = rewards.AsReadOnly();
        }
    }

    /// <summary>
    /// 우편 발송, 조회, 모든 첨부의 원자적 수령을 담당합니다.
    /// </summary>
    public sealed class MailboxService
    {
        private readonly SaveManager m_SaveManager;
        private readonly ILobbyStateProvider m_LobbyState;

        /// <summary>
        /// 저장과 로비 상태 공급자를 사용해 우편 서비스를 구성합니다.
        /// </summary>
        public MailboxService(SaveManager saveManager, ILobbyStateProvider lobbyState)
        {
            m_SaveManager = saveManager;
            m_LobbyState = lobbyState;
        }

        /// <summary>
        /// 동일 SourceKey를 중복 생성하지 않고 일반 우편을 저장합니다.
        /// </summary>
        public MailSendResult TrySend(MailGrantRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.SourceKey))
            {
                return MailSendResult.InvalidReward;
            }

            if (!ValidateRewards(request.Rewards))
            {
                return MailSendResult.InvalidReward;
            }

            PlayerProgressState candidate = m_SaveManager?.CreateSnapshot();
            if (candidate == null)
            {
                return MailSendResult.Unavailable;
            }

            if (candidate.ContainsMailSource(request.SourceKey))
            {
                return MailSendResult.AlreadySent;
            }

            candidate.AddMail(CreateMail(request, m_SaveManager.UtcNow));
            if (!m_SaveManager.TryCommitCandidate(candidate, out _))
            {
                return MailSendResult.SaveFailed;
            }

            return MailSendResult.Success;
        }

        /// <summary>
        /// 최신 생성 시각과 MailId 순서로 안정 정렬된 우편 목록을 반환합니다.
        /// </summary>
        public IReadOnlyList<MailView> GetMails()
        {
            List<MailView> views = new List<MailView>();
            PlayerProgressState state = m_SaveManager?.CreateSnapshot();
            if (state == null)
            {
                return views.AsReadOnly();
            }

            for (int i = 0; i < state.MailEntries.Count; i++)
            {
                MailEntryPayload entry = state.MailEntries[i];
                if (entry != null)
                {
                    views.Add(new MailView(entry));
                }
            }

            views.Sort(CompareMails);
            return views.AsReadOnly();
        }

        /// <summary>
        /// 로비에서 우편 첨부 전체와 수령 상태를 하나의 저장으로 확정합니다.
        /// </summary>
        public MailClaimResult TryClaim(string mailId)
        {
            if (m_LobbyState == null || !m_LobbyState.IsInLobby)
            {
                return MailClaimResult.NotInLobby;
            }

            PlayerProgressState candidate = m_SaveManager?.CreateSnapshot();
            if (candidate == null)
            {
                return MailClaimResult.Unavailable;
            }

            MailEntryPayload mail = candidate.FindMail(mailId);
            if (mail == null)
            {
                return MailClaimResult.NotFound;
            }

            if (mail.isClaimed)
            {
                return MailClaimResult.AlreadyClaimed;
            }

            MailClaimResult applyResult = ApplyRewards(candidate, mail.rewards, m_SaveManager.UtcNow);
            if (applyResult != MailClaimResult.Claimed)
            {
                return applyResult;
            }

            mail.isClaimed = true;
            mail.claimedAtUtcTicks = m_SaveManager.UtcNow.Ticks;
            if (!m_SaveManager.TryCommitCandidate(candidate, out _))
            {
                return MailClaimResult.SaveFailed;
            }

            return MailClaimResult.Claimed;
        }

        /// <summary>
        /// 현재 저장에서 아직 수령하지 않은 우편 수를 계산합니다.
        /// </summary>
        public int GetUnclaimedCount()
        {
            int count = 0;
            IReadOnlyList<MailView> mails = GetMails();
            for (int i = 0; i < mails.Count; i++)
            {
                if (!mails[i].IsClaimed)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 발송 요청을 저장 가능한 우편 DTO로 변환합니다.
        /// </summary>
        public static MailEntryPayload CreateMail(MailGrantRequest request, DateTime createdAtUtc)
        {
            RewardEntryPayload[] rewards = new RewardEntryPayload[request.Rewards.Count];
            for (int i = 0; i < request.Rewards.Count; i++)
            {
                rewards[i] = request.Rewards[i].DeepClone();
            }

            return new MailEntryPayload
            {
                mailId = Guid.NewGuid().ToString("N"),
                sourceKey = request.SourceKey,
                title = request.Title,
                body = request.Body,
                createdAtUtcTicks = createdAtUtc.Ticks,
                rewards = rewards,
                isClaimed = false,
                claimedAtUtcTicks = 0
            };
        }

        /// <summary>
        /// 모든 첨부를 후보 상태에 적용하며 하나라도 실패하면 수령 전체를 거절합니다.
        /// </summary>
        private static MailClaimResult ApplyRewards(
            PlayerProgressState candidate,
            RewardEntryPayload[] rewards,
            DateTime claimedAtUtc)
        {
            if (rewards == null || rewards.Length == 0)
            {
                return MailClaimResult.InvalidReward;
            }

            try
            {
                for (int i = 0; i < rewards.Length; i++)
                {
                    RewardEntryPayload reward = rewards[i];
                    if (!ValidateReward(reward))
                    {
                        return MailClaimResult.InvalidReward;
                    }

                    MailRewardKind kind = (MailRewardKind)reward.kind;
                    if (kind == MailRewardKind.Gold)
                    {
                        candidate.SetGold(checked(candidate.Gold + reward.amount));
                    }
                    else if (kind == MailRewardKind.EntranceTicket)
                    {
                        candidate.SetHearts(checked(candidate.Hearts + reward.amount));
                        if (candidate.Hearts >= SaveManager.MaxHearts)
                        {
                            candidate.SetLastHeartRecoveryUtcTicks(claimedAtUtc.Ticks);
                        }
                    }
                    else
                    {
                        ItemType itemType = (ItemType)reward.itemType;
                        candidate.SetItemCount(itemType, checked(candidate.GetItemCount(itemType) + reward.amount));
                    }
                }
            }
            catch (OverflowException)
            {
                return MailClaimResult.Overflow;
            }

            return MailClaimResult.Claimed;
        }

        /// <summary>
        /// 첨부 목록 전체가 지원되는 보상 형식인지 검사합니다.
        /// </summary>
        private static bool ValidateRewards(IReadOnlyList<RewardEntryPayload> rewards)
        {
            if (rewards == null || rewards.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < rewards.Count; i++)
            {
                if (!ValidateReward(rewards[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 첨부 하나의 종류와 수량이 안전한지 검사합니다.
        /// </summary>
        private static bool ValidateReward(RewardEntryPayload reward)
        {
            if (reward == null || reward.amount <= 0 || !Enum.IsDefined(typeof(MailRewardKind), reward.kind))
            {
                return false;
            }

            if ((MailRewardKind)reward.kind != MailRewardKind.Item)
            {
                return true;
            }

            if (!Enum.IsDefined(typeof(ItemType), reward.itemType))
            {
                return false;
            }

            return (ItemType)reward.itemType != ItemType.None;
        }

        /// <summary>
        /// 최신 생성 시각 우선, 동률이면 MailId 순서로 안정 정렬합니다.
        /// </summary>
        private static int CompareMails(MailView left, MailView right)
        {
            int timeOrder = right.CreatedAtUtcTicks.CompareTo(left.CreatedAtUtcTicks);
            if (timeOrder != 0)
            {
                return timeOrder;
            }

            return string.CompareOrdinal(left.MailId, right.MailId);
        }
    }
}
