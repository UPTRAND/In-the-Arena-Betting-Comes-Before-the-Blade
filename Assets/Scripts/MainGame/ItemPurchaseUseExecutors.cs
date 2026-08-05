#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;

namespace InTheArena.MainGame
{
    public sealed class BettingItemUseExecutor : IReversibleItemPurchaseUseExecutor
    {
        private readonly BettingPhase m_BettingPhase;
        private List<SpecialBetType> m_PreviousSpecialBets;
        private SpecialBetType? m_PreviousOverride;
        private bool m_HasSnapshot;

        public BettingItemUseExecutor(BettingPhase bettingPhase)
        {
            m_BettingPhase = bettingPhase;
        }

        public bool TryExecute(ItemData itemData, out string message)
        {
            message = string.Empty;
            if (m_BettingPhase == null)
            {
                message = "베팅 페이즈가 없습니다.";
                return false;
            }

            IReadOnlyList<SpecialBetType> activeSpecialBets = m_BettingPhase.GetActiveSpecialBetsForItemUse();
            m_PreviousSpecialBets = activeSpecialBets != null
                ? new List<SpecialBetType>(activeSpecialBets)
                : new List<SpecialBetType>();
            m_PreviousOverride = m_BettingPhase.OverriddenSpecialBet;
            m_HasSnapshot = true;
            return m_BettingPhase.TryApplyPurchasedItemEffect(itemData, out message);
        }

        public void Rollback(ItemData itemData)
        {
            if (!m_HasSnapshot || m_BettingPhase == null)
            {
                return;
            }

            m_BettingPhase.RestorePurchasedItemState(m_PreviousSpecialBets, m_PreviousOverride);
            m_HasSnapshot = false;
        }
    }

    public sealed class CombatTimeExtensionUseExecutor : IReversibleItemPurchaseUseExecutor
    {
        private readonly CombatPhase m_CombatPhase;

        public CombatTimeExtensionUseExecutor(CombatPhase combatPhase)
        {
            m_CombatPhase = combatPhase;
        }

        public bool TryExecute(ItemData itemData, out string message)
        {
            if (m_CombatPhase == null)
            {
                message = "전투 페이즈가 없습니다.";
                return false;
            }

            return m_CombatPhase.TryApplyPurchasedItemEffect(itemData, out message);
        }

        public void Rollback(ItemData itemData)
        {
            m_CombatPhase?.RollbackPurchasedTimeExtension();
        }
    }

    public sealed class CombatMeteorUseExecutor : IItemPurchaseUseExecutor
    {
        private readonly CombatPhase m_CombatPhase;
        private readonly UnityEngine.Vector3 m_TargetPosition;

        public CombatMeteorUseExecutor(CombatPhase combatPhase, UnityEngine.Vector3 targetPosition)
        {
            m_CombatPhase = combatPhase;
            m_TargetPosition = targetPosition;
        }

        public bool TryExecute(ItemData itemData, out string message)
        {
            if (m_CombatPhase == null || m_CombatPhase.IsCombatEnded)
            {
                message = "유효하지 않은 전투 상태입니다.";
                return false;
            }

            return m_CombatPhase.TryApplyMeteorEffect(m_TargetPosition, out message);
        }
    }

    public sealed class CombatMercenaryUseExecutor : IItemPurchaseUseExecutor
    {
        private readonly CombatPhase m_CombatPhase;
        private readonly UnityEngine.Vector3 m_TargetPosition;

        public CombatMercenaryUseExecutor(CombatPhase combatPhase, UnityEngine.Vector3 targetPosition)
        {
            m_CombatPhase = combatPhase;
            m_TargetPosition = targetPosition;
        }

        public bool TryExecute(ItemData itemData, out string message)
        {
            if (m_CombatPhase == null || m_CombatPhase.IsCombatEnded)
            {
                message = "유효하지 않은 전투 상태입니다.";
                return false;
            }

            return m_CombatPhase.TrySpawnMercenaries(m_TargetPosition, out message);
        }
    }
}
#endif
