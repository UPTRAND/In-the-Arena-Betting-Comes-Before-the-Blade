#if UNITY_6000_0_OR_NEWER
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using InTheArena.MainGame;

namespace InTheArena.UI
{
    /// <summary>
    /// Runtime options popup shared by the lobby and gameplay UI.
    /// The popup is built at runtime so it follows every scene without a duplicate prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UI_OptionsPopup : MonoBehaviour
    {
        private const string LobbySceneName = "Lobby";
        private const string GalmuriFontName = "Galmuri9 SDF";

        private static UI_OptionsPopup s_Instance;
        private static TMP_FontAsset s_GalmuriFont;

        private CanvasGroup m_CanvasGroup;
        private Slider m_BgmSlider;
        private Slider m_SfxSlider;
        private Button m_EasyButton;
        private Button m_NormalButton;
        private Button m_HardButton;
        private bool m_IsBuilt;

        public static void Show()
        {
            EnsureInstance();
            s_Instance.Open();
        }

        private static void EnsureInstance()
        {
            if (s_Instance != null)
            {
                return;
            }

            GameObject root = new GameObject("UI_OptionsPopup", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(UI_OptionsPopup));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            DontDestroyOnLoad(root);
            s_Instance = root.GetComponent<UI_OptionsPopup>();
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
            Build();
            Close();
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }
        }

        private void Open()
        {
            if (!m_IsBuilt)
            {
                Build();
            }

            SyncVolumeValues();
            SyncDifficultyButtons();
            gameObject.SetActive(true);
            m_CanvasGroup.alpha = 1f;
            m_CanvasGroup.interactable = true;
            m_CanvasGroup.blocksRaycasts = true;
        }

        private void Close()
        {
            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.alpha = 0f;
                m_CanvasGroup.interactable = false;
                m_CanvasGroup.blocksRaycasts = false;
            }

            gameObject.SetActive(false);
        }

        private void Build()
        {
            if (m_IsBuilt)
            {
                return;
            }

            m_IsBuilt = true;
            m_CanvasGroup = gameObject.AddComponent<CanvasGroup>();

            Image dimmer = CreateImage("Dimmer", transform, new Color(0.02f, 0.04f, 0.08f, 0.78f));
            Stretch(dimmer.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Button dimmerButton = dimmer.gameObject.AddComponent<Button>();
            dimmerButton.transition = Selectable.Transition.None;
            dimmerButton.onClick.AddListener(Close);

            Image panel = CreateImage("OptionsPanel", transform, new Color(0.075f, 0.12f, 0.20f, 1f));
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(740f, 540f);
            panelRect.anchoredPosition = Vector2.zero;

            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.36f, 0.75f, 1f, 0.9f);
            outline.effectDistance = new Vector2(4f, -4f);

            TMP_Text title = CreateText("Title", panel.transform, "OPTIONS", 50, TextAlignmentOptions.Center,
                new Color(0.72f, 0.90f, 1f));
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -38f);
            titleRect.sizeDelta = new Vector2(-120f, 70f);

            Button closeButton = CreateButton("CloseButton", panel.transform, "×", new Color(0.22f, 0.37f, 0.55f));
            RectTransform closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-26f, -24f);
            closeRect.sizeDelta = new Vector2(58f, 58f);
            closeButton.onClick.AddListener(Close);

            m_BgmSlider = CreateVolumeRow(panel.transform, "BGM VOLUME", 225f);
            m_SfxSlider = CreateVolumeRow(panel.transform, "SFX VOLUME", 135f);
            m_BgmSlider.onValueChanged.AddListener(SetBgmVolume);
            m_SfxSlider.onValueChanged.AddListener(SetSfxVolume);

            TMP_Text difficultyLabel = CreateText("DifficultyLabel", panel.transform, "\uB09C\uC774\uB3C4", 30, TextAlignmentOptions.Left, Color.white);
            RectTransform difficultyLabelRect = difficultyLabel.rectTransform;
            difficultyLabelRect.anchorMin = difficultyLabelRect.anchorMax = new Vector2(0.5f, 0.5f);
            difficultyLabelRect.pivot = new Vector2(0f, 0.5f);
            difficultyLabelRect.anchoredPosition = new Vector2(-275f, 20f);
            difficultyLabelRect.sizeDelta = new Vector2(550f, 42f);

            m_EasyButton = CreateDifficultyButton(panel.transform, "\uC26C\uC6C0", StageDifficulty.Easy, -185f);
            m_NormalButton = CreateDifficultyButton(panel.transform, "\uC911\uAC04", StageDifficulty.Normal, 0f);
            m_HardButton = CreateDifficultyButton(panel.transform, "\uC5B4\uB824\uC6C0", StageDifficulty.Hard, 185f);

            Button lobbyButton = CreateButton("ReturnToLobbyButton", panel.transform, "\uB85C\uBE44\uB85C \uB3CC\uC544\uAC00\uAE30",
                new Color(0.16f, 0.45f, 0.66f));
            RectTransform lobbyRect = lobbyButton.GetComponent<RectTransform>();
            lobbyRect.anchorMin = lobbyRect.anchorMax = new Vector2(0.5f, 0f);
            lobbyRect.pivot = new Vector2(0.5f, 0f);
            lobbyRect.anchoredPosition = new Vector2(0f, 48f);
            lobbyRect.sizeDelta = new Vector2(420f, 74f);
            lobbyButton.onClick.AddListener(ReturnToLobby);
        }

        private Slider CreateVolumeRow(Transform parent, string label, float y)
        {
            TMP_Text labelText = CreateText(label, parent, label, 30, TextAlignmentOptions.Left, Color.white);
            RectTransform labelRect = labelText.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.anchoredPosition = new Vector2(-275f, y);
            labelRect.sizeDelta = new Vector2(550f, 42f);

            GameObject sliderObject = new GameObject(label + "Slider", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(parent, false);
            RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.anchorMin = sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRect.anchoredPosition = new Vector2(0f, y - 58f);
            sliderRect.sizeDelta = new Vector2(550f, 38f);

            Image background = CreateImage("Background", sliderObject.transform, new Color(0.02f, 0.04f, 0.08f));
            Stretch(background.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 7f), new Vector2(0f, -7f));
            Image fill = CreateImage("Fill", sliderObject.transform, new Color(0.23f, 0.74f, 1f));
            RectTransform fillRect = fill.rectTransform;
            Stretch(fillRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image handle = CreateImage("Handle", sliderObject.transform, new Color(0.88f, 0.96f, 1f));
            handle.rectTransform.sizeDelta = new Vector2(34f, 52f);

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.targetGraphic = handle;
            slider.fillRect = fillRect;
            slider.handleRect = handle.rectTransform;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private Button CreateDifficultyButton(Transform parent, string label, StageDifficulty difficulty, float x)
        {
            Button button = CreateButton(difficulty + "DifficultyButton", parent, label, new Color(0.16f, 0.45f, 0.66f));
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, -45f);
            rect.sizeDelta = new Vector2(160f, 58f);
            button.onClick.AddListener(() => SetStageDifficulty(difficulty));
            return button;
        }

        private static Button CreateButton(string name, Transform parent, string label, Color color)
        {
            Image image = CreateImage(name, parent, color);
            Button button = image.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.2f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.25f);
            button.colors = colors;
            TMP_Text text = CreateText("Label", image.transform, label, 28, TextAlignmentOptions.Center, Color.white);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return button;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(parent, false);
            Image image = child.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateText(string name, Transform parent, string value, float size,
            TextAlignmentOptions alignment, Color color)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            child.transform.SetParent(parent, false);
            TMP_Text text = child.GetComponent<TMP_Text>();
            text.font = GetGalmuriFont();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static TMP_FontAsset GetGalmuriFont()
        {
            if (s_GalmuriFont != null)
            {
                return s_GalmuriFont;
            }

            s_GalmuriFont = Resources.Load<TMP_FontAsset>(GalmuriFontName);
            if (s_GalmuriFont != null)
            {
                return s_GalmuriFont;
            }

#if UNITY_EDITOR
            s_GalmuriFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/Galmuri9 SDF.asset");
            if (s_GalmuriFont != null)
            {
                return s_GalmuriFont;
            }
#endif

            TMP_FontAsset[] loadedFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            for (int i = 0; i < loadedFonts.Length; i++)
            {
                if (loadedFonts[i] != null && loadedFonts[i].name == GalmuriFontName)
                {
                    s_GalmuriFont = loadedFonts[i];
                    return s_GalmuriFont;
                }
            }

            return TMP_Settings.defaultFontAsset;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private void SyncVolumeValues()
        {
            SoundManager sound = SoundManager.Instance;
            if (sound == null)
            {
                return;
            }

            m_BgmSlider.SetValueWithoutNotify(sound.BgmVolume);
            m_SfxSlider.SetValueWithoutNotify(sound.SfxVolume);
        }

        private static void SetBgmVolume(float value)
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.BgmVolume = value;
            }
        }

        private static void SetSfxVolume(float value)
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.SfxVolume = value;
            }
        }

        private void SetStageDifficulty(StageDifficulty difficulty)
        {
            if (SaveManager.Instance != null &&
                !SaveManager.Instance.TrySetSelectedStageDifficulty(difficulty, out string error))
            {
                Debug.LogWarning($"[UI_OptionsPopup] 스테이지 난이도 저장에 실패했습니다: {error}");
            }

            SyncDifficultyButtons();
        }

        private void SyncDifficultyButtons()
        {
            StageDifficulty selected = SaveManager.Instance != null
                ? SaveManager.Instance.SelectedStageDifficulty
                : StageDifficulty.Easy;

            SetDifficultyButtonState(m_EasyButton, selected == StageDifficulty.Easy);
            SetDifficultyButtonState(m_NormalButton, selected == StageDifficulty.Normal);
            SetDifficultyButtonState(m_HardButton, selected == StageDifficulty.Hard);
        }

        private static void SetDifficultyButtonState(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            Color baseColor = selected ? new Color(0.93f, 0.68f, 0.20f) : new Color(0.16f, 0.45f, 0.66f);
            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = baseColor;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.2f);
            colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.25f);
            button.colors = colors;
        }

        private void ReturnToLobby()
        {
            Close();
            if (SceneManager.GetActiveScene().name != LobbySceneName)
            {
                AsyncSceneLoader.LoadScene(LobbySceneName);
            }
        }
    }
}
#endif
