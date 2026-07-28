#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;

namespace InTheArena.Unit
{
    /// <summary>
    /// 스킬 타입 열거형
    /// </summary>
    public enum SkillType
    {
        Active = 0,   // 액티브: 쿨타임 존재, 수동/자동 발동
        Passive = 1   // 패시브: 상시 발동 또는 조건 발동
    }

    /// <summary>
    /// 스킬 타겟 타입 열거형
    /// </summary>
    public enum SkillTargetType
    {
        Enemy = 0,        // 적 단일
        Enemies = 1,      // 적 다중
        Ally = 2,         // 아군 단일
        Allies = 3,       // 아군 다중
        Self = 4,         // 자기 자신
        Ground = 5        // 지정 위치
    }

    /// <summary>
    /// 스킬 기본 추상 클래스
    /// ScriptableObject를 상속받지 않는 일반 클래스로 변경
    /// 실제 스킬 동작 로직을 구현
    /// </summary>
    [Serializable]
    public abstract class Skill_Base
    {
        /// <summary> 스킬 데이터 참조 (설정값 접근용) </summary>
        public SkillData Data { get; protected set; }

        /// <summary> 현재 남은 쿨타임 (런타임 데이터) </summary>
        public float CurrentCooldown { get; protected set; }

        /// <summary>
        /// 스킬 타입 (데이터에서 가져옴)
        /// </summary>
        public SkillType SkillType => Data?.SkillType ?? SkillType.Active;

        /// <summary>
        /// 타겟 타입 (데이터에서 가져옴)
        /// </summary>
        public SkillTargetType TargetType => Data?.TargetType ?? SkillTargetType.Enemy;

        /// <summary>
        /// 스킬 데미지 (데이터에서 가져옴)
        /// </summary>
        public float SkillDamage => Data?.SkillDamage ?? 0f;

        /// <summary>
        /// 스킬 사거리 (데이터에서 가져옴)
        /// </summary>
        public float SkillRange => Data?.SkillRange ?? 0f;

        /// <summary>
        /// 쿨타임 (데이터에서 가져옴)
        /// </summary>
        public float Cooldown => Data?.Cooldown ?? 0f;

        /// <summary>
        /// 시전 시간 (데이터에서 가져옴)
        /// </summary>
        public float CastTime => Data?.CastTime ?? 0f;

        /// <summary>
        /// 스킬 사용 가능 여부 확인
        /// </summary>
        public virtual bool CanUse()
        {
            return CurrentCooldown <= 0f;
        }

        /// <summary>
        /// 스킬 초기화 (유닛 스폰 시 호출)
        /// </summary>
        /// <param name="owner">스킬을 사용하는 유닛</param>
        public virtual void Initialize(Unit owner) { }

        /// <summary>
        /// 스킬 실행 (액티브 스킬 - 타겟 지정)
        /// </summary>
        /// <param name="owner">시전자</param>
        /// <param name="target">타겟</param>
        public virtual void Execute(Unit owner, Unit target = null) { }

        /// <summary>
        /// 스킬 실행 (위치 지정 스킬용)
        /// </summary>
        /// <param name="owner">시전자</param>
        /// <param name="position">대상 위치</param>
        public virtual void Execute(Unit owner, Vector3 position) { }

        /// <summary>
        /// 패시브 스킬 트리거 체크 및 실행
        /// </summary>
        /// <param name="owner">소유자</param>
        /// <param name="triggerType">트리거 타입</param>
        /// <param name="param">추가 파라미터</param>
        public virtual void OnTrigger(Unit owner, PassiveTriggerType triggerType, object param = null) { }

        /// <summary>
        /// 쿨타임 감소 (매 프레임/고정 업데이트에서 호출)
        /// </summary>
        /// <param name="deltaTime">델타 타임</param>
        public virtual void TickCooldown(float deltaTime)
        {
            if (CurrentCooldown > 0f)
            {
                CurrentCooldown = Mathf.Max(0f, CurrentCooldown - deltaTime);
            }
        }

        /// <summary>
        /// 쿨타임 리셋
        /// </summary>
        public virtual void ResetCooldown()
        {
            CurrentCooldown = Cooldown;
        }

        /// <summary>
        /// 스킬 데이터 설정 (런타임 인스턴스 생성 시 호출)
        /// </summary>
        internal void SetData(SkillData data)
        {
            Data = data;
            CurrentCooldown = 0f;
        }

        /// <summary>
        /// 스킬 데이터 복사 (런타임 인스턴스 생성용)
        /// </summary>
        public virtual Skill_Base Clone()
        {
            var clone = (Skill_Base)MemberwiseClone();
            clone.CurrentCooldown = 0f;
            return clone;
        }

        /// <summary>
        /// 스킬 데미지 계산 (방어력 적용 전 순수 스킬 데미지)
        /// </summary>
        public virtual float CalculateDamage(Unit attacker, Unit defender)
        {
            return SkillDamage;
        }
    }

    /// <summary>
    /// 패시브 스킬 트리거 타입
    /// </summary>
    public enum PassiveTriggerType
    {
        OnAttack,           // 공격 시
        OnHit,              // 피격 시
        OnKill,             // 처치 시
        OnLowHealth,        // 체력 낮을 때
        OnTurnStart,        // 턴/라운드 시작 시
        OnTurnEnd,          // 턴/라운드 종료 시
        Always              // 상시
    }
}
#endif