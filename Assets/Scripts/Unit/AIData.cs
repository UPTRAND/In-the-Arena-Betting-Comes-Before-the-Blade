#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using MackySoft.SerializeReferenceExtensions;

namespace InTheArena.Unit
{
    /// <summary>
    /// AI 데이터 ScriptableObject
    /// AI 설정(탐색 주기, 타겟 우선순위 등)과 로직(UnitAI_Base 구현체)을 함께 관리
    /// </summary>
    [CreateAssetMenu(fileName = "AIData_", menuName = "In The Arena/Unit/AI Data", order = 10)]
    public class AIData : ScriptableObject
    {
        [Header("AI 기본 설정")]
        [Tooltip("AI 이름")]
        [SerializeField] private string m_AIName = "Default AI";

        [Tooltip("AI 설명")]
        [SerializeField] [TextArea(2, 4)] private string m_Description;

        [Header("AI 로직")]
        [Tooltip("실제 AI 동작을 구현한 UnitAI_Base 상속 클래스")]
        [SerializeReference, SubclassSelector]
        private UnitAI_Base m_AILogic;

        /// <summary> AI 이름 </summary>
        public string AIName => m_AIName;

        /// <summary> AI 설명 </summary>
        public string Description => m_Description;

        /// <summary> AI 로직 </summary>
        public UnitAI_Base AILogic => m_AILogic;

        /// <summary>
        /// 런타임용 AI 로직 인스턴스 생성 (Initialize는 호출자에서 수행)
        /// </summary>
        public UnitAI_Base CreateRuntimeAI()
        {
            if (m_AILogic == null) return null;

            var runtimeAI = m_AILogic.Clone();
            return runtimeAI;
        }

        /// <summary>
        /// 런타임용 AI 로직 인스턴스 생성 및 초기화 (편의 메서드)
        /// </summary>
        public UnitAI_Base CreateAndInitializeRuntimeAI(Unit owner)
        {
            var ai = CreateRuntimeAI();
            if (ai != null)
            {
                ai.Initialize(owner);
            }
            return ai;
        }

        /// <summary>
        /// 데이터 유효성 검사
        /// </summary>
        public bool IsValid()
        {
            bool isValid = true;

            if (string.IsNullOrEmpty(m_AIName))
            {
                Debug.LogError($"[AIData] {name}: AI 이름이 비어있습니다.");
                isValid = false;
            }

            if (m_AILogic == null)
            {
                Debug.LogError($"[AIData] {name}: AI 로직이 할당되지 않았습니다.");
                isValid = false;
            }

            return isValid;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            IsValid();
        }
#endif
    }
}
#endif