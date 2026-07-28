#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace InTheArena.Unit
{
    /// <summary>
    /// 유닛의 기본 스탯을 정의하는 구조체
    /// </summary>
    [System.Serializable]
    public struct UnitStat
    {
        [Header("기본 스탯")]
        [Tooltip("체력: 0이 되면 유닛이 사망")]
        public float maxHp;

        [Tooltip("공격력: 기본 공격 데미지")]
        public float attackPower;

        [Tooltip("방어력: 모든 데미지 경감 (최종 데미지 = 공격 데미지 - 방어력, 최소 1)")]
        public float defense;

        [Tooltip("공격 속도: 1초당 공격 횟수 (예: 1 = 1초에 1회, 0.5 = 2초에 1회)")]
        public float attackSpeed;

        [Tooltip("이동 속도")]
        public float moveSpeed;

        [Tooltip("공격 범위: 기본 공격 및 스킬 사거리 통합")]
        public float attackRange;

        /// <summary>
        /// 공격 간격 계산 (공격 속도의 역수)
        /// </summary>
        public float AttackInterval => attackSpeed > 0f ? 1f / attackSpeed : float.MaxValue;

        /// <summary>
        /// 스탯 유효성 검사
        /// </summary>
        public bool IsValid()
        {
            return maxHp > 0f && attackPower >= 0f && defense >= 0f && attackSpeed > 0f && moveSpeed >= 0f && attackRange >= 0f;
        }

        /// <summary>
        /// 두 스탯을 더한 새로운 스탯 반환 (버프/디버프 적용용)
        /// </summary>
        public static UnitStat operator +(UnitStat a, UnitStat b)
        {
            return new UnitStat
            {
                maxHp = a.maxHp + b.maxHp,
                attackPower = a.attackPower + b.attackPower,
                defense = a.defense + b.defense,
                attackSpeed = a.attackSpeed + b.attackSpeed,
                moveSpeed = a.moveSpeed + b.moveSpeed,
                attackRange = a.attackRange + b.attackRange
            };
        }

        /// <summary>
        /// 스탯 감산 (디버프 적용용)
        /// </summary>
        public static UnitStat operator -(UnitStat a, UnitStat b)
        {
            return new UnitStat
            {
                maxHp = a.maxHp - b.maxHp,
                attackPower = a.attackPower - b.attackPower,
                defense = a.defense - b.defense,
                attackSpeed = a.attackSpeed - b.attackSpeed,
                moveSpeed = a.moveSpeed - b.moveSpeed,
                attackRange = a.attackRange - b.attackRange
            };
        }

        /// <summary>
        /// 스탯에 승수 적용 (버프 배율 적용용)
        /// </summary>
        public UnitStat Multiply(float multiplier)
        {
            return new UnitStat
            {
                maxHp = maxHp * multiplier,
                attackPower = attackPower * multiplier,
                defense = defense * multiplier,
                attackSpeed = attackSpeed * multiplier,
                moveSpeed = moveSpeed * multiplier,
                attackRange = attackRange * multiplier
            };
        }

        /// <summary>
        /// 기본 스탯 반환 (테스트/기본값용)
        /// </summary>
        public static UnitStat Default => new UnitStat
        {
            maxHp = 100f,
            attackPower = 10f,
            defense = 5f,
            attackSpeed = 1f,
            moveSpeed = 3f,
            attackRange = 2f
        };
    }

    /// <summary>
    /// 유닛의 공격 타입 열거형
    /// </summary>
    public enum UnitAttackType
    {
        Melee = 0,      // 근거리
        Ranged = 1      // 원거리
    }
}
#endif