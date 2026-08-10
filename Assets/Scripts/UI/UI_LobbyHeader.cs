#if UNITY_6000_0_OR_NEWER
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    public sealed class UI_LobbyHeader : UI_Base
    {
        [SerializeField] private TMP_Text m_GoldText;
        [SerializeField] private TMP_Text m_HeartText;
        [SerializeField] private TMP_Text m_TimerText;
        [SerializeField] private RectTransform m_TimerBox;
        [SerializeField] private TMP_Text m_StarText;
        [SerializeField] private Image m_GoldImage;
        [SerializeField] private Image m_StarImage;
        [SerializeField] private Button m_SettingsButton;
        [SerializeField] private UI_OptionsPopup m_OptionsPopupPrefab;
        private float m_NextRefresh;
        private bool m_HasTimerState;
        private bool m_HasInitializedTimerState;
        private Tween m_TimerBoxTween;
        private int m_ActiveRewardAnimations;

        protected override void Awake()
        {
            base.Awake();
            ResolveRewardReferences();
            if (m_SettingsButton != null)
            {
                m_SettingsButton.onClick.AddListener(OpenOptionsPopup);
            }
        }

        private async void Start()
        {
            // AsyncSceneLoader clears its transition tweens after the target scene is active.
            // Wait until that cleanup has completed so this reward sequence cannot be killed midway.
            while (InTheArena.Util.LoadingProgressService.Instance.IsLoading)
            {
                await Awaitable.NextFrameAsync();
            }

            if (this == null)
            {
                return;
            }

            TryPlayPendingStageClearRewards();
        }

        protected override void OnDestroy()
        {
            m_TimerBoxTween?.Kill();
            if (m_SettingsButton != null)
            {
                m_SettingsButton.onClick.RemoveListener(OpenOptionsPopup);
            }

            base.OnDestroy();
        }

        public override void OnOpened() { base.OnOpened(); Refresh(); }
        private void Update() { if (BIsOpened && Time.unscaledTime >= m_NextRefresh) Refresh(); }
        public void Refresh()
        {
            m_NextRefresh = Time.unscaledTime + 1f;
            SaveManager save = SaveManager.Instance;
            if (save == null) return;
            save.RefreshHearts();
            if (m_ActiveRewardAnimations == 0)
            {
                m_GoldText.text = save.Gold.ToString();
                m_StarText.text = save.Stars.ToString();
            }
            bool needsTimer = save.Hearts < SaveManager.MaxHearts;
            m_HeartText.text = $"{save.Hearts}/{SaveManager.MaxHearts}";
            if (m_TimerText != null)
            {
                m_TimerText.text = needsTimer ? save.GetRemainingHeartTime().ToString(@"mm\:ss") : string.Empty;
            }
            RefreshTimerBox(needsTimer);
        }

        private void TryPlayPendingStageClearRewards()
        {
            ResolveRewardReferences();
            if (!StageClearRewardPresentation.TryConsume(out StageClearRewardPresentation.RewardData reward)) return;

            if (m_GoldText != null) m_GoldText.text = reward.GoldBeforeReward.ToString();
            if (m_StarText != null) m_StarText.text = reward.StarsBeforeReward.ToString();

            PlayReward(
                reward.GoldBeforeReward,
                reward.GoldAfterReward,
                m_GoldImage,
                m_GoldText,
                8,
                "G",
                new Vector2(0f, 46f));
            PlayReward(
                reward.StarsBeforeReward,
                reward.StarsAfterReward,
                m_StarImage,
                m_StarText,
                int.MaxValue,
                "Star",
                new Vector2(0f, -46f));
        }

        private void PlayReward(int beforeValue, int afterValue, Image targetImage, TMP_Text targetText, int maxIconCount, string unitLabel, Vector2 previewOffset)
        {
            int reward = Mathf.Max(0, afterValue - beforeValue);
            if (reward == 0 || targetImage == null || targetImage.sprite == null || targetText == null)
            {
                if (targetText != null) targetText.text = afterValue.ToString();
                return;
            }

            m_ActiveRewardAnimations++;
            int received = 0;
            int displayed = beforeValue;
            UI_FlyingRewardEffect.PlayFromScreenPoint(
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) + previewOffset,
                targetImage.rectTransform,
                targetImage.sprite,
                reward,
                amount =>
                {
                    received += amount;
                    int nextValue = beforeValue + received;
                    targetText.DOKill();
                    DOTween.To(() => displayed, value =>
                    {
                        displayed = value;
                        targetText.text = value.ToString();
                    }, nextValue, 0.18f).SetEase(Ease.OutCubic).SetTarget(targetText).SetUpdate(true);
                },
                () =>
                {
                    targetText.DOKill();
                    targetText.text = afterValue.ToString();
                    m_ActiveRewardAnimations = Mathf.Max(0, m_ActiveRewardAnimations - 1);
                    if (m_ActiveRewardAnimations == 0) Refresh();
                },
                maxIconCount,
                $"+{reward} {unitLabel}");
        }

        private void ResolveRewardReferences()
        {
            m_GoldImage ??= FindDescendant(transform, "GoldImage")?.GetComponent<Image>();
            m_StarImage ??= FindDescendant(transform, "StarImage")?.GetComponent<Image>();
        }

        private void OpenOptionsPopup()
        {
            UI_OptionsPopup.Show(m_OptionsPopupPrefab, GetComponentInParent<UI_Root>());
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDescendant(root.GetChild(i), objectName);
                if (found != null) return found;
            }
            return null;
        }

        private void RefreshTimerBox(bool needsTimer)
        {
            if (m_TimerBox == null) return;

            float targetY = needsTimer ? -50f : 0f;
            if (!m_HasInitializedTimerState)
            {
                m_HasInitializedTimerState = true;
                m_HasTimerState = needsTimer;
                Vector2 position = m_TimerBox.anchoredPosition;
                position.y = targetY;
                m_TimerBox.anchoredPosition = position;
                return;
            }

            if (m_HasTimerState == needsTimer) return;
            m_HasTimerState = needsTimer;
            m_TimerBoxTween?.Kill();
            m_TimerBoxTween = m_TimerBox
                .DOAnchorPosY(targetY, 0.3f)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
        }
    }
}
#endif
