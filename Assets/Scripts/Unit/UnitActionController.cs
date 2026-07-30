#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;

namespace InTheArena.Unit
{
    public enum UnitActionState
    {
        Idle = 0,
        Moving = 1,
        Attack = 2,
        Casting = 3,
        Stunned = 4,
        Dead = 5
    }

    /// <summary>
    /// Owns gameplay action priority. Presentation observes this state but never changes it.
    /// </summary>
    [Serializable]
    public sealed class UnitActionController
    {
        private UnitActionState m_State;
        private float m_LockRemaining;

        public UnitActionState State => m_State;
        public float LockRemaining => m_LockRemaining;
        public bool IsAttacking => m_State == UnitActionState.Attack;
        public bool IsCasting => m_State == UnitActionState.Casting;
        public bool IsMoving => m_State == UnitActionState.Moving;
        public bool IsStunned => m_State == UnitActionState.Stunned;
        public bool IsDead => m_State == UnitActionState.Dead;
        public bool CanStartAction =>
            m_State == UnitActionState.Idle || m_State == UnitActionState.Moving;

        public void Reset()
        {
            m_State = UnitActionState.Idle;
            m_LockRemaining = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (m_LockRemaining <= 0f) return;

            m_LockRemaining = Mathf.Max(0f, m_LockRemaining - deltaTime);
            if (m_LockRemaining <= 0.0001f && m_State == UnitActionState.Attack)
            {
                m_State = UnitActionState.Idle;
                m_LockRemaining = 0f;
            }
        }

        public bool TryBeginAttack(float lockDuration)
        {
            if (!CanStartAction) return false;
            m_State = UnitActionState.Attack;
            m_LockRemaining = Mathf.Max(0f, lockDuration);
            return true;
        }

        public bool TryBeginCast(float castDuration)
        {
            if (!CanStartAction) return false;
            m_State = UnitActionState.Casting;
            m_LockRemaining = Mathf.Max(0f, castDuration);
            return true;
        }

        public void CompleteCast()
        {
            if (m_State != UnitActionState.Casting) return;
            m_State = UnitActionState.Idle;
            m_LockRemaining = 0f;
        }

        public void SetMoveIntent(bool moving)
        {
            if (m_State == UnitActionState.Dead ||
                m_State == UnitActionState.Stunned ||
                m_State == UnitActionState.Attack ||
                m_State == UnitActionState.Casting)
                return;

            m_State = moving ? UnitActionState.Moving : UnitActionState.Idle;
        }

        public void SetStunned(bool stunned)
        {
            if (m_State == UnitActionState.Dead) return;
            if (stunned)
            {
                m_State = UnitActionState.Stunned;
                m_LockRemaining = 0f;
            }
            else if (m_State == UnitActionState.Stunned)
            {
                m_State = UnitActionState.Idle;
            }
        }

        public void MarkDead()
        {
            m_State = UnitActionState.Dead;
            m_LockRemaining = 0f;
        }
    }
}
#endif
