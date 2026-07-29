#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

namespace InTheArena.Unit
{
    /// <summary>
    /// 유닛의 최상위 공개 컴포넌트이자 런타임 상태 소유자입니다.
    /// 개별 Update 없이 UnitSimulationSystem이 AI, 스킬, 상태효과, 이동을 일괄 갱신합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class Unit : MonoBehaviour
    {
        private const string RedTeamTag = "RedTeam";
        private const string BlueTeamTag = "BlueTeam";
        private const float AttackAnimationLock = 0.3f;
        private const float DefaultCastHeight = 0.75f;
        private const float DefaultHitHeight = 0.5f;

        public event Action<float, float> OnHpChanged;
        public event Action<float, Unit, bool> OnDamaged;
        public event Action<float, Unit> OnHealed;
        public event Action<Unit> OnDied;
        public event Action<Skill_Base> OnSkillCastStart;
        public event Action<Skill_Base> OnSkillCastComplete;
        public event Action<Unit> OnAttack;
        public event Action<UnitStatusEffect> OnStatusEffectApplied;
        public event Action<UnitStatusEffect, bool> OnStatusEffectRemoved;
        public event Action<StatusEffectRuntime> OnStatusDataApplied;
        public event Action<StatusEffectRuntime, bool> OnStatusDataRemoved;
        public event Action<float> OnShieldAbsorb;
        public event Action OnMoveStart;
        public event Action OnMoveComplete;

        [Header("컴포넌트")]
        [SerializeField] private Animator m_Animator;
        [SerializeField] private Rigidbody m_Rigidbody;
        [SerializeField] private Collider m_Collider;
        [SerializeField] private AudioSource m_AudioSource;

        [Header("Billboard와 Anchor")]
        [SerializeField] private Transform m_VisualRoot;
        [SerializeField] private Transform m_GroundAnchor;
        [SerializeField] private Transform m_CastAnchor;
        [SerializeField] private Transform m_HitAnchor;

        [Header("시각 효과")]
        [SerializeField] private Material m_HitFlashMaterial;
        [SerializeField] private Material m_OriginalMaterial;
        [SerializeField, HideInInspector] private GameObject m_HpBarPrefab;

        [Header("사운드")]
        [SerializeField] private AudioClip m_HitSound;
        [SerializeField] private AudioClip m_DeathSound;
        [SerializeField] private AudioClip m_AttackSound;

        private UnitData m_UnitData;
        private UnitStat m_BaseStat;
        private UnitStat m_CurrentStat;
        private UnitStat m_StatModifierBuffSum;
        private UnitStat m_StatModifierDebuffSum;
        private float m_CurrentHp;
        private float m_AttackCooldown;
        private float m_AttackLockRemaining;
        private bool m_IsStunned;
        private bool m_IsSilenced;
        private bool m_IsCastingSkill;
        private bool m_IsAttacking;
        private bool m_IsInitialized;
        private bool m_IsRegistered;
        private int m_Team;
        private int m_InstanceId;

        private Vector3 m_MoveTargetPosition;
        private float m_MoveStopDistance;
        private bool m_IsMoving;

        private readonly List<UnitStatusEffect> m_ActiveStatusEffects = new List<UnitStatusEffect>(8);
        private readonly List<UnitStatusEffect> m_PendingRemovalEffects = new List<UnitStatusEffect>(4);
        private readonly List<StatusEffectRuntime> m_ActiveDataEffects = new List<StatusEffectRuntime>(8);
        private readonly List<Skill_Base> m_RuntimeSkills = new List<Skill_Base>(4);
        private Skill_Base m_RuntimeSkill;
        private Skill_Base m_CastingSkill;
        private Unit m_CastingTarget;
        private Vector3 m_CastingPosition;
        private bool m_CastUsesPosition;
        private float m_CastRemaining;
        private UnitAI_Base m_RuntimeAI;

        private SpriteRenderer m_SourceSpriteRenderer;
        private SpriteRenderer m_BillboardSpriteRenderer;
        private Renderer[] m_Renderers = Array.Empty<Renderer>();
        private MaterialPropertyBlock m_MaterialPropertyBlock;
        private float m_HitFlashRemaining;
        private GameObject m_PoolSource;

        public UnitData UnitData => m_UnitData;
        public UnitStat BaseStat => m_BaseStat;
        public UnitStat CurrentStat => m_CurrentStat;
        public float CurrentHp => m_CurrentHp;
        public float MaxHp => m_CurrentStat.maxHp;
        public float CurrentAttackPower => m_CurrentStat.attackPower;
        public float CurrentDefense => m_CurrentStat.defense;
        public float CurrentAttackSpeed => m_CurrentStat.attackSpeed;
        public float CurrentMoveSpeed => m_CurrentStat.moveSpeed;
        public float CurrentAttackRange => m_CurrentStat.attackRange;
        public float AttackInterval => m_CurrentStat.AttackInterval;
        public int Team => m_Team;
        public int InstanceId => m_InstanceId;
        public bool IsDead => m_IsInitialized && m_CurrentHp <= 0f;
        public bool IsStunned => m_IsStunned;
        public bool IsSilenced => m_IsSilenced;
        public bool IsCastingSkill => m_IsCastingSkill;
        public bool IsAttacking => m_IsAttacking;
        public bool IsMoving => m_IsMoving;
        public bool CanAttack => m_IsInitialized && !IsDead && !m_IsStunned &&
                                 !m_IsCastingSkill && !m_IsAttacking && m_AttackCooldown <= 0f;
        public Skill_Base Skill => m_RuntimeSkill;
        public IReadOnlyList<Skill_Base> Skills => m_RuntimeSkills;
        public UnitAI_Base AI => m_RuntimeAI;
        public IReadOnlyList<UnitStatusEffect> ActiveStatusEffects => m_ActiveStatusEffects;
        public IReadOnlyList<StatusEffectRuntime> ActiveDataEffects => m_ActiveDataEffects;
        public Animator Animator => m_Animator;
        public Rigidbody Rigidbody => m_Rigidbody;
        public Collider Collider => m_Collider;
        public Transform VisualRoot => m_VisualRoot;
        public Vector3 GroundPosition => m_GroundAnchor != null ? m_GroundAnchor.position : transform.position;
        public Vector3 CastPosition => m_CastAnchor != null ? m_CastAnchor.position : transform.position + Vector3.up * DefaultCastHeight;
        public Vector3 HitPosition => m_HitAnchor != null ? m_HitAnchor.position : transform.position + Vector3.up * DefaultHitHeight;
        internal GameObject PoolSource => m_PoolSource;

        private void Awake()
        {
            CacheComponents();
            EnsureRuntimeVisualHierarchy();
            m_InstanceId = GetHashCode();
            m_MaterialPropertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (m_IsInitialized) RegisterRuntime();
        }

        private void OnDisable()
        {
            UnregisterRuntime();
        }

        private void OnDestroy()
        {
            UnregisterRuntime();
            ClearAllStatusEffects();
            ClearDataStatusEffects();
        }

        public void Initialize(UnitData data, int team)
        {
            if (data == null)
            {
                Debug.LogError("[Unit] UnitData 없이 초기화할 수 없습니다.", this);
                return;
            }

            UnregisterRuntime();
            ClearAllStatusEffects();
            ClearDataStatusEffects();

            m_UnitData = data;
            m_Team = team;
            m_BaseStat = data.BaseStat;
            m_CurrentStat = m_BaseStat;
            m_CurrentHp = m_BaseStat.maxHp;
            m_StatModifierBuffSum = default;
            m_StatModifierDebuffSum = default;
            m_AttackCooldown = 0f;
            m_AttackLockRemaining = 0f;
            m_IsStunned = false;
            m_IsSilenced = false;
            m_IsCastingSkill = false;
            m_IsAttacking = false;
            m_IsMoving = false;
            m_CastingSkill = null;
            m_RuntimeSkills.Clear();

            IReadOnlyList<SkillData> skillDatas = data.SkillDatas;
            if (skillDatas != null)
            {
                for (int i = 0; i < skillDatas.Count; i++)
                {
                    AddRuntimeSkill(skillDatas[i]);
                }
            }
            if (m_RuntimeSkills.Count == 0) AddRuntimeSkill(data.SkillData);
            m_RuntimeSkill = m_RuntimeSkills.Count > 0 ? m_RuntimeSkills[0] : null;

            m_RuntimeAI = data.CreateRuntimeAI(this);
            SetupComponents();
            EnsureRuntimeVisualHierarchy();
            m_IsInitialized = true;

            IReadOnlyList<StatusEffectData> startingEffects = data.StartingStatusEffects;
            if (startingEffects != null)
            {
                for (int i = 0; i < startingEffects.Count; i++)
                    ApplyStatusEffect(startingEffects[i], this);
            }

            OnHpChanged?.Invoke(m_CurrentHp, MaxHp);
            if (gameObject.activeInHierarchy) RegisterRuntime();
        }

        /// <summary>Pool 및 외부 Factory가 사용하는 명시적 Spawn 진입점입니다.</summary>
        public void Spawn(UnitData data, int team, Vector3 position)
        {
            transform.SetPositionAndRotation(position, Quaternion.identity);
            Initialize(data, team);
            gameObject.SetActive(true);
        }

        /// <summary>유닛을 풀로 반환합니다.</summary>
        public void Despawn()
        {
            UnitPoolService.Return(this);
        }

        public void SetAIActive(bool active)
        {
            if (active) m_RuntimeAI?.Resume();
            else
            {
                m_RuntimeAI?.Pause();
                StopMovement();
            }
        }

        internal void SetPoolSource(GameObject source)
        {
            m_PoolSource = source;
        }

        internal void PrepareForPool()
        {
            SetAIActive(false);
            ClearAllStatusEffects();
            ClearDataStatusEffects();
            StopMovement();
            m_IsInitialized = false;
            m_CurrentHp = 0f;
            m_RuntimeAI = null;
            m_RuntimeSkill = null;
            m_RuntimeSkills.Clear();
            m_CastingSkill = null;
            m_HitFlashRemaining = 0f;
            ResetMaterialFlash();

            OnHpChanged = null;
            OnDamaged = null;
            OnHealed = null;
            OnDied = null;
            OnSkillCastStart = null;
            OnSkillCastComplete = null;
            OnAttack = null;
            OnStatusEffectApplied = null;
            OnStatusEffectRemoved = null;
            OnStatusDataApplied = null;
            OnStatusDataRemoved = null;
            OnShieldAbsorb = null;
            OnMoveStart = null;
            OnMoveComplete = null;
        }

        internal void SimulationTick(float deltaTime)
        {
            if (!m_IsInitialized || IsDead) return;

            if (m_AttackCooldown > 0f) m_AttackCooldown = Mathf.Max(0f, m_AttackCooldown - deltaTime);
            if (m_AttackLockRemaining > 0f)
            {
                m_AttackLockRemaining -= deltaTime;
                if (m_AttackLockRemaining <= 0f) m_IsAttacking = false;
            }

            for (int i = 0; i < m_RuntimeSkills.Count; i++)
                m_RuntimeSkills[i]?.TickCooldown(deltaTime);

            UpdateCasting(deltaTime);
            UpdateStatusEffects(deltaTime);
            UpdateDataStatusEffects(deltaTime);
            m_RuntimeAI?.UpdateAI(deltaTime);
        }

        internal void SimulationFrame(float deltaTime)
        {
            if (!m_IsInitialized || IsDead) return;
            UpdateMovement(deltaTime);

            if (m_HitFlashRemaining > 0f)
            {
                m_HitFlashRemaining -= deltaTime;
                if (m_HitFlashRemaining <= 0f) ResetMaterialFlash();
            }
        }

        internal void ApplyBillboard(Quaternion cameraRotation)
        {
            if (m_VisualRoot == null) return;
            m_VisualRoot.rotation = cameraRotation;

            if (m_SourceSpriteRenderer != null && m_BillboardSpriteRenderer != null &&
                m_SourceSpriteRenderer != m_BillboardSpriteRenderer)
            {
                m_BillboardSpriteRenderer.sprite = m_SourceSpriteRenderer.sprite;
                m_BillboardSpriteRenderer.color = m_SourceSpriteRenderer.color;
                m_BillboardSpriteRenderer.flipX = m_SourceSpriteRenderer.flipX;
                m_BillboardSpriteRenderer.flipY = m_SourceSpriteRenderer.flipY;
                m_BillboardSpriteRenderer.enabled = m_SourceSpriteRenderer.gameObject.activeInHierarchy;
            }
        }

        public float ApplyDamage(float damage, Unit attacker = null, bool isCritical = false, bool isSkillDamage = false)
        {
            if (IsDead || damage <= 0f) return 0f;

            float finalDamage = damage;
            for (int i = 0; i < m_ActiveStatusEffects.Count; i++)
            {
                UnitStatusEffect effect = m_ActiveStatusEffects[i];
                if (effect.Category == StatusEffectCategory.Shield && effect is Buff_Shield shield)
                {
                    finalDamage = shield.AbsorbDamage(finalDamage);
                    if (finalDamage <= 0f) break;
                }
            }

            if (finalDamage > 0f)
            {
                finalDamage = Mathf.Max(1f, finalDamage - m_CurrentStat.defense);
                if (isCritical) finalDamage *= 1.5f;
                m_CurrentHp = Mathf.Max(0f, m_CurrentHp - finalDamage);
            }

            OnDamaged?.Invoke(finalDamage, attacker, isCritical);
            TriggerPassiveSkills(PassiveTriggerType.OnHit, attacker);
            OnHpChanged?.Invoke(m_CurrentHp, MaxHp);
            PlayHitEffect();
            UnitHpBarPresenter.NotifyDamaged(this);

            if (m_CurrentHp <= 0f)
            {
                Die(attacker);
                attacker?.TriggerPassiveSkills(PassiveTriggerType.OnKill, this);
            }
            return finalDamage;
        }

        public float Heal(float amount, Unit caster = null)
        {
            if (IsDead || amount <= 0f) return 0f;
            float previous = m_CurrentHp;
            m_CurrentHp = Mathf.Min(MaxHp, m_CurrentHp + amount);
            float actual = m_CurrentHp - previous;
            if (actual <= 0f) return 0f;

            OnHealed?.Invoke(actual, caster);
            OnHpChanged?.Invoke(m_CurrentHp, MaxHp);
            m_Animator?.SetTrigger("Heal");
            UnitHpBarPresenter.NotifyDamaged(this);
            return actual;
        }

        private void Die(Unit killer)
        {
            if (!m_IsInitialized) return;
            ClearAllStatusEffects();
            ClearDataStatusEffects();
            m_RuntimeAI?.Deactivate();
            StopMovement();
            if (m_Collider != null) m_Collider.enabled = false;
            UnitRegistry.NotifyDeath(this);
            OnDied?.Invoke(killer);
            PlayClip(m_DeathSound);
            m_Animator?.SetTrigger("Die");
            gameObject.SetActive(false);
        }

        public void Attack(Unit target)
        {
            if (!CanAttack || target == null || target.IsDead || target.Team == Team) return;
            m_IsAttacking = true;
            m_AttackCooldown = AttackInterval;
            m_AttackLockRemaining = AttackAnimationLock;
            m_Animator?.SetTrigger("Attack");
            PlayClip(m_AttackSound);
            OnAttack?.Invoke(target);

            float damage = m_CurrentStat.attackPower;
            bool critical = UnityEngine.Random.value < 0.05f;
            float actualDamage = target.ApplyDamage(damage, this, critical, false);
            TriggerPassiveSkills(PassiveTriggerType.OnAttack, actualDamage);
        }

        public bool TryUseSkill(Unit target = null)
        {
            if (m_IsSilenced || m_IsStunned || m_IsCastingSkill || IsDead) return false;

            for (int i = 0; i < m_RuntimeSkills.Count; i++)
            {
                Skill_Base skill = m_RuntimeSkills[i];
                if (skill == null || skill.SkillType != SkillType.Active || !skill.CanUse()) continue;
                if (!IsSkillTargetValid(skill, target)) continue;
                UseSkill(skill, target);
                return true;
            }
            return false;
        }

        public void UseSkill(Skill_Base skill, Unit target = null)
        {
            if (skill == null || !skill.CanUse() || m_IsSilenced || m_IsCastingSkill || IsDead || m_IsStunned)
                return;
            if (!IsSkillTargetValid(skill, target)) return;

            BeginCast(skill, target, default, false);
        }

        public void UseSkill(Skill_Base skill, Vector3 position)
        {
            if (skill == null || !skill.CanUse() || m_IsSilenced || m_IsCastingSkill || IsDead || m_IsStunned)
                return;
            position.y = GroundPosition.y;
            BeginCast(skill, null, position, true);
        }

        private void BeginCast(Skill_Base skill, Unit target, Vector3 position, bool usesPosition)
        {
            m_IsCastingSkill = true;
            m_IsAttacking = false;
            m_CastingSkill = skill;
            m_CastingTarget = target;
            m_CastingPosition = position;
            m_CastUsesPosition = usesPosition;
            m_CastRemaining = skill.CastTime;
            m_Animator?.SetTrigger("CastSkill");
            OnSkillCastStart?.Invoke(skill);
            if (m_CastRemaining <= 0f) CompleteCast();
        }

        private void UpdateCasting(float deltaTime)
        {
            if (!m_IsCastingSkill || m_CastingSkill == null) return;
            if (m_IsStunned || IsDead)
            {
                CancelCast();
                return;
            }

            m_CastRemaining -= deltaTime;
            if (m_CastRemaining <= 0f) CompleteCast();
        }

        private void CompleteCast()
        {
            Skill_Base skill = m_CastingSkill;
            Unit target = m_CastingTarget;
            Vector3 position = m_CastingPosition;
            bool usesPosition = m_CastUsesPosition;
            CancelCast();

            if (usesPosition) skill.Execute(this, position);
            else skill.Execute(this, target);
            skill.ResetCooldown();
            OnSkillCastComplete?.Invoke(skill);
        }

        private void CancelCast()
        {
            m_IsCastingSkill = false;
            m_CastingSkill = null;
            m_CastingTarget = null;
            m_CastRemaining = 0f;
        }

        private bool IsSkillTargetValid(Skill_Base skill, Unit target)
        {
            if (skill.TargetType == SkillTargetType.Self || skill.TargetType == SkillTargetType.Ground)
                return true;
            if (target == null || target.IsDead) return false;

            bool requiresEnemy = skill.TargetType == SkillTargetType.Enemy ||
                                 skill.TargetType == SkillTargetType.Enemies;
            if (requiresEnemy && target.Team == Team) return false;
            if (!requiresEnemy && target.Team != Team) return false;

            Vector3 delta = target.GroundPosition - GroundPosition;
            delta.y = 0f;
            return skill.SkillRange <= 0f || delta.sqrMagnitude <= skill.SkillRange * skill.SkillRange;
        }

        public UnitStatusEffect ApplyStatusEffect(UnitStatusEffect effectData, Unit caster = null, float durationOverride = -1f)
        {
            if (effectData == null || IsDead) return null;
            if (effectData is Debuff_Base debuff && UnityEngine.Random.value < debuff.CalculateResistance(this))
                return null;

            UnitStatusEffect existing = FindActiveEffect(effectData.GetType());
            if (existing != null)
            {
                int stacks = existing.CurrentStacks;
                if ((effectData.StackType == StackType.Intensity || effectData.StackType == StackType.Both) &&
                    stacks < effectData.MaxStacks)
                    stacks++;
                existing.OnStackRefreshed(stacks, durationOverride > 0f ? durationOverride : effectData.BaseDuration);
                return existing;
            }

            UnitStatusEffect runtime = effectData.Clone();
            runtime.Initialize(this, caster, durationOverride);
            m_ActiveStatusEffects.Add(runtime);
            OnStatusEffectApplied?.Invoke(runtime);
            return runtime;
        }

        public StatusEffectRuntime ApplyStatusEffect(StatusEffectData data, Unit caster = null, float durationOverride = -1f)
        {
            if (data == null || IsDead) return null;

            for (int i = 0; i < m_ActiveDataEffects.Count; i++)
            {
                StatusEffectRuntime existing = m_ActiveDataEffects[i];
                if (existing.Data != data) continue;
                existing.Refresh(durationOverride);
                return existing;
            }

            StatusEffectRuntime runtime = StatusEffectRuntimePool.Rent();
            runtime.Initialize(data, this, caster, durationOverride);
            m_ActiveDataEffects.Add(runtime);
            OnStatusDataApplied?.Invoke(runtime);
            return runtime;
        }

        public void RemoveStatusEffect(UnitStatusEffect effect, bool expired = false)
        {
            if (effect != null && !m_PendingRemovalEffects.Contains(effect))
                m_PendingRemovalEffects.Add(effect);
        }

        public void RemoveStatusEffect(StatusEffectRuntime effect, bool expired = false)
        {
            if (effect == null) return;
            int index = m_ActiveDataEffects.IndexOf(effect);
            if (index < 0) return;
            m_ActiveDataEffects.RemoveAt(index);
            OnStatusDataRemoved?.Invoke(effect, expired);
            effect.Release(expired);
            StatusEffectRuntimePool.Return(effect);
        }

        public T FindActiveEffect<T>() where T : UnitStatusEffect
        {
            for (int i = 0; i < m_ActiveStatusEffects.Count; i++)
                if (m_ActiveStatusEffects[i] is T typed) return typed;
            return null;
        }

        public UnitStatusEffect FindActiveEffect(Type type)
        {
            for (int i = 0; i < m_ActiveStatusEffects.Count; i++)
                if (m_ActiveStatusEffects[i].GetType() == type) return m_ActiveStatusEffects[i];
            return null;
        }

        public List<UnitStatusEffect> FindActiveEffects(StatusEffectCategory category)
        {
            var result = new List<UnitStatusEffect>();
            for (int i = 0; i < m_ActiveStatusEffects.Count; i++)
                if (m_ActiveStatusEffects[i].Category == category) result.Add(m_ActiveStatusEffects[i]);
            return result;
        }

        public void ClearAllStatusEffects()
        {
            for (int i = m_ActiveStatusEffects.Count - 1; i >= 0; i--)
            {
                UnitStatusEffect effect = m_ActiveStatusEffects[i];
                if (effect == null) continue;
                effect.Remove(false);
                if (Application.isPlaying) Destroy(effect);
            }
            m_ActiveStatusEffects.Clear();
            m_PendingRemovalEffects.Clear();
        }

        internal void SetStunned(bool stunned)
        {
            m_IsStunned = stunned;
            m_Animator?.SetBool("Stunned", stunned);
            if (!stunned) return;
            StopMovement();
            m_IsAttacking = false;
            CancelCast();
        }

        internal void SetSilenced(bool silenced)
        {
            m_IsSilenced = silenced;
            if (silenced) CancelCast();
        }

        internal void OnShieldAbsorbCallback(float amount) => OnShieldAbsorb?.Invoke(amount);

        internal void ApplyStatModifier(UnitStat modifier, bool isBuff)
        {
            if (isBuff) m_StatModifierBuffSum += modifier;
            else m_StatModifierDebuffSum += modifier;
            RecalculateStats();
        }

        internal void RemoveStatModifier(UnitStat modifier, bool isBuff)
        {
            if (isBuff) m_StatModifierBuffSum -= modifier;
            else m_StatModifierDebuffSum -= modifier;
            RecalculateStats();
        }

        public void MoveTo(Vector3 targetPosition, float stopDistance = 0f)
        {
            if (IsDead || m_IsStunned) return;
            targetPosition.y = transform.position.y;
            bool wasMoving = m_IsMoving;
            m_MoveTargetPosition = targetPosition;
            m_MoveStopDistance = Mathf.Max(0f, stopDistance);
            m_IsMoving = true;
            if (!wasMoving) OnMoveStart?.Invoke();
        }

        public void StopMovement()
        {
            if (!m_IsMoving) return;
            m_IsMoving = false;
            if (m_Rigidbody != null) m_Rigidbody.linearVelocity = Vector3.zero;
            OnMoveComplete?.Invoke();
        }

        private void UpdateMovement(float deltaTime)
        {
            if (!m_IsMoving || m_CurrentStat.moveSpeed <= 0f) return;

            Vector3 current = transform.position;
            Vector3 delta = m_MoveTargetPosition - current;
            delta.y = 0f;
            float stopDistance = m_MoveStopDistance;
            if (delta.sqrMagnitude <= stopDistance * stopDistance)
            {
                StopMovement();
                return;
            }

            Vector3 direction = delta.normalized;
            Vector3 separation = UnitRegistry.CalculateSeparation(
                this,
                (m_UnitData?.VisualRadius ?? 0.5f) * 2f);
            direction = Vector3.Normalize(direction + separation * 0.35f);
            float distance = Mathf.Min(delta.magnitude - stopDistance, m_CurrentStat.moveSpeed * deltaTime);
            transform.position = current + direction * Mathf.Max(0f, distance);

            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        private void UpdateStatusEffects(float deltaTime)
        {
            for (int i = m_ActiveStatusEffects.Count - 1; i >= 0; i--)
            {
                UnitStatusEffect effect = m_ActiveStatusEffects[i];
                bool forced = m_PendingRemovalEffects.Contains(effect);
                if (!forced && effect.Tick(deltaTime)) continue;

                effect.Remove(!forced);
                m_ActiveStatusEffects.RemoveAt(i);
                OnStatusEffectRemoved?.Invoke(effect, !forced);
                Destroy(effect);
            }
            m_PendingRemovalEffects.Clear();
        }

        private void UpdateDataStatusEffects(float deltaTime)
        {
            for (int i = m_ActiveDataEffects.Count - 1; i >= 0; i--)
            {
                StatusEffectRuntime effect = m_ActiveDataEffects[i];
                if (effect.Tick(deltaTime)) continue;

                m_ActiveDataEffects.RemoveAt(i);
                OnStatusDataRemoved?.Invoke(effect, true);
                effect.Release(true);
                StatusEffectRuntimePool.Return(effect);
            }
        }

        private void ClearDataStatusEffects()
        {
            for (int i = m_ActiveDataEffects.Count - 1; i >= 0; i--)
            {
                StatusEffectRuntime effect = m_ActiveDataEffects[i];
                effect.Release(false);
                StatusEffectRuntimePool.Return(effect);
            }
            m_ActiveDataEffects.Clear();
        }

        private void RecalculateStats()
        {
            float hpRatio = MaxHp > 0f ? m_CurrentHp / MaxHp : 1f;
            m_CurrentStat = m_BaseStat + m_StatModifierBuffSum - m_StatModifierDebuffSum;
            m_CurrentStat.maxHp = Mathf.Max(1f, m_CurrentStat.maxHp);
            m_CurrentStat.attackPower = Mathf.Max(0f, m_CurrentStat.attackPower);
            m_CurrentStat.defense = Mathf.Max(0f, m_CurrentStat.defense);
            m_CurrentStat.attackSpeed = Mathf.Max(0.01f, m_CurrentStat.attackSpeed);
            m_CurrentStat.moveSpeed = Mathf.Max(0f, m_CurrentStat.moveSpeed);
            m_CurrentStat.attackRange = Mathf.Max(0f, m_CurrentStat.attackRange);
            m_CurrentHp = Mathf.Clamp(m_CurrentStat.maxHp * hpRatio, 0f, m_CurrentStat.maxHp);
        }

        private void AddRuntimeSkill(SkillData skillData)
        {
            if (skillData == null || skillData.SkillLogic == null) return;
            Skill_Base runtime = skillData.SkillLogic.Clone();
            runtime.SetData(skillData);
            runtime.Initialize(this);
            m_RuntimeSkills.Add(runtime);
        }

        private void TriggerPassiveSkills(PassiveTriggerType triggerType, object parameter)
        {
            for (int i = 0; i < m_RuntimeSkills.Count; i++)
            {
                Skill_Base skill = m_RuntimeSkills[i];
                if (skill != null && skill.SkillType == SkillType.Passive)
                    skill.OnTrigger(this, triggerType, parameter);
            }
        }

        private void SetupComponents()
        {
            string teamName = m_Team == 0 ? RedTeamTag : BlueTeamTag;
            gameObject.tag = teamName;
            int teamLayer = LayerMask.NameToLayer(teamName);
            if (teamLayer >= 0) SetLayerRecursively(transform, teamLayer);

            if (m_Rigidbody != null)
            {
                m_Rigidbody.isKinematic = true;
                m_Rigidbody.useGravity = false;
                m_Rigidbody.constraints = RigidbodyConstraints.FreezeRotationX |
                                          RigidbodyConstraints.FreezeRotationZ |
                                          RigidbodyConstraints.FreezePositionY;
            }
            if (m_Collider != null)
            {
                m_Collider.enabled = true;
                m_Collider.isTrigger = true;
            }
        }

        private void CacheComponents()
        {
            if (m_Animator == null) m_Animator = GetComponentInChildren<Animator>(true);
            if (m_Rigidbody == null) m_Rigidbody = GetComponent<Rigidbody>();
            if (m_Collider == null) m_Collider = GetComponent<Collider>();
            if (m_AudioSource == null) m_AudioSource = GetComponent<AudioSource>();
            if (m_AudioSource == null && Application.isPlaying) m_AudioSource = gameObject.AddComponent<AudioSource>();

            m_VisualRoot ??= transform.Find("VisualRoot");
            m_GroundAnchor ??= transform.Find("GroundAnchor");
            m_CastAnchor ??= transform.Find("CastAnchor");
            m_HitAnchor ??= transform.Find("HitAnchor");
        }

        private void EnsureRuntimeVisualHierarchy()
        {
            if (!Application.isPlaying) return;

            m_SourceSpriteRenderer ??= GetComponent<SpriteRenderer>();
            if (m_VisualRoot == null)
            {
                var visual = new GameObject("VisualRoot_Runtime");
                m_VisualRoot = visual.transform;
                m_VisualRoot.SetParent(transform, false);
            }

            m_BillboardSpriteRenderer = m_VisualRoot.GetComponentInChildren<SpriteRenderer>(true);
            if (m_BillboardSpriteRenderer == null && m_SourceSpriteRenderer != null)
            {
                m_BillboardSpriteRenderer = m_VisualRoot.gameObject.AddComponent<SpriteRenderer>();
                CopySpriteRenderer(m_SourceSpriteRenderer, m_BillboardSpriteRenderer);
                if (m_SourceSpriteRenderer.sprite != null)
                {
                    m_BillboardSpriteRenderer.transform.localPosition =
                        Vector3.up * m_SourceSpriteRenderer.sprite.bounds.extents.y;
                }
                m_SourceSpriteRenderer.enabled = false;
            }

            EnsureAnchor(ref m_GroundAnchor, "GroundAnchor", 0f);
            EnsureAnchor(ref m_CastAnchor, "CastAnchor", DefaultCastHeight);
            EnsureAnchor(ref m_HitAnchor, "HitAnchor", DefaultHitHeight);
            m_Renderers = m_VisualRoot.GetComponentsInChildren<Renderer>(true);
        }

        private void EnsureAnchor(ref Transform anchor, string anchorName, float height)
        {
            if (anchor != null) return;
            var anchorObject = new GameObject(anchorName + "_Runtime");
            anchor = anchorObject.transform;
            anchor.SetParent(transform, false);
            anchor.localPosition = Vector3.up * height;
        }

        private static void CopySpriteRenderer(SpriteRenderer source, SpriteRenderer destination)
        {
            destination.sprite = source.sprite;
            destination.sharedMaterials = source.sharedMaterials;
            destination.color = source.color;
            destination.flipX = source.flipX;
            destination.flipY = source.flipY;
            destination.sortingLayerID = source.sortingLayerID;
            destination.sortingOrder = source.sortingOrder;
            destination.maskInteraction = source.maskInteraction;
        }

        private void PlayHitEffect()
        {
            m_HitFlashRemaining = 0.1f;
            for (int i = 0; i < m_Renderers.Length; i++)
            {
                Renderer renderer = m_Renderers[i];
                if (renderer == null) continue;
                renderer.GetPropertyBlock(m_MaterialPropertyBlock);
                m_MaterialPropertyBlock.SetFloat(Shader.PropertyToID("_FlashAmount"), 1f);
                renderer.SetPropertyBlock(m_MaterialPropertyBlock);
            }
            PlayClip(m_HitSound);
            m_Animator?.SetTrigger("Hit");
        }

        private void ResetMaterialFlash()
        {
            if (m_MaterialPropertyBlock == null) return;
            for (int i = 0; i < m_Renderers.Length; i++)
            {
                Renderer renderer = m_Renderers[i];
                if (renderer == null) continue;
                renderer.GetPropertyBlock(m_MaterialPropertyBlock);
                m_MaterialPropertyBlock.SetFloat(Shader.PropertyToID("_FlashAmount"), 0f);
                renderer.SetPropertyBlock(m_MaterialPropertyBlock);
            }
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip != null && m_AudioSource != null) m_AudioSource.PlayOneShot(clip);
        }

        private void RegisterRuntime()
        {
            if (m_IsRegistered || !m_IsInitialized || IsDead) return;
            UnitSimulationSystem.Register(this);
            m_IsRegistered = true;
        }

        private void UnregisterRuntime()
        {
            if (!m_IsRegistered) return;
            UnitSimulationSystem.Unregister(this);
            m_IsRegistered = false;
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i), layer);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheComponents();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, m_CurrentStat.attackRange > 0f ? m_CurrentStat.attackRange : 2f);
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(GroundPosition, 0.08f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(CastPosition, 0.08f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(HitPosition, 0.08f);
        }
#endif
    }
}
#endif
