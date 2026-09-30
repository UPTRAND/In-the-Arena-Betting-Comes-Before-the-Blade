using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    /// <summary>
    /// NPC 머리 위치를 따라가며 연결된 퀘스트 정보 팝업을 여는 말풍선 프리팹입니다.
    /// </summary>
    public sealed class QuestSpeechBubbleView : MonoBehaviour
    {
        [SerializeField] private Button m_Button;
        [SerializeField] private TMP_Text m_Label;
        [SerializeField] private Image m_Background;

        private UI_QuestLobbyHud m_Owner;
        private string m_InstanceId;

        /// <summary>
        /// 말풍선 선택 버튼 이벤트를 등록합니다.
        /// </summary>
        private void Awake()
        {
            if (m_Button != null)
            {
                m_Button.onClick.AddListener(OnClicked);
            }
        }

        /// <summary>
        /// 말풍선을 퀘스트 인스턴스에 연결하고 종류·상태 임시 이미지를 갱신합니다.
        /// </summary>
        public void Configure(UI_QuestLobbyHud owner, string instanceId, bool emergency, bool ready)
        {
            m_Owner = owner;
            m_InstanceId = instanceId;

            if (m_Label != null)
            {
                if (ready)
                {
                    m_Label.text = "완료!";
                }
                else if (emergency)
                {
                    m_Label.text = "긴급!";
                }
                else
                {
                    m_Label.text = "퀘스트";
                }
            }

            if (m_Background != null)
            {
                if (ready)
                {
                    m_Background.color = new Color(0.30f, 0.75f, 0.36f, 0.96f);
                }
                else if (emergency)
                {
                    m_Background.color = new Color(0.87f, 0.25f, 0.20f, 0.96f);
                }
                else
                {
                    m_Background.color = new Color(0.20f, 0.52f, 0.84f, 0.96f);
                }
            }
        }

        /// <summary>
        /// 연결된 HUD에 현재 퀘스트 정보 열기를 요청합니다.
        /// </summary>
        private void OnClicked()
        {
            if (m_Owner != null)
            {
                m_Owner.OpenQuest(m_InstanceId);
            }
        }

        /// <summary>
        /// 말풍선 파괴 시 선택 버튼 이벤트 구독을 정리합니다.
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
