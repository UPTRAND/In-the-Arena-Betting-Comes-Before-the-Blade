#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using System.Collections.Generic;

namespace InTheArena.Unit
{
    /// <summary>
    /// 상태 효과 타입 열거형
    /// </summary>
    public enum StatusEffectType
    {
        Buff = 0,     // 버프 (이로운 효과)
        Debuff = 1    // 디버프 (해로운 효과)
    }

    /// <summary>
    /// 상태 효과 카테고리 (중첩/해제 규칙 결정용)
    /// </summary>
    public enum StatusEffectCategory
    {
        StatModifier,     // 스탯 변경 (공격력, 방어력, 속도 등)
        HealOverTime,     // 지속 회복
        DamageOverTime,   // 지속 데미지
        Stun,             // 스턴 (행동 불가)
        Silence,          // 침묵 (스킬 사용 불가)
        Slow,             // 둔화 (이동/공격 속도 감소)
        Shield,           // 보호막
        Invisibility,     // 은신
        Taunt,            // 도발
        Custom            // 커스텀
    }

    /// <summary>
    /// 상태 효과 중첩 타입
    /// </summary>
    public enum StackType
    {
        None = 0,         // 중첩 안 됨 (갱신만)
        Duration = 1,     // 지속시간 갱신
        Intensity = 2,    // 강도 중첩 (최대 중첩 수 제한)
        Both = 3          // 지속시간 갱신 + 강도 중첩
    }

    /// <summary>
    /// 모든 상태 효과의 최상위 기본 클래스
    /// ScriptableObject로 제작하여 데이터 기반 관리
    /// </summary>
    public abstract class UnitStatusEffect : ScriptableObject
    {
        [Header("기본 정보")]
        [Tooltip("효과 이름")]
        [SerializeField] protected string m_EffectName;

        [Tooltip("효과 설명")]
        [SerializeField] [TextArea(2, 3)] protected string m_Description;

        [Tooltip("효과 타입 (버프/디버프)")]
        [SerializeField] protected StatusEffectType m_EffectType;

        [Tooltip("효과 카테고리")]
        [SerializeField] protected StatusEffectCategory m_Category;

        [Tooltip("아이콘 스프라이트 (UI 표시용)")]
        [SerializeField] protected Sprite m_Icon;

        [Header("지속 및 중첩 설정")]
        [Tooltip("지속 시간 (초, 0 이하면 영구)")]
        [SerializeField] protected float m_Duration;

        [Tooltip("중첩 타입")]
        [SerializeField] protected StackType m_StackType;

        [Tooltip("최대 중첩 수 (Intensity/Both 타입일 때만 적용)")]
        [SerializeField] protected int m_MaxStacks;

        [Header("스탯 수정 (StatModifier 카테고리일 때 사용)")]
        [Tooltip("적용할 스탯 변경량")]
        [SerializeField] protected UnitStat m_StatModifier;

        /// <summary> 효과 이름 </summary>
        public string EffectName => m_EffectName;

        /// <summary> 효과 설명 </summary>
        public string Description => m_Description;

        /// <summary> 효과 타입 </summary>
        public StatusEffectType EffectType => m_EffectType;

        /// <summary> 효과 카테고리 </summary>
        public StatusEffectCategory Category => m_Category;

        /// <summary> 아이콘 </summary>
        public Sprite Icon => m_Icon;

        /// <summary> 기본 지속 시간 </summary>
        public float BaseDuration => m_Duration;

        /// <summary> 중첩 타입 </summary>
        public StackType StackType => m_StackType;

        /// <summary> 최대 중첩 수 </summary>
        public int MaxStacks => Mathf.Max(1, m_MaxStacks);

        /// <summary> 스탯 수정량 </summary>
        public UnitStat StatModifier => m_StatModifier;

        /// <summary>
        /// 런타임 데이터: 남은 시간
        /// </summary>
        public float RemainingTime { get; protected set; }

        /// <summary>
        /// 런타임 데이터: 현재 중첩 수
        /// </summary>
        public int CurrentStacks { get; protected set; }

        /// <summary>
        /// 런타임 데이터: 소유자 유닛
        /// </summary>
        public Unit Owner { get; protected set; }

        /// <summary>
        /// 런타임 데이터: 시전자 유닛 (디버프의 경우 누가 걸었는지)
        /// </summary>
        public Unit Caster { get; protected set; }

        /// <summary>
        /// 효과 활성화 여부
        /// </summary>
        public bool IsActive => RemainingTime > 0f || m_Duration <= 0f;

        /// <summary>
        /// 효과 만료 여부
        /// </summary>
        public bool IsExpired => m_Duration > 0f && RemainingTime <= 0f;

        /// <summary>
        /// 효과 초기화 (적용 시 호출)
        /// </summary>
        /// <param name="owner">대상 유닛</param>
        /// <param name="caster">시전자 (선택사항)</param>
        /// <param name="duration">지속 시간 오버라이드 (선택사항, 0 이하면 기본값 사용)</param>
        public virtual void Initialize(Unit owner, Unit caster = null, float duration = -1f)
        {
            Owner = owner;
            Caster = caster;
            RemainingTime = duration > 0f ? duration : m_Duration;
            CurrentStacks = 1;
            OnApplied();
        }

        /// <summary>
        /// 효과 적용 시 로직 (자식 클래스에서 오버라이드)
        /// </summary>
        protected virtual void OnApplied() { }

        /// <summary>
        /// 효과 제거 시 로직 (자식 클래스에서 오버라이드)
        /// </summary>
        protected virtual void OnRemoved(bool expired) { }

        /// <summary>
        /// 중첩 추가/갱신 시 로직
        /// </summary>
        public virtual void OnStackRefreshed(int newStacks, float newDuration)
        {
            CurrentStacks = newStacks;
            if (m_StackType == StackType.Duration || m_StackType == StackType.Both)
            {
                RemainingTime = newDuration > 0f ? newDuration : m_Duration;
            }
        }

        /// <summary>
        /// 매 프레임/틱 업데이트
        /// </summary>
        /// <param name="deltaTime">델타 타임</param>
        /// <returns>효과가 계속 유지되어야 하면 true, 제거되어야 하면 false</returns>
        public virtual bool Tick(float deltaTime)
        {
            if (m_Duration > 0f)
            {
                RemainingTime -= deltaTime;
                if (RemainingTime <= 0f)
                {
                    return false; // 만료됨
                }
            }

            OnTick(deltaTime);
            return true;
        }

        /// <summary>
        /// 주기적 효과 처리 (자식 클래스에서 오버라이드)
        /// </summary>
        protected virtual void OnTick(float deltaTime) { }

        /// <summary>
        /// 강제 제거
        /// </summary>
        /// <param name="expired">자연 만료인지 강제 제거인지</param>
        public virtual void Remove(bool expired = false)
        {
            OnRemoved(expired);
            Owner = null;
            Caster = null;
        }

        /// <summary>
        /// 효과 복사 (런타임 인스턴스 생성용)
        /// </summary>
        public virtual UnitStatusEffect Clone()
        {
            return Instantiate(this);
        }

        /// <summary>
        /// 현재 효과의 강도 계산 (중첩 반영)
        /// </summary>
        public virtual UnitStat GetCurrentStatModifier()
        {
            if (m_Category != StatusEffectCategory.StatModifier)
                return new UnitStat();

            return m_StatModifier.Multiply(CurrentStacks);
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            m_Duration = Mathf.Max(-1f, m_Duration); // -1 허용 (영구 표시용)
            m_MaxStacks = Mathf.Max(1, m_MaxStacks);
        }
#endif
    }

    /// <summary>
    /// 버프 기본 클래스 (이로운 효과)
    /// </summary>
    public abstract class Buff_Base : UnitStatusEffect
    {
        protected override void OnValidate()
        {
            base.OnValidate();
            m_EffectType = StatusEffectType.Buff;
        }
    }

    /// <summary>
    /// 디버프 기본 클래스 (해로운 효과)
    /// </summary>
    public abstract class Debuff_Base : UnitStatusEffect
    {
        protected override void OnValidate()
        {
            base.OnValidate();
            m_EffectType = StatusEffectType.Debuff;
        }

        /// <summary>
        /// 디버프 저항력 계산 (대상의 저항 스탯 등으로 저항 확률 계산 시 사용)
        /// </summary>
        public virtual float CalculateResistance(Unit target)
        {
            return 0f; // 기본 0% 저항
        }
    }

    /// <summary>
    /// 스탯 수정 버프/디버프 (공용)
    /// </summary>
    [CreateAssetMenu(fileName = "StatusEffect_StatMod_", menuName = "In The Arena/Status Effect/Stat Modifier", order = -1)]
    public class StatusEffect_StatModifier : UnitStatusEffect
    {
        [Header("적용 타입")]
        [Tooltip("버프면 true, 디버프면 false")]
        [SerializeField] private bool m_IsBuff;

        protected override void OnValidate()
        {
            base.OnValidate();
            m_EffectType = m_IsBuff ? StatusEffectType.Buff : StatusEffectType.Debuff;
            m_Category = StatusEffectCategory.StatModifier;
            m_StackType = StackType.Intensity;
        }

        protected override void OnApplied()
        {
            if (Owner != null)
            {
                Owner.ApplyStatModifier(GetCurrentStatModifier(), m_IsBuff);
            }
        }

        protected override void OnRemoved(bool expired)
        {
            if (Owner != null)
            {
                Owner.RemoveStatModifier(GetCurrentStatModifier(), m_IsBuff);
            }
        }

        public override UnitStatusEffect Clone()
        {
            return Instantiate(this);
        }
    }
}
#endif