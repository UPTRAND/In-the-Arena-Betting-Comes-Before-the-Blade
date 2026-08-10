#if UNITY_6000_0_OR_NEWER
using System.Collections;
using DG.Tweening;
using InTheArena.MainGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    [DisallowMultipleComponent]
    public sealed class UI_ChestOpeningPopup : MonoBehaviour
    {
        private enum PopupState { WaitingForOpen, Opening, Opened }

        [SerializeField] private Sprite[] m_ChestSprites;
        [SerializeField] private TMP_FontAsset m_ResultFont;

        private Button m_ScreenButton;
        private Image m_FlashImage;
        private Image m_GlowImage;
        private Image m_ChestImage;
        private Image m_ItemImage;
        private TMP_Text m_ResultText;
        private ChestReward m_Reward;
        private PopupState m_State;
        private bool m_IsBuilt;

        private void Awake() => Build();

        public void Show(ChestReward reward)
        {
            Build();
            m_Reward = reward;
            m_State = PopupState.WaitingForOpen;
            m_ChestImage.sprite = GetChestSprite(false);
            m_ChestImage.rectTransform.localRotation = Quaternion.identity;
            m_ChestImage.rectTransform.localScale = Vector3.one;
            m_GlowImage.sprite = m_ChestImage.sprite;
            m_GlowImage.color = new Color(0f, 0f, 0f, 0f);
            m_GlowImage.rectTransform.localScale = Vector3.one * 1.15f;
            m_FlashImage.color = TierColor(0f);
            m_ItemImage.gameObject.SetActive(false);
            m_ItemImage.rectTransform.anchoredPosition = new Vector2(0f, 25f);
            m_ItemImage.rectTransform.localScale = Vector3.zero;
            m_ResultText.text = "상자를 터치하세요";
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
        }

        private void OnScreenTapped()
        {
            if (m_State == PopupState.WaitingForOpen)
            {
                SoundManager.Instance?.PlaySfx(SfxIds.ButtonPositive);
                m_State = PopupState.Opening;
                PlayTouchEmphasis();
                StartCoroutine(OpenRoutine());
            }
            else if (m_State == PopupState.Opened)
            {
                SoundManager.Instance?.PlaySfx(SfxIds.ButtonNegative);
                Destroy(gameObject);
            }
        }

        private IEnumerator OpenRoutine()
        {
            float duration = Random.Range(1f, 2f);
            yield return m_ChestImage.rectTransform
                .DOShakeRotation(duration, 14f, 18, 90f)
                .SetUpdate(true)
                .WaitForCompletion();

            m_ChestImage.rectTransform.localRotation = Quaternion.identity;
            m_ChestImage.sprite = GetChestSprite(true);
            m_ChestImage.rectTransform.localScale = Vector3.one * 1.1f;
            PlayOpenEmphasis();
            m_ItemImage.sprite = m_Reward.Item.Icon;
            m_ItemImage.gameObject.SetActive(m_ItemImage.sprite != null);
            m_ResultText.text = $"{GetKoreanItemName(m_Reward.Item.ItemType)} x{m_Reward.Amount} 획득!";

            yield return DOTween.Sequence()
                .Join(m_ItemImage.rectTransform.DOAnchorPosY(285f, .45f).SetEase(Ease.OutBack))
                .Join(m_ItemImage.rectTransform.DOScale(1f, .45f).SetEase(Ease.OutBack))
                .SetUpdate(true)
                .WaitForCompletion();

            m_State = PopupState.Opened;
        }

        private void PlayTouchEmphasis()
        {
            m_ChestImage.rectTransform.DOPunchScale(Vector3.one * .12f, .28f, 8, .8f).SetUpdate(true);
            m_GlowImage.DOFade(.55f, .16f).SetUpdate(true);
            m_GlowImage.rectTransform.DOScale(1.35f, .24f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        private void PlayOpenEmphasis()
        {
            m_FlashImage.DOFade(.5f, .08f).SetUpdate(true)
                .OnComplete(() => m_FlashImage.DOFade(0f, .22f).SetUpdate(true));
            m_ChestImage.rectTransform.DOPunchScale(Vector3.one * .24f, .38f, 10, .9f).SetUpdate(true);
            m_GlowImage.sprite = m_ChestImage.sprite;
            m_GlowImage.DOFade(.85f, .08f).SetUpdate(true)
                .OnComplete(() => m_GlowImage.DOFade(.18f, .45f).SetUpdate(true));
            m_GlowImage.rectTransform.DOScale(1.65f, .45f).SetEase(Ease.OutCubic).SetUpdate(true);
        }

        private Color TierColor(float alpha)
        {
            Color color = m_Reward.Tier switch
            {
                ChestTier.Bronze => new Color(1f, .48f, .18f, alpha),
                ChestTier.Silver => new Color(.65f, .88f, 1f, alpha),
                ChestTier.Gold => new Color(1f, .82f, .18f, alpha),
                _ => new Color(1f, 1f, 1f, alpha)
            };
            return color;
        }

        private static string GetKoreanItemName(ItemType itemType)
        {
            return itemType switch
            {
                ItemType.AdditionalBetTicket => "추가 배팅권",
                ItemType.Insurance => "보험",
                ItemType.RerollTicket => "리롤권",
                ItemType.Meteor => "메테오",
                ItemType.Mercenary => "용병 고용",
                ItemType.TimeExtension => "시간 연장",
                _ => "아이템"
            };
        }

        private Sprite GetChestSprite(bool open)
        {
            if (m_ChestSprites == null || m_ChestSprites.Length < 6) return null;
            return m_ChestSprites[(int)m_Reward.Tier + (open ? 3 : 0)];
        }

        private void Build()
        {
            if (m_IsBuilt) return;
            m_IsBuilt = true;

            if (!TryGetComponent(out Canvas canvas)) canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;

            if (!TryGetComponent<GraphicRaycaster>(out _)) gameObject.AddComponent<GraphicRaycaster>();
            if (!TryGetComponent(out Image background)) background = gameObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, .72f);
            background.raycastTarget = true;

            RectTransform root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            if (!TryGetComponent(out m_ScreenButton)) m_ScreenButton = gameObject.AddComponent<Button>();
            m_ScreenButton.targetGraphic = background;
            m_ScreenButton.transition = Selectable.Transition.None;
            m_ScreenButton.onClick.AddListener(OnScreenTapped);

            m_FlashImage = MakeFullscreenImage("OpenFlash");
            m_GlowImage = MakeImage("ChestGlow", new Vector2(0f, 10f), new Vector2(495f, 495f));
            m_GlowImage.preserveAspect = true;
            m_ChestImage = MakeImage("Chest", new Vector2(0f, 10f), new Vector2(450f, 450f));
            m_ChestImage.preserveAspect = true;
            m_ItemImage = MakeImage("RewardItem", new Vector2(0f, 25f), new Vector2(110f, 110f));
            m_ItemImage.preserveAspect = true;
            m_ResultText = MakeText("Result", new Vector2(0f, 430f), new Vector2(900f, 120f));
        }

        private Image MakeFullscreenImage(string objectName)
        {
            GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            child.transform.SetParent(transform, false);
            RectTransform rect = (RectTransform)child.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = child.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private Image MakeImage(string objectName, Vector2 position, Vector2 size)
        {
            GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            child.transform.SetParent(transform, false);
            RectTransform rect = (RectTransform)child.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = child.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text MakeText(string objectName, Vector2 position, Vector2 size)
        {
            GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            child.transform.SetParent(transform, false);
            RectTransform rect = (RectTransform)child.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            TMP_Text text = child.GetComponent<TextMeshProUGUI>();
            text.font = m_ResultFont;
            text.fontSize = 60f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private void OnDestroy()
        {
            if (m_ChestImage != null) m_ChestImage.rectTransform.DOKill();
            if (m_ItemImage != null) m_ItemImage.rectTransform.DOKill();
            if (m_GlowImage != null) { m_GlowImage.DOKill(); m_GlowImage.rectTransform.DOKill(); }
            if (m_FlashImage != null) m_FlashImage.DOKill();
        }
    }
}
#endif
