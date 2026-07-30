#if UNITY_6000_0_OR_NEWER
using InTheArena.Unit;
using UnityEngine;
using UnityEngine.UI;
using UnitTarget = InTheArena.Unit.Unit;

namespace InTheArena.UI
{
    /// <summary>
    /// 개별 유닛의 월드 위치를 추적하고 체력(HP) 변동을 시각화하는 UI 클래스입니다.
    /// [UI] World Canvas 하위에 생성되어 작동합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class UI_UnitHPBar : UI_Base
    {
        [Header("UI Element References")]
        [SerializeField] private Image m_BackgroundFrame;
        [SerializeField] private Image m_HpFillImage;
        [SerializeField] private Image m_ShieldFillImage;

        [Header("Display Settings")]
        [SerializeField] private float m_HeightOffset = 24f;
        [SerializeField] private float m_DefaultVisibleDuration = 1.5f;

        private UnitTarget m_TargetUnit;
        private float m_VisibleTimer;
        private UnityEngine.Camera m_MainCamera;
        private RectTransform m_RectTransform;

        protected override void Awake()
        {
            base.Awake();
            m_RectTransform = GetComponent<RectTransform>();
            EnsureUIComponents();
        }

        private void OnEnable()
        {
            m_MainCamera = UnityEngine.Camera.main;
        }

        /// <summary>
        /// 체력바가 추적할 대상 유닛을 설정합니다.
        /// </summary>
        public void SetTarget(UnitTarget target)
        {
            m_TargetUnit = target;
            UpdateHpFill();
        }

        /// <summary>
        /// 지정한 시간 동안 체력바를 화면에 표시합니다.
        /// </summary>
        public void ShowHpBar(float duration = 1.5f)
        {
            if (this == null || gameObject == null)
            {
                return;
            }

            m_VisibleTimer = duration;
            if (!gameObject.activeSelf)
            {
                Open();
            }
            UpdateHpFill();
            UpdatePosition();
        }

        /// <summary>
        /// 체력바를 즉시 비활성화(숨김) 처리합니다.
        /// </summary>
        public void HideHpBar()
        {
            m_VisibleTimer = 0f;
            if (this == null || gameObject == null)
            {
                return;
            }

            if (gameObject.activeSelf)
            {
                Close();
            }
        }

        private void LateUpdate()
        {
            if (this == null || gameObject == null)
            {
                return;
            }

            if (m_TargetUnit == null)
            {
                HideHpBar();
                return;
            }

            if (m_TargetUnit.IsDead)
            {
                HideHpBar();
                return;
            }

            if (!m_TargetUnit.gameObject.activeInHierarchy)
            {
                HideHpBar();
                return;
            }

            if (m_VisibleTimer > 0f)
            {
                m_VisibleTimer -= Time.deltaTime;
                if (m_VisibleTimer <= 0f)
                {
                    HideHpBar();
                    return;
                }
            }

            UpdateHpFill();
            UpdatePosition();
        }

        /// <summary>
        /// 유닛의 현재 체력 비율에 맞춰 Fill Amount 수치를 갱신합니다.
        /// </summary>
        private void UpdateHpFill()
        {
            if (this == null || gameObject == null || m_TargetUnit == null)
            {
                return;
            }

            if (m_HpFillImage == null)
            {
                EnsureUIComponents();
            }

            if (m_HpFillImage == null)
            {
                return;
            }

            float currentHp = m_TargetUnit.CurrentHp;
            float maxHp = m_TargetUnit.MaxHp;
            float ratio = 0f;

            if (maxHp > 0f)
            {
                ratio = currentHp / maxHp;
            }

            m_HpFillImage.fillAmount = Mathf.Clamp01(ratio);
        }

        /// <summary>
        /// 월드 상의 유닛 위치를 스크린 좌표로 변환하여 체력바 위치를 이동시킵니다.
        /// </summary>
        private void UpdatePosition()
        {
            if (this == null || gameObject == null || m_TargetUnit == null)
            {
                return;
            }

            if (m_MainCamera == null)
            {
                m_MainCamera = UnityEngine.Camera.main;
                if (m_MainCamera == null)
                {
                    return;
                }
            }

            Vector3 hitPosition = m_TargetUnit.HitPosition;
            Vector3 screenPosition = m_MainCamera.WorldToScreenPoint(hitPosition);

            if (screenPosition.z <= 0f)
            {
                if (gameObject.activeSelf)
                {
                    gameObject.SetActive(false);
                }
                return;
            }

            if (!gameObject.activeSelf && m_VisibleTimer > 0f)
            {
                gameObject.SetActive(true);
            }

            screenPosition.y += m_HeightOffset;
            transform.position = screenPosition;
        }

        /// <summary>
        /// 필요 UI 컴포넌트가 인스펙터에 미할당되었을 경우 동적으로 찾거나 생성하고, 스크린 크기를 적절히 조절합니다.
        /// </summary>
        private void EnsureUIComponents()
        {
            if (this == null || gameObject == null)
            {
                return;
            }

            if (m_RectTransform == null)
            {
                m_RectTransform = (RectTransform)transform;
            }

            if (m_RectTransform != null)
            {
                // 프리팹의 크기가 너무 작거나(미세 규격) 미설정된 경우 스크린 해상도에 맞게 보정
                if (m_RectTransform.sizeDelta.x < 30f)
                {
                    m_RectTransform.sizeDelta = new Vector2(60f, 8f);
                }
            }

            if (m_BackgroundFrame == null)
            {
                Transform frameChild = transform.Find("Frame");
                if (frameChild != null)
                {
                    m_BackgroundFrame = frameChild.GetComponent<Image>();
                }
                else
                {
                    m_BackgroundFrame = GetComponent<Image>();
                    if (m_BackgroundFrame == null)
                    {
                        m_BackgroundFrame = gameObject.AddComponent<Image>();
                        m_BackgroundFrame.color = new Color(0f, 0f, 0f, 0.7f);
                    }
                }
            }

            if (m_BackgroundFrame != null)
            {
                RectTransform frameRect = m_BackgroundFrame.rectTransform;
                if (frameRect != null && frameRect != m_RectTransform)
                {
                    frameRect.anchorMin = Vector2.zero;
                    frameRect.anchorMax = Vector2.one;
                    frameRect.offsetMin = Vector2.zero;
                    frameRect.offsetMax = Vector2.zero;
                }
            }

            if (m_HpFillImage == null)
            {
                Transform valueChild = transform.Find("Value");
                if (valueChild == null)
                {
                    valueChild = transform.Find("Fill");
                }

                if (valueChild != null)
                {
                    m_HpFillImage = valueChild.GetComponent<Image>();
                }
                else
                {
                    GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                    fillObj.transform.SetParent(transform, false);
                    RectTransform fillRect = (RectTransform)fillObj.transform;
                    fillRect.anchorMin = Vector2.zero;
                    fillRect.anchorMax = Vector2.one;
                    fillRect.offsetMin = new Vector2(1f, 1f);
                    fillRect.offsetMax = new Vector2(-1f, -1f);

                    m_HpFillImage = fillObj.GetComponent<Image>();
                    m_HpFillImage.color = new Color(0.2f, 0.9f, 0.25f, 1f);
                    m_HpFillImage.type = Image.Type.Filled;
                    m_HpFillImage.fillMethod = Image.FillMethod.Horizontal;
                }
            }

            if (m_HpFillImage != null)
            {
                m_HpFillImage.type = Image.Type.Filled;
                m_HpFillImage.fillMethod = Image.FillMethod.Horizontal;
                RectTransform fillRect = m_HpFillImage.rectTransform;
                if (fillRect != null)
                {
                    fillRect.anchorMin = Vector2.zero;
                    fillRect.anchorMax = Vector2.one;
                    fillRect.offsetMin = new Vector2(1f, 1f);
                    fillRect.offsetMax = new Vector2(-1f, -1f);
                }
            }

            if (m_ShieldFillImage == null)
            {
                Transform shieldChild = transform.Find("ShieldValue");
                if (shieldChild != null)
                {
                    m_ShieldFillImage = shieldChild.GetComponent<Image>();
                }
            }
        }
    }
}
#endif
