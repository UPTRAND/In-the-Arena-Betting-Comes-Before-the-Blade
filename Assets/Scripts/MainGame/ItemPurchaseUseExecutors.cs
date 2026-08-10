#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;

namespace InTheArena.MainGame
{
    public sealed class BettingItemUseExecutor : IReversibleItemPurchaseUseExecutor
    {
        private readonly BettingPhase m_BettingPhase;
        private List<SpecialBetType> m_PreviousSpecialBetOrder;
        private BettingDraftSpecialPredictionState m_PreviousDraftSpecialPredictions;
        private bool m_HasSnapshot;

        public BettingItemUseExecutor(BettingPhase bettingPhase)
        {
            m_BettingPhase = bettingPhase;
        }

        public bool CanExecute(ItemData itemData, out string message)
        {
            if (m_BettingPhase == null)
            {
                message = "베팅 페이즈가 없습니다.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        public bool TryExecute(ItemData itemData, out string message)
        {
            message = string.Empty;
            if (m_BettingPhase == null)
            {
                message = "베팅 페이즈가 없습니다.";
                return false;
            }

            IReadOnlyList<SpecialBetType> specialBetOrder = m_BettingPhase.GetSpecialBetOrderForItemUse();
            m_PreviousSpecialBetOrder = specialBetOrder != null
                ? new List<SpecialBetType>(specialBetOrder)
                : new List<SpecialBetType>();
            m_PreviousDraftSpecialPredictions = m_BettingPhase.GetDraftSpecialPredictionsForItemUse();
            m_HasSnapshot = true;
            return m_BettingPhase.TryApplyPurchasedItemEffect(itemData, out message);
        }

        public void Rollback(ItemData itemData)
        {
            if (!m_HasSnapshot || m_BettingPhase == null)
            {
                return;
            }

            m_BettingPhase.RestorePurchasedItemState(
                m_PreviousSpecialBetOrder,
                m_PreviousDraftSpecialPredictions);
            m_PreviousDraftSpecialPredictions = null;
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

        public bool CanExecute(ItemData itemData, out string message)
        {
            if (m_CombatPhase == null)
            {
                message = "전투 페이즈가 없습니다.";
                return false;
            }

            message = string.Empty;
            return true;
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

        public bool CanExecute(ItemData itemData, out string message)
        {
            if (m_CombatPhase == null || m_CombatPhase.IsCombatEnded ||
                !m_CombatPhase.CanCommitGroundTargetItem())
            {
                message = "유효하지 않은 전투 상태입니다.";
                return false;
            }

            message = string.Empty;
            return true;
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

        public bool CanExecute(ItemData itemData, out string message)
        {
            if (m_CombatPhase == null || m_CombatPhase.IsCombatEnded ||
                !m_CombatPhase.CanCommitGroundTargetItem())
            {
                message = "유효하지 않은 전투 상태입니다.";
                return false;
            }

            message = string.Empty;
            return true;
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
