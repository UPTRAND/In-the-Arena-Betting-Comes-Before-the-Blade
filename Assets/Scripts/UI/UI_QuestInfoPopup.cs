using System;
using InTheArena.Quests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    /// <summary>
    /// 퀘스트의 확정 목표, 진행도, 기한, 보상과 완료 확인을 표시하는 공통 팝업입니다.
    /// </summary>
    public sealed class UI_QuestInfoPopup : UI_Base
    {
        [SerializeField] private TMP_Text m_KindText;
        [SerializeField] private TMP_Text m_TitleText;
        [SerializeField] private TMP_Text m_ObjectiveText;
        [SerializeField] private TMP_Text m_ProgressText;
        [SerializeField] private TMP_Text m_RewardText;
        [SerializeField] private TMP_Text m_RemainingText;
        [SerializeField] private TMP_Text m_StatusText;
        [SerializeField] private TMP_Text m_ConfirmButtonText;
        [SerializeField] private Button m_ConfirmButton;
        [SerializeField] private Button m_CloseButton;

        private string m_InstanceId;
        private float m_NextRefresh;
        private bool m_IsEmergencyIntro;

        /// <summary>
        /// 프리팹에 연결된 완료·닫기 버튼 이벤트를 등록합니다.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            if (m_ConfirmButton != null)
            {
                m_ConfirmButton.onClick.AddListener(OnConfirmClicked);
            }

            if (m_CloseButton != null)
            {
                m_CloseButton.onClick.AddListener(OnCloseClicked);
            }
        }

        /// <summary>
        /// 지정 퀘스트를 표시하고 UIManager의 제어 스택에 팝업을 엽니다.
        /// </summary>
        public void ShowQuest(string instanceId, bool isEmergencyIntro = false)
        {
            m_InstanceId = instanceId;
            m_IsEmergencyIntro = isEmergencyIntro;
            if (m_IsEmergencyIntro && EventManager.Instance != null)
            {
                EventManager.Instance.SetEmergencyIntroVisible(true);
            }

            Refresh();
            if (UIManager.Instance != null)
            {
                UIManager.Instance.OpenControl(this);
            }
        }

        /// <summary>
        /// 팝업이 열릴 때 최신 저장 상태로 내용을 다시 그립니다.
        /// </summary>
        public override void OnOpened()
        {
            base.OnOpened();
            Refresh();
        }

        /// <summary>
        /// 긴급 최초 표시가 닫혔음을 알려 대기 중인 로비 보상 연출을 재개할 수 있게 합니다.
        /// </summary>
        public override void OnClosed()
        {
            base.OnClosed();
            ReleaseEmergencyIntroBlock();
        }

        /// <summary>
        /// 열린 동안 남은 시간과 만료 상태를 짧은 주기로 갱신합니다.
        /// </summary>
        private void Update()
        {
            if (!BIsOpened || Time.unscaledTime < m_NextRefresh)
            {
                return;
            }

            m_NextRefresh = Time.unscaledTime + 0.25f;
            Refresh();
        }

        /// <summary>
        /// 저장된 읽기 모델을 다시 조회하여 만료와 완료 가능 상태를 즉시 반영합니다.
        /// </summary>
        public void Refresh()
        {
            QuestView quest = null;
            if (EventManager.Instance != null && EventManager.Instance.Quests != null)
            {
                quest = EventManager.Instance.Quests.GetQuest(m_InstanceId);
            }

            if (quest == null)
            {
                Close();
                return;
            }

            if (m_KindText != null)
            {
                m_KindText.text = GetKindLabel(quest.Kind);
            }

            if (m_TitleText != null)
            {
                m_TitleText.text = $"{quest.Title} · {(int)quest.Tier}등급";
            }

            if (m_ObjectiveText != null)
            {
                m_ObjectiveText.text = quest.Description;
            }

            if (m_ProgressText != null)
            {
                m_ProgressText.text = $"진행 {quest.Progress:N0} / {quest.Target:N0}";
            }

            if (m_RewardText != null)
            {
                m_RewardText.text = $"보상: 영구 골드 {quest.RewardGold:N0}";
            }

            if (m_StatusText != null)
            {
                m_StatusText.text = GetStatusLabel(quest.Status);
            }

            if (m_RemainingText != null)
            {
                m_RemainingText.text = BuildRemainingLabel(quest);
            }

            bool canConfirm = quest.Status == QuestStatus.ReadyToConfirm;
            if (m_ConfirmButton != null)
            {
                m_ConfirmButton.interactable = canConfirm;
            }

            if (m_ConfirmButtonText != null)
            {
                if (canConfirm)
                {
                    m_ConfirmButtonText.text = "완료 확인";
                }
                else
                {
                    m_ConfirmButtonText.text = GetStatusLabel(quest.Status);
                }
            }
        }

        /// <summary>
        /// 저장된 퀘스트 상태를 사용자에게 표시할 문자열로 변환합니다.
        /// </summary>
        public static string GetStatusLabel(QuestStatus status)
        {
            switch (status)
            {
                case QuestStatus.Active:
                    return "진행 중";
                case QuestStatus.ReadyToConfirm:
                    return "완료 확인 가능";
                case QuestStatus.Submitting:
                    return "발송 처리 중";
                case QuestStatus.Delivered:
                    return "보상 우편 발송 완료";
                case QuestStatus.Failed:
                    return "실패";
                case QuestStatus.Expired:
                    return "만료";
                default:
                    return "알 수 없음";
            }
        }

        /// <summary>
        /// 일일·긴급 종류를 정보 팝업 표제 문자열로 변환합니다.
        /// </summary>
        private static string GetKindLabel(QuestKind kind)
        {
            if (kind == QuestKind.Emergency)
            {
                return "긴급 퀘스트";
            }

            return "일일 퀘스트";
        }

        /// <summary>
        /// 저장된 UTC 만료 시각과 현재 시각으로 남은 시간을 계산합니다.
        /// </summary>
        private static string BuildRemainingLabel(QuestView quest)
        {
            long nowTicks = DateTime.UtcNow.Ticks;
            if (EventManager.Instance != null && SaveManager.Instance != null)
            {
                nowTicks = SaveManager.Instance.UtcNow.Ticks;
            }

            long remainingTicks = Math.Max(0, quest.ExpiresAtUtcTicks - nowTicks);
            TimeSpan remaining = TimeSpan.FromTicks(remainingTicks);
            return $"남은 시간 {Math.Floor(remaining.TotalHours):00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
        }

        /// <summary>
        /// 완료 확인 요청을 서비스에 전달하고 결과 메시지를 갱신합니다.
        /// </summary>
        private void OnConfirmClicked()
        {
            if (EventManager.Instance == null || EventManager.Instance.Quests == null)
            {
                return;
            }

            QuestCompletionResult result = EventManager.Instance.Quests.TryConfirmCompletion(m_InstanceId);
            Refresh();
            if (m_StatusText != null)
            {
                m_StatusText.text = GetCompletionResultLabel(result);
            }
        }

        /// <summary>
        /// 완료 확인 결과를 정보 팝업 메시지로 변환합니다.
        /// </summary>
        private static string GetCompletionResultLabel(QuestCompletionResult result)
        {
            switch (result)
            {
                case QuestCompletionResult.Success:
                    return "보상 우편을 발송했습니다.";
                case QuestCompletionResult.SaveFailed:
                    return "저장 실패 · 우편함에서 재시도하세요.";
                case QuestCompletionResult.Expired:
                    return "기한이 만료되었습니다.";
                case QuestCompletionResult.NotInLobby:
                    return "로비에서만 완료할 수 있습니다.";
                default:
                    return result.ToString();
            }
        }

        /// <summary>
        /// UIManager를 통해 현재 팝업을 닫습니다.
        /// </summary>
        private void OnCloseClicked()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.CloseControl(this);
            }
        }

        /// <summary>
        /// 프리팹 파괴 시 버튼 이벤트 구독을 정리합니다.
        /// </summary>
        protected override void OnDestroy()
        {
            if (m_ConfirmButton != null)
            {
                m_ConfirmButton.onClick.RemoveListener(OnConfirmClicked);
            }

            if (m_CloseButton != null)
            {
                m_CloseButton.onClick.RemoveListener(OnCloseClicked);
            }

            ReleaseEmergencyIntroBlock();
            base.OnDestroy();
        }

        /// <summary>
        /// 긴급 최초 표시용 보상 연출 대기 표식을 한 번만 해제합니다.
        /// </summary>
        private void ReleaseEmergencyIntroBlock()
        {
            if (!m_IsEmergencyIntro)
            {
                return;
            }

            m_IsEmergencyIntro = false;
            if (EventManager.Instance != null)
            {
                EventManager.Instance.SetEmergencyIntroVisible(false);
            }
        }
    }
}
