using InTheArena.Quests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    /// <summary>
    /// 퀘스트 목록 한 행의 표시와 선택 입력을 담당합니다.
    /// </summary>
    public sealed class QuestListRowView : MonoBehaviour
    {
        [SerializeField] private Button m_Button;
        [SerializeField] private TMP_Text m_TitleText;
        [SerializeField] private TMP_Text m_ProgressText;
        [SerializeField] private TMP_Text m_StatusText;

        private UI_QuestListPopup m_Owner;
        private string m_InstanceId;

        /// <summary>
        /// 행 선택 버튼 이벤트를 등록합니다.
        /// </summary>
        private void Awake()
        {
            if (m_Button != null)
            {
                m_Button.onClick.AddListener(OnClicked);
            }
        }

        /// <summary>
        /// 읽기 모델을 행에 표시하고 선택할 인스턴스 ID를 보관합니다.
        /// </summary>
        public void Bind(UI_QuestListPopup owner, QuestView quest)
        {
            m_Owner = owner;
            m_InstanceId = quest.InstanceId;

            if (m_TitleText != null)
            {
                string kind = "일일";
                if (quest.Kind == QuestKind.Emergency)
                {
                    kind = "긴급";
                }

                m_TitleText.text = $"[{kind}] {quest.Title} · {GetTierLabel(quest.Tier)}";
            }

            if (m_ProgressText != null)
            {
                m_ProgressText.text = $"{quest.Progress:N0} / {quest.Target:N0}";
            }

            if (m_StatusText != null)
            {
                m_StatusText.text = UI_QuestInfoPopup.GetStatusLabel(quest.Status);
            }
        }

        /// <summary>
        /// 연결된 목록 팝업에 현재 퀘스트 정보 열기를 요청합니다.
        /// </summary>
        private void OnClicked()
        {
            if (m_Owner != null)
            {
                m_Owner.OpenQuest(m_InstanceId);
            }
        }

        /// <summary>
        /// 등급 열거값을 사용자 표시 문자열로 변환합니다.
        /// </summary>
        private static string GetTierLabel(QuestTier tier)
        {
            return $"{(int)tier}등급";
        }

        /// <summary>
        /// 행 파괴 시 선택 버튼 이벤트 구독을 정리합니다.
        /// </summary>
        private void OnDestroy()
        {
            if (m_Button != null)
            {
                m_Button.onClick.RemoveListener(OnClicked);
            }
        }
    }
}
