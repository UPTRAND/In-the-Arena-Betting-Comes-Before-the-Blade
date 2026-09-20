#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace InTheArena.Unit
{
    /// <summary>유닛별 스킬 상태와 공통 실행·재시도·해제를 관리합니다.</summary>
    public sealed class SkillRuntime
    {
        private readonly SkillBehaviorRuntime m_Behavior;

        private float m_CurrentCooldown;
        private float m_RetryRemaining;
        private bool m_IsReset;

        public SkillData Data { get; }

        public Unit Owner { get; private set; }

        public float CurrentCooldown => m_CurrentCooldown;
        public float RetryRemaining => m_RetryRemaining;

        public bool CanUse => !m_IsReset && !BattleSimulation.IsBattleFrozen && Data != null && Owner != null &&
            !Owner.IsDead && m_CurrentCooldown <= 0f && m_RetryRemaining <= 0f;

        /// <summary>데이터의 설정을 바탕으로 소유자 전용 행동을 만듭니다.</summary>
        public SkillRuntime(SkillData data, Unit owner)
        {
            Data = data;
            Owner = owner;

            if (data != null && data.ExecutionMode != SkillExecutionMode.EffectsOnly &&
                data.Behavior != null)
            {
                m_Behavior = data.Behavior.CreateRuntime();
                m_Behavior?.Bind(this);
            }
        }

        /// <summary>유효한 소유자의 쿨다운과 시간 기반 행동을 갱신합니다.</summary>
        public void Tick(float deltaTime)
        {
            if (m_IsReset || Owner == null || Owner.IsDead ||
                float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
            {
                return;
            }

            m_CurrentCooldown = Mathf.Max(0f, m_CurrentCooldown - deltaTime);
            m_RetryRemaining = Mathf.Max(0f, m_RetryRemaining - deltaTime);
            m_Behavior?.Tick(this, deltaTime);
        }

        /// <summary>기본 또는 커스텀 타기팅을 실행하고 추가 시전 조건을 검사합니다.</summary>
        public bool TryResolve(in SkillUseRequest request, SkillTargetSet targets)
        {
            if (targets == null)
            {
                return false;
            }

            targets.Clear();
            if (!CanUse)
            {
                return false;
            }

            bool resolved;
            if (m_Behavior != null)
            {
                resolved = m_Behavior.TryResolveTargets(this, request, targets);
            }
            else
            {
                resolved = Data.Targeting != null &&
                    Data.Targeting.TryResolve(Owner, Data, request, targets);
            }

            if (!resolved || (m_Behavior != null && !m_Behavior.CanExecute(this, Owner, targets)))
            {
                targets.Clear();
                return false;
            }

            return true;
        }

        /// <summary>시전 완료 후 효과를 순서대로 실행하고 부분 성공도 쿨다운에 반영합니다.</summary>
        public SkillExecutionResult Execute(SkillTargetSet targets, bool isReaction = false)
        {
            if (!CanUse)
            {
                return SkillExecutionResult.Interrupted;
            }

            bool validTargets = false;
            if (targets != null)
            {
                if (m_Behavior != null)
                {
                    validTargets = m_Behavior.RevalidateTargets(this, targets);
                }
                else if (Data.Targeting != null)
                {
                    validTargets = Data.Targeting.Revalidate(Owner, Data, targets);
                }
            }

            if (!validTargets)
            {
                return Fail(SkillExecutionResult.InvalidTarget);
            }

            if (m_Behavior != null && !m_Behavior.CanExecute(this, Owner, targets))
            {
                return Fail(SkillExecutionResult.NoEffect);
            }

            var context = new SkillEffectContext(this, Owner, targets, isReaction);
            SkillExecutionResult result = SkillExecutionResult.NoEffect;

            if (m_Behavior != null)
            {
                result = m_Behavior.Execute(context);

                // 조건 실패나 자원 부족이면 후속 효과를 실행하지 않습니다.
                if (result != SkillExecutionResult.Success && result != SkillExecutionResult.NoEffect)
                {
                    return Fail(result);
                }
            }

            if (Data.ExecutionMode != SkillExecutionMode.BehaviorOnly)
            {
                var effects = Data.Effects;
                for (int i = 0; effects != null && i < effects.Count; i++)
                {
                    SkillEffectDefinition effect = effects[i];
                    if (effect == null)
                    {
                        continue;
                    }

                    SkillExecutionResult effectResult = effect.Apply(context);
                    if (effectResult == SkillExecutionResult.Success)
                    {
                        result = SkillExecutionResult.Success;
                    }
                    else if (effectResult != SkillExecutionResult.NoEffect)
                    {
                        // 앞 효과가 적용됐다면 이를 취소할 수 없으므로 성공을 보존합니다.
                        if (result != SkillExecutionResult.Success)
                        {
                            result = effectResult;
                        }

                        break;
                    }
                }
            }

            if (result == SkillExecutionResult.Success)
            {
                m_CurrentCooldown = Data.Cooldown;
                m_RetryRemaining = 0f;
                return result;
            }

            return Fail(result);
        }

        /// <summary>일반 패시브는 쿨다운을 적용하고 전투 시작·종료는 항상 전달합니다.</summary>
        public void HandleTrigger(in SkillTriggerContext context)
        {
            if (m_IsReset || Data == null || Owner == null || m_Behavior == null ||
                context.Receiver.Unit != Owner)
            {
                return;
            }

            bool lifecycleEvent = context.Trigger == SkillTriggerType.OnBattleStart ||
                context.Trigger == SkillTriggerType.OnBattleEnd;

            if (!lifecycleEvent && !CanUse)
            {
                return;
            }

            m_Behavior.OnTrigger(this, context);
        }

        /// <summary>패시브가 실제 효과를 적용했을 때 쿨다운을 시작합니다.</summary>
        public void CommitPassiveSuccess()
        {
            if (!m_IsReset && Data != null)
            {
                m_CurrentCooldown = Data.Cooldown;
                m_RetryRemaining = 0f;
            }
        }

        /// <summary>실행 실패 후 매 틱 같은 요청을 반복하지 않도록 지연합니다.</summary>
        public void DelayRetry()
        {
            if (!m_IsReset && Data != null)
            {
                m_RetryRemaining = Data.FailureRetryDelay;
            }
        }

        /// <summary>행동을 한 번만 해제하고 이후 호출에서 재사용되지 않도록 종료합니다.</summary>
        public void Reset()
        {
            if (m_IsReset)
            {
                return;
            }

            m_IsReset = true;
            try
            {
                m_Behavior?.Reset();
            }
            finally
            {
                m_CurrentCooldown = 0f;
                m_RetryRemaining = 0f;
                Owner = null;
            }
        }

        /// <summary>중단 이외의 실패에 재시도 지연을 적용합니다.</summary>
        private SkillExecutionResult Fail(SkillExecutionResult result)
        {
            if (result != SkillExecutionResult.Interrupted)
            {
                DelayRetry();
            }

            return result;
        }
    }
}
#endif
