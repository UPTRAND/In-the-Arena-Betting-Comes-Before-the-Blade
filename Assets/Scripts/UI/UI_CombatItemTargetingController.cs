#if UNITY_6000_0_OR_NEWER
using System;
using InTheArena.Battlefield;
using InTheArena.MainGame;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InTheArena.UI
{
    public enum CombatItemTargetingState
    {
        Idle,
        Armed,
        DraggingValid,
        DraggingInvalid
    }

    /// <summary>
    /// 구매 확인이 끝난 전투 아이템의 화면 전체 입력과 타기팅 표시를 담당합니다.
    /// 이 컴포넌트가 붙은 투명 Image가 활성화된 동안에만 입력을 받습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UI_CombatItemTargetingController : MonoBehaviour,
        IInitializePotentialDragHandler,
        IPointerDownHandler,
        IDragHandler,
        IPointerUpHandler
    {
        private const int NoPointerId = int.MinValue;
        private static readonly Color RangeFillColor = new Color(0.2f, 0.9f, 0.35f, 0.18f);
        private static readonly Color RangeOutlineColor = new Color(0.35f, 1f, 0.55f, 0.85f);

        private Image m_InputImage;
        private UI_CombatItemRangeIndicator m_RangeIndicator;
        private RectTransform m_DisplayRoot;
        private RectTransform m_SelectedSlot;
        private Image m_CancelOverlay;
        private CombatPhase m_CombatPhase;
        private ItemType m_ItemType;
        private Vector3 m_OriginalSlotScale = Vector3.one;
        private Vector3 m_LastWorldPosition;
        private bool m_HasWorldPosition;
        private bool m_IsPointerDown;
        private bool m_SuppressDisableAbort;
        private int m_PointerId = NoPointerId;

        public CombatItemTargetingState State { get; private set; } = CombatItemTargetingState.Idle;

        public event Action<Vector3> TargetConfirmed;
        public event Action TargetCanceled;

        private void Awake()
        {
            m_InputImage = GetComponent<Image>();
            if (m_InputImage != null)
            {
                m_InputImage.color = Color.clear;
                m_InputImage.raycastTarget = true;
            }

            gameObject.SetActive(false);
        }

        public bool BeginTargeting(
            ItemType itemType,
            CombatPhase combatPhase,
            RectTransform selectedSlot,
            Image cancelOverlay)
        {
            if (combatPhase == null || selectedSlot == null ||
                (itemType != ItemType.Meteor && itemType != ItemType.Mercenary))
            {
                return false;
            }

            AbortTargeting();

            if (!combatPhase.BeginItemCastingSlowMotion())
            {
                return false;
            }

            m_ItemType = itemType;
            m_CombatPhase = combatPhase;
            m_SelectedSlot = selectedSlot;
            m_CancelOverlay = cancelOverlay;
            m_OriginalSlotScale = selectedSlot.localScale;
            selectedSlot.localScale = m_OriginalSlotScale * 1.1f;
            SetCancelOverlay(false);
            m_PointerId = NoPointerId;
            m_IsPointerDown = false;
            m_HasWorldPosition = false;
            State = CombatItemTargetingState.Armed;

            EnsureRangeIndicator();
            if (m_RangeIndicator != null)
            {
                m_RangeIndicator.gameObject.SetActive(false);
            }

            gameObject.SetActive(true);
            return true;
        }

        public void AbortTargeting()
        {
            if (State == CombatItemTargetingState.Idle && m_CombatPhase == null)
            {
                return;
            }

            CombatPhase phase = m_CombatPhase;
            RestoreSelectionVisuals();
            ClearState();
            phase?.EndItemCastingSlowMotion();
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (eventData != null)
            {
                eventData.useDragThreshold = false;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (State != CombatItemTargetingState.Armed || eventData == null ||
                m_PointerId != NoPointerId)
            {
                return;
            }

            m_PointerId = eventData.pointerId;
            m_IsPointerDown = true;
            EvaluatePointer(eventData.position);
            eventData.Use();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsActivePointer(eventData))
            {
                return;
            }

            EvaluatePointer(eventData.position);
            eventData.Use();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!IsActivePointer(eventData))
            {
                return;
            }

            EvaluatePointer(eventData.position);
            bool isValid = m_IsPointerDown && State == CombatItemTargetingState.DraggingValid &&
                           m_HasWorldPosition;
            Vector3 targetPosition = m_LastWorldPosition;

            FinishTargetingVisuals();
            if (isValid)
            {
                TargetConfirmed?.Invoke(targetPosition);
            }
            else
            {
                TargetCanceled?.Invoke();
            }

            eventData.Use();
        }

        private void LateUpdate()
        {
            if (State == CombatItemTargetingState.DraggingValid && m_HasWorldPosition)
            {
                UpdateRangeIndicator(m_LastWorldPosition);
            }
        }

        private bool IsActivePointer(PointerEventData eventData)
        {
            return eventData != null && m_IsPointerDown && eventData.pointerId == m_PointerId;
        }

        private void EvaluatePointer(Vector2 screenPosition)
        {
            if (m_CombatPhase == null || !m_CombatPhase.CanCommitGroundTargetItem())
            {
                SetInvalidTargetingState();
                return;
            }

            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            BattlefieldArea area = BattlefieldArea.Active;
            if (mainCamera == null || area == null ||
                !area.TryGetGroundPosition(mainCamera, screenPosition, out Vector3 worldPosition))
            {
                SetInvalidTargetingState();
                return;
            }

            if (m_ItemType == ItemType.Mercenary)
            {
                worldPosition = area.ClampPosition(
                    worldPosition,
                    m_CombatPhase.MercenaryFormationPadding);
            }

            m_LastWorldPosition = worldPosition;
            m_HasWorldPosition = true;
            State = CombatItemTargetingState.DraggingValid;
            SetCancelOverlay(false);
            if (m_RangeIndicator != null)
            {
                m_RangeIndicator.gameObject.SetActive(true);
                UpdateRangeIndicator(worldPosition);
            }
        }

        private void SetInvalidTargetingState()
        {
            State = CombatItemTargetingState.DraggingInvalid;
            m_HasWorldPosition = false;
            SetCancelOverlay(true);
            if (m_RangeIndicator != null)
            {
                m_RangeIndicator.gameObject.SetActive(false);
            }
        }

        private void EnsureRangeIndicator()
        {
            Transform groundWorldRoot =
                UIManager.Instance?.GetRootFromType(EUIObjectPoolingParent.GroundWorld)?.transform;

            if (m_RangeIndicator == null)
            {
                GameObject indicatorObject = new GameObject(
                    "CombatItemRangeIndicator",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(UI_CombatItemRangeIndicator));

                Transform root = groundWorldRoot;
                if (root == null)
                {
                    root = transform.parent != null ? transform.parent : transform;
                }

                indicatorObject.transform.SetParent(root, false);
                m_RangeIndicator = indicatorObject.GetComponent<UI_CombatItemRangeIndicator>();
                m_DisplayRoot = root as RectTransform;
            }

            if (groundWorldRoot != null && m_RangeIndicator.transform.parent != groundWorldRoot)
            {
                m_RangeIndicator.transform.SetParent(groundWorldRoot, false);
                m_DisplayRoot = groundWorldRoot as RectTransform;
            }

            if (m_DisplayRoot == null)
            {
                m_DisplayRoot = m_RangeIndicator != null
                    ? m_RangeIndicator.transform.parent as RectTransform
                    : transform.parent as RectTransform;
            }

            CombatItemRangeShape shape = m_ItemType == ItemType.Meteor
                ? CombatItemRangeShape.Circle
                : CombatItemRangeShape.Rectangle;
            m_RangeIndicator?.Configure(shape, RangeFillColor, RangeOutlineColor);
        }

        private void UpdateRangeIndicator(Vector3 worldPosition)
        {
            if (m_RangeIndicator == null || m_DisplayRoot == null || UnityEngine.Camera.main == null)
            {
                return;
            }

            UnityEngine.Camera camera = UnityEngine.Camera.main;
            Vector2 center = WorldToDisplayPoint(camera, worldPosition, out bool centerVisible);
            if (!centerVisible)
            {
                m_RangeIndicator.gameObject.SetActive(false);
                return;
            }

            Vector2 size;
            if (m_ItemType == ItemType.Meteor)
            {
                float radius = m_CombatPhase.MeteorTargetRadius;
                Vector2 left = WorldToDisplayPoint(camera, worldPosition - Vector3.right * radius, out bool leftVisible);
                Vector2 right = WorldToDisplayPoint(camera, worldPosition + Vector3.right * radius, out bool rightVisible);
                Vector2 bottom = WorldToDisplayPoint(camera, worldPosition - Vector3.forward * radius, out bool bottomVisible);
                Vector2 top = WorldToDisplayPoint(camera, worldPosition + Vector3.forward * radius, out bool topVisible);
                if (!leftVisible || !rightVisible || !bottomVisible || !topVisible)
                {
                    m_RangeIndicator.gameObject.SetActive(false);
                    return;
                }

                size = new Vector2(
                    Mathf.Max(1f, Mathf.Abs(right.x - left.x)),
                    Mathf.Max(1f, Mathf.Abs(top.y - bottom.y)));
            }
            else
            {
                Vector2 halfSize = m_CombatPhase.MercenaryFormationPreviewSize * 0.5f;
                Vector2 left = WorldToDisplayPoint(camera, worldPosition - Vector3.right * halfSize.x, out bool leftVisible);
                Vector2 right = WorldToDisplayPoint(camera, worldPosition + Vector3.right * halfSize.x, out bool rightVisible);
                Vector2 bottom = WorldToDisplayPoint(camera, worldPosition - Vector3.forward * halfSize.y, out bool bottomVisible);
                Vector2 top = WorldToDisplayPoint(camera, worldPosition + Vector3.forward * halfSize.y, out bool topVisible);
                if (!leftVisible || !rightVisible || !bottomVisible || !topVisible)
                {
                    m_RangeIndicator.gameObject.SetActive(false);
                    return;
                }

                size = new Vector2(
                    Mathf.Max(1f, Mathf.Abs(right.x - left.x)),
                    Mathf.Max(1f, Mathf.Abs(top.y - bottom.y)));
            }

            RectTransform indicatorRect = m_RangeIndicator.rectTransform;
            indicatorRect.anchorMin = new Vector2(0.5f, 0.5f);
            indicatorRect.anchorMax = new Vector2(0.5f, 0.5f);
            indicatorRect.pivot = new Vector2(0.5f, 0.5f);
            indicatorRect.anchoredPosition = center;
            indicatorRect.sizeDelta = size;
        }

        private Vector2 WorldToDisplayPoint(UnityEngine.Camera camera, Vector3 worldPosition, out bool visible)
        {
            Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition);
            visible = screenPosition.z > 0f;
            if (!visible)
            {
                return Vector2.zero;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                m_DisplayRoot,
                screenPosition,
                null,
                out Vector2 localPosition);
            return localPosition;
        }

        private void FinishTargetingVisuals()
        {
            CombatPhase phase = m_CombatPhase;
            RestoreSelectionVisuals();
            ClearState();
            phase?.EndItemCastingSlowMotion();
        }

        private void RestoreSelectionVisuals()
        {
            if (m_SelectedSlot != null)
            {
                m_SelectedSlot.localScale = m_OriginalSlotScale;
            }

            SetCancelOverlay(false);
            if (m_RangeIndicator != null)
            {
                m_RangeIndicator.gameObject.SetActive(false);
            }
        }

        private void ClearState()
        {
            State = CombatItemTargetingState.Idle;
            m_CombatPhase = null;
            m_SelectedSlot = null;
            m_CancelOverlay = null;
            m_PointerId = NoPointerId;
            m_IsPointerDown = false;
            m_HasWorldPosition = false;

            if (gameObject.activeSelf)
            {
                m_SuppressDisableAbort = true;
                gameObject.SetActive(false);
                m_SuppressDisableAbort = false;
            }
        }

        private void SetCancelOverlay(bool visible)
        {
            if (m_CancelOverlay != null)
            {
                m_CancelOverlay.enabled = visible;
                m_CancelOverlay.raycastTarget = false;
            }
        }

        private void OnDisable()
        {
            if (!m_SuppressDisableAbort &&
                (State != CombatItemTargetingState.Idle || m_CombatPhase != null))
            {
                AbortTargeting();
            }
        }

        private void OnDestroy()
        {
            AbortTargeting();
            if (m_RangeIndicator != null)
            {
                Destroy(m_RangeIndicator.gameObject);
            }
        }
    }
}
#endif
