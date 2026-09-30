using System.Collections.Generic;
using InTheArena.Quests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    /// <summary>
    /// 모든 일일·긴급 퀘스트와 NPC 미배정 항목을 함께 제공하는 보조 목록 팝업입니다.
    /// </summary>
    public sealed class UI_QuestListPopup : UI_Base
    {
        [SerializeField] private RectTransform m_ContentRoot;
        [SerializeField] private QuestListRowView m_RowPrefab;
        [SerializeField] private TMP_Text m_EmptyText;
        [SerializeField] private TMP_Text m_UnassignedText;
        [SerializeField] private Button m_CloseButton;

        private readonly List<QuestListRowView> m_Rows = new List<QuestListRowView>();

        /// <summary>
        /// 닫기 버튼 이벤트를 등록합니다.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            if (m_CloseButton != null)
            {
                m_CloseButton.onClick.AddListener(OnCloseClicked);
            }
        }

        /// <summary>
        /// 목록이 열릴 때 최신 저장 상태로 행을 다시 만듭니다.
        /// </summary>
        public override void OnOpened()
        {
            base.OnOpened();
            Rebuild();
        }

        /// <summary>
        /// 저장된 퀘스트 목록을 행 프리팹으로 다시 구성합니다.
        /// </summary>
        public void Rebuild()
        {
            ClearRows();
            List<QuestView> quests = new List<QuestView>();
            IReadOnlyList<QuestView> daily = EventManager.Instance?.Quests?.GetDailyQuests();
            if (daily != null)
            {
                for (int i = 0; i < daily.Count; i++)
                {
                    quests.Add(daily[i]);
                }
            }

            QuestView emergency = EventManager.Instance?.Quests?.GetEmergencyQuest();
            if (emergency != null)
            {
                quests.Add(emergency);
            }

            int unassignedCount = 0;
            for (int i = 0; i < quests.Count; i++)
            {
                QuestView quest = quests[i];
                if (string.IsNullOrWhiteSpace(quest.AssignedNpcId)
                    && quest.Status != QuestStatus.Delivered
                    && quest.Status != QuestStatus.Failed
                    && quest.Status != QuestStatus.Expired)
                {
                    unassignedCount++;
                }

                QuestListRowView row = Instantiate(m_RowPrefab, m_ContentRoot);
                row.gameObject.SetActive(true);
                row.Bind(this, quest);
                m_Rows.Add(row);
            }

            if (m_EmptyText != null)
            {
                m_EmptyText.gameObject.SetActive(quests.Count == 0);
            }

            if (m_UnassignedText != null)
            {
                m_UnassignedText.text = $"담당 NPC 없음: {unassignedCount}";
            }
        }

        /// <summary>
        /// 선택한 행과 같은 공통 퀘스트 정보 팝업을 엽니다.
        /// </summary>
        public void OpenQuest(string instanceId)
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.OpenQuestInfo(instanceId);
            }
        }

        /// <summary>
        /// 다시 만들기 전에 기존 퀘스트 행 오브젝트를 정리합니다.
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
        /// UIManager를 통해 목록 팝업을 닫습니다.
        /// </summary>
        private void OnCloseClicked()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.CloseControl(this);
            }
        }

        /// <summary>
        /// 프리팹 파괴 시 닫기 버튼 이벤트 구독을 정리합니다.
        /// </summary>
        protected override void OnDestroy()
        {
            if (m_CloseButton != null)
            {
                m_CloseButton.onClick.RemoveListener(OnCloseClicked);
            }

            base.OnDestroy();
        }
    }
}
