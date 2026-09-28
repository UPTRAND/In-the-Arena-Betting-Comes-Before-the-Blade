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
        [SerializeField] private Button m_ChestButton;
        [SerializeField] private List<ItemData> m_ChestItems = new List<ItemData>();
        [SerializeField] private LobbyPlazaController m_Plaza;

        private StageData m_Target;
        private bool m_PlazaSaveReady;
        private int m_PlazaStage;

        protected override void Awake()
        {
            base.Awake();
            m_StartButton.onClick.AddListener(StartStage);
            m_ChestButton ??= FindDescendant(transform, "Chest_Button")?.GetComponent<Button>();
            if (m_ChestButton != null) m_ChestButton.onClick.AddListener(OpenChest);
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
            RefreshChestButton();
            m_PlazaSaveReady = IsSaveReadyForPlaza();
            m_PlazaStage = next;
            m_Plaza?.SetContext(next, m_BackgroundImage != null ? m_BackgroundImage.rectTransform : null, m_PlazaSaveReady);
        }

        private static bool IsSaveReadyForPlaza() => SaveManager.Instance != null &&
            SaveManager.Instance.IsInitialized && SaveManager.Instance.Availability == SaveAvailability.Ready;

        private void Update()
        {
            if (m_Plaza != null && IsLobbySceneActive() &&
                (m_PlazaSaveReady != IsSaveReadyForPlaza() || m_PlazaStage != GetNextStageNumber())) Refresh();
        }

        private int GetNextStageNumber()
        {
            return (SaveManager.Instance != null ? SaveManager.Instance.ClearedStageNumber : 0) + 1;
        }

        private StageData FindStage(int stageNumber)
        {
            return m_StageDatas.Find(stage => stage != null && stage.StageNum == stageNumber);
        }

        /// <summary>현재 스테이지 배경을 원본 비율을 유지하며 화면 전체에 채웁니다.</summary>
        private void RefreshBackground()
        {
            StageData backgroundStage = m_Target;
            if (backgroundStage == null)
            {
                foreach (StageData stage in m_StageDatas)
                    if (stage != null && stage.StageNum < GetNextStageNumber() &&
                        (backgroundStage == null || stage.StageNum > backgroundStage.StageNum)) backgroundStage = stage;
            }
            if (m_BackgroundImage != null && backgroundStage != null && backgroundStage.BackgroundSprite != null)
            {
                m_BackgroundImage.gameObject.SetActive(true);
                m_BackgroundImage.sprite = backgroundStage.BackgroundSprite;
                m_BackgroundImage.preserveAspect = true;

                AspectRatioFitter fitter = m_BackgroundImage.GetComponent<AspectRatioFitter>();
                if (fitter != null)
                {
                    fitter.aspectRatio = backgroundStage.BackgroundSprite.rect.width /
                        Mathf.Max(1f, backgroundStage.BackgroundSprite.rect.height);
                }
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
                SoundManager.Instance?.PlaySfx(SfxIds.ButtonNegative);
                Debug.Log("[Lobby] Target stage is not ready.");
                return;
            }

            if (StageManager.Instance == null)
            {
                SoundManager.Instance?.PlaySfx(SfxIds.ButtonNegative);
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
                SoundManager.Instance?.PlaySfx(SfxIds.ButtonNegative);
                Debug.Log($"[Lobby] Not enough hearts. Next heart in {save?.GetRemainingHeartTime():mm\\:ss}");
                return;
            }

            SoundManager.Instance?.PlaySfx(SfxIds.ButtonPositive);
            _ = StageManager.Instance.StartStageAsync(m_Target);
        }

        private void OpenChest()
        {
            SaveManager save = SaveManager.Instance;
            if (save == null || save.Availability != SaveAvailability.Ready)
            {
                SoundManager.Instance?.PlaySfx(SfxIds.ButtonNegative);
                Debug.LogWarning("[Lobby] Chest is unavailable because save data is not ready.");
                return;
            }
            if (save.Stars < 3) { SoundManager.Instance?.PlaySfx(SfxIds.ButtonNegative); Debug.LogWarning("[Lobby] Chest requires 3 stars."); return; }
            if (!ChestDrawService.TryDraw(m_ChestItems, new UnityChestRandom(), out ChestReward reward)) { SoundManager.Instance?.PlaySfx(SfxIds.ButtonNegative); Debug.LogError("[Lobby] Chest item list is invalid."); return; }
            if (!save.TryOpenChest(reward.Item.ItemType, reward.Amount, out string error))
            {
                SoundManager.Instance?.PlaySfx(SfxIds.ButtonNegative);
                Debug.LogWarning($"[Lobby] Chest save failed: {error}");
                return;
            }
            UI_ChestOpeningPopup prefab = Resources.Load<UI_ChestOpeningPopup>("UI/UI_ChestOpeningPopup");
            if (prefab == null) { SoundManager.Instance?.PlaySfx(SfxIds.ButtonNegative); Debug.LogError("[Lobby] Chest popup prefab could not be loaded."); return; }
            UI_ChestOpeningPopup popup = Instantiate(prefab, GetComponentInParent<UI_Root>()?.transform);
            SoundManager.Instance?.PlaySfx(SfxIds.ButtonPositive);
            popup.Show(reward);
        }

        private void RefreshChestButton()
        {
            if (m_ChestButton == null) return;
            SaveManager save = SaveManager.Instance;
            m_ChestButton.interactable = save != null && save.Availability == SaveAvailability.Ready && save.Stars >= 3;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindDescendant(root.GetChild(i), objectName);
                if (result != null) return result;
            }
            return null;
        }
    }
}
#endif
