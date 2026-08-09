#if UNITY_6000_0_OR_NEWER
namespace InTheArena.UI
{
    /// <summary>One-shot data handoff for the lobby reward animation after a successful stage-clear save.</summary>
    public static class StageClearRewardPresentation
    {
        public readonly struct RewardData
        {
            public readonly int GoldBeforeReward;
            public readonly int GoldAfterReward;
            public readonly int StarsBeforeReward;
            public readonly int StarsAfterReward;

            public RewardData(int goldBeforeReward, int goldAfterReward, int starsBeforeReward, int starsAfterReward)
            {
                GoldBeforeReward = goldBeforeReward;
                GoldAfterReward = goldAfterReward;
                StarsBeforeReward = starsBeforeReward;
                StarsAfterReward = starsAfterReward;
            }
        }

        private static RewardData? s_PendingReward;

        public static void Queue(RewardData reward) => s_PendingReward = reward;

        public static bool TryConsume(out RewardData reward)
        {
            if (!s_PendingReward.HasValue)
            {
                reward = default;
                return false;
            }

            reward = s_PendingReward.Value;
            s_PendingReward = null;
            return true;
        }

        public static void Clear() => s_PendingReward = null;
    }
}
#endif
