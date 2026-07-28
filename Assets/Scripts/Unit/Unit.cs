#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using System.Collections.Generic;
using System;
using DG.Tweening;

namespace InTheArena.Unit
{
    /// <summary>
    /// 유닛 메인 컴포넌트
    /// 스탯, 상태효과, 스킬, AI, 이동, 데미지 파이프라인을 통합 관리
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class Unit : MonoBehaviour
    {
        private const string RedTeamTag = "RedTeam";
        private const string BlueTeamTag = "BlueTeam";

        #region 이벤트 정의 (옵저버 패턴)
        /// <summary> 체력 변경 이벤트 (현재체력, 최대체력) </summary>
        public event Action<float, float> OnHpChanged;

        /// <summary> 데미지 입음 이벤트 (데미지량, 공격자, 치명타여부) </summary>
        public event Action<float, Unit, bool> OnDamaged;

        /// <summary> 회복 이벤트 (회복량, 시전자) </summary>
        public event Action<float, Unit> OnHealed;

        /// <summary> 사망 이벤트 </summary>
        public event Action<Unit> OnDied;

        /// <summary> 스킬 시전 시작 이벤트 (스킬) </summary>
        public event Action<Skill_Base> OnSkillCastStart;

        /// <summary> 스킬 시전 완료 이벤트 (스킬) </summary>
        public event Action<Skill_Base> OnSkillCastComplete;

        /// <summary> 기본 공격 이벤트 (타겟) </summary>
        public event Action<Unit> OnAttack;

        /// <summary> 상태효과 적용 이벤트 (효과) </summary>
        public event Action<UnitStatusEffect> OnStatusEffectApplied;

        /// <summary> 상태효과 제거 이벤트 (효과, 만료여부) </summary>
        public event Action<UnitStatusEffect, bool> OnStatusEffectRemoved;

        /// <summary> 보호막 흡수 이벤트 (흡수량) </summary>
        public event Action<float> OnShieldAbsorb;

        /// <summary> 이동 시작 이벤트 </summary>
        public event Action OnMoveStart;

        /// <summary> 이동 완료 이벤트 </summary>
        public event Action OnMoveComplete;
        #endregion

        #region Serialized Fields
        [Header("컴포넌트 참조")]
        [Tooltip("애니메이터 (자동 할당됨)")]
        [SerializeField] private Animator m_Animator;

        [Tooltip("리지드바디 (자동 할당됨)")]
        [SerializeField] private Rigidbody m_Rigidbody;

        [Tooltip("콜라이더 (자동 할당됨)")]
        [SerializeField] private Collider m_Collider;

        [Header("시각 효과")]
        [Tooltip("피격 플래시 머티리얼")]
        [SerializeField] private Material m_HitFlashMaterial;

        [Tooltip("기본 머티리얼 (자동 저장됨)")]
        [SerializeField] private Material m_OriginalMaterial;

        [Tooltip("HP 바 UI 프리팹")]
        [SerializeField] private GameObject m_HpBarPrefab;

        [Header("사운드")]
        [Tooltip("피격 사운드")]
        [SerializeField] private AudioClip m_HitSound;

        [Tooltip("사망 사운드")]
        [SerializeField] private AudioClip m_DeathSound;

        [Tooltip("공격 사운드")]
        [SerializeField] private AudioClip m_AttackSound;
        #endregion

        #region Runtime Data
        // 데이터 참조
        private UnitData m_UnitData;
        private UnitStat m_BaseStat;
        private UnitStat m_CurrentStat;
        private UnitStat m_StatModifierBuffSum;
        private UnitStat m_StatModifierDebuffSum;

        // 스탯 프로퍼티
        private float m_CurrentHp;
        private float m_AttackCooldown;
        private bool m_IsStunned;
        private bool m_IsCastingSkill;
        private bool m_IsAttacking;

        // 팀 및 식별
        private int m_Team;
        private int m_InstanceId;

        // 이동 관련
        private Vector3 m_MoveTargetPosition;
        private float m_MoveStopDistance;
        private bool m_IsMoving;
        private Tween m_MoveTween;

        // 상태효과 리스트
        private readonly List<UnitStatusEffect> m_ActiveStatusEffects = new List<UnitStatusEffect>();
        private readonly List<UnitStatusEffect> m_PendingRemovalEffects = new List<UnitStatusEffect>();

        // 스킬 런타임 인스턴스
        private Skill_Base m_RuntimeSkill;

        // AI 런타임 인스턴스
        private UnitAI_Base m_RuntimeAI;

        // HP 바
        private GameObject m_HpBarInstance;
        private UnityEngine.UI.Slider m_HpBarSlider;

        // 캐시
        private static readonly int HitFlashProperty = Shader.PropertyToID("_FlashAmount");
        private MaterialPropertyBlock m_MaterialPropertyBlock;
        #endregion

        #region Properties
        /// <summary> 유닛 데이터 (ScriptableObject) </summary>
        public UnitData UnitData => m_UnitData;

        /// <summary> 기본 스탯 (데이터 기반) </summary>
        public UnitStat BaseStat => m_BaseStat;

        /// <summary> 현재 적용된 스탯 (버프/디버프 포함) </summary>
        public UnitStat CurrentStat => m_CurrentStat;

        /// <summary> 현재 체력 </summary>
        public float CurrentHp => m_CurrentHp;

        /// <summary> 최대 체력 </summary>
        public float MaxHp => m_CurrentStat.maxHp;

        /// <summary> 현재 공격력 </summary>
        public float CurrentAttackPower => m_CurrentStat.attackPower;

        /// <summary> 현재 방어력 </summary>
        public float CurrentDefense => m_CurrentStat.defense;

        /// <summary> 현재 공격 속도 </summary>
        public float CurrentAttackSpeed => m_CurrentStat.attackSpeed;

        /// <summary> 현재 이동 속도 </summary>
        public float CurrentMoveSpeed => m_CurrentStat.moveSpeed;

        /// <summary> 현재 공격 범위 </summary>
        public float CurrentAttackRange => m_CurrentStat.attackRange;

        /// <summary> 공격 간격 </summary>
        public float AttackInterval => m_CurrentStat.AttackInterval;

        /// <summary> 팀 번호 (0: 아군, 1: 적군 등) </summary>
        public int Team => m_Team;

        /// <summary> 인스턴스 ID </summary>
        public int InstanceId => m_InstanceId;

        /// <summary> 사망 여부 </summary>
        public bool IsDead => m_CurrentHp <= 0f;

        /// <summary> 기절 여부 </summary>
        public bool IsStunned => m_IsStunned;

        /// <summary> 스킬 시전 중 여부 </summary>
        public bool IsCastingSkill => m_IsCastingSkill;

        /// <summary> 기본 공격 중 여부 </summary>
        public bool IsAttacking => m_IsAttacking;

        /// <summary> 이동 중 여부 </summary>
        public bool IsMoving => m_IsMoving;

        /// <summary> 전투 페이즈에서 런타임 AI의 행동 여부를 제어합니다. </summary>
        public void SetAIActive(bool active)
        {
            if (active)
            {
                m_RuntimeAI?.Resume();
            }
            else
            {
                m_RuntimeAI?.Pause();
                StopMovement();
            }
        }

        /// <summary> 공격 가능 여부 (쿨다운, 기절, 시전중, 사망 체크) </summary>
        public bool CanAttack => !IsDead && !m_IsStunned && !m_IsCastingSkill && !m_IsAttacking && m_AttackCooldown <= 0f;

        /// <summary> 런타임 스킬 </summary>
        public Skill_Base Skill => m_RuntimeSkill;

        /// <summary> 런타임 AI </summary>
        public UnitAI_Base AI => m_RuntimeAI;

        /// <summary> 활성 상태효과 리스트 (읽기 전용) </summary>
        public IReadOnlyList<UnitStatusEffect> ActiveStatusEffects => m_ActiveStatusEffects;

        /// <summary> 애니메이터 </summary>
        public Animator Animator => m_Animator;

        /// <summary> 리지드바디 </summary>
        public Rigidbody Rigidbody => m_Rigidbody;

        /// <summary> 콜라이더 </summary>
        public Collider Collider => m_Collider;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            CacheComponents();
            m_InstanceId = GetHashCode(); // GetInstanceID is deprecated
            m_MaterialPropertyBlock = new MaterialPropertyBlock();
        }

        private void Start()
        {
            // HP 바 생성
            if (m_HpBarPrefab != null)
            {
                CreateHpBar();
            }
        }

        private void Update()
        {
            if (IsDead) return;

            // 공격 쿨다운 감소
            if (m_AttackCooldown > 0f)
            {
                m_AttackCooldown -= Time.deltaTime;
            }

            // 스킬 쿨다운 감소
            m_RuntimeSkill?.TickCooldown(Time.deltaTime);

            // 상태효과 업데이트
            UpdateStatusEffects(Time.deltaTime);

            // AI 업데이트
            m_RuntimeAI?.UpdateAI(Time.deltaTime);

            // HP 바 업데이트
            UpdateHpBar();
        }

        private void FixedUpdate()
        {
            // 물리 기반 이동 처리가 필요하면 여기에서
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void OnDisable()
        {
            // 풀링 등으로 비활성화될 때 정리
            m_MoveTween?.Kill();
            m_MoveTween = null;
        }
        #endregion

        #region Initialization
        /// <summary>
        /// 유닛 초기화 (UnitData에서 호출)
        /// </summary>
        /// <param name="data">유닛 데이터</param>
        /// <param name="team">팀 번호</param>
        public void Initialize(UnitData data, int team)
        {
            m_UnitData = data;
            m_Team = team;
            m_BaseStat = data.BaseStat;
            m_CurrentStat = m_BaseStat;
            m_CurrentHp = m_BaseStat.maxHp;
            m_AttackCooldown = 0f;
            m_IsStunned = false;
            m_IsCastingSkill = false;
            m_IsAttacking = false;
            m_IsMoving = false;

            // 상태효과 리스트 초기화
            m_ActiveStatusEffects.Clear();
            m_PendingRemovalEffects.Clear();
            m_StatModifierBuffSum = new UnitStat();
            m_StatModifierDebuffSum = new UnitStat();

            // 스킬 인스턴스 생성 및 초기화 (SkillData에서 로직 가져옴)
            if (data.SkillData != null && data.SkillData.SkillLogic != null)
            {
                m_RuntimeSkill = data.SkillData.SkillLogic.Clone();
                m_RuntimeSkill.SetData(data.SkillData);
                m_RuntimeSkill.Initialize(this);
            }

            // AI 인스턴스 생성 및 초기화
            if (data.AIData != null)
            {
                m_RuntimeAI = data.AIData.CreateRuntimeAI();
                m_RuntimeAI?.Initialize(this);
            }

            // 컴포넌트 설정
            SetupComponents();

            // 이벤트 구독
            SubscribeEvents();

            // HP 바 업데이트
            UpdateHpBar();
        }

        private void CacheComponents()
        {
            if (m_Animator == null) m_Animator = GetComponentInChildren<Animator>();
            if (m_Rigidbody == null) m_Rigidbody = GetComponent<Rigidbody>();
            if (m_Collider == null) m_Collider = GetComponent<Collider>();

            // 리지드바디 설정 (탑다운 게임용)
            if (m_Rigidbody != null)
            {
                m_Rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;
                m_Rigidbody.useGravity = false;
            }

            // 콜라이더 트리거 설정
            if (m_Collider != null)
            {
                m_Collider.isTrigger = false;
            }
        }

        private void SetupComponents()
        {
            string teamName = m_Team == 0 ? RedTeamTag : BlueTeamTag;

            // 팀 태그는 모든 유닛 초기화 경로에서 일관되게 여기서 설정한다.
            gameObject.tag = teamName;

            // 프로젝트에 동일한 이름의 레이어가 있을 때만 레이어를 변경한다.
            // 등록되지 않은 레이어는 NameToLayer가 -1을 반환하므로 기존 레이어를 유지한다.
            int teamLayer = LayerMask.NameToLayer(teamName);
            if (teamLayer >= 0)
            {
                gameObject.layer = teamLayer;
            }
        }

        private void SubscribeEvents()
        {
            // 스킬 이벤트 구독 등 필요시 추가
        }

        private void CreateHpBar()
        {
            if (m_HpBarInstance != null) return;

            m_HpBarInstance = Instantiate(m_HpBarPrefab, transform);
            m_HpBarInstance.transform.localPosition = new Vector3(0f, 2.5f, 0f); // 머리 위
            m_HpBarSlider = m_HpBarInstance.GetComponentInChildren<UnityEngine.UI.Slider>();

            // 빌보드 효과 (카메라 바라보게)
            var billboard = m_HpBarInstance.AddComponent<BillboardUI>();
            billboard.TargetCamera = UnityEngine.Camera.main;
        }
        #endregion

        #region Stat Management
        /// <summary>
        /// 스탯 재계산 (버프/디버프 합산 적용)
        /// </summary>
        private void RecalculateStats()
        {
            m_CurrentStat = m_BaseStat + m_StatModifierBuffSum - m_StatModifierDebuffSum;

            // 최소값 보정
            m_CurrentStat.maxHp = Mathf.Max(1f, m_CurrentStat.maxHp);
            m_CurrentStat.attackPower = Mathf.Max(0f, m_CurrentStat.attackPower);
            m_CurrentStat.defense = Mathf.Max(0f, m_CurrentStat.defense);
            m_CurrentStat.attackSpeed = Mathf.Max(0.01f, m_CurrentStat.attackSpeed);
            m_CurrentStat.moveSpeed = Mathf.Max(0f, m_CurrentStat.moveSpeed);
            m_CurrentStat.attackRange = Mathf.Max(0f, m_CurrentStat.attackRange);

            // 최대 체력 변경 시 현재 체력 비율 유지
            float hpRatio = m_BaseStat.maxHp > 0f ? m_CurrentHp / m_BaseStat.maxHp : 1f;
            m_CurrentHp = Mathf.Min(m_CurrentHp, m_CurrentStat.maxHp);
        }

        /// <summary>
        /// 버프 스탯 추가
        /// </summary>
        internal void ApplyStatModifier(UnitStat modifier, bool isBuff)
        {
            if (isBuff)
            {
                m_StatModifierBuffSum += modifier;
            }
            else
            {
                m_StatModifierDebuffSum += modifier;
            }
            RecalculateStats();
        }

        /// <summary>
        /// 버프 스탯 제거
        /// </summary>
        internal void RemoveStatModifier(UnitStat modifier, bool isBuff)
        {
            if (isBuff)
            {
                m_StatModifierBuffSum -= modifier;
            }
            else
            {
                m_StatModifierDebuffSum -= modifier;
            }
            RecalculateStats();
        }
        #endregion

        #region Damage Pipeline
        /// <summary>
        /// 데미지 적용 (메인 진입점)
        /// </summary>
        /// <param name="damage">기본 데미지 (공격력 또는 스킬 데미지)</param>
        /// <param name="attacker">공격자</param>
        /// <param name="isCritical">치명타 여부</param>
        /// <param name="isSkillDamage">스킬 데미지 여부</param>
        /// <returns>실제 적용된 데미지</returns>
        public float ApplyDamage(float damage, Unit attacker = null, bool isCritical = false, bool isSkillDamage = false)
        {
            if (IsDead) return 0f;

            // 보호막 흡수 체크
            float finalDamage = damage;
            foreach (var effect in m_ActiveStatusEffects)
            {
                if (effect.Category == StatusEffectCategory.Shield && effect is Buff_Shield shield)
                {
                    finalDamage = shield.AbsorbDamage(finalDamage);
                    if (finalDamage <= 0f) break;
                }
            }

            // 방어력 적용: 최종 데미지 = 공격 데미지 - 방어력 (최소 1)
            finalDamage = Mathf.Max(1f, finalDamage - m_CurrentStat.defense);

            // 치명타 처리
            if (isCritical)
            {
                finalDamage *= 1.5f; // 치명타 배율 150%
            }

            // 체력 감소
            float prevHp = m_CurrentHp;
            m_CurrentHp = Mathf.Max(0f, m_CurrentHp - finalDamage);

            // 이벤트 발생
            OnDamaged?.Invoke(finalDamage, attacker, isCritical);
            OnHpChanged?.Invoke(m_CurrentHp, MaxHp);

            // 피격 시각/청각 효과
            PlayHitEffect();

            // 사망 체크
            if (m_CurrentHp <= 0f)
            {
                Die(attacker);
            }

            return finalDamage;
        }

        /// <summary>
        /// 회복 처리
        /// </summary>
        /// <param name="amount">회복량</param>
        /// <param name="caster">시전자</param>
        /// <returns>실제 회복된 양</returns>
        public float Heal(float amount, Unit caster = null)
        {
            if (IsDead || amount <= 0f) return 0f;

            float prevHp = m_CurrentHp;
            m_CurrentHp = Mathf.Min(MaxHp, m_CurrentHp + amount);
            float actualHeal = m_CurrentHp - prevHp;

            if (actualHeal > 0f)
            {
                OnHealed?.Invoke(actualHeal, caster);
                OnHpChanged?.Invoke(m_CurrentHp, MaxHp);
                PlayHealEffect();
            }

            return actualHeal;
        }

        /// <summary>
        /// 사망 처리
        /// </summary>
        private void Die(Unit killer = null)
        {
            // 모든 상태효과 제거
            ClearAllStatusEffects();

            // AI 비활성화
            m_RuntimeAI?.Deactivate();

            // 이동 정지
            StopMovement();

            // 콜라이더 비활성화
            if (m_Collider != null) m_Collider.enabled = false;

            // 이벤트 발생
            OnDied?.Invoke(killer);

            // 사망 효과
            PlayDeathEffect();

            // 오브젝트 비활성화/파괴 (풀링 고려)
            gameObject.SetActive(false);
        }
        #endregion

        #region Attack System
        /// <summary>
        /// 기본 공격 실행
        /// </summary>
        /// <param name="target">공격 대상</param>
        public void Attack(Unit target)
        {
            if (!CanAttack || target == null || target.IsDead) return;

            m_IsAttacking = true;
            m_AttackCooldown = AttackInterval;

            // 공격 애니메이션 트리거
            m_Animator?.SetTrigger("Attack");

            // 공격 사운드
            if (m_AttackSound != null)
            {
                AudioSource.PlayClipAtPoint(m_AttackSound, transform.position);
            }

            // 공격 이벤트 (자식에서 데미지 처리 또는 투사체 생성)
            OnAttack?.Invoke(target);

            // 근거리/원거리 분기
            if (m_UnitData != null && m_UnitData.AttackType == UnitAttackType.Melee)
            {
                // 근거리: 즉시 데미지 적용
                ProcessMeleeAttack(target);
            }
            else
            {
                // 원거리: 투사체 생성 (별도 구현 필요)
                ProcessRangedAttack(target);
            }

            // 공격 완료 후 플래그 해제 (애니메이션 이벤트나 코루틴에서 처리 권장)
            // 여기서는 간단히 코루틴으로 처리
            StartCoroutine(ResetAttackFlag());
        }

        private void ProcessMeleeAttack(Unit target)
        {
            float damage = m_CurrentStat.attackPower;
            bool isCritical = UnityEngine.Random.value < 0.05f; // 5% 치명타 (나중에 스탯으로 분리)

            target.ApplyDamage(damage, this, isCritical, false);
        }

        private void ProcessRangedAttack(Unit target)
        {
            // 투사체 프리팹이 있으면 생성, 없으면 즉시 히트 처리
            // 구현 예시: Projectile 풀링 시스템과 연동
            float damage = m_CurrentStat.attackPower;
            bool isCritical = UnityEngine.Random.value < 0.05f;

            // 임시로 즉시 적용 (투사체 시스템 구현 시 교체)
            target.ApplyDamage(damage, this, isCritical, false);
        }

        private System.Collections.IEnumerator ResetAttackFlag()
        {
            yield return new WaitForSeconds(0.3f); // 애니메이션 길이 고려
            m_IsAttacking = false;
        }
        #endregion

        #region Skill System
        /// <summary>
        /// 스킬 사용
        /// </summary>
        /// <param name="skill">사용할 스킬</param>
        /// <param name="target">대상</param>
        public void UseSkill(Skill_Base skill, Unit target = null)
        {
            if (skill == null || m_IsCastingSkill || IsDead || m_IsStunned) return;

            // 타겟 검증
            if (skill.TargetType != SkillTargetType.Self && skill.TargetType != SkillTargetType.Ground)
            {
                if (target == null || target.IsDead) return;
            }

            m_IsCastingSkill = true;
            m_IsAttacking = false; // 스킬 시전 중 기본 공격 중단

            // 시전 시간 처리
            if (skill.CastTime > 0f)
            {
                StartCoroutine(CastSkillRoutine(skill, target));
            }
            else
            {
                ExecuteSkill(skill, target);
            }
        }

        private System.Collections.IEnumerator CastSkillRoutine(Skill_Base skill, Unit target)
        {
            // 시전 애니메이션
            m_Animator?.SetTrigger("CastSkill");
            OnSkillCastStart?.Invoke(skill);

            yield return new WaitForSeconds(skill.CastTime);

            if (!IsDead && !m_IsStunned)
            {
                ExecuteSkill(skill, target);
            }
            else
            {
                m_IsCastingSkill = false;
            }
        }

        private void ExecuteSkill(Skill_Base skill, Unit target)
        {
            // 스킬 실행
            skill.Execute(this, target);

            // 쿨다임 설정
            skill.ResetCooldown();

            // 이벤트
            OnSkillCastComplete?.Invoke(skill);

            m_IsCastingSkill = false;
        }

        /// <summary>
        /// 위치 지정 스킬 사용
        /// </summary>
        public void UseSkill(Skill_Base skill, Vector3 position)
        {
            if (skill == null || m_IsCastingSkill || IsDead || m_IsStunned) return;

            m_IsCastingSkill = true;
            m_IsAttacking = false;

            if (skill.CastTime > 0f)
            {
                StartCoroutine(CastSkillPositionRoutine(skill, position));
            }
            else
            {
                ExecuteSkillPosition(skill, position);
            }
        }

        private System.Collections.IEnumerator CastSkillPositionRoutine(Skill_Base skill, Vector3 position)
        {
            m_Animator?.SetTrigger("CastSkill");
            OnSkillCastStart?.Invoke(skill);

            yield return new WaitForSeconds(skill.CastTime);

            if (!IsDead && !m_IsStunned)
            {
                ExecuteSkillPosition(skill, position);
            }
            else
            {
                m_IsCastingSkill = false;
            }
        }

        private void ExecuteSkillPosition(Skill_Base skill, Vector3 position)
        {
            skill.Execute(this, position);
            skill.ResetCooldown();
            OnSkillCastComplete?.Invoke(skill);
            m_IsCastingSkill = false;
        }
        #endregion

        #region Status Effect System
        /// <summary>
        /// 상태효과 적용
        /// </summary>
        /// <param name="effectData">적용할 효과 데이터 (ScriptableObject)</param>
        /// <param name="caster">시전자</param>
        /// <param name="durationOverride">지속시간 오버라이드</param>
        /// <returns>적용된 효과 인스턴스 (실패 시 null)</returns>
        public UnitStatusEffect ApplyStatusEffect(UnitStatusEffect effectData, Unit caster = null, float durationOverride = -1f)
        {
            if (effectData == null || IsDead) return null;

            // 디버프 저항 체크
            if (effectData is Debuff_Base debuff)
            {
                float resistChance = debuff.CalculateResistance(this);
                if (UnityEngine.Random.value < resistChance)
                {
                    // 저항 성공
                    return null;
                }
            }

            // 기존 동일 효과 확인 (중첩 처리)
            UnitStatusEffect existingEffect = FindActiveEffect(effectData.GetType());
            if (existingEffect != null)
            {
                // 중첩 타입에 따라 처리
                switch (effectData.StackType)
                {
                    case StackType.None:
                        // 갱신만
                        existingEffect.OnStackRefreshed(existingEffect.CurrentStacks, durationOverride > 0f ? durationOverride : effectData.BaseDuration);
                        return existingEffect;

                    case StackType.Duration:
                        // 지속시간 갱신
                        existingEffect.OnStackRefreshed(existingEffect.CurrentStacks, durationOverride > 0f ? durationOverride : effectData.BaseDuration);
                        return existingEffect;

                    case StackType.Intensity:
                        // 강도 중첩
                        if (existingEffect.CurrentStacks < effectData.MaxStacks)
                        {
                            existingEffect.OnStackRefreshed(existingEffect.CurrentStacks + 1, durationOverride > 0f ? durationOverride : effectData.BaseDuration);
                        }
                        else
                        {
                            // 최대 중첩 시 지속시간만 갱신
                            existingEffect.OnStackRefreshed(existingEffect.CurrentStacks, durationOverride > 0f ? durationOverride : effectData.BaseDuration);
                        }
                        return existingEffect;

                    case StackType.Both:
                        if (existingEffect.CurrentStacks < effectData.MaxStacks)
                        {
                            existingEffect.OnStackRefreshed(existingEffect.CurrentStacks + 1, durationOverride > 0f ? durationOverride : effectData.BaseDuration);
                        }
                        else
                        {
                            existingEffect.OnStackRefreshed(existingEffect.CurrentStacks, durationOverride > 0f ? durationOverride : effectData.BaseDuration);
                        }
                        return existingEffect;
                }
            }

            // 새 효과 인스턴스 생성 및 적용
            UnitStatusEffect newEffect = effectData.Clone();
            newEffect.Initialize(this, caster, durationOverride);
            m_ActiveStatusEffects.Add(newEffect);

            // 이벤트
            OnStatusEffectApplied?.Invoke(newEffect);

            return newEffect;
        }

        /// <summary>
        /// 상태효과 제거
        /// </summary>
        public void RemoveStatusEffect(UnitStatusEffect effect, bool expired = false)
        {
            if (effect == null) return;

            m_PendingRemovalEffects.Add(effect);
        }

        /// <summary>
        /// 타입으로 활성 효과 찾기
        /// </summary>
        public T FindActiveEffect<T>() where T : UnitStatusEffect
        {
            foreach (var effect in m_ActiveStatusEffects)
            {
                if (effect is T typedEffect) return typedEffect;
            }
            return null;
        }

        /// <summary>
        /// 타입으로 활성 효과 찾기 (제네릭 아닌 버전)
        /// </summary>
        public UnitStatusEffect FindActiveEffect(System.Type type)
        {
            foreach (var effect in m_ActiveStatusEffects)
            {
                if (effect.GetType() == type) return effect;
            }
            return null;
        }

        /// <summary>
        /// 특정 카테고리의 모든 효과 찾기
        /// </summary>
        public List<UnitStatusEffect> FindActiveEffects(StatusEffectCategory category)
        {
            var result = new List<UnitStatusEffect>();
            foreach (var effect in m_ActiveStatusEffects)
            {
                if (effect.Category == category) result.Add(effect);
            }
            return result;
        }

        /// <summary>
        /// 모든 상태효과 제거
        /// </summary>
        public void ClearAllStatusEffects()
        {
            foreach (var effect in m_ActiveStatusEffects)
            {
                effect.Remove(true);
            }
            m_ActiveStatusEffects.Clear();
            m_PendingRemovalEffects.Clear();
        }

        /// <summary>
        /// 상태효과 업데이트 (매 프레임)
        /// </summary>
        private void UpdateStatusEffects(float deltaTime)
        {
            // 만료/제거 대상 처리
            for (int i = m_ActiveStatusEffects.Count - 1; i >= 0; i--)
            {
                var effect = m_ActiveStatusEffects[i];

                // 강제 제거 목록에 있으면 제거
                if (m_PendingRemovalEffects.Contains(effect))
                {
                    effect.Remove(false);
                    m_ActiveStatusEffects.RemoveAt(i);
                    OnStatusEffectRemoved?.Invoke(effect, false);
                    continue;
                }

                // 틱 업데이트
                if (!effect.Tick(deltaTime))
                {
                    // 자연 만료
                    effect.Remove(true);
                    m_ActiveStatusEffects.RemoveAt(i);
                    OnStatusEffectRemoved?.Invoke(effect, true);
                }
            }

            m_PendingRemovalEffects.Clear();
        }

        /// <summary>
        /// 기절 상태 설정
        /// </summary>
        internal void SetStunned(bool stunned)
        {
            m_IsStunned = stunned;

            if (stunned)
            {
                // 기절 시 이동/공격/스킬 중단
                StopMovement();
                m_IsAttacking = false;
                m_IsCastingSkill = false;
                m_Animator?.SetBool("Stunned", true);
            }
            else
            {
                m_Animator?.SetBool("Stunned", false);
            }
        }

        /// <summary>
        /// 침묵 상태 설정 (스킬 사용 불가)
        /// </summary>
        internal void SetSilenced(bool silenced)
        {
            // 필요시 스킬 사용 차단 로직 추가
            // 현재는 패시브 트리거에서 처리
        }

        /// <summary>
        /// 보호막 흡수 알림
        /// </summary>
        internal void OnShieldAbsorbCallback(float amount)
        {
            OnShieldAbsorb?.Invoke(amount);
        }
        #endregion

        #region Movement System
        /// <summary>
        /// 목표 위치로 이동
        /// </summary>
        /// <param name="targetPosition">목표 위치</param>
        /// <param name="stopDistance">정지 거리</param>
        public void MoveTo(Vector3 targetPosition, float stopDistance = 0f)
        {
            if (IsDead || m_IsStunned) return;

            m_MoveTargetPosition = targetPosition;
            m_MoveStopDistance = stopDistance;
            m_IsMoving = true;

            // DOTween을 사용한 부드러운 이동
            m_MoveTween?.Kill();
            m_MoveTween = m_Rigidbody.DOMove(targetPosition, Vector3.Distance(transform.position, targetPosition) / Mathf.Max(0.01f, m_CurrentStat.moveSpeed))
                .SetEase(Ease.Linear)
                .OnStart(() => OnMoveStart?.Invoke())
                .OnComplete(() =>
                {
                    m_IsMoving = false;
                    OnMoveComplete?.Invoke();
                })
                .OnUpdate(() =>
                {
                    // 정지 거리 체크
                    if (Vector3.Distance(transform.position, m_MoveTargetPosition) <= m_MoveStopDistance)
                    {
                        m_MoveTween?.Kill();
                        m_IsMoving = false;
                        OnMoveComplete?.Invoke();
                    }
                });
        }

        /// <summary>
        /// 이동 정지
        /// </summary>
        public void StopMovement()
        {
            m_MoveTween?.Kill();
            m_MoveTween = null;
            m_IsMoving = false;
            var rb = m_Rigidbody;
            if (rb != null) rb.linearVelocity = Vector3.zero;
        }
        #endregion

        #region Visual & Audio Effects
        private void PlayHitEffect()
        {
            // 피격 플래시
            if (m_HitFlashMaterial != null && m_OriginalMaterial != null)
            {
                var renderers = GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    renderer.material = m_HitFlashMaterial;
                }

                DOVirtual.DelayedCall(0.1f, () =>
                {
                    foreach (var renderer in renderers)
                    {
                        renderer.material = m_OriginalMaterial;
                    }
                });
            }

            // 피격 사운드
            if (m_HitSound != null)
            {
                AudioSource.PlayClipAtPoint(m_HitSound, transform.position);
            }

            // 피격 애니메이션
            m_Animator?.SetTrigger("Hit");
        }

        private void PlayHealEffect()
        {
            // 회복 이펙트 (파티클 등) - 추후 구현
            m_Animator?.SetTrigger("Heal");
        }

        private void PlayDeathEffect()
        {
            // 사망 사운드
            if (m_DeathSound != null)
            {
                AudioSource.PlayClipAtPoint(m_DeathSound, transform.position);
            }

            // 사망 애니메이션
            m_Animator?.SetTrigger("Die");
        }
        #endregion

        #region HP Bar
        private void UpdateHpBar()
        {
            if (m_HpBarSlider != null && MaxHp > 0f)
            {
                m_HpBarSlider.value = m_CurrentHp / MaxHp;
            }
        }
        #endregion

        #region Cleanup
        private void Cleanup()
        {
            m_MoveTween?.Kill();
            m_MoveTween = null;

            ClearAllStatusEffects();

            if (!ReferenceEquals(m_HpBarInstance, null) && m_HpBarInstance != null)
            {
                Destroy(m_HpBarInstance);
            }

            m_RuntimeSkill = null;
            m_RuntimeAI = null;
        }
        #endregion

        #region Editor
#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheComponents();
        }

        private void OnDrawGizmosSelected()
        {
            // 공격 범위 기즈모
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, m_CurrentStat.attackRange > 0f ? m_CurrentStat.attackRange : 2f);

            // 이동 목표 기즈모
            if (m_IsMoving)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, m_MoveTargetPosition);
                Gizmos.DrawWireSphere(m_MoveTargetPosition, 0.3f);
            }
        }
#endif
        #endregion
    }

    /// <summary>
    /// 빌보드 UI 컴포넌트 (HP 바가 카메라를 바라보게)
    /// </summary>
    public class BillboardUI : MonoBehaviour
    {
        public UnityEngine.Camera TargetCamera { get; set; }

        private void LateUpdate()
        {
            if (TargetCamera != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - TargetCamera.transform.position);
            }
        }
    }
}
#endif
