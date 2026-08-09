#if UNITY_6000_0_OR_NEWER
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

        private static UI_OptionsPopup s_Instance;

        private CanvasGroup m_CanvasGroup;
        private Slider m_BgmSlider;
        private Slider m_SfxSlider;
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

            m_BgmSlider = CreateVolumeRow(panel.transform, "BGM VOLUME", 220f);
            m_SfxSlider = CreateVolumeRow(panel.transform, "SFX VOLUME", 130f);
            m_BgmSlider.onValueChanged.AddListener(SetBgmVolume);
            m_SfxSlider.onValueChanged.AddListener(SetSfxVolume);

            Button lobbyButton = CreateButton("ReturnToLobbyButton", panel.transform, "RETURN TO LOBBY",
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
            text.font = TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
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
