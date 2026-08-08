#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
using InTheArena.UI;
using InTheArena.Battlefield;

namespace InTheArena.Unit
{
    /// <summary>
    /// 유닛의 최상위 공개 컴포넌트이자 런타임 상태 소유자입니다.
    /// 개별 Update 없이 BattleSimulation이 AI, 스킬, 상태효과, 이동을 일괄 갱신합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class Unit : MonoBehaviour, IPoolLifecycle
    {
        private const string RedTeamTag = "RedTeam";
        private const string BlueTeamTag = "BlueTeam";
        private const float MinimumAttackAnimationLock = 0.3f;
        private const float ProjectileReleaseFrameOffset = 3f;
        private const float DefaultAnimationFrameRate = 12f;
        private const float DefaultCastHeight = 0.75f;
        private const float DefaultHitHeight = 0.5f;

        public event Action<float, float> OnHpChanged;
        public event Action<float, Unit, bool> OnDamaged;
        public event Action<float, Unit> OnHealed;
        public event Action<Unit> OnDied;
        public event Action<SkillRuntime> OnSkillCastStart;
        public event Action<SkillRuntime> OnSkillCastComplete;
        public event Action<Unit> OnAttack;
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
        [SerializeField] private Sprite[] m_RedTeamSprites = Array.Empty<Sprite>();

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
        private UnitStat m_WeaponStatOverride;
        private bool m_HasWeaponStatOverride;
        private BasicAttackData m_BasicAttackOverride;
        private float m_CurrentHp;
        private float m_AttackCooldown;
        private BasicAttackData m_PendingProjectileAttackData;
        private UnitHandle m_PendingProjectileAttackTarget;
        private float m_PendingProjectileAttackRemaining;
        private bool m_IsSilenced;
        private bool m_IsInitialized;
        private bool m_IsRegistered;
        private bool m_HoldDeathPresentation;
        private int m_Team;
        private int m_InstanceId;
        private int m_SpawnVersion;
        private int m_CombatLogNumber;

        private Vector3 m_MoveTargetPosition;
        private float m_MoveStopDistance;
        private Vector3 m_FacingDirection;
        private Vector3 m_PreviousSimulationPosition;
        private Vector3 m_SimulationPosition;
        private readonly UnitActionController m_ActionController = new UnitActionController();
        private UnitAnimationPresenter m_AnimationPresenter;

        private readonly List<StatusEffectRuntime> m_ActiveDataEffects = new List<StatusEffectRuntime>(8);
        private readonly List<SkillRuntime> m_RuntimeSkills = new List<SkillRuntime>(8);
        private readonly SkillTargetSet m_CastingTargets = new SkillTargetSet();
        private SkillRuntime m_RuntimeSkill;
        private SkillRuntime m_CastingSkill;
        private float m_CastRemaining;
        private UnitDecisionAgent m_RuntimeAI;

        private SpriteRenderer m_SourceSpriteRenderer;
        private SpriteRenderer m_BillboardSpriteRenderer;
        private Renderer[] m_Renderers = Array.Empty<Renderer>();
        private MaterialPropertyBlock m_MaterialPropertyBlock;
        private float m_HitFlashRemaining;
        private GameObject m_PoolSource;

        public UnitData UnitData => m_UnitData;
        public UnitStat BaseStat => m_BaseStat;
        public UnitStat CurrentStat => m_CurrentStat;
        public BasicAttackData CurrentBasicAttackData =>
            m_BasicAttackOverride != null ? m_BasicAttackOverride : m_UnitData?.BasicAttackData;
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
        public int SpawnVersion => m_SpawnVersion;
        public bool IsDead => m_IsInitialized && m_CurrentHp <= 0f;
        public UnitActionState ActionState => m_ActionController.State;
        public bool IsStunned => m_ActionController.IsStunned;
        public bool IsSilenced => m_IsSilenced;
        public bool IsCastingSkill => m_ActionController.IsCasting;
        public bool IsAttacking => m_ActionController.IsAttacking;
        public bool IsMoving => m_ActionController.IsMoving;
        /// <summary>현재 유닛이 바라보는 월드 방향 벡터 (y=0).</summary>
        public Vector3 FacingDirection => m_FacingDirection;
        public Vector3 SimulationPosition => m_SimulationPosition;
        public UnitRuntime Runtime => new UnitRuntime(
            m_InstanceId,
            m_SpawnVersion,
            m_Team,
            m_CurrentHp,
            m_CurrentStat,
            m_SimulationPosition,
            (m_SimulationPosition - m_PreviousSimulationPosition) * 20f,
            new UnitHandle(m_RuntimeAI?.CurrentTarget),
            m_ActionController.State,
            m_AttackCooldown);
        internal bool IsDeathPresentationHeld => m_HoldDeathPresentation;
        public bool CanAttack => m_IsInitialized && !IsDead &&
                                 m_ActionController.CanStartAction && m_AttackCooldown <= 0f;
        public SkillRuntime Skill => m_RuntimeSkill;
        public IReadOnlyList<SkillRuntime> Skills => m_RuntimeSkills;
        public UnitDecisionAgent AI => m_RuntimeAI;
        public UI_UnitHPBar HpBar { get; set; }
        public IReadOnlyList<StatusEffectRuntime> ActiveDataEffects => m_ActiveDataEffects;
        public Animator Animator => m_Animator;
        public Rigidbody Rigidbody => m_Rigidbody;
        public Collider Collider => m_Collider;
        public Transform VisualRoot => m_VisualRoot;
        public Vector3 GroundPosition => Application.isPlaying && m_IsInitialized
            ? m_SimulationPosition
            : m_GroundAnchor != null ? m_GroundAnchor.position : transform.position;
        public Vector3 CastPosition => m_CastAnchor != null ? m_CastAnchor.position : transform.position + Vector3.up * DefaultCastHeight;
        public Vector3 HitPosition => m_HitAnchor != null ? m_HitAnchor.position : transform.position + Vector3.up * DefaultHitHeight;
        internal GameObject PoolSource => m_PoolSource;

        private void Awake()
        {
            CacheComponents();
            m_AnimationPresenter = new UnitAnimationPresenter(m_Animator);
            m_ActionController.Reset();
            m_AnimationPresenter.Reset();
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
            ClearDataStatusEffects();
            if (HpBar != null)
            {
                HpBar.HideHpBar();
                HpBar = null;
            }
        }

        public void Initialize(UnitData data, int team)
        {
            if (data == null)
            {
                Debug.LogError("[Unit] UnitData 없이 초기화할 수 없습니다.", this);
                return;
            }

            UnregisterRuntime();
            ClearDataStatusEffects();
            ResetRuntimeSkills();

            m_UnitData = data;
            m_SpawnVersion++;
            if (m_SpawnVersion == 0) m_SpawnVersion = 1;
            m_Team = team;
            m_BaseStat = data.BaseStat;
            m_CurrentStat = m_BaseStat;
            m_CurrentHp = m_BaseStat.maxHp;
            m_StatModifierBuffSum = default;
            m_StatModifierDebuffSum = default;
            m_WeaponStatOverride = default;
            m_HasWeaponStatOverride = false;
            m_BasicAttackOverride = null;
            m_AttackCooldown = 0f;
            ClearPendingProjectileAttack();
            m_IsSilenced = false;
            m_ActionController.Reset();
            m_AnimationPresenter ??= new UnitAnimationPresenter(m_Animator);
            m_AnimationPresenter.Reset();
            Vector3 initialPosition = ClampToBattlefield(transform.position);
            transform.position = initialPosition;
            m_PreviousSimulationPosition = initialPosition;
            m_SimulationPosition = initialPosition;
            m_FacingDirection = transform.forward;
            m_HoldDeathPresentation = false;
            m_CastingSkill = null;
            m_CastingTargets.Clear();

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
            PoolManager.Require().Units.Return(this);
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

        internal void AssignSimulationId(int unitId)
        {
            m_InstanceId = unitId;
        }

        internal void AssignCombatLogNumber(int number)
        {
            m_CombatLogNumber = Mathf.Max(1, number);
        }

        internal void PrepareForPool()
        {
            SetAIActive(false);
            ClearDataStatusEffects();
            ResetRuntimeSkills();
            StopMovement();
            m_ActionController.Reset();
            m_AnimationPresenter?.Reset();
            m_IsInitialized = false;
            m_CurrentHp = 0f;
            m_HoldDeathPresentation = false;
            m_RuntimeAI = null;
            m_RuntimeSkill = null;
            m_CastingSkill = null;
            m_WeaponStatOverride = default;
            m_HasWeaponStatOverride = false;
            m_BasicAttackOverride = null;
            ClearPendingProjectileAttack();
            m_CombatLogNumber = 0;
            m_CastingTargets.Clear();
            m_HitFlashRemaining = 0f;
            ResetMaterialFlash();

            if (HpBar != null)
            {
                HpBar.HideHpBar();
                HpBar = null;
            }

            OnHpChanged = null;
            OnDamaged = null;
            OnHealed = null;
            OnDied = null;
            OnSkillCastStart = null;
            OnSkillCastComplete = null;
            OnAttack = null;
            OnStatusDataApplied = null;
            OnStatusDataRemoved = null;
            OnShieldAbsorb = null;
            OnMoveStart = null;
            OnMoveComplete = null;
        }

        public void OnPoolRent(in PoolSpawnContext context) { }
        public void OnPoolReturn() => PrepareForPool();

        internal void SimulationTick(float deltaTime)
        {
            if (!m_IsInitialized || IsDead) return;

            if (m_AttackCooldown > 0f) m_AttackCooldown = Mathf.Max(0f, m_AttackCooldown - deltaTime);
            m_ActionController.Tick(deltaTime);
            UpdatePendingProjectileAttack(deltaTime);

            for (int i = 0; i < m_RuntimeSkills.Count; i++)
                m_RuntimeSkills[i]?.Tick(deltaTime);

            UpdateCasting(deltaTime);
            UpdateDataStatusEffects(deltaTime);
            m_RuntimeAI?.UpdateAI(deltaTime);
            m_PreviousSimulationPosition = m_SimulationPosition;
            UpdateMovement(deltaTime);
        }

        internal void SimulationFrame(float deltaTime, float interpolationAlpha)
        {
            if (!m_IsInitialized || IsDead) return;
            Vector3 previousPosition = transform.position;
            transform.position = Vector3.Lerp(
                m_PreviousSimulationPosition,
                m_SimulationPosition,
                Mathf.Clamp01(interpolationAlpha));
            float actualSpeed = deltaTime > 0f
                ? Vector3.Distance(previousPosition, transform.position) / deltaTime
                : 0f;
            m_AnimationPresenter?.SetActualSpeed(actualSpeed);
            UpdateFacingDirection();

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
                m_BillboardSpriteRenderer.sprite = ResolveTeamSprite(m_SourceSpriteRenderer.sprite);
                m_BillboardSpriteRenderer.color = m_SourceSpriteRenderer.color;
                m_BillboardSpriteRenderer.flipY = m_SourceSpriteRenderer.flipY;
                m_BillboardSpriteRenderer.enabled = m_SourceSpriteRenderer.gameObject.activeInHierarchy;

                // 카메라 기준으로 facing 방향의 좌/우를 판단하여 flipX 적용
                Vector3 cameraRight = cameraRotation * Vector3.right;
                bool facingLeft = Vector3.Dot(m_FacingDirection, cameraRight) < 0f;
                // 소스 스프라이트의 원본 flipX를 기본값으로 사용하고, facingLeft이면 반전
                m_BillboardSpriteRenderer.flipX = m_SourceSpriteRenderer.flipX ^ facingLeft;
            }
            else if (m_BillboardSpriteRenderer != null)
            {
                // 소스가 없고 빌보드만 있는 경우에도 플립 적용
                Vector3 cameraRight = cameraRotation * Vector3.right;
                bool facingLeft = Vector3.Dot(m_FacingDirection, cameraRight) < 0f;
                m_BillboardSpriteRenderer.flipX = facingLeft;
            }
        }

        private Sprite ResolveTeamSprite(Sprite source)
        {
            if (m_Team != 0 || source == null || m_RedTeamSprites == null) return source;

            string spriteName = source.name;
            int separator = spriteName.LastIndexOf('_');
            if (separator < 0 || separator == spriteName.Length - 1) return source;

            int frameIndex = 0;
            for (int i = separator + 1; i < spriteName.Length; i++)
            {
                int digit = spriteName[i] - '0';
                if ((uint)digit > 9u) return source;
                frameIndex = frameIndex * 10 + digit;
            }

            if ((uint)frameIndex >= (uint)m_RedTeamSprites.Length) return source;
            return m_RedTeamSprites[frameIndex] != null ? m_RedTeamSprites[frameIndex] : source;
        }

        public float ApplyDamage(float damage, Unit attacker = null, bool isCritical = false, bool isSkillDamage = false)
        {
            var context = new DamageContext
            {
                Source = new UnitHandle(attacker),
                Target = this,
                Amount = damage,
                IsCritical = isCritical,
                IsSkill = isSkillDamage,
                IsReaction = false
            };
            return ApplyDamage(in context);
        }

        public float ApplyDamage(in DamageContext sourceContext)
        {
            DamageContext context = sourceContext;
            if (IsDead || context.Target != this || context.Amount <= 0f) return 0f;

            float previousRatio = m_CurrentHp / Mathf.Max(1f, MaxHp);
            for (int i = 0; i < m_ActiveDataEffects.Count && context.Amount > 0f; i++)
                m_ActiveDataEffects[i].ModifyIncomingDamage(ref context);

            float finalDamage = 0f;
            if (context.Amount > 0f)
            {
                finalDamage = Mathf.Max(1f, context.Amount - m_CurrentStat.defense);
                if (context.IsCritical) finalDamage *= 1.5f;
                m_CurrentHp = Mathf.Max(0f, m_CurrentHp - finalDamage);
            }

            Unit attacker = context.Source.Unit;
            OnDamaged?.Invoke(finalDamage, attacker, context.IsCritical);
            EnqueueSkillEvent(
                SkillTriggerType.OnDamaged,
                this,
                attacker,
                this,
                finalDamage,
                context.IsSkill,
                context.IsCritical,
                context.IsReaction);
            OnHpChanged?.Invoke(m_CurrentHp, MaxHp);
            PlayHitEffect();
            if (Application.isPlaying) UnitHpBarPresenter.NotifyDamaged(this);

            float currentRatio = m_CurrentHp / Mathf.Max(1f, MaxHp);
            if (previousRatio > 0.25f && currentRatio <= 0.25f && m_CurrentHp > 0f)
            {
                EnqueueSkillEvent(
                    SkillTriggerType.OnLowHealth,
                    this,
                    attacker,
                    this,
                    finalDamage,
                    context.IsSkill,
                    context.IsCritical,
                    context.IsReaction);
            }

            if (m_CurrentHp <= 0f)
            {
                Die(attacker);
                if (attacker != null)
                {
                    EnqueueSkillEvent(
                        SkillTriggerType.OnKill,
                        attacker,
                        attacker,
                        this,
                        finalDamage,
                        context.IsSkill,
                        context.IsCritical,
                        context.IsReaction);
                }
            }
            return finalDamage;
        }

        public float Heal(float amount, Unit caster = null)
        {
            var context = new HealContext
            {
                Source = new UnitHandle(caster),
                Target = this,
                Amount = amount,
                IsSkill = false,
                IsReaction = false
            };
            return Heal(in context);
        }

        public float Heal(in HealContext context)
        {
            if (IsDead || context.Target != this || context.Amount <= 0f) return 0f;
            float previous = m_CurrentHp;
            m_CurrentHp = Mathf.Min(MaxHp, m_CurrentHp + context.Amount);
            float actual = m_CurrentHp - previous;
            if (actual <= 0f) return 0f;

            OnHealed?.Invoke(actual, context.Source.Unit);
            OnHpChanged?.Invoke(m_CurrentHp, MaxHp);
            if (Application.isPlaying) UnitHpBarPresenter.NotifyDamaged(this);
            return actual;
        }

        private void Die(Unit killer)
        {
            if (!m_IsInitialized) return;
            ClearDataStatusEffects();
            m_RuntimeAI?.Deactivate();
            StopMovement();
            ClearPendingProjectileAttack();
            m_ActionController.MarkDead();
            if (m_Collider != null) m_Collider.enabled = false;
            UnitRegistry.NotifyDeath(this);
            OnDied?.Invoke(killer);
            PlayClip(m_DeathSound);
            m_AnimationPresenter?.PlayDeath();
            if (!m_HoldDeathPresentation) gameObject.SetActive(false);
        }

        internal void HoldDeathPresentation()
        {
            if (IsDead) m_HoldDeathPresentation = true;
        }

        internal void CompleteDeathPresentation()
        {
            m_HoldDeathPresentation = false;
            if (IsDead && gameObject.activeSelf) gameObject.SetActive(false);
        }

        public void Attack(Unit target) => TryAttack(target);

        public bool TryAttack(Unit target)
        {
            if (!CanAttack || target == null || target.IsDead || target.Team == Team) return false;

            BasicAttackData attackData = CurrentBasicAttackData;
            if (attackData == null) return false;

            float attackAnimationLock = ResolveAttackAnimationLock(attackData);
            if (!m_ActionController.TryBeginAttack(attackAnimationLock)) return false;

            m_AttackCooldown = AttackInterval;
            m_AnimationPresenter?.PlayAttack(attackData);
            PlayClip(m_AttackSound);

            if (attackData?.Delivery is HomingProjectileAttackDelivery)
            {
                float releaseDelay = ResolveProjectileAttackReleaseDelay(attackData, attackAnimationLock);
                SchedulePendingProjectileAttack(attackData, target, releaseDelay);
                return true;
            }

            if (!attackData.TryExecute(this, target))
            {
                m_ActionController.Tick(attackAnimationLock);
                m_AttackCooldown = attackData.FailureRetryDelay;
                return false;
            }

            OnAttack?.Invoke(target);
            return true;
        }

        private void SchedulePendingProjectileAttack(BasicAttackData attackData, Unit target, float releaseDelay)
        {
            m_PendingProjectileAttackData = attackData;
            m_PendingProjectileAttackTarget = new UnitHandle(target);
            m_PendingProjectileAttackRemaining = Mathf.Max(0f, releaseDelay);
        }

        private void UpdatePendingProjectileAttack(float deltaTime)
        {
            if (m_PendingProjectileAttackData == null) return;
            if (IsStunned)
            {
                ClearPendingProjectileAttack();
                return;
            }

            m_PendingProjectileAttackRemaining =
                Mathf.Max(0f, m_PendingProjectileAttackRemaining - deltaTime);
            if (m_PendingProjectileAttackRemaining > 0f) return;

            BasicAttackData attackData = m_PendingProjectileAttackData;
            Unit target = m_PendingProjectileAttackTarget.Unit;
            ClearPendingProjectileAttack();

            if (target == null || target.IsDead || target.Team == Team) return;
            if (attackData.TryExecute(this, target))
            {
                OnAttack?.Invoke(target);
                return;
            }

            m_AttackCooldown = Mathf.Min(m_AttackCooldown, attackData.FailureRetryDelay);
        }

        private void ClearPendingProjectileAttack()
        {
            m_PendingProjectileAttackData = null;
            m_PendingProjectileAttackTarget = default;
            m_PendingProjectileAttackRemaining = 0f;
        }

        private float ResolveAttackAnimationLock(BasicAttackData attackData = null)
        {
            AnimationClip clip = FindPreferredAttackClip(attackData);
            float bestLength = clip != null ? clip.length : 0f;

            if (bestLength <= 0f && attackData?.Delivery is ImmediateAttackDelivery)
                return ResolveAttackAnimationLock();

            return Mathf.Max(MinimumAttackAnimationLock, bestLength);
        }

        private float ResolveProjectileAttackReleaseDelay(BasicAttackData attackData, float fallbackLock)
        {
            AnimationClip clip = FindPreferredAttackClip(attackData);
            float clipLength = clip != null ? clip.length : fallbackLock;
            float frameRate = clip != null && clip.frameRate > 0f
                ? clip.frameRate
                : DefaultAnimationFrameRate;
            float frameOffset = attackData != null
                ? attackData.ProjectileReleaseFrameOffset
                : ProjectileReleaseFrameOffset;
            float releaseDelay = clipLength - (frameOffset / frameRate);
            return Mathf.Clamp(releaseDelay, 0f, fallbackLock);
        }

        private AnimationClip FindPreferredAttackClip(BasicAttackData attackData)
        {
            RuntimeAnimatorController controller = m_Animator != null ? m_Animator.runtimeAnimatorController : null;
            if (controller == null) return null;

            string preferredClipName = attackData?.Delivery is ImmediateAttackDelivery
                ? "DaggerAttack"
                : "Attack";
            AnimationClip clip = FindLongestClip(controller, preferredClipName);
            if (clip == null && !preferredClipName.Equals("Attack", StringComparison.OrdinalIgnoreCase))
                clip = FindLongestClip(controller, "Attack");

            return clip;
        }

        private static AnimationClip FindLongestClip(RuntimeAnimatorController controller, string clipName)
        {
            AnimationClip[] clips = controller.animationClips;
            AnimationClip bestClip = null;
            float bestLength = 0f;
            for (int i = 0; clips != null && i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip == null) continue;
                if (!clip.name.Equals(clipName, StringComparison.OrdinalIgnoreCase)) continue;
                if (bestClip != null && clip.length <= bestLength) continue;

                bestClip = clip;
                bestLength = clip.length;
            }

            return bestClip;
        }

        internal void NotifyBasicAttackHit(
            Unit target,
            float actualDamage,
            bool critical,
            bool isReaction)
        {
            if (target == null || actualDamage <= 0f) return;
            LogCombatAction("기본 공격", target, actualDamage, "피해");
            EnqueueSkillEvent(
                SkillTriggerType.OnAttack,
                this,
                this,
                target,
                actualDamage,
                false,
                critical,
                isReaction);
        }

        internal void LogCombatAction(string actionName, Unit target, float amount, string resultType)
        {
            string sourceLabel = BuildCombatLogLabel();
            string targetLabel = target != null ? target.BuildCombatLogLabel() : "대상 없음";
            Debug.Log(
                $"{sourceLabel}이(가) {targetLabel}에게 {actionName}을 사용하여 {amount:0.##}의 {resultType}을(를) 줌.",
                this);
        }

        internal void LogDefenseReduction(
            string actionName,
            Unit target,
            float amount,
            float previousDefense,
            float currentDefense)
        {
            if (amount <= 0f) return;

            string sourceLabel = BuildCombatLogLabel();
            string targetLabel = target != null ? target.BuildCombatLogLabel() : "대상 없음";
            Debug.Log(
                $"{sourceLabel}이(가) {targetLabel}에게 {actionName}을 사용하여 방어력을 {amount:0.##} 감소시킴. ({previousDefense:0.##} -> {currentDefense:0.##})",
                this);
        }

        internal void LogHunterModeChange(string modeName)
        {
            string teamName = m_Team == 0 ? "Red" : "Blue";
            int logNumber = m_CombatLogNumber > 0 ? m_CombatLogNumber : 1;
            Debug.Log(
                $"팀 {teamName} 유닛 사냥꾼 (n : {logNumber}) 이(가) 현재 {modeName} 모드입니다.",
                this);
        }

        private string BuildCombatLogLabel()
        {
            string teamName = m_Team == 0 ? "Red" : "Blue";
            string unitName = !string.IsNullOrWhiteSpace(m_UnitData?.UnitName)
                ? m_UnitData.UnitName
                : name;
            int logNumber = m_CombatLogNumber > 0 ? m_CombatLogNumber : 1;
            return $"팀 {teamName} 유닛 {unitName} (n : {logNumber})";
        }

        public bool TryUseSkill(Unit target = null)
            => TryUseSkill(new SkillUseRequest(target));

        public bool TryUseSkill(Vector3 groundPosition)
            => TryUseSkill(new SkillUseRequest(groundPosition));

        public bool TryUseSkill(in SkillUseRequest request)
        {
            if (m_IsSilenced || IsDead || !m_ActionController.CanStartAction) return false;

            for (int i = 0; i < m_RuntimeSkills.Count; i++)
            {
                SkillRuntime skill = m_RuntimeSkills[i];
                if (skill == null || skill.Data.SkillType != SkillType.Active || !skill.CanUse) continue;
                if (!skill.TryResolve(request, m_CastingTargets)) continue;
                BeginCast(skill);
                return true;
            }
            return false;
        }

        public bool UseSkill(SkillRuntime skill, Unit target = null)
            => UseSkill(skill, new SkillUseRequest(target));

        public bool UseSkill(SkillRuntime skill, Vector3 position)
            => UseSkill(skill, new SkillUseRequest(position));

        private bool UseSkill(SkillRuntime skill, in SkillUseRequest request)
        {
            if (skill == null || !skill.CanUse || m_IsSilenced || IsDead ||
                !m_ActionController.CanStartAction)
                return false;
            if (!skill.TryResolve(request, m_CastingTargets)) return false;
            BeginCast(skill);
            return true;
        }

        private void BeginCast(SkillRuntime skill)
        {
            if (!m_ActionController.TryBeginCast(skill.Data.CastTime)) return;
            m_CastingSkill = skill;
            m_CastRemaining = skill.Data.CastTime;
            m_AnimationPresenter?.PlayCast();
            OnSkillCastStart?.Invoke(skill);
            if (m_CastRemaining <= 0f) CompleteCast();
        }

        private void UpdateCasting(float deltaTime)
        {
            if (!IsCastingSkill || m_CastingSkill == null) return;
            if (IsStunned || IsDead)
            {
                CancelCast();
                return;
            }

            m_CastRemaining -= deltaTime;
            if (m_CastRemaining <= 0f) CompleteCast();
        }

        private void CompleteCast()
        {
            SkillRuntime skill = m_CastingSkill;
            m_ActionController.CompleteCast();
            m_CastingSkill = null;
            m_CastRemaining = 0f;

            SkillExecutionResult result = skill != null
                ? skill.Execute(m_CastingTargets)
                : SkillExecutionResult.Interrupted;
            m_CastingTargets.Clear();
            if (result == SkillExecutionResult.Success)
                OnSkillCastComplete?.Invoke(skill);
        }

        public void CancelCasting() => CancelCast();

        private void CancelCast()
        {
            m_ActionController.CompleteCast();
            m_CastingSkill = null;
            m_CastRemaining = 0f;
            m_CastingTargets.Clear();
        }

        public StatusEffectRuntime ApplyStatusEffect(StatusEffectData data, Unit caster = null, float durationOverride = -1f)
        {
            if (data == null || IsDead) return null;

            for (int i = 0; i < m_ActiveDataEffects.Count; i++)
            {
                StatusEffectRuntime existing = m_ActiveDataEffects[i];
                if (existing.Data != data) continue;
                existing.Refresh(durationOverride);
                RefreshControlStates();
                return existing;
            }

            StatusEffectRuntime runtime = StatusEffectRuntimePool.Rent();
            runtime.Initialize(data, this, caster, durationOverride);
            m_ActiveDataEffects.Add(runtime);
            runtime.Apply();
            RefreshControlStates();
            OnStatusDataApplied?.Invoke(runtime);
            return runtime;
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
            RefreshControlStates();
        }

        public StatusEffectRuntime FindActiveEffect(StatusEffectData data)
        {
            if (data == null) return null;
            for (int i = 0; i < m_ActiveDataEffects.Count; i++)
                if (m_ActiveDataEffects[i].Data == data) return m_ActiveDataEffects[i];
            return null;
        }

        private void RefreshControlStates()
        {
            bool stunned = false;
            bool silenced = false;
            for (int i = 0; i < m_ActiveDataEffects.Count; i++)
            {
                StatusEffectRuntime effect = m_ActiveDataEffects[i];
                stunned |= effect.GrantsStun;
                silenced |= effect.GrantsSilence;
            }
            bool becameStunned = !IsStunned && stunned;
            m_IsSilenced = silenced;
            if (becameStunned)
            {
                StopMovement();
            }
            m_ActionController.SetStunned(stunned);
            if (stunned || silenced) CancelCast();
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

        public void MoveTo(
            Vector3 targetPosition,
            float stopDistance = 0f)
        {
            if (IsDead || IsStunned)
            {
                return;
            }

            targetPosition.y = m_SimulationPosition.y;

            // AI가 전장 밖 목적지를 계산하더라도 안쪽으로 보정한다.
            targetPosition = ClampToBattlefield(targetPosition);

            bool wasMoving = IsMoving;

            m_MoveTargetPosition = targetPosition;
            m_MoveStopDistance = Mathf.Max(0f, stopDistance);

            m_ActionController.SetMoveIntent(true);

            if (!wasMoving && IsMoving)
            {
                OnMoveStart?.Invoke();
            }
        }

        public void StopMovement()
        {
            if (!IsMoving) return;
            m_ActionController.SetMoveIntent(false);
            if (m_Rigidbody != null && !m_Rigidbody.isKinematic)
            {
                m_Rigidbody.linearVelocity = Vector3.zero;
            }
            OnMoveComplete?.Invoke();
        }

        private void UpdateMovement(float deltaTime)
        {
            if (!IsMoving || m_CurrentStat.moveSpeed <= 0f)
            {
                return;
            }

            Vector3 current = m_SimulationPosition;
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

            Vector3 combinedDirection =
                direction + separation * 0.35f;

            if (combinedDirection.sqrMagnitude > 0.0001f)
            {
                direction = combinedDirection.normalized;
            }

            float distance = Mathf.Min(
                delta.magnitude - stopDistance,
                m_CurrentStat.moveSpeed * deltaTime);

            Vector3 candidatePosition =
                current + direction * Mathf.Max(0f, distance);

            // separation을 포함한 실제 최종 위치를 제한한다.
            m_SimulationPosition =
                ClampToBattlefield(candidatePosition);

            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation =
                    Quaternion.LookRotation(direction, Vector3.up);

                m_FacingDirection = direction;
            }
        }

        private void UpdateDataStatusEffects(float deltaTime)
        {
            bool removed = false;
            for (int i = m_ActiveDataEffects.Count - 1; i >= 0; i--)
            {
                StatusEffectRuntime effect = m_ActiveDataEffects[i];
                if (effect.Tick(deltaTime)) continue;

                m_ActiveDataEffects.RemoveAt(i);
                OnStatusDataRemoved?.Invoke(effect, true);
                effect.Release(true);
                StatusEffectRuntimePool.Return(effect);
                removed = true;
            }
            if (removed) RefreshControlStates();
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
            RefreshControlStates();
        }

        private void RecalculateStats()
        {
            float hpRatio = MaxHp > 0f ? m_CurrentHp / MaxHp : 1f;
            m_CurrentStat = m_BaseStat + m_StatModifierBuffSum - m_StatModifierDebuffSum;
            if (m_HasWeaponStatOverride)
            {
                m_CurrentStat.attackPower = m_WeaponStatOverride.attackPower;
                m_CurrentStat.attackSpeed = m_WeaponStatOverride.attackSpeed;
                m_CurrentStat.attackRange = m_WeaponStatOverride.attackRange;
            }
            m_CurrentStat.maxHp = Mathf.Max(1f, m_CurrentStat.maxHp);
            m_CurrentStat.attackPower = Mathf.Max(0f, m_CurrentStat.attackPower);
            m_CurrentStat.defense = Mathf.Max(0f, m_CurrentStat.defense);
            m_CurrentStat.attackSpeed = Mathf.Max(0.01f, m_CurrentStat.attackSpeed);
            m_CurrentStat.moveSpeed = Mathf.Max(0f, m_CurrentStat.moveSpeed);
            m_CurrentStat.attackRange = Mathf.Max(0f, m_CurrentStat.attackRange);
            m_CurrentHp = Mathf.Clamp(m_CurrentStat.maxHp * hpRatio, 0f, m_CurrentStat.maxHp);
        }

        public void SetWeaponOverride(
            BasicAttackData attackData,
            float attackPower,
            float attackSpeed,
            float attackRange)
        {
            m_BasicAttackOverride = attackData;
            m_WeaponStatOverride = m_CurrentStat;
            m_WeaponStatOverride.attackPower = Mathf.Max(0f, attackPower);
            m_WeaponStatOverride.attackSpeed = Mathf.Max(0.01f, attackSpeed);
            m_WeaponStatOverride.attackRange = Mathf.Max(0f, attackRange);
            m_HasWeaponStatOverride = true;
            RecalculateStats();
        }

        public void ClearWeaponOverride()
        {
            m_BasicAttackOverride = null;
            m_WeaponStatOverride = default;
            m_HasWeaponStatOverride = false;
            RecalculateStats();
        }

        private void AddRuntimeSkill(SkillData skillData)
        {
            if (skillData == null) return;
            m_RuntimeSkills.Add(skillData.CreateRuntime(this));
        }

        private void ResetRuntimeSkills()
        {
            for (int i = 0; i < m_RuntimeSkills.Count; i++)
                m_RuntimeSkills[i]?.Reset();
            m_RuntimeSkills.Clear();
        }

        internal void DispatchSkillTrigger(in SkillTriggerContext context)
        {
            if (!context.Receiver.IsValid || context.Receiver.Unit != this) return;
            for (int i = 0; i < m_RuntimeSkills.Count; i++)
                m_RuntimeSkills[i]?.HandleTrigger(context);
        }

        public void NotifyBattleStarted()
            => EnqueueSkillEvent(
                SkillTriggerType.OnBattleStart,
                this,
                this,
                this,
                0f,
                false,
                false,
                false);

        public void NotifyBattleEnded()
            => EnqueueSkillEvent(
                SkillTriggerType.OnBattleEnd,
                this,
                this,
                this,
                0f,
                false,
                false,
                false);

        private static void EnqueueSkillEvent(
            SkillTriggerType trigger,
            Unit receiver,
            Unit source,
            Unit target,
            float amount,
            bool isSkill,
            bool isCritical,
            bool isReaction)
        {
            var context = new SkillTriggerContext
            {
                Trigger = trigger,
                Receiver = new UnitHandle(receiver),
                Source = new UnitHandle(source),
                Target = new UnitHandle(target),
                Amount = amount,
                Position = target != null ? target.GroundPosition : default,
                Flags = (isSkill ? SkillEventFlags.Skill : SkillEventFlags.None) |
                        (isCritical ? SkillEventFlags.Critical : SkillEventFlags.None) |
                        (isReaction ? SkillEventFlags.Reaction : SkillEventFlags.None)
            };
            BattleSimulation.EnqueueSkillEvent(in context);
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
            if (!IsAttacking && !IsCastingSkill)
                m_AnimationPresenter?.PlayHit();
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

        /// <summary>
        /// 유닛의 이동/타겟 방향에 따라 facing 방향을 갱신합니다.
        /// </summary>
        private void UpdateFacingDirection()
        {
            if (IsMoving)
            {
                // 이동 중이면 이동 방향으로 facing (UpdateMovement에서 이미 갱신됨)
                return;
            }

            // 정지 중에는 AI의 현재 타겟 방향 또는 transform.forward를 사용
            Unit target = m_RuntimeAI?.CurrentTarget;
            if (target != null && !target.IsDead && target.gameObject.activeInHierarchy)
            {
                Vector3 toTarget = target.transform.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    m_FacingDirection = toTarget.normalized;
                    return;
                }
            }

            // 기본값: transform.forward 사용
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.0001f)
                m_FacingDirection = fwd.normalized;
        }

        private Vector3 ClampToBattlefield(Vector3 position)
        {
            BattlefieldArea area = BattlefieldArea.Active;

            // 전투 외 Scene이나 단위 테스트에서는 기존 동작을 유지한다.
            if (area == null)
            {
                return position;
            }

            float radius = Mathf.Max(
                0f,
                m_UnitData?.VisualRadius ?? 0.5f);

            return area.ClampPosition(position, radius);
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip != null && m_AudioSource != null) m_AudioSource.PlayOneShot(clip);
        }

        private void RegisterRuntime()
        {
            if (m_IsRegistered || !m_IsInitialized || IsDead) return;
            BattleSimulation.Register(this);
            m_IsRegistered = true;
        }

        private void UnregisterRuntime()
        {
            if (!m_IsRegistered) return;
            BattleSimulation.Unregister(this);
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
