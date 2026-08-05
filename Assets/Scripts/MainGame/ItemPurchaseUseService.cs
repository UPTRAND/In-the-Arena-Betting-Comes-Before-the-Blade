#if UNITY_6000_0_OR_NEWER
using System;

namespace InTheArena.MainGame
{
    /// <summary>
    /// Phase 1-A에서 효과 실행을 대체하는 테스트용 실행 경계입니다.
    /// 실제 아이템 효과와 골드/사용 기록 커밋은 후속 단계에서 연결합니다.
    /// </summary>
    public interface IItemPurchaseUseExecutor
    {
        bool TryExecute(ItemData itemData, out string message);
    }

    public interface IReversibleItemPurchaseUseExecutor : IItemPurchaseUseExecutor
    {
        void Rollback(ItemData itemData);
    }

    /// <summary>
    /// 구매·사용 거래의 검증과 실행 요청을 담당합니다.
    /// Phase 1-A에서는 실제 골드 차감과 사용 완료 기록을 수행하지 않습니다.
    /// </summary>
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

            if (itemData == null)
            {
                message = "유효하지 않은 아이템입니다.";
                return false;
            }

            if (itemData.ItemType == ItemType.None)
            {
                message = "아이템 종류가 설정되지 않았습니다.";
                return false;
            }

            if (m_Context == null || m_Context.CurrentRound <= 0)
            {
                message = "활성 라운드가 없습니다.";
                return false;
            }

            if (m_PlayerState == null)
            {
                message = "스테이지 플레이어 상태가 없습니다.";
                return false;
            }

            if (itemData.PriceGold < 0)
            {
                message = "아이템 가격이 잘못되었습니다.";
                return false;
            }

            if (observedGold != m_PlayerState.Gold)
            {
                message = "골드 정보가 변경되었습니다.";
                return false;
            }

            if (m_PlayerState.Gold < itemData.PriceGold)
            {
                message = "골드가 부족합니다.";
                return false;
            }

            if (m_Context.RoundItemUsage.HasUsed(itemData.ItemType))
            {
                message = "이번 라운드에 이미 사용한 아이템입니다.";
                return false;
            }

            return true;
        }

        public bool TryPreview(
            ItemData itemData,
            IItemPurchaseUseExecutor executor,
            int observedGold,
            out string message)
        {
            if (!TryValidate(itemData, observedGold, out message))
            {
                return false;
            }

            if (executor == null)
            {
                message = "아이템 실행기가 없습니다.";
                return false;
            }

            try
            {
                return executor.TryExecute(itemData, out message);
            }
            catch (Exception exception)
            {
                message = $"아이템 실행 중 오류가 발생했습니다: {exception.Message}";
                return false;
            }
        }

        public bool TryUse(
            ItemData itemData,
            IItemPurchaseUseExecutor executor,
            int observedGold,
            out string message)
        {
            message = string.Empty;

            if (!TryValidate(itemData, observedGold, out message))
            {
                return false;
            }

            if (executor == null)
            {
                message = "아이템 실행기가 없습니다.";
                return false;
            }

            if (!m_Context.RoundItemUsage.TryMarkUsed(itemData.ItemType))
            {
                message = "이번 라운드에 이미 사용한 아이템입니다.";
                return false;
            }

            int originalGold = m_PlayerState.Gold;
            m_PlayerState.Gold = originalGold - itemData.PriceGold;

            bool success;
            try
            {
                success = executor.TryExecute(itemData, out message);
            }
            catch (Exception exception)
            {
                success = false;
                message = $"아이템 실행 중 오류가 발생했습니다: {exception.Message}";
            }

            if (success)
            {
                if (string.IsNullOrEmpty(message))
                {
                    message = "아이템을 사용했습니다.";
                }

                return true;
            }

            if (executor is IReversibleItemPurchaseUseExecutor reversibleExecutor)
            {
                try
                {
                    reversibleExecutor.Rollback(itemData);
                }
                catch (Exception exception)
                {
                    message = $"아이템 실패 복구 중 오류가 발생했습니다: {exception.Message}";
                }
            }

            m_Context.RoundItemUsage.TryUnmarkUsed(itemData.ItemType);
            m_PlayerState.Gold = originalGold;
            return false;
        }
    }
}
#endif
