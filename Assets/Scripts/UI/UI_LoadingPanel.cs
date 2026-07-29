#if UNITY_6000_0_OR_NEWER
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    /// <summary>
    /// Loading 씬 및 비동기 씬 전환 시 로딩 상태와 진행률(ProgressBar)을 연동하는 UI 패널 컴포넌트입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UI_LoadingPanel : UI_Base
    {
        [Header("Progress Bar References")]
        [SerializeField] private Image m_ProgressFillImage;
        [SerializeField] private TMP_Text m_ProgressPercentText;

        [Header("Tip UI References")]
        [SerializeField] private TMP_Text m_TipTitleText;
        [SerializeField] private TMP_Text m_TipContentText;

        protected override void Awake()
        {
            base.Awake();
            EnsureReferences();
        }

        private void OnEnable()
        {
            EnsureReferences();
            UpdateProgress(AsyncSceneLoader.LoadingProgress);
        }

        private void Update()
        {
            UpdateProgress(AsyncSceneLoader.LoadingProgress);
        }

        /// <summary>
        /// 진행률(0.0 ~ 1.0)을 받아 UI 요소를 갱신합니다.
        /// </summary>
        public void SetProgress(float progress)
        {
            UpdateProgress(progress);
        }

        private void UpdateProgress(float progress)
        {
            float clampedProgress = Mathf.Clamp01(progress);

            if (m_ProgressFillImage != null)
            {
                m_ProgressFillImage.fillAmount = clampedProgress;
            }

            if (m_ProgressPercentText != null)
            {
                m_ProgressPercentText.text = $"{Mathf.RoundToInt(clampedProgress * 100f)}%";
            }
        }

        private void EnsureReferences()
        {
            if (m_ProgressFillImage == null)
            {
                Transform fillTransform = transform.Find("ProgressBarArea/ProgressFill");
                if (fillTransform == null)
                {
                    fillTransform = transform.Find("CenterContent/ProgressBarArea/ProgressFill");
                }

                if (fillTransform != null)
                {
                    m_ProgressFillImage = fillTransform.GetComponent<Image>();
                }
            }

            if (m_TipContentText == null)
            {
                Transform tipContentTransform = transform.Find("CenterContent/TipGroup/TipContentText");
                if (tipContentTransform != null)
                {
                    m_TipContentText = tipContentTransform.GetComponent<TMP_Text>();
                }
            }

            if (m_TipTitleText == null)
            {
                Transform tipTitleTransform = transform.Find("CenterContent/TipGroup/TipTitleText");
                if (tipTitleTransform != null)
                {
                    m_TipTitleText = tipTitleTransform.GetComponent<TMP_Text>();
                }
            }
        }
    }
}
#endif
