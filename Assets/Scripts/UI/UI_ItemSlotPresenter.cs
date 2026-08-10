#if UNITY_6000_0_OR_NEWER
using DG.Tweening;
using InTheArena.MainGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    public enum ItemSlotVisualState
    {
        Normal,
        Casting,
        Used
    }

    /// <summary>
    /// 아이템 슬롯의 수량과 라운드 시각 상태만 담당합니다.
    /// 구매, 사용, 라운드 정책은 이 컴포넌트가 알지 못합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UI_ItemSlotPresenter : MonoBehaviour
    {
        [SerializeField] private RectTransform m_SlotRoot;
        [SerializeField] private Image m_Icon;
        [SerializeField] private TMP_Text m_CountText;
        [SerializeField] private float m_ActiveScale = 1.1f;
        [SerializeField] private float m_TweenDuration = 0.15f;

        private Tween m_ScaleTween;
        private Vector3 m_BaseScale = Vector3.one;
        private ItemType m_ItemType = ItemType.None;

        public ItemSlotVisualState State { get; private set; } = ItemSlotVisualState.Normal;

        private void Awake()
        {
            m_SlotRoot ??= transform as RectTransform;
            ResolveIcon();
            m_BaseScale = m_SlotRoot != null ? m_SlotRoot.localScale : Vector3.one;
            ResolveOrCreateCountText();
            SetState(ItemSlotVisualState.Normal, false);
        }

        public void Bind(ItemData itemData)
        {
            m_ItemType = itemData == null ? ItemType.None : itemData.ItemType;
            if (m_Icon != null && itemData != null)
            {
                m_Icon.sprite = itemData.Icon;
                m_Icon.enabled = itemData.Icon != null;
            }

            RefreshCount();
        }

        public void Bind(ItemType itemType, Image icon = null)
        {
            m_ItemType = itemType;
            m_Icon ??= icon;
            RefreshCount();
        }

        public void RefreshCount()
        {
            if (m_CountText == null)
            {
                return;
            }

            int count = SaveManager.Instance != null && m_ItemType != ItemType.None
                ? SaveManager.Instance.GetItemCount(m_ItemType)
                : 0;
            m_CountText.text = $"x{count}";
        }

        public void SetState(ItemSlotVisualState state, bool animate = true)
        {
            if (State == state && m_SlotRoot != null)
            {
                return;
            }

            State = state;
            Vector3 targetScale = state == ItemSlotVisualState.Normal
                ? m_BaseScale
                : m_BaseScale * Mathf.Max(1f, m_ActiveScale);

            if (m_SlotRoot == null)
            {
                return;
            }

            m_ScaleTween?.Kill();
            if (!animate || m_TweenDuration <= 0f)
            {
                m_SlotRoot.localScale = targetScale;
                return;
            }

            m_ScaleTween = m_SlotRoot
                .DOScale(targetScale, m_TweenDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
        }

        public void ResetToNormal(bool animate = false)
        {
            SetState(ItemSlotVisualState.Normal, animate);
        }

        private void ResolveOrCreateCountText()
        {
            if (m_CountText != null || m_SlotRoot == null)
            {
                return;
            }

            Transform existing = m_SlotRoot.Find("CountText") ??
                m_SlotRoot.Find("ItemCount_Text");
            if (existing != null)
            {
                m_CountText = existing.GetComponent<TMP_Text>();
            }

            if (m_CountText == null)
            {
                GameObject countObject = new GameObject(
                    "CountText",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                countObject.transform.SetParent(m_SlotRoot, false);
                m_CountText = countObject.GetComponent<TextMeshProUGUI>();
            }

            RectTransform countRect = m_CountText.rectTransform;
            countRect.anchorMin = new Vector2(1f, 0f);
            countRect.anchorMax = new Vector2(1f, 0f);
            countRect.pivot = new Vector2(1f, 0f);
            countRect.anchoredPosition = new Vector2(-6f, 6f);
            countRect.sizeDelta = new Vector2(80f, 32f);

            m_CountText.alignment = TextAlignmentOptions.BottomRight;
            m_CountText.fontSize = 18f;
            m_CountText.raycastTarget = false;
            m_CountText.color = Color.white;
            m_CountText.font ??= TMP_Settings.defaultFontAsset;
        }

        private void ResolveIcon()
        {
            if (m_Icon != null)
            {
                return;
            }

            if (m_SlotRoot != null)
            {
                m_Icon = m_SlotRoot.Find("Image")?.GetComponent<Image>() ??
                    m_SlotRoot.Find("Iteam_Image")?.GetComponent<Image>();
            }

            if (m_Icon != null)
            {
                return;
            }

            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i].transform != transform)
                {
                    m_Icon = images[i];
                    break;
                }
            }
        }

        private void OnDestroy()
        {
            m_ScaleTween?.Kill();
        }
    }
}
#endif
