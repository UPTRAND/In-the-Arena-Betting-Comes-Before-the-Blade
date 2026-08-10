#if UNITY_6000_0_OR_NEWER
using System;

namespace InTheArena.MainGame
{
    public interface IItemPurchaseUseExecutor
    {
        bool CanExecute(ItemData itemData, out string message);
        bool TryExecute(ItemData itemData, out string message);
    }

    public interface IReversibleItemPurchaseUseExecutor : IItemPurchaseUseExecutor
    {
        void Rollback(ItemData itemData);
    }

    public sealed class ItemPurchaseUseService
    {
        private readonly RoundContext m_Context;
        private readonly StagePlayerState m_PlayerState;

        public ItemPurchaseUseService(RoundContext context, StagePlayerState playerState)
        {
            m_Context = context;
            m_PlayerState = playerState;
        }

        public bool TryValidateUse(ItemData itemData, out string message)
        {
            message = string.Empty;
            if (itemData == null || itemData.ItemType == ItemType.None)
            {
                message = "Invalid item.";
                return false;
            }

            if (m_Context == null || m_Context.CurrentRound <= 0)
            {
                message = "No active round.";
                return false;
            }

            if (SaveManager.Instance == null ||
                SaveManager.Instance.GetItemCount(itemData.ItemType) <= 0)
            {
                message = "Item is not owned.";
                return false;
            }

            if (m_Context.RoundItemUsage.HasUsed(itemData.ItemType))
            {
                message = "Item was already used this round.";
                return false;
            }

            return true;
        }

        // Compatibility overload for callers that still pass the old observed-gold value.
        public bool TryValidate(ItemData itemData, int observedGold, out string message)
        {
            return TryValidateUse(itemData, out message);
        }

        public bool TryPreview(
            ItemData itemData,
            IItemPurchaseUseExecutor executor,
            out string message)
        {
            if (!TryValidateUse(itemData, out message))
            {
                return false;
            }

            if (executor == null)
            {
                message = "Item executor is unavailable.";
                return false;
            }

            try
            {
                if (!executor.CanExecute(itemData, out message))
                {
                    return false;
                }

                return executor.TryExecute(itemData, out message);
            }
            catch (Exception exception)
            {
                message = exception.Message;
                return false;
            }
        }

        // Compatibility overload for callers that still pass the old observed-gold value.
        public bool TryPreview(
            ItemData itemData,
            IItemPurchaseUseExecutor executor,
            int observedGold,
            out string message)
        {
            return TryPreview(itemData, executor, out message);
        }

        public bool TryUse(
            ItemData itemData,
            IItemPurchaseUseExecutor executor,
            out string message)
        {
            if (!TryValidateUse(itemData, out message))
            {
                return false;
            }

            if (executor == null)
            {
                message = "Item executor is unavailable.";
                return false;
            }

            try
            {
                if (!executor.CanExecute(itemData, out message))
                {
                    return false;
                }
            }
            catch (Exception exception)
            {
                message = exception.Message;
                return false;
            }

            return executor is IReversibleItemPurchaseUseExecutor reversible
                ? TryUseReversible(itemData, reversible, out message)
                : TryUseIrreversible(itemData, executor, out message);
        }

        // Compatibility overload for callers that still pass the old observed-gold value.
        public bool TryUse(
            ItemData itemData,
            IItemPurchaseUseExecutor executor,
            int observedGold,
            out string message)
        {
            return TryUse(itemData, executor, out message);
        }

        private bool TryUseReversible(
            ItemData itemData,
            IReversibleItemPurchaseUseExecutor executor,
            out string message)
        {
            message = string.Empty;
            if (!m_Context.RoundItemUsage.TryMarkUsed(itemData.ItemType))
            {
                message = "Item was already used this round.";
                return false;
            }

            bool succeeded;
            try
            {
                succeeded = executor.TryExecute(itemData, out message);
            }
            catch (Exception exception)
            {
                succeeded = false;
                message = exception.Message;
            }

            string saveError = null;
            bool spent = succeeded && TrySpendItem(itemData.ItemType, out saveError);
            if (spent)
            {
                return true;
            }

            if (succeeded)
            {
                SafeRollback(executor, itemData);
                m_Context.RoundItemUsage.TryUnmarkUsed(itemData.ItemType);
                message = string.IsNullOrEmpty(saveError)
                    ? "Item save failed."
                    : saveError;
                return false;
            }

            SafeRollback(executor, itemData);
            m_Context.RoundItemUsage.TryUnmarkUsed(itemData.ItemType);
            return false;
        }

        private bool TryUseIrreversible(
            ItemData itemData,
            IItemPurchaseUseExecutor executor,
            out string message)
        {
            message = string.Empty;
            if (!m_Context.RoundItemUsage.TryMarkUsed(itemData.ItemType))
            {
                message = "Item was already used this round.";
                return false;
            }

            if (!TrySpendItem(itemData.ItemType, out string saveError))
            {
                m_Context.RoundItemUsage.TryUnmarkUsed(itemData.ItemType);
                message = saveError;
                return false;
            }

            bool succeeded;
            try
            {
                succeeded = executor.TryExecute(itemData, out message);
            }
            catch (Exception exception)
            {
                succeeded = false;
                message = exception.Message;
            }

            if (succeeded)
            {
                return true;
            }

            bool refunded = TryGrantItem(itemData.ItemType, out string refundError);
            if (refunded)
            {
                m_Context.RoundItemUsage.TryUnmarkUsed(itemData.ItemType);
            }
            else
            {
                message = string.IsNullOrEmpty(refundError)
                    ? $"{message} Item refund failed."
                    : $"{message} {refundError}";
            }

            return false;
        }

        private static void SafeRollback(
            IReversibleItemPurchaseUseExecutor executor,
            ItemData itemData)
        {
            try
            {
                executor.Rollback(itemData);
            }
            catch
            {
                // The original failure is the useful message for the caller.
            }
        }

        private static bool TrySpendItem(ItemType itemType, out string error)
        {
            try
            {
                if (SaveManager.Instance == null)
                {
                    error = "Save manager is unavailable.";
                    return false;
                }

                return SaveManager.Instance.TrySpendItem(itemType, out error);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool TryGrantItem(ItemType itemType, out string error)
        {
            try
            {
                if (SaveManager.Instance == null)
                {
                    error = "Save manager is unavailable.";
                    return false;
                }

                return SaveManager.Instance.TryAddItem(itemType, 1, out error);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }
    }
}
#endif
