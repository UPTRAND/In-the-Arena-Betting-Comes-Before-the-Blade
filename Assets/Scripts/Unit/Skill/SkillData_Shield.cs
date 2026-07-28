#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace InTheArena.Unit
{
    /// <summary>
    /// 실드 스킬 전용 데이터 (사전 설정된 SkillData)
    /// 쿨타임 5초, 패시브 타입, 다음 공격 1회 무효화
    /// 인스펙터에서 Skill Logic에 SkillLogic_Shield를 할당해서 사용
    /// </summary>
    [CreateAssetMenu(fileName = "SkillData_Shield_", menuName = "In The Arena/Unit/Skill/Shield Skill Data", order = 10)]
    public class SkillData_Shield : SkillData
    {
        protected override void OnValidate()
        {
            // 기본값 설정
            if (string.IsNullOrEmpty(m_SkillName))
                m_SkillName = "Shield";

            if (string.IsNullOrEmpty(m_Description))
                m_Description = "쿨타임이 0일 때 다음 공격을 1회 무효화합니다. (데미지 0)";

            m_SkillType = SkillType.Passive;        // 패시브: 피격 시 자동 발동
            m_TargetType = SkillTargetType.Self;    // 자기 자신
            m_SkillDamage = 0f;                     // 데미지 없음
            m_SkillRange = 0f;                      // 사거리 없음 (패시브)
            m_Cooldown = 5f;                        // 5초 쿨타임
            m_CastTime = 0f;                        // 시전 시간 없음

            base.OnValidate();
        }
    }
}
#endif