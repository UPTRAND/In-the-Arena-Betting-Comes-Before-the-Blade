#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace InTheArena.Unit
{
    /// <summary>
    /// The only unit runtime object allowed to drive Animator state.
    /// Missing optional states are safe fallbacks, not Animator warnings.
    /// </summary>
    public sealed class UnitAnimationPresenter
    {
        private const float MoveStartSpeed = 0.05f;
        private const float MoveStopSpeed = 0.02f;
        private const float CrossFadeDuration = 0.05f;

        private static readonly int IsMovingParameter = Animator.StringToHash("IsMoving");
        private static readonly int AttackState = Animator.StringToHash("Attack");
        private static readonly int SkillState = Animator.StringToHash("Skill");
        private static readonly int HitState = Animator.StringToHash("Hit");
        private static readonly int DeathState = Animator.StringToHash("Death");
        private static readonly int ShieldState = Animator.StringToHash("Shield");

        private readonly Animator m_Animator;
        private readonly bool m_HasIsMoving;
        private bool m_IsMoving;

        public UnitAnimationPresenter(Animator animator)
        {
            m_Animator = animator;
            if (animator == null) return;

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.nameHash == IsMovingParameter &&
                    parameter.type == AnimatorControllerParameterType.Bool)
                {
                    m_HasIsMoving = true;
                    break;
                }
            }
        }

        public void Reset()
        {
            m_IsMoving = false;
            if (m_Animator != null && m_HasIsMoving)
                m_Animator.SetBool(IsMovingParameter, false);
        }

        public void SetActualSpeed(float speed)
        {
            if (m_Animator == null || !m_HasIsMoving) return;

            bool moving = m_IsMoving
                ? speed > MoveStopSpeed
                : speed >= MoveStartSpeed;
            if (moving == m_IsMoving) return;

            m_IsMoving = moving;
            m_Animator.SetBool(IsMovingParameter, moving);
        }

        public void PlayAttack() => TryCrossFade(AttackState);

        public void PlayCast()
        {
            if (!TryCrossFade(SkillState)) TryCrossFade(ShieldState);
        }

        public void PlayDeath() => TryCrossFade(DeathState);

        public void PlayHit()
        {
            if (IsProtectedActionPlaying()) return;
            TryCrossFade(HitState);
        }

        private bool IsProtectedActionPlaying()
        {
            if (m_Animator == null) return false;

            AnimatorStateInfo current = m_Animator.GetCurrentAnimatorStateInfo(0);
            if (current.shortNameHash == AttackState || current.shortNameHash == SkillState ||
                current.shortNameHash == ShieldState)
                return true;

            if (!m_Animator.IsInTransition(0)) return false;
            AnimatorStateInfo next = m_Animator.GetNextAnimatorStateInfo(0);
            return next.shortNameHash == AttackState || next.shortNameHash == SkillState ||
                   next.shortNameHash == ShieldState;
        }

        private bool TryCrossFade(int shortStateHash)
        {
            if (m_Animator == null) return false;

            int fullPathHash = shortStateHash == AttackState
                ? Animator.StringToHash("Base Layer.Attack")
                : shortStateHash == HitState
                    ? Animator.StringToHash("Base Layer.Hit")
                    : shortStateHash == DeathState
                        ? Animator.StringToHash("Base Layer.Death")
                        : shortStateHash == SkillState
                            ? Animator.StringToHash("Base Layer.Skill")
                            : Animator.StringToHash("Base Layer.Shield");

            if (!m_Animator.HasState(0, fullPathHash)) return false;
            m_Animator.CrossFade(fullPathHash, CrossFadeDuration, 0, 0f);
            return true;
        }
    }
}
#endif
