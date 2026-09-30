using System.Text;
using InTheArena.Mail;
using InTheArena.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    /// <summary>
    /// 우편 한 행의 내용과 개별 수령 입력을 담당합니다.
    /// </summary>
    public sealed class MailListRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text m_TitleText;
        [SerializeField] private TMP_Text m_BodyText;
        [SerializeField] private TMP_Text m_RewardText;
        [SerializeField] private TMP_Text m_ClaimButtonText;
        [SerializeField] private Button m_ClaimButton;

        private UI_MailboxPopup m_Owner;
        private string m_MailId;

        /// <summary>
        /// 개별 수령 버튼 이벤트를 등록합니다.
        /// </summary>
        private void Awake()
        {
            if (m_ClaimButton != null)
            {
                m_ClaimButton.onClick.AddListener(OnClaimClicked);
            }
        }

        /// <summary>
        /// 우편 제목, 본문, 첨부와 수령 상태를 행에 표시합니다.
        /// </summary>
        public void Bind(UI_MailboxPopup owner, MailView mail)
        {
            m_Owner = owner;
            m_MailId = mail.MailId;

            if (m_TitleText != null)
            {
                m_TitleText.text = mail.Title;
            }

            if (m_BodyText != null)
            {
                m_BodyText.text = mail.Body;
            }

            if (m_RewardText != null)
            {
                m_RewardText.text = BuildRewardText(mail);
            }

            if (m_ClaimButton != null)
            {
                m_ClaimButton.interactable = !mail.IsClaimed;
            }

            if (m_ClaimButtonText != null)
            {
                if (mail.IsClaimed)
                {
                    m_ClaimButtonText.text = "수령 완료";
                }
                else
                {
                    m_ClaimButtonText.text = "수령";
                }
            }
        }

        /// <summary>
        /// 연결된 우편함 팝업에 현재 MailId 수령을 요청합니다.
        /// </summary>
        private void OnClaimClicked()
        {
            if (m_Owner != null)
            {
                m_Owner.Claim(m_MailId);
            }
        }

        /// <summary>
        /// 모든 우편 첨부를 한 줄의 사용자 표시 문자열로 조합합니다.
        /// </summary>
        private static string BuildRewardText(MailView mail)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < mail.Rewards.Count; i++)
            {
                RewardEntryPayload reward = mail.Rewards[i];
                if (i > 0)
                {
                    builder.Append(" · ");
                }

                MailRewardKind kind = (MailRewardKind)reward.kind;
                if (kind == MailRewardKind.Gold)
                {
                    builder.Append($"골드 {reward.amount:N0}");
                }
                else if (kind == MailRewardKind.EntranceTicket)
                {
                    builder.Append($"입장권 {reward.amount:N0}");
                }
                else
                {
                    builder.Append($"아이템 {reward.amount:N0}");
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// 행 파괴 시 수령 버튼 이벤트 구독을 정리합니다.
        /// </summary>
        private void OnDestroy()
        {
            if (m_ClaimButton != null)
            {
                m_ClaimButton.onClick.RemoveListener(OnClaimClicked);
            }
        }
    }
}
