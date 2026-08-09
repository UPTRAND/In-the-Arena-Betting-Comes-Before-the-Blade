#if UNITY_6000_0_OR_NEWER
using TMPro;
using InTheArena.MainGame;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace InTheArena.UI
{
    [DisallowMultipleComponent]
    public sealed class UI_OptionsPopup : UI_Base
    {
        private const string LobbySceneName = "Lobby";

        [Header("Controls")]
        [SerializeField] private Button m_DimmerButton;
        [SerializeField] private Button m_CloseButton;
        [SerializeField] private Button m_ReturnToLobbyButton;
        [SerializeField] private Button m_GameQuitButton;
        [SerializeField] private Slider m_BgmSlider;
        [SerializeField] private Slider m_SfxSlider;

        public static void Show(UI_OptionsPopup prefab, UI_Root root)
        {
            if (prefab == null || root == null)
            {
                Debug.LogError("[UI_OptionsPopup] Popup prefab or UI root is missing.");
                return;
            }

            UI_OptionsPopup popup = root.GetComponentInChildren<UI_OptionsPopup>(true);
            if (popup == null)
            {
                popup = Instantiate(prefab, root.transform);
                popup.name = prefab.name;
            }

            popup.SetRoot(root);
            if (UIManager.Instance != null)
            {
                UIManager.Instance.OpenControl(popup);
            }
            else
            {
                popup.Open();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            ResolveReferences();
            m_DimmerButton?.onClick.AddListener(ClosePopup);
            m_CloseButton?.onClick.AddListener(ClosePopup);
            m_ReturnToLobbyButton?.onClick.AddListener(ReturnToLobby);
            m_GameQuitButton?.onClick.AddListener(QuitGame);
            m_BgmSlider?.onValueChanged.AddListener(SetBgmVolume);
            m_SfxSlider?.onValueChanged.AddListener(SetSfxVolume);
        }

        public override void OnOpened()
        {
            base.OnOpened();
            RefreshSceneButtons();
            SoundManager sound = SoundManager.Instance;
            if (sound == null)
            {
                return;
            }

            m_BgmSlider?.SetValueWithoutNotify(sound.BgmVolume);
            m_SfxSlider?.SetValueWithoutNotify(sound.SfxVolume);
        }

        protected override void OnDestroy()
        {
            m_DimmerButton?.onClick.RemoveListener(ClosePopup);
            m_CloseButton?.onClick.RemoveListener(ClosePopup);
            m_ReturnToLobbyButton?.onClick.RemoveListener(ReturnToLobby);
            m_GameQuitButton?.onClick.RemoveListener(QuitGame);
            m_BgmSlider?.onValueChanged.RemoveListener(SetBgmVolume);
            m_SfxSlider?.onValueChanged.RemoveListener(SetSfxVolume);
            base.OnDestroy();
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

        private void ClosePopup()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.CloseControl(this);
            }
            else
            {
                Close();
            }
        }

        private void ReturnToLobby()
        {
            ClosePopup();
            if (SceneManager.GetActiveScene().name != LobbySceneName)
            {
                if (StageManager.Instance != null)
                {
                    StageManager.Instance.ReturnToLobbyFromOptions();
                }
                else
                {
                    AsyncSceneLoader.LoadScene(LobbySceneName);
                }
            }
        }

        private void QuitGame()
        {
            Application.Quit();
        }

        private void RefreshSceneButtons()
        {
            bool isLobby = SceneManager.GetActiveScene().name == LobbySceneName;
            if (m_ReturnToLobbyButton != null)
            {
                m_ReturnToLobbyButton.gameObject.SetActive(!isLobby);
            }

            if (m_GameQuitButton != null)
            {
                m_GameQuitButton.gameObject.SetActive(isLobby);
            }
        }

        private void ResolveReferences()
        {
            m_CloseButton ??= FindButton("Button");
            m_ReturnToLobbyButton ??= FindButton("Loby_Button");
            m_GameQuitButton ??= FindButton("GameQuit_Button");
        }

        private Button FindButton(string objectName)
        {
            Transform target = FindDescendant(transform, objectName);
            return target != null ? target.GetComponent<Button>() : null;
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
