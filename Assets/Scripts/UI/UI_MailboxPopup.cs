using System.Collections.Generic;
using InTheArena.Mail;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    /// <summary>
    /// 우편 목록, 개별 수령과 실패한 퀘스트 발송 재시도를 제공하는 팝업입니다.
    /// </summary>
    public sealed class UI_MailboxPopup : UI_Base
    {
        [SerializeField] private RectTransform m_ContentRoot;
        [SerializeField] private MailListRowView m_RowPrefab;
        [SerializeField] private TMP_Text m_EmptyText;
        [SerializeField] private TMP_Text m_PendingText;
        [SerializeField] private Button m_RetryPendingButton;
        [SerializeField] private Button m_CloseButton;

        private readonly List<MailListRowView> m_Rows = new List<MailListRowView>();

        /// <summary>
        /// 재시도와 닫기 버튼 이벤트를 등록합니다.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            if (m_RetryPendingButton != null)
            {
                m_RetryPendingButton.onClick.AddListener(OnRetryPendingClicked);
            }

            if (m_CloseButton != null)
            {
                m_CloseButton.onClick.AddListener(OnCloseClicked);
            }
        }

        /// <summary>
        /// 우편함이 열릴 때 최신 저장 상태로 행을 다시 만듭니다.
        /// </summary>
        public override void OnOpened()
        {
            base.OnOpened();
            Rebuild();
        }

        /// <summary>
        /// 최신 우편과 미발송 완료 요청 영역을 다시 표시합니다.
        /// </summary>
        public void Rebuild()
        {
            ClearRows();
            IReadOnlyList<MailView> mails = null;
            if (EventManager.Instance != null && EventManager.Instance.Mailbox != null)
            {
                mails = EventManager.Instance.Mailbox.GetMails();
            }

            int count = 0;
            if (mails != null)
            {
                count = mails.Count;
            }

            for (int i = 0; i < count; i++)
            {
                MailListRowView row = Instantiate(m_RowPrefab, m_ContentRoot);
                row.gameObject.SetActive(true);
                row.Bind(this, mails[i]);
                m_Rows.Add(row);
            }

            if (m_EmptyText != null)
            {
                m_EmptyText.gameObject.SetActive(count == 0);
            }

            int pendingCount = 0;
            if (EventManager.Instance != null && EventManager.Instance.Quests != null)
            {
                pendingCount = EventManager.Instance.Quests.PendingDeliveryCount;
            }

            if (m_PendingText != null)
            {
                m_PendingText.text = $"미발송 완료 보상: {pendingCount}";
            }

            if (m_RetryPendingButton != null)
            {
                m_RetryPendingButton.interactable = pendingCount > 0;
            }
        }

        /// <summary>
        /// 선택한 우편의 모든 첨부를 한 저장으로 수령하고 목록을 갱신합니다.
        /// </summary>
        public void Claim(string mailId)
        {
            if (EventManager.Instance == null || EventManager.Instance.Mailbox == null)
            {
                return;
            }

            MailClaimResult result = EventManager.Instance.Mailbox.TryClaim(mailId);
            if (result != MailClaimResult.Claimed && m_PendingText != null)
            {
                m_PendingText.text = $"수령 실패: {result}";
            }
            Rebuild();
        }

        /// <summary>
        /// 저장에 실패했던 완료 보상 발송을 수동으로 다시 요청합니다.
        /// </summary>
        private void OnRetryPendingClicked()
        {
            if (EventManager.Instance != null && EventManager.Instance.Quests != null)
            {
                EventManager.Instance.Quests.RetryPendingDeliveries(true);
            }

            Rebuild();
        }

        /// <summary>
        /// UIManager를 통해 우편함 팝업을 닫습니다.
        /// </summary>
        private void OnCloseClicked()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.CloseControl(this);
            }
        }

        /// <summary>
        /// 다시 만들기 전에 기존 우편 행 오브젝트를 정리합니다.
        /// </summary>
        private void ClearRows()
        {
            for (int i = 0; i < m_Rows.Count; i++)
            {
                if (m_Rows[i] != null)
                {
                    Destroy(m_Rows[i].gameObject);
                }
            }

            m_Rows.Clear();
        }

        /// <summary>
        /// 프리팹 파괴 시 버튼 이벤트 구독을 정리합니다.
        /// </summary>
        protected override void OnDestroy()
        {
            if (m_RetryPendingButton != null)
            {
                m_RetryPendingButton.onClick.RemoveListener(OnRetryPendingClicked);
            }

            if (m_CloseButton != null)
            {
                m_CloseButton.onClick.RemoveListener(OnCloseClicked);
            }

            base.OnDestroy();
        }
    }
}
