#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace InTheArena.Unit
{
    [DefaultExecutionOrder(150)]
    [DisallowMultipleComponent]
    public sealed class HunterWeaponSwitcher : MonoBehaviour
    {
        [SerializeField] private BasicAttackData m_BowAttackData;
        [SerializeField] private BasicAttackData m_DaggerAttackData;
        [SerializeField, Min(0f)] private float m_SwitchToDaggerDistance = 1.4f;
        [SerializeField, Min(0f)] private float m_SwitchToBowDistance = 1.8f;
        [SerializeField, Min(0f)] private float m_BowAttackPower = 16f;
        [SerializeField, Min(0.01f)] private float m_BowAttackSpeed = 0.55f;
        [SerializeField, Min(0f)] private float m_BowAttackRange = 3.5f;
        [SerializeField, Min(0f)] private float m_DaggerAttackPower = 12f;
        [SerializeField, Min(0.01f)] private float m_DaggerAttackSpeed = 1.35f;
        [SerializeField, Min(0f)] private float m_DaggerAttackRange = 1f;

        private Unit m_Unit;
        private bool m_UsingDagger;
        private bool m_HasLoggedMode;
        private bool m_BlockDaggerUntilBowDistance;

        private void Awake()
        {
            m_Unit = GetComponent<Unit>();
        }

        private void OnEnable()
        {
            m_UsingDagger = false;
            m_HasLoggedMode = false;
            m_BlockDaggerUntilBowDistance = false;
        }

        private void Update()
        {
            if (m_Unit == null || m_Unit.IsDead) return;

            Unit target = m_Unit.AI?.CurrentTarget;
            if (target == null || target.IsDead || target.Team == m_Unit.Team ||
                !target.gameObject.activeInHierarchy)
            {
                EquipBow();
                return;
            }

            Vector3 delta = target.GroundPosition - m_Unit.GroundPosition;
            delta.y = 0f;
            float distance = delta.magnitude;

            if (m_UsingDagger)
            {
                if (distance >= m_SwitchToBowDistance)
                {
                    m_BlockDaggerUntilBowDistance = false;
                    EquipBow();
                }
            }
            else if (!m_Unit.IsMoving && distance <= m_SwitchToDaggerDistance)
            {
                if (m_BlockDaggerUntilBowDistance)
                {
                    EquipBow();
                }
                else
                {
                    EquipDagger();
                }
            }
            else if (m_Unit.IsMoving && distance <= m_SwitchToDaggerDistance)
            {
                m_BlockDaggerUntilBowDistance = true;
                EquipBow();
            }
            else
            {
                if (distance >= m_SwitchToBowDistance)
                    m_BlockDaggerUntilBowDistance = false;
                EquipBow();
            }
        }

        private void EquipBow()
        {
            bool alreadyEquipped = !m_UsingDagger && m_Unit.CurrentBasicAttackData == m_BowAttackData;
            if (alreadyEquipped && m_HasLoggedMode) return;

            m_UsingDagger = false;
            if (!alreadyEquipped)
            {
                m_Unit.SetWeaponOverride(
                    m_BowAttackData,
                    m_BowAttackPower,
                    m_BowAttackSpeed,
                    m_BowAttackRange);
            }
            m_Unit.LogHunterModeChange("원거리");
            m_HasLoggedMode = true;
        }

        private void EquipDagger()
        {
            bool alreadyEquipped = m_UsingDagger && m_Unit.CurrentBasicAttackData == m_DaggerAttackData;
            if (alreadyEquipped && m_HasLoggedMode) return;

            m_UsingDagger = true;
            if (!alreadyEquipped)
            {
                m_Unit.SetWeaponOverride(
                    m_DaggerAttackData,
                    m_DaggerAttackPower,
                    m_DaggerAttackSpeed,
                    m_DaggerAttackRange);
            }
            m_Unit.LogHunterModeChange("근접");
            m_HasLoggedMode = true;
        }
    }
}
#endif
