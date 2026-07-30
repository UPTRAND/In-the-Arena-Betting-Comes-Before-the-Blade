#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace InTheArena.Unit
{
    [CreateAssetMenu(fileName = "AIData_", menuName = "In The Arena/Unit/AI Data", order = 10)]
    public sealed class AIData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string m_AIName = "Default AI";
        [SerializeField, TextArea(2, 4)] private string m_Description;

        [Header("Decision Settings")]
        [SerializeField, Min(0f)] private float m_SearchInterval = 0.5f;
        [SerializeField] private TargetPriorityType m_TargetPriority = TargetPriorityType.Nearest;
        [SerializeField, Min(0f)] private float m_MaxSearchDistance;
        [SerializeField, Range(0f, 1f)] private float m_AttackStopDistanceRatio = 0.9f;
        [SerializeField, Min(0f)] private float m_InitialSearchDelay = 0.1f;

        public string AIName => m_AIName;
        public string Description => m_Description;
        public float SearchInterval => Mathf.Max(0f, m_SearchInterval);
        public TargetPriorityType TargetPriority => m_TargetPriority;
        public float MaxSearchDistance => Mathf.Max(0f, m_MaxSearchDistance);
        public float AttackStopDistanceRatio => Mathf.Clamp01(m_AttackStopDistanceRatio);
        public float InitialSearchDelay => Mathf.Max(0f, m_InitialSearchDelay);

        public UnitDecisionAgent CreateAndInitializeRuntimeAI(Unit owner)
        {
            var agent = new UnitDecisionAgent(this);
            agent.Initialize(owner);
            return agent;
        }

        public bool IsValid()
        {
            if (!string.IsNullOrWhiteSpace(m_AIName)) return true;
            Debug.LogError($"[AIData] {name}: AI name is empty.", this);
            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            m_SearchInterval = Mathf.Max(0f, m_SearchInterval);
            m_MaxSearchDistance = Mathf.Max(0f, m_MaxSearchDistance);
            m_AttackStopDistanceRatio = Mathf.Clamp01(m_AttackStopDistanceRatio);
            m_InitialSearchDelay = Mathf.Max(0f, m_InitialSearchDelay);
        }
#endif
    }
}
#endif
