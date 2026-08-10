#if UNITY_6000_0_OR_NEWER
using System;
using System.Threading;
using UnityEngine;

namespace InTheArena.MainGame
{
    public enum ItemPurchaseUseState
    {
        Idle,
        ConfirmingPurchase,
        AwaitingTarget,
        Committing
    }

    public enum ItemPurchaseUseMode
    {
        Immediate,
        Targeted
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
        UseSucceeded
    }

    public interface IItemPurchaseConfirmationView
    {
        Awaitable ShowAsync(
            ItemData itemData,
            int currentGold,
            CancellationToken token);

        ItemPurchaseDecision LastDecision { get; }

        void Cancel();
    }

    /// <summary>
    /// 팝업·타기팅 입력 순서와 요청 수명만 관리하는 비전역 Coordinator입니다.
    /// 골드와 효과 변경은 ItemPurchaseUseService에 위임합니다.
    /// </summary>
    public sealed class ItemPurchaseUseCoordinator : IDisposable
    {
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
        private bool m_RequiresPurchase;
        private long m_RequestVersion;
        private long m_ActiveRequestVersion;
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
            requestVersion = 0;

            if (m_IsDisposed || m_State != ItemPurchaseUseState.Idle)
            {
                return false;
            }

            SaveManager saveManager = SaveManager.Instance;
            if (itemData == null || itemData.ItemType == ItemType.None || m_Context.CurrentRound <= 0 ||
                saveManager == null)
            {
                return false;
            }

            m_RequestVersion++;
            m_ActiveRequestVersion = m_RequestVersion;
            m_ActiveItem = itemData;
            m_Mode = mode;
            m_ObservedGold = saveManager.Gold;
            m_ObservedRound = m_Context.CurrentRound;
            m_RequiresPurchase = saveManager.GetItemCount(itemData.ItemType) <= 0;
            m_State = ItemPurchaseUseState.ConfirmingPurchase;
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

            if (m_RequiresPurchase && popup == null)
            {
                FinishRequest(requestVersion);
                return;
            }

            m_ActiveCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            RegisterRequestCancellation(token);

            if (!IsCurrentRequest(requestVersion))
            {
                return;
            }

            try
            {
                if (!await TryPurchaseIfNeededAsync(itemData, popup, requestVersion))
                {
                    FinishRequest(requestVersion);
                    return;
                }

                m_State = ItemPurchaseUseState.Committing;
                bool success = m_Service.TryPreview(
                    itemData,
                    executor,
                    m_ObservedGold,
                    out _);

                m_LastResult = success
                    ? ItemPurchaseUseResult.PreviewSucceeded
                    : ItemPurchaseUseResult.Failed;

                FinishRequest(requestVersion);
                return;
            }
            catch (OperationCanceledException)
            {
                FinishRequest(requestVersion);
                m_LastResult = ItemPurchaseUseResult.Cancelled;
                return;
            }
            catch (Exception)
            {
                FinishRequest(requestVersion);
                m_LastResult = ItemPurchaseUseResult.Failed;
                return;
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

            if (m_RequiresPurchase && popup == null)
            {
                FinishRequest(requestVersion);
                return;
            }

            m_ActiveCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            RegisterRequestCancellation(token);

            if (!IsCurrentRequest(requestVersion))
            {
                return;
            }

            try
            {
                if (!await TryPurchaseIfNeededAsync(itemData, popup, requestVersion))
                {
                    FinishRequest(requestVersion);
                    return;
                }

                m_State = ItemPurchaseUseState.Committing;
                bool success = m_Service.TryUse(
                    itemData,
                    executor,
                    m_ObservedGold,
                    out _);

                m_LastResult = success
                    ? ItemPurchaseUseResult.UseSucceeded
                    : ItemPurchaseUseResult.Failed;

                FinishRequest(requestVersion);
            }
            catch (OperationCanceledException)
            {
                FinishRequest(requestVersion);
                m_LastResult = ItemPurchaseUseResult.Cancelled;
            }
            catch (Exception)
            {
                FinishRequest(requestVersion);
                m_LastResult = ItemPurchaseUseResult.Failed;
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

            if (m_RequiresPurchase && popup == null)
            {
                FinishRequest(requestVersion);
                return;
            }

            m_ActiveCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            RegisterRequestCancellation(token);

            if (!IsCurrentRequest(requestVersion))
            {
                return;
            }

            try
            {
                if (!await TryPurchaseIfNeededAsync(itemData, popup, requestVersion))
                {
                    FinishRequest(requestVersion);
                    return;
                }

                m_State = ItemPurchaseUseState.AwaitingTarget;
                m_LastResult = ItemPurchaseUseResult.AwaitingTarget;
            }
            catch (OperationCanceledException)
            {
                FinishRequest(requestVersion);
                m_LastResult = ItemPurchaseUseResult.Cancelled;
            }
            catch (Exception)
            {
                FinishRequest(requestVersion);
                m_LastResult = ItemPurchaseUseResult.Failed;
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

            if (m_RequiresPurchase && popup == null)
            {
                FinishRequest(requestVersion);
                return -1;
            }

            m_ActiveCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            RegisterRequestCancellation(token);

            if (!IsCurrentRequest(requestVersion))
            {
                return -1;
            }

            try
            {
                if (!await TryPurchaseIfNeededAsync(itemData, popup, requestVersion))
                {
                    FinishRequest(requestVersion);
                    return -1;
                }

                m_State = ItemPurchaseUseState.AwaitingTarget;
                m_LastResult = ItemPurchaseUseResult.AwaitingTarget;
                return requestVersion;
            }
            catch (OperationCanceledException)
            {
                FinishRequest(requestVersion);
                m_LastResult = ItemPurchaseUseResult.Cancelled;
                return -1;
            }
            catch (Exception)
            {
                FinishRequest(requestVersion);
                m_LastResult = ItemPurchaseUseResult.Failed;
                return -1;
            }
        }

        private async Awaitable<bool> TryPurchaseIfNeededAsync(
            ItemData itemData,
            IItemPurchaseConfirmationView popup,
            long requestVersion)
        {
            if (!m_RequiresPurchase)
            {
                return true;
            }

            m_ActivePopup = popup;
            await popup.ShowAsync(
                itemData,
                m_ObservedGold,
                m_ActiveCancellationSource.Token);

            if (!IsActiveRequest(requestVersion))
            {
                m_LastResult = ItemPurchaseUseResult.Cancelled;
                return false;
            }

            m_ActivePopup = null;
            if (popup.LastDecision != ItemPurchaseDecision.Confirmed)
            {
                m_LastResult = ItemPurchaseUseResult.Cancelled;
                return false;
            }

            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null ||
                !saveManager.TryPurchaseItem(itemData.ItemType, itemData.PriceGold, out _))
            {
                m_LastResult = ItemPurchaseUseResult.Failed;
                return false;
            }

            m_PlayerState.Gold = saveManager.Gold;
            m_ObservedGold = saveManager.Gold;
            m_RequiresPurchase = false;
            return true;
        }

        private bool TryBeginTargetCommit(long requestVersion)
        {
            if (!IsActiveRequest(requestVersion) ||
                m_Mode != ItemPurchaseUseMode.Targeted ||
                m_State != ItemPurchaseUseState.AwaitingTarget)
            {
                return false;
            }

            m_State = ItemPurchaseUseState.Committing;
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

            bool success = m_Service.TryPreview(
                m_ActiveItem,
                executor,
                m_ObservedGold,
                out message);

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

            bool success = m_Service.TryUse(
                m_ActiveItem,
                executor,
                m_ObservedGold,
                out message);

            result = success
                ? ItemPurchaseUseResult.UseSucceeded
                : ItemPurchaseUseResult.Failed;

            m_LastResult = result;

            FinishRequest(requestVersion);
            return success;
        }

        public void CancelActiveRequest()
        {
            if (m_IsDisposed)
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
            m_RequiresPurchase = false;
            m_State = ItemPurchaseUseState.Idle;
        }

        private void RegisterRequestCancellation(CancellationToken token)
        {
            m_ActiveRequestCancellationRegistration = token.Register(
                static state => ((ItemPurchaseUseCoordinator)state).CancelActiveRequest(),
                this);
            m_HasActiveRequestCancellationRegistration = true;
        }
    }
}
#endif
