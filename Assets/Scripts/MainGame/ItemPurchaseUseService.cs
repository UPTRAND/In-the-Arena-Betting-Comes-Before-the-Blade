#if UNITY_6000_0_OR_NEWER
using System;

namespace InTheArena.MainGame
{
    public interface IItemPurchaseUseExecutor { bool TryExecute(ItemData itemData, out string message); }
    public interface IReversibleItemPurchaseUseExecutor : IItemPurchaseUseExecutor { void Rollback(ItemData itemData); }

    public sealed class ItemPurchaseUseService
    {
        private readonly RoundContext m_Context;
        private readonly StagePlayerState m_PlayerState;

        public ItemPurchaseUseService(RoundContext context, StagePlayerState playerState)
        {
            m_Context = context;
            m_PlayerState = playerState;
        }

        public bool TryValidate(ItemData itemData, int observedGold, out string message)
        {
            message = string.Empty;
            if (itemData == null || itemData.ItemType == ItemType.None) { message = "Invalid item."; return false; }
            if (m_Context == null || m_Context.CurrentRound <= 0) { message = "No active round."; return false; }
            if (SaveManager.Instance == null || SaveManager.Instance.GetItemCount(itemData.ItemType) <= 0) { message = "Item is not owned."; return false; }
            if (m_Context.RoundItemUsage.HasUsed(itemData.ItemType)) { message = "Item was already used this round."; return false; }
            return true;
        }

        public bool TryPreview(ItemData itemData, IItemPurchaseUseExecutor executor, int observedGold, out string message)
        {
            if (!TryValidate(itemData, observedGold, out message) || executor == null) return false;
            try { return executor.TryExecute(itemData, out message); }
            catch (Exception exception) { message = exception.Message; return false; }
        }

        public bool TryUse(ItemData itemData, IItemPurchaseUseExecutor executor, int observedGold, out string message)
        {
            if (!TryValidate(itemData, observedGold, out message) || executor == null) return false;
            if (!m_Context.RoundItemUsage.TryMarkUsed(itemData.ItemType)) { message = "Item was already used this round."; return false; }
            bool succeeded;
            try { succeeded = executor.TryExecute(itemData, out message); }
            catch (Exception exception) { succeeded = false; message = exception.Message; }
            string saveError = null;
            if (succeeded && SaveManager.Instance.TrySpendItem(itemData.ItemType, out saveError)) return true;
            if (executor is IReversibleItemPurchaseUseExecutor reversible) reversible.Rollback(itemData);
            m_Context.RoundItemUsage.TryUnmarkUsed(itemData.ItemType);
            if (succeeded) message = saveError;
            return false;
        }
    }
}
#endif
