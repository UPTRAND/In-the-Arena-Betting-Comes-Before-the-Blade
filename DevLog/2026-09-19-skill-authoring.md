# 단일 클래스 스킬 작성

작성 기준: 2026-09-19. 구현: `Assets/Scripts/Unit/SkillTypes.cs`, `SkillRuntime.cs`, `SkillData.cs`.

## 기본 사용

`SkillBehaviorDefinition`을 상속한 `[Serializable]` 클래스 하나를 작성하고 `SkillData`의 Behavior에 연결한다. Execution Mode는 직접 실행만 하면 `BehaviorOnly`, 공용 효과도 이어서 실행하면 `BehaviorThenEffects`로 지정한다. `EffectsOnly`는 행동을 생성하지 않는다. 기존 Targeting과 Effects는 선택적으로 재사용하며, 별도 Runtime 클래스를 작성할 필요가 없다.

```csharp
using System;
using InTheArena.Unit;
using UnityEngine;

[Serializable]
public sealed class RecoverSelfSkill : SkillBehaviorDefinition
{
    [SerializeField, Min(0f)] private float m_HealAmount = 20f;

    /// <summary>체력이 부족할 때만 시전 후보가 됩니다.</summary>
    public override bool CanExecute(SkillRuntime runtime, Unit owner, SkillTargetSet targets)
    {
        return owner != null && owner.CurrentHp < owner.MaxHp;
    }

    /// <summary>실제로 회복한 경우에만 성공을 반환합니다.</summary>
    public override SkillExecutionResult Execute(in SkillEffectContext context)
    {
        float healed = context.Owner.Heal(m_HealAmount, context.Owner);
        if (healed > 0f)
        {
            return SkillExecutionResult.Success;
        }

        return SkillExecutionResult.NoEffect;
    }
}
```

이 예제는 Targeting을 `SelfSkillTargeting`으로 설정한다. 스킬 이름, 사거리, 시전 시간, 쿨다운은 SkillData에서 조정한다. Inspector의 Behavior/Targeting/Effects 타입 선택 버튼이 직렬화 가능한 구체 클래스를 자동으로 검색한다. 설정을 펼쳐 필드를 편집할 수 있고, 타입 교체는 Undo를 지원한다. 작성한 스킬은 public, 비추상, 최상위 클래스로 선언하고 기본 생성자를 유지한다.

## 확장 지점

| 함수 | 용도 |
| --- | --- |
| `OnCreated` / `OnRemoved` | 유닛별 상태 초기화 / 구독·지연 동작 정리 |
| `TryResolveTargets` / `RevalidateTargets` | 직접 대상 선택 / 시전 완료 시 재검사 |
| `CanExecute` | 체력, 자원, 무기 등 추가 실행 조건 |
| `Execute` | 실제 스킬 효과 적용 |
| `Tick` | 지속 장판, 여러 단계 동작, 시간 기반 상태 갱신 |
| `OnTrigger` | 공격·피격·처치·전투 시작/종료 반응 |
| `CollectProjectilePrefabs` | 커스텀 투사체의 풀 사전 준비 |

직접 타기팅하는 행동은 `UsesCustomTargeting`을 true로 재정의하고 선택·재검사 함수를 같은 클래스에서 구현한다. 이때 SkillData의 Targeting은 비워도 된다. AI 후보 판단에서 선택 함수가 반복 호출되므로 선택과 조건 검사에는 피해·회복·자원 차감 같은 부작용을 넣지 않는다.

## 상태와 수명

- 직렬화된 필드는 설정이다. Unity JSON 직렬화로 유닛별 행동 복사본을 만들며, 설정 리스트도 분리한다. Unity 오브젝트 참조는 같은 에셋/오브젝트를 참조한다.
- 실행 중 변경되는 타이머·콤보 횟수·대상 핸들은 `[NonSerialized]`로 선언하고 `OnCreated`에서 초기화한다. Dictionary, 델리게이트 등 Unity가 직렬화하지 않는 상태도 여기서 준비한다. 공유 ScriptableObject 자체를 실행 상태처럼 수정하지 않는다.
- 대상 보관은 풀 재사용을 구분하는 `UnitHandle`을 사용한다. 외부 이벤트 구독은 반드시 `OnRemoved`에서 해제한다.
- `Tick`은 살아 있는 유닛의 시뮬레이션 주기에 호출된다. 전투 종료 정리는 `OnBattleEnd`, 풀 반환/파괴 정리는 `OnRemoved`를 사용한다.
- 패시브는 실제 효과가 적용된 뒤 `runtime.CommitPassiveSuccess()`를 호출한다. 일반 이벤트는 쿨다운/재시도 지연을 따르며, 전투 시작·종료 이벤트는 쿨다운 중에도 전달된다.
- 연쇄 이벤트는 틱당 2,048개까지 처리한다. 대기 큐 자체가 가득 차면 초과 이벤트를 버리고 `EventOverflowCount`에 기록한다. 무한 순환 반응은 스킬에서 `IsReaction` 등으로 차단한다.
- 일부 효과 적용 뒤 다음 효과가 실패하면 이미 적용한 성공을 보존하고 쿨다운을 시작한다. 자동 롤백은 제공하지 않는다.

기존 `CreateRuntime` 재정의 방식도 유지한다. 새 스킬에는 공용 어댑터를 사용하면 된다. Android IL2CPP 빌드 및 실제 기기 실행은 이번 EditMode 검증과 별도로 확인해야 한다.

## 피해·효과·전투 종료 정책

- 일반 피해는 방어, 치명타, 보호막, HP 감소 순서로 처리한다. 고정 피해는 `DamageContext.IgnoreDefense`를 사용해 방어만 생략한다. 피해 반환값과 피해 이벤트의 수치는 실제 감소한 HP이므로 흡혈에도 같은 값을 사용한다. 흡수량·처치 여부까지 필요하면 `ApplyDamageDetailed`의 `DamageResult.HpLost`, `ShieldAbsorbed`, `Killed`를 사용한다. 커스텀 보호막은 감소시킨 피해만큼 `DamageContext.ShieldAbsorbed`에도 누적한다.
- 상태 효과의 `durationOverride`는 음수면 원본 시간, 0이면 영구, 양수면 지정 시간이다. 보호막처럼 시간이 남아도 종료해야 하는 효과는 `Expire`를 사용한다. 효과 콜백 안에서 제거하더라도 해당 콜백이 끝나기 전에는 객체를 풀에 반환하지 않는다.
- 강제 이동은 `Unit.ApplyForcedMovement`를 사용한다. 이동하는 동안 AI 목적지보다 우선하며 공격과 시전을 중단한다.
- `SingleUnitSkillTargeting`은 기본적으로 선택한 유닛을 유지한다. 대상 상실 시 재탐색은 선택 설정이다. 지점 대상은 선택한 좌표를 유지하고 범위 대상은 실행 시 범위를 다시 수집한다.
- VFX의 대상 추적과 대상 사망 시 중단은 각각 `FollowTarget`, `StopOnTargetDeath`로 지정한다. 지점 VFX는 기본적으로 위치가 고정되고 대상 사망과 독립적으로 재생된다.
- 전투 종료 후에는 피해·회복·효과 적용이 차단되고 진행 중인 투사체와 모루가 회수된다. 종료 정리는 전투 종료 콜백에서 수행한다.

제안별 변경 근거와 실제 검증 범위는 [후속 구현 기록](2026-09-19-review-followup.md)에 정리했다.
