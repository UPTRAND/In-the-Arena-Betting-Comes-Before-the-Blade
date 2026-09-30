using System.Collections.Generic;
using InTheArena.Quests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    /// <summary>
    /// 로비의 퀘스트·우편 진입 버튼과 NPC 말풍선 프리팹을 관리합니다.
    /// </summary>
    public sealed class UI_QuestLobbyHud : UI_Base
    {
        [SerializeField] private Button m_QuestListButton;
        [SerializeField] private Button m_MailboxButton;
        [SerializeField] private TMP_Text m_QuestBadgeText;
        [SerializeField] private TMP_Text m_MailBadgeText;
        [SerializeField] private RectTransform m_BubbleRoot;
        [SerializeField] private QuestSpeechBubbleView m_BubblePrefab;
        [SerializeField] private Vector2 m_BubbleScreenOffset = new Vector2(0f, 26f);

        private readonly Dictionary<string, QuestSpeechBubbleView> m_Bubbles = new Dictionary<string, QuestSpeechBubbleView>();
        private readonly Dictionary<string, LobbyNpcView> m_NpcsById = new Dictionary<string, LobbyNpcView>();
        private LobbyPlazaController m_Plaza;
        private float m_NextRefresh;
        private bool m_TriedEmergencyIntro;

        /// <summary>
        /// 로비 진입 버튼 이벤트를 등록합니다.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            if (m_QuestListButton != null)
            {
                m_QuestListButton.onClick.AddListener(OnQuestListClicked);
            }

            if (m_MailboxButton != null)
            {
                m_MailboxButton.onClick.AddListener(OnMailboxClicked);
            }
        }

        /// <summary>
        /// HUD가 열리면 현재 광장과 퀘스트 상태를 연결합니다.
        /// </summary>
        public override void OnOpened()
        {
            base.OnOpened();
            BindPlaza();
            Refresh();
        }

        /// <summary>
        /// NPC 이동에는 매 프레임 따라가고 저장 상태는 제한된 주기로 갱신합니다.
        /// </summary>
        private void Update()
        {
            if (!BIsOpened)
            {
                return;
            }

            UpdateBubblePositions();
            if (Time.unscaledTime < m_NextRefresh)
            {
                return;
            }

            m_NextRefresh = Time.unscaledTime + 0.5f;
            BindPlaza();
            Refresh();
            TryShowEmergencyIntro();
        }

        /// <summary>
        /// 현재 퀘스트·우편 수량과 실제 NPC 배정에 맞춰 프리팹 표시를 갱신합니다.
        /// </summary>
        public void Refresh()
        {
            QuestService quests = EventManager.Instance?.Quests;
            if (quests == null)
            {
                return;
            }

            IReadOnlyList<QuestView> daily = quests.GetDailyQuests();
            QuestView emergency = quests.GetEmergencyQuest();
            List<QuestView> visibleQuests = new List<QuestView>();
            int actionableCount = 0;

            for (int i = 0; i < daily.Count; i++)
            {
                QuestView quest = daily[i];
                if (IsVisibleOnNpc(quest))
                {
                    visibleQuests.Add(quest);
                }

                if (quest.Status == QuestStatus.ReadyToConfirm)
                {
                    actionableCount++;
                }
            }

            if (emergency != null)
            {
                if (IsVisibleOnNpc(emergency))
                {
                    visibleQuests.Add(emergency);
                }

                if (emergency.Status == QuestStatus.ReadyToConfirm)
                {
                    actionableCount++;
                }
            }

            if (m_QuestBadgeText != null)
            {
                m_QuestBadgeText.text = actionableCount.ToString();
            }

            if (m_MailBadgeText != null)
            {
                int mailCount = 0;
                if (EventManager.Instance.Mailbox != null)
                {
                    mailCount = EventManager.Instance.Mailbox.GetUnclaimedCount();
                }

                m_MailBadgeText.text = mailCount.ToString();
            }

            AssignResidents(quests);
            RebuildBubbles(visibleQuests);
        }

        /// <summary>
        /// 말풍선에서 선택한 퀘스트 정보 프리팹을 엽니다.
        /// </summary>
        public void OpenQuest(string instanceId)
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.OpenQuestInfo(instanceId);
            }
        }

        /// <summary>
        /// 현재 로비에 생성된 광장 컨트롤러와 주민 갱신 이벤트를 연결합니다.
        /// </summary>
        private void BindPlaza()
        {
            if (m_Plaza != null)
            {
                return;
            }

            m_Plaza = FindAnyObjectByType<LobbyPlazaController>(FindObjectsInactive.Exclude);
            if (m_Plaza != null)
            {
                m_Plaza.ResidentsChanged -= OnResidentsChanged;
                m_Plaza.ResidentsChanged += OnResidentsChanged;
            }
        }

        /// <summary>
        /// 실제 생성에 성공한 주민 ID를 서비스에 전달하여 1:1 배정을 저장합니다.
        /// </summary>
        private void AssignResidents(QuestService quests)
        {
            m_NpcsById.Clear();
            if (m_Plaza == null || m_Plaza.CurrentProfile == null)
            {
                return;
            }

            List<string> npcIds = new List<string>();
            for (int i = 0; i < m_Plaza.Residents.Count; i++)
            {
                LobbyNpcView resident = m_Plaza.Residents[i];
                if (resident == null || string.IsNullOrWhiteSpace(resident.NpcId))
                {
                    continue;
                }

                npcIds.Add(resident.NpcId);
                m_NpcsById[resident.NpcId] = resident;
            }

            quests.AssignLobbyNpcs(m_Plaza.CurrentProfile.StableId, npcIds);
        }

        /// <summary>
        /// 표시 대상 퀘스트와 일치하도록 편집 가능한 말풍선 프리팹 집합을 맞춥니다.
        /// </summary>
        private void RebuildBubbles(IReadOnlyList<QuestView> quests)
        {
            if (m_BubblePrefab == null || m_BubbleRoot == null)
            {
                return;
            }

            HashSet<string> required = new HashSet<string>();
            for (int i = 0; i < quests.Count; i++)
            {
                QuestView quest = quests[i];
                if (string.IsNullOrWhiteSpace(quest.AssignedNpcId) || !m_NpcsById.ContainsKey(quest.AssignedNpcId))
                {
                    continue;
                }

                required.Add(quest.InstanceId);
                if (!m_Bubbles.TryGetValue(quest.InstanceId, out QuestSpeechBubbleView bubble) || bubble == null)
                {
                    bubble = Instantiate(m_BubblePrefab, m_BubbleRoot);
                    bubble.gameObject.SetActive(true);
                    m_Bubbles[quest.InstanceId] = bubble;
                }

                bubble.Configure(
                    this,
                    quest.InstanceId,
                    quest.Kind == QuestKind.Emergency,
                    quest.Status == QuestStatus.ReadyToConfirm);
            }

            List<string> removals = new List<string>();
            foreach (KeyValuePair<string, QuestSpeechBubbleView> pair in m_Bubbles)
            {
                if (!required.Contains(pair.Key))
                {
                    if (pair.Value != null)
                    {
                        Destroy(pair.Value.gameObject);
                    }

                    removals.Add(pair.Key);
                }
            }

            for (int i = 0; i < removals.Count; i++)
            {
                m_Bubbles.Remove(removals[i]);
            }
        }

        /// <summary>
        /// 월드의 NPC 머리 위치를 HUD 로컬 좌표로 변환해 말풍선을 추적합니다.
        /// </summary>
        private void UpdateBubblePositions()
        {
            UnityEngine.Camera bubbleCamera = GetCanvasCamera(m_BubbleRoot);

            foreach (KeyValuePair<string, QuestSpeechBubbleView> pair in m_Bubbles)
            {
                QuestView quest = EventManager.Instance?.Quests?.GetQuest(pair.Key);
                if (quest == null || !m_NpcsById.TryGetValue(quest.AssignedNpcId, out LobbyNpcView npc) || npc == null)
                {
                    continue;
                }

                UnityEngine.Camera npcCamera = GetCanvasCamera(npc);
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(npcCamera, npc.HeadWorldPosition);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(m_BubbleRoot, screenPoint, bubbleCamera, out Vector2 localPoint))
                {
                    RectTransform bubbleRect = pair.Value.transform as RectTransform;
                    if (bubbleRect != null)
                    {
                        bubbleRect.anchoredPosition = localPoint + m_BubbleScreenOffset;
                    }
                }
            }
        }

        /// <summary>
        /// 지정한 UI가 속한 Canvas의 좌표 변환용 카메라를 반환합니다.
        /// Screen Space Overlay Canvas는 카메라를 사용하지 않습니다.
        /// </summary>
        private static UnityEngine.Camera GetCanvasCamera(Component component)
        {
            if (component == null)
            {
                return null;
            }

            Canvas canvas = component.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return canvas.worldCamera;
        }

        /// <summary>
        /// 저장된 최초 표시 대기를 소비하고 긴급 정보 팝업을 한 세션에 한 번 엽니다.
        /// </summary>
        private void TryShowEmergencyIntro()
        {
            if (m_TriedEmergencyIntro || EventManager.Instance?.Quests == null)
            {
                return;
            }

            m_TriedEmergencyIntro = true;
            string instanceId = EventManager.Instance.Quests.ConsumeEmergencyIntro();
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.OpenQuestInfo(instanceId, true);
                }
            }
        }

        /// <summary>
        /// NPC 배정과 말풍선을 유지해야 하는 진행 상태인지 판정합니다.
        /// </summary>
        private static bool IsVisibleOnNpc(QuestView quest)
        {
            return quest.Status == QuestStatus.Active
                || quest.Status == QuestStatus.ReadyToConfirm
                || quest.Status == QuestStatus.Submitting;
        }

        /// <summary>
        /// 미배정 퀘스트도 확인 가능한 전체 목록을 엽니다.
        /// </summary>
        private void OnQuestListClicked()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.OpenQuestList();
            }
        }

        /// <summary>
        /// 우편함 팝업을 엽니다.
        /// </summary>
        private void OnMailboxClicked()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.OpenMailbox();
            }
        }

        /// <summary>
        /// 광장 주민이 다시 생성되면 배정과 말풍선을 즉시 갱신합니다.
        /// </summary>
        private void OnResidentsChanged()
        {
            Refresh();
        }

        /// <summary>
        /// 씬 종료 시 광장과 버튼 이벤트 구독을 정리합니다.
        /// </summary>
        protected override void OnDestroy()
        {
            if (m_Plaza != null)
            {
                m_Plaza.ResidentsChanged -= OnResidentsChanged;
            }

            if (m_QuestListButton != null)
            {
                m_QuestListButton.onClick.RemoveListener(OnQuestListClicked);
            }

            if (m_MailboxButton != null)
            {
                m_MailboxButton.onClick.RemoveListener(OnMailboxClicked);
            }

            base.OnDestroy();
        }
    }
}
