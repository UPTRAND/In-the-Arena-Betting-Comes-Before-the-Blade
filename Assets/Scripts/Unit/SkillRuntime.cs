#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace InTheArena.Unit
{
    public sealed class SkillRuntime
    {
        private readonly SkillBehaviorRuntime m_Behavior;
        private float m_CurrentCooldown;
        private float m_RetryRemaining;

        public SkillRuntime(SkillData data, Unit owner)
        {
            Data = data;
            Owner = owner;
            m_Behavior = data != null ? data.Behavior?.CreateRuntime() : null;
        }

        public SkillData Data { get; }
        public Unit Owner { get; private set; }
        public float CurrentCooldown => m_CurrentCooldown;
        public float RetryRemaining => m_RetryRemaining;
        public bool CanUse => Data != null && Owner != null && !Owner.IsDead &&
                              m_CurrentCooldown <= 0f && m_RetryRemaining <= 0f;

        public void Tick(float deltaTime)
        {
            if (m_CurrentCooldown > 0f)
                m_CurrentCooldown = Mathf.Max(0f, m_CurrentCooldown - deltaTime);
            if (m_RetryRemaining > 0f)
                m_RetryRemaining = Mathf.Max(0f, m_RetryRemaining - deltaTime);
            m_Behavior?.Tick(this, deltaTime);
        }

        public bool TryResolve(in SkillUseRequest request, SkillTargetSet targets)
        {
            if (!CanUse || Data.Targeting == null || targets == null) return false;
            targets.Clear();
            return Data.Targeting.TryResolve(Owner, Data, request, targets);
        }

        public SkillExecutionResult Execute(SkillTargetSet targets, bool isReaction = false)
        {
            if (Data == null || Owner == null || Owner.IsDead)
                return SkillExecutionResult.Interrupted;
            if (Data.Targeting == null || !Data.Targeting.Revalidate(Owner, Data, targets))
                return Fail(SkillExecutionResult.InvalidTarget);
            if (m_Behavior != null && !m_Behavior.CanExecute(this, Owner, targets))
                return Fail(SkillExecutionResult.NoEffect);

            var context = new SkillEffectContext(this, Owner, targets, isReaction);
            SkillExecutionResult behaviorResult = SkillExecutionResult.NoEffect;
            if (Data.ExecutionMode != SkillExecutionMode.EffectsOnly)
                behaviorResult = m_Behavior != null
                    ? m_Behavior.Execute(context)
                    : SkillExecutionResult.NoEffect;

            SkillExecutionResult effectsResult = SkillExecutionResult.NoEffect;
            if (Data.ExecutionMode != SkillExecutionMode.BehaviorOnly)
            {
                var effects = Data.Effects;
                for (int i = 0; effects != null && i < effects.Count; i++)
                {
                    SkillEffectDefinition effect = effects[i];
                    if (effect == null) continue;
                    SkillExecutionResult result = effect.Apply(context);
                    if (result == SkillExecutionResult.PoolExhausted)
                    {
                        if (effectsResult != SkillExecutionResult.Success)
                            return Fail(result);
                        break;
                    }
                    if (result == SkillExecutionResult.Success)
                        effectsResult = SkillExecutionResult.Success;
                }
            }

            SkillExecutionResult finalResult =
                behaviorResult == SkillExecutionResult.Success || effectsResult == SkillExecutionResult.Success
                    ? SkillExecutionResult.Success
                    : behaviorResult != SkillExecutionResult.NoEffect ? behaviorResult : effectsResult;

            if (finalResult == SkillExecutionResult.Success)
                m_CurrentCooldown = Data.Cooldown;
            else
                Fail(finalResult);
            return finalResult;
        }

        public void HandleTrigger(in SkillTriggerContext context)
        {
            if (Data == null || Data.SkillType != SkillType.Passive || !CanUse || m_Behavior == null)
                return;
            m_Behavior.OnTrigger(this, context);
        }

        public void CommitPassiveSuccess()
        {
            if (Data != null) m_CurrentCooldown = Data.Cooldown;
        }

        public void DelayRetry()
        {
            if (Data != null) m_RetryRemaining = Data.FailureRetryDelay;
        }

        public void Reset()
        {
            m_CurrentCooldown = 0f;
            m_RetryRemaining = 0f;
            m_Behavior?.Reset();
            Owner = null;
        }

        private SkillExecutionResult Fail(SkillExecutionResult result)
        {
            if (result != SkillExecutionResult.Interrupted) DelayRetry();
            return result;
        }
    }
}
#endif
