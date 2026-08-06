#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using InTheArena.MainGame;
using TMPro;

namespace InTheArena.UI
{
    public class UI_CombatItemDragController : UI_Base, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Drag Item Settings")]
        [SerializeField] private ItemData m_ItemData;
        [SerializeField] private Image m_DragIconImage;
        [SerializeField] private CanvasGroup m_IconCanvasGroup;
        [SerializeField] private LayerMask m_BattlefieldLayerMask;
        [SerializeField] private TMP_Text m_CountText;
        [SerializeField] private TMP_Text m_FeedbackText;

        private Vector2 m_OriginalPosition;
        private Transform m_OriginalParent;
        private bool m_IsDragging = false;

        protected override void Awake()
        {
            base.Awake();
            RefreshItemCount();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (m_ItemData == null)
            {
                return;
            }

            ShowFeedback("드래그를 통한 아이템 사용은 비활성화되었습니다.");
            return;


            m_IsDragging = true;
            m_OriginalPosition = transform.position;
            m_OriginalParent = transform.parent;

            Canvas rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas != null)
            {
                transform.SetParent(rootCanvas.transform);
                transform.SetAsLastSibling();
            }

            if (m_IconCanvasGroup != null)
            {
                m_IconCanvasGroup.blocksRaycasts = false;
                m_IconCanvasGroup.alpha = 0.6f;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (m_IsDragging)
            {
                transform.position = eventData.position;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (m_IsDragging == false)
            {
                return;
            }

            m_IsDragging = false;

            if (m_IconCanvasGroup != null)
            {
                m_IconCanvasGroup.blocksRaycasts = true;
                m_IconCanvasGroup.alpha = 1f;
            }

            transform.SetParent(m_OriginalParent);
            transform.position = m_OriginalPosition;

            bool isValidDropZone = false;
            Vector3 dropPosition = Vector3.zero;

            if (UnityEngine.Camera.main != null)
            {
                Ray ray = UnityEngine.Camera.main.ScreenPointToRay(eventData.position);
                Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
                if (groundPlane.Raycast(ray, out float distance))
                {
                    dropPosition = ray.GetPoint(distance);
                }

                if (Physics.Raycast(ray, out RaycastHit hit, 100f, m_BattlefieldLayerMask))
                {
                    isValidDropZone = true;
                }
            }

            if (isValidDropZone)
            {
                TryUseCombatItem(dropPosition);
            }
            else
            {
                ShowFeedback("전장 영역에 드롭해야 합니다.");
            }
        }

        private void TryUseCombatItem(Vector3 dropPosition)
        {
            ShowFeedback("드래그를 통한 아이템 사용은 비활성화되었습니다.");
        }

        private void OnEnable()
        {
            if (RoundManager.Instance != null && RoundManager.Instance.CombatPhase != null)
            {
                RoundManager.Instance.CombatPhase.OnItemUsed += HandleItemUsed;
            }
        }

        private void OnDisable()
        {
            if (RoundManager.Instance != null && RoundManager.Instance.CombatPhase != null)
            {
                RoundManager.Instance.CombatPhase.OnItemUsed -= HandleItemUsed;
            }
        }

        private void HandleItemUsed(ItemData usedItem)
        {
            if (usedItem == m_ItemData)
            {
                RefreshItemCount();
            }
        }

        public void RefreshItemCount()
        {
            if (m_ItemData != null && m_CountText != null)
            {
                m_CountText.text = string.Empty;
            }
        }

        private void ShowFeedback(string message)
        {
            if (m_FeedbackText != null)
            {
                m_FeedbackText.text = message;
            }
            Debug.Log("[UI_CombatItemDragController] " + message);
        }
    }
}
#endif
