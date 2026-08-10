#if UNITY_6000_0_OR_NEWER
using System;
using System.Threading;
using UnityEngine;

namespace InTheArena.MainGame
{
    public enum ItemPurchaseUseState
    {
        Idle,
        Preparing,
        ConfirmingPurchase,
        ConfirmingUse,
        AwaitingTarget,
        CommittingPurchase,
        CommittingUse
    }

    public enum ItemPurchaseUseMode
    {
        Immediate,
        Targeted
    }

    public enum ItemConfirmationMode
    {
        Purchase,
        Use
    }

    public enum ItemPurchaseDecision
    {
        Confirmed,
        Cancelled
    }

    public enum ItemPurchaseUseResult
    {
        Rejected,
        Cancelled,
        Failed,
        AwaitingTarget,
        PreviewSucceeded,
        PurchaseSucceeded,
        UseSucceeded
    }

    public interface IItemPurchaseConfirmationView
    {
        Awaitable ShowAsync(
            ItemData itemData,
            ItemConfirmationMode mode,
            int currentGold,
            int ownedCount,
            CancellationToken token);

        ItemPurchaseDecision LastDecision { get; }

        void Cancel();
    }

    /// <summary>
    /// 팝업·타기팅 입력 순서와 요청 수명만 관리하는 비전역 Coordinator입니다.
    /// 구매는 이 클래스에서 완료하고, 사용 효과와 아이템 차감은 Service에 위임합니다.
    /// </summary>
    public sealed class ItemPurchaseUseCoordinator : IDisposable
    {
        private enum ItemRequestPreparationResult
        {
            ReadyForUse,
            PurchaseSucceeded,
            Cancelled,
            Rejected,
            Failed
        }

        private readonly RoundContext m_Context;
        private readonly StagePlayerState m_PlayerState;
        private readonly ItemPurchaseUseService m_Service;

        private ItemPurchaseUseState m_State = ItemPurchaseUseState.Idle;
        private ItemPurchaseUseMode m_Mode;
        private ItemData m_ActiveItem;
        private IItemPurchaseConfirmationView m_ActivePopup;
        private CancellationTokenSource m_ActiveCancellationSource;
        private CancellationTokenRegistration m_ActiveRequestCancellationRegistration;
        private bool m_HasActiveRequestCancellationRegistration;
        private int m_ObservedGold;
        private int m_ObservedRound;
        private long m_RequestVersion;
        private long m_ActiveRequestVersion = -1;
        private bool m_IsDisposed;
        private ItemPurchaseUseResult m_LastResult = ItemPurchaseUseResult.Rejected;

        public ItemPurchaseUseCoordinator(
            RoundContext context,
            StagePlayerState playerState,
            ItemPurchaseUseService service)
        {
            m_Context = context ?? throw new ArgumentNullException(nameof(context));
            m_PlayerState = playerState ?? throw new ArgumentNullException(nameof(playerState));
            m_Service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public ItemPurchaseUseState State => m_State;
        public long ActiveRequestVersion => m_ActiveRequestVersion;
        public bool IsDisposed => m_IsDisposed;
        public ItemPurchaseUseResult LastResult => m_LastResult;

        private bool TryBeginRequest(
            ItemData itemData,
            ItemPurchaseUseMode mode,
            out long requestVersion)
        {
            requestVersion = -1;

            if (m_IsDisposed || m_State != ItemPurchaseUseState.Idle)
            {
                return false;
            }

            SaveManager saveManager = SaveManager.Instance;
            if (itemData == null || itemData.ItemType == ItemType.None ||
                m_Context.CurrentRound <= 0 || saveManager == null)
            {
                return false;
            }

            m_RequestVersion++;
            m_ActiveRequestVersion = m_RequestVersion;
            m_ActiveItem = itemData;
            m_Mode = mode;
            m_ObservedGold = saveManager.Gold;
            m_ObservedRound = m_Context.CurrentRound;
            m_State = ItemPurchaseUseState.Preparing;
            requestVersion = m_ActiveRequestVersion;
            return true;
        }

        public async Awaitable RequestImmediatePreviewAsync(
            ItemData itemData,
            IItemPurchaseConfirmationView popup,
            IItemPurchaseUseExecutor executor,
            CancellationToken token)
        {
            m_LastResult = ItemPurchaseUseResult.Rejected;

            if (!TryBeginRequest(itemData, ItemPurchaseUseMode.Immediate, out long requestVersion))
            {
                return;
            }

            BeginCancellation(token);
            if (!IsCurrentRequest(requestVersion))
            {
                return;
            }

            try
            {
                ItemRequestPreparationResult preparation = await PrepareActionAsync(
                    itemData,
                    popup,
                    requestVersion);

                if (preparation != ItemRequestPreparationResult.ReadyForUse ||
                    !IsActiveRequest(requestVersion))
                {
                    return;
                }

                m_State = ItemPurchaseUseState.CommittingUse;
                bool success = m_Service.TryPreview(itemData, executor, out _);
                m_LastResult = success
                    ? ItemPurchaseUseResult.PreviewSucceeded
                    : ItemPurchaseUseResult.Failed;
                FinishRequest(requestVersion);
            }
            catch (OperationCanceledException)
            {
                CancelCurrentRequest(requestVersion);
            }
            catch (Exception)
            {
                FailCurrentRequest(requestVersion);
            }
        }

        public async Awaitable RequestImmediateUseAsync(
            ItemData itemData,
            IItemPurchaseConfirmationView popup,
            IItemPurchaseUseExecutor executor,
            CancellationToken token)
        {
            m_LastResult = ItemPurchaseUseResult.Rejected;

            if (!TryBeginRequest(itemData, ItemPurchaseUseMode.Immediate, out long requestVersion))
            {
                return;
            }

            BeginCancellation(token);
            if (!IsCurrentRequest(requestVersion))
            {
                return;
            }

            try
            {
                ItemRequestPreparationResult preparation = await PrepareActionAsync(
                    itemData,
                    popup,
                    requestVersion);

                if (preparation != ItemRequestPreparationResult.ReadyForUse ||
                    !IsActiveRequest(requestVersion))
                {
                    return;
                }

                m_State = ItemPurchaseUseState.CommittingUse;
                bool success = m_Service.TryUse(itemData, executor, out _);
                m_LastResult = success
                    ? ItemPurchaseUseResult.UseSucceeded
                    : ItemPurchaseUseResult.Failed;
                FinishRequest(requestVersion);
            }
            catch (OperationCanceledException)
            {
                CancelCurrentRequest(requestVersion);
            }
            catch (Exception)
            {
                FailCurrentRequest(requestVersion);
            }
        }

        public async Awaitable RequestTargetedPreviewAsync(
            ItemData itemData,
            IItemPurchaseConfirmationView popup,
            CancellationToken token)
        {
            m_LastResult = ItemPurchaseUseResult.Rejected;

            if (!TryBeginRequest(itemData, ItemPurchaseUseMode.Targeted, out long requestVersion))
            {
                return;
            }

            BeginCancellation(token);
            if (!IsCurrentRequest(requestVersion))
            {
                return;
            }

            try
            {
                ItemRequestPreparationResult preparation = await PrepareActionAsync(
                    itemData,
                    popup,
                    requestVersion);

                if (preparation != ItemRequestPreparationResult.ReadyForUse ||
                    !IsActiveRequest(requestVersion))
                {
                    return;
                }

                m_State = ItemPurchaseUseState.AwaitingTarget;
                m_LastResult = ItemPurchaseUseResult.AwaitingTarget;
            }
            catch (OperationCanceledException)
            {
                CancelCurrentRequest(requestVersion);
            }
            catch (Exception)
            {
                FailCurrentRequest(requestVersion);
            }
        }

        public async Awaitable<long> RequestTargetedUseAsync(
            ItemData itemData,
            IItemPurchaseConfirmationView popup,
            CancellationToken token)
        {
            m_LastResult = ItemPurchaseUseResult.Rejected;

            if (!TryBeginRequest(itemData, ItemPurchaseUseMode.Targeted, out long requestVersion))
            {
                return -1;
            }

            BeginCancellation(token);
            if (!IsCurrentRequest(requestVersion))
            {
                return -1;
            }

            try
            {
                ItemRequestPreparationResult preparation = await PrepareActionAsync(
                    itemData,
                    popup,
                    requestVersion);

                if (preparation != ItemRequestPreparationResult.ReadyForUse ||
                    !IsActiveRequest(requestVersion))
                {
                    return -1;
                }

                m_State = ItemPurchaseUseState.AwaitingTarget;
                m_LastResult = ItemPurchaseUseResult.AwaitingTarget;
                return requestVersion;
            }
            catch (OperationCanceledException)
            {
                CancelCurrentRequest(requestVersion);
                return -1;
            }
            catch (Exception)
            {
                FailCurrentRequest(requestVersion);
                return -1;
            }
        }

        private void BeginCancellation(CancellationToken token)
        {
            m_ActiveCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            m_ActiveRequestCancellationRegistration = token.Register(
                static state => ((ItemPurchaseUseCoordinator)state).CancelActiveRequest(),
                this);
            m_HasActiveRequestCancellationRegistration = true;
        }

        private async Awaitable<ItemRequestPreparationResult> PrepareActionAsync(
            ItemData itemData,
            IItemPurchaseConfirmationView popup,
            long requestVersion)
        {
            if (!IsActiveRequest(requestVersion))
            {
                return HandleInactiveRequest(requestVersion);
            }

            SaveManager saveManager = SaveManager.Instance;
            int ownedCount = saveManager?.GetItemCount(itemData.ItemType) ?? 0;
            if (ownedCount <= 0)
            {
                if (popup == null)
                {
                    return FinishPreparation(
                        requestVersion,
                        ItemRequestPreparationResult.Failed,
                        ItemPurchaseUseResult.Failed);
                }

                m_State = ItemPurchaseUseState.ConfirmingPurchase;
                bool confirmed = await ConfirmActionAsync(
                    itemData,
                    ItemConfirmationMode.Purchase,
                    ownedCount,
                    popup,
                    requestVersion);

                if (!IsActiveRequest(requestVersion))
                {
                    return HandleInactiveRequest(requestVersion);
                }

                if (!confirmed)
                {
                    return FinishPreparation(
                        requestVersion,
                        ItemRequestPreparationResult.Cancelled,
                        ItemPurchaseUseResult.Cancelled);
                }

                m_State = ItemPurchaseUseState.CommittingPurchase;
                if (!TryCommitPurchase(itemData, out _))
                {
                    return FinishPreparation(
                        requestVersion,
                        ItemRequestPreparationResult.Failed,
                        ItemPurchaseUseResult.Failed);
                }

                return FinishPreparation(
                    requestVersion,
                    ItemRequestPreparationResult.PurchaseSucceeded,
                    ItemPurchaseUseResult.PurchaseSucceeded);
            }

            if (m_Context.RoundItemUsage.HasUsed(itemData.ItemType))
            {
                return FinishPreparation(
                    requestVersion,
                    ItemRequestPreparationResult.Rejected,
                    ItemPurchaseUseResult.Rejected);
            }

            if (popup == null)
            {
                return FinishPreparation(
                    requestVersion,
                    ItemRequestPreparationResult.Failed,
                    ItemPurchaseUseResult.Failed);
            }

            m_State = ItemPurchaseUseState.ConfirmingUse;
            bool useConfirmed = await ConfirmActionAsync(
                itemData,
                ItemConfirmationMode.Use,
                ownedCount,
                popup,
                requestVersion);

            if (!IsActiveRequest(requestVersion))
            {
                return HandleInactiveRequest(requestVersion);
            }

            if (!useConfirmed)
            {
                return FinishPreparation(
                    requestVersion,
                    ItemRequestPreparationResult.Cancelled,
                    ItemPurchaseUseResult.Cancelled);
            }

            return ItemRequestPreparationResult.ReadyForUse;
        }

        private async Awaitable<bool> ConfirmActionAsync(
            ItemData itemData,
            ItemConfirmationMode mode,
            int ownedCount,
            IItemPurchaseConfirmationView popup,
            long requestVersion)
        {
            if (!IsActiveRequest(requestVersion))
            {
                return false;
            }

            m_ActivePopup = popup;
            try
            {
                await popup.ShowAsync(
                    itemData,
                    mode,
                    m_ObservedGold,
                    ownedCount,
                    m_ActiveCancellationSource.Token);
            }
            finally
            {
                if (ReferenceEquals(m_ActivePopup, popup))
                {
                    m_ActivePopup = null;
                }
            }

            return popup.LastDecision == ItemPurchaseDecision.Confirmed;
        }

        private bool TryCommitPurchase(ItemData itemData, out string message)
        {
            message = string.Empty;
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null ||
                !saveManager.TryPurchaseItem(itemData.ItemType, itemData.PriceGold, out message))
            {
                return false;
            }

            m_PlayerState.Gold = saveManager.Gold;
            return true;
        }

        private bool TryBeginTargetCommit(long requestVersion)
        {
            if (!IsCurrentRequest(requestVersion) ||
                m_Mode != ItemPurchaseUseMode.Targeted ||
                m_State != ItemPurchaseUseState.AwaitingTarget)
            {
                return false;
            }

            if (m_Context.CurrentRound != m_ObservedRound)
            {
                CancelActiveRequest();
                return false;
            }

            m_State = ItemPurchaseUseState.CommittingUse;
            return true;
        }

        public bool TryCompleteTargetPreview(
            long requestVersion,
            IItemPurchaseUseExecutor executor,
            out ItemPurchaseUseResult result,
            out string message)
        {
            result = ItemPurchaseUseResult.Rejected;
            message = string.Empty;

            if (!TryBeginTargetCommit(requestVersion))
            {
                return false;
            }

            bool success = m_Service.TryPreview(m_ActiveItem, executor, out message);
            result = success
                ? ItemPurchaseUseResult.PreviewSucceeded
                : ItemPurchaseUseResult.Failed;
            m_LastResult = result;
            FinishRequest(requestVersion);
            return success;
        }

        public bool TryCompleteTargetUse(
            long requestVersion,
            IItemPurchaseUseExecutor executor,
            out ItemPurchaseUseResult result,
            out string message)
        {
            result = ItemPurchaseUseResult.Rejected;
            message = string.Empty;

            if (!TryBeginTargetCommit(requestVersion))
            {
                return false;
            }

            bool success = m_Service.TryUse(m_ActiveItem, executor, out message);
            result = success
                ? ItemPurchaseUseResult.UseSucceeded
                : ItemPurchaseUseResult.Failed;
            m_LastResult = result;
            FinishRequest(requestVersion);
            return success;
        }

        public void CancelActiveRequest()
        {
            if (m_IsDisposed || m_State == ItemPurchaseUseState.Idle)
            {
                return;
            }

            m_RequestVersion++;
            m_ActiveRequestVersion = m_RequestVersion;
            m_LastResult = ItemPurchaseUseResult.Cancelled;
            m_ActiveCancellationSource?.Cancel();
            m_ActivePopup?.Cancel();
            ClearActiveRequest();
        }

        public void Dispose()
        {
            if (m_IsDisposed)
            {
                return;
            }

            CancelActiveRequest();
            m_IsDisposed = true;
        }

        private ItemRequestPreparationResult HandleInactiveRequest(long requestVersion)
        {
            if (IsCurrentRequest(requestVersion))
            {
                m_LastResult = ItemPurchaseUseResult.Cancelled;
                FinishRequest(requestVersion);
            }

            return ItemRequestPreparationResult.Cancelled;
        }

        private ItemRequestPreparationResult FinishPreparation(
            long requestVersion,
            ItemRequestPreparationResult preparation,
            ItemPurchaseUseResult result)
        {
            if (IsCurrentRequest(requestVersion))
            {
                m_LastResult = result;
                FinishRequest(requestVersion);
            }

            return preparation;
        }

        private void CancelCurrentRequest(long requestVersion)
        {
            if (IsCurrentRequest(requestVersion))
            {
                m_LastResult = ItemPurchaseUseResult.Cancelled;
                FinishRequest(requestVersion);
            }
        }

        private void FailCurrentRequest(long requestVersion)
        {
            if (IsCurrentRequest(requestVersion))
            {
                m_LastResult = ItemPurchaseUseResult.Failed;
                FinishRequest(requestVersion);
            }
        }

        private bool IsActiveRequest(long requestVersion)
        {
            return IsCurrentRequest(requestVersion) &&
                   m_Context.CurrentRound == m_ObservedRound;
        }

        private bool IsCurrentRequest(long requestVersion)
        {
            return !m_IsDisposed &&
                   m_State != ItemPurchaseUseState.Idle &&
                   requestVersion == m_ActiveRequestVersion;
        }

        private void FinishRequest(long requestVersion)
        {
            if (!IsCurrentRequest(requestVersion))
            {
                return;
            }

            m_RequestVersion++;
            m_ActiveRequestVersion = m_RequestVersion;
            ClearActiveRequest();
        }

        private void ClearActiveRequest()
        {
            if (m_HasActiveRequestCancellationRegistration)
            {
                m_ActiveRequestCancellationRegistration.Dispose();
                m_HasActiveRequestCancellationRegistration = false;
            }

            m_ActiveCancellationSource?.Dispose();
            m_ActiveCancellationSource = null;
            m_ActivePopup = null;
            m_ActiveItem = null;
            m_ObservedGold = 0;
            m_ObservedRound = 0;
            m_State = ItemPurchaseUseState.Idle;
        }
    }
}
#endif
