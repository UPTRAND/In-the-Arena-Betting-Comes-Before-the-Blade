#if UNITY_6000_0_OR_NEWER
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    [DisallowMultipleComponent]
    public sealed class UI_FlyingRewardPreviewView : MonoBehaviour
    {
        [SerializeField] private Image m_RewardIcon;
        [SerializeField] private TMP_Text m_RewardText;

        public void SetReward(Sprite icon, string message)
        {
            ResolveReferences();
            if (m_RewardIcon != null) m_RewardIcon.sprite = icon;
            if (m_RewardText != null)
            {
                if (m_RewardText.font == null) m_RewardText.font = TMP_Settings.defaultFontAsset;
                m_RewardText.text = message;
            }
        }

        private void Awake() => ResolveReferences();

        private void ResolveReferences()
        {
            m_RewardIcon ??= transform.Find("RewardIcon")?.GetComponent<Image>();
            m_RewardText ??= transform.Find("RewardText")?.GetComponent<TMP_Text>();
        }
    }
}
#endif
