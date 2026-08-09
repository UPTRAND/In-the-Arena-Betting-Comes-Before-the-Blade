#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using InTheArena.MainGame;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace InTheArena.UI
{
    public sealed class UI_LobbyStagePanel : UI_Base
    {
        private const string LobbySceneName = "Lobby";

        [SerializeField] private TMP_Text m_LevelText;
        [SerializeField] private Button m_StartButton;
        [SerializeField] private TMP_Text m_StartButtonLabel;
        [SerializeField] private Image m_BackgroundImage;
        [SerializeField] private List<StageData> m_StageDatas = new List<StageData>();

        private StageData m_Target;

        protected override void Awake()
        {
            base.Awake();
            m_StartButton.onClick.AddListener(StartStage);
        }

        public override void OnOpened()
        {
            base.OnOpened();
            Refresh();
        }

        private void OnEnable()
        {
            if (IsLobbySceneActive())
            {
                Refresh();
            }
        }

        public void Refresh()
        {
            if (!IsLobbySceneActive())
            {
                return;
            }

            RestoreBackgroundForLobby();

            int next = GetNextStageNumber();
            m_Target = FindStage(next);

            if (m_LevelText != null)
            {
                m_LevelText.text = m_Target != null ? m_Target.StageName : "\uC900\uBE44 \uC911";
            }

            if (m_StartButtonLabel != null)
            {
                m_StartButtonLabel.text = $"\uB808\uBCA8 {next}";
            }

            RefreshBackground();
        }

        private int GetNextStageNumber()
        {
            return (SaveManager.Instance != null ? SaveManager.Instance.ClearedStageNumber : 0) + 1;
        }

        private StageData FindStage(int stageNumber)
        {
            return m_StageDatas.Find(stage => stage != null && stage.StageNum == stageNumber);
        }

        private void RefreshBackground()
        {
            if (m_BackgroundImage != null && m_Target != null && m_Target.BackgroundSprite != null)
            {
                m_BackgroundImage.gameObject.SetActive(true);
                m_BackgroundImage.sprite = m_Target.BackgroundSprite;
                m_BackgroundImage.preserveAspect = true;
            }
        }

        private static bool IsLobbySceneActive()
        {
            return SceneManager.GetActiveScene().name == LobbySceneName;
        }

        private void RestoreBackgroundForLobby()
        {
            if (m_BackgroundImage != null)
            {
                m_BackgroundImage.gameObject.SetActive(true);
            }
        }

        private void StartStage()
        {
            if (m_Target == null)
            {
                m_Target = FindStage(GetNextStageNumber());
            }

            if (m_Target == null)
            {
                Debug.Log("[Lobby] Target stage is not ready.");
                return;
            }

            if (StageManager.Instance == null)
            {
                Debug.LogError("[Lobby] StageManager was not found.");
                return;
            }

            if (StageManager.Instance.IsStageRunning ||
                (InTheArena.Util.LoadingProgressService.Instance != null &&
                 InTheArena.Util.LoadingProgressService.Instance.IsLoading))
            {
                return;
            }

            SaveManager save = SaveManager.Instance;
            if (save == null || !save.TrySpendHeart())
            {
                Debug.Log($"[Lobby] Not enough hearts. Next heart in {save?.GetRemainingHeartTime():mm\\:ss}");
                return;
            }

            _ = StageManager.Instance.StartStageAsync(m_Target);
        }
    }
}
#endif
