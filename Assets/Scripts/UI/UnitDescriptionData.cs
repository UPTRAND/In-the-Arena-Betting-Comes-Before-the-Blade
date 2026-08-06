#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace InTheArena.UI
{
    [CreateAssetMenu(
        fileName = "UnitDescription_",
        menuName = "In The Arena/UI/Unit Description Data",
        order = 0)]
    public sealed class UnitDescriptionData : ScriptableObject
    {
        [Header("Unit")]
        [SerializeField] private string m_UnitId;
        [SerializeField] private string m_UnitName;
        [SerializeField, TextArea(1, 2)] private string m_Summary;
        [SerializeField] private Sprite m_UnitIcon;

        [Header("Skill")]
        [SerializeField] private Sprite m_SkillIcon;
        [SerializeField] private string m_SkillName;
        [SerializeField, TextArea(2, 4)] private string m_SkillDescription;

        public string UnitId => m_UnitId;
        public string UnitName => m_UnitName;
        public string Summary => m_Summary;
        public Sprite UnitIcon => m_UnitIcon;
        public Sprite SkillIcon => m_SkillIcon;
        public string SkillName => m_SkillName;
        public string SkillDescription => m_SkillDescription;
    }
}
#endif
