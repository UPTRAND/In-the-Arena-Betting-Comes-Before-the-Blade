using UnityEngine;
using TMPro;

namespace InTheArena.UI
{
    public sealed class UI_LobbyUnitRegionSection : MonoBehaviour
    {
        [SerializeField] private TMP_Text m_Title;
        [SerializeField] private RectTransform m_UnitRoot;

        public RectTransform UnitRoot => m_UnitRoot;

        public void Bind(string title)
        {
            if (m_Title != null)
            {
                m_Title.text = title;
            }
        }
    }
}
