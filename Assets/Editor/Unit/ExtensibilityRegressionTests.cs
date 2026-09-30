#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using InTheArena.Unit;
using InTheArena.MainGame;
using NUnit.Framework;
using UnityEngine;
using UnitObject = InTheArena.Unit.Unit;
using UnityObject = UnityEngine.Object;

namespace InTheArena.Editor.Unit
{
    public sealed class ExtensibilityRegressionTests
    {
        private readonly List<UnityObject> m_Created = new List<UnityObject>();

        /// <summary>전투 전역 상태를 각 검사 전에 초기화합니다.</summary>
        [SetUp]
        public void SetUp()
        {
            BattleSimulation.BeginBattle();
        }

        /// <summary>테스트별 생성 객체와 전투 전역 상태를 정리합니다.</summary>
        [TearDown]
        public void TearDown()
        {
            for (int i = m_Created.Count - 1; i >= 0; i--)
            {
                if (m_Created[i] != null)
                {
                    UnityObject.DestroyImmediate(m_Created[i]);
                }
            }

            m_Created.Clear();
            UnitRegistry.Clear();
            ProbeSkill.Instances.Clear();
            BattleSimulation.BeginBattle();
        }

        /// <summary>동일 에셋의 설정·리스트·상태는 유닛별로 독립적이어야 합니다.</summary>
        [Test]
        public void SingleClassBehavior_ClonesSettingsAndKeepsUnityReferences()
        {
            UnitObject first = CreateUnit(0, position: Vector3.zero);
            UnitObject second = CreateUnit(0, position: Vector3.right);
            var behavior = new ProbeSkill { Reference = first.gameObject };
            behavior.Values.Add(7);
            SkillData data = CreateSkill(behavior);
            SkillRuntime a = data.CreateRuntime(first);
            SkillRuntime b = data.CreateRuntime(second);

            Assert.That(ProbeSkill.Instances.Count, Is.EqualTo(2));
            ProbeSkill firstCopy = ProbeSkill.Instances[0];
            ProbeSkill secondCopy = ProbeSkill.Instances[1];
            Assert.That(firstCopy.Reference, Is.SameAs(first.gameObject));
            firstCopy.Values.Add(9);
            a.Tick(0.5f);

            Assert.That(firstCopy.Elapsed, Is.EqualTo(0.5f));
            Assert.That(secondCopy.Elapsed, Is.Zero);
            Assert.That(secondCopy.Values, Is.EqualTo(new[] { 7 }));
            Assert.That(behavior.Values, Is.EqualTo(new[] { 7 }));
            a.Reset();
            b.Reset();
        }

        /// <summary>한 행동 클래스만으로 대상 선택과 실행을 완성할 수 있어야 합니다.</summary>
        [Test]
        public void CustomTargeting_WorksWithoutTargetingDefinition()
        {
            UnitObject owner = CreateUnit(0, position: Vector3.zero);
            SkillData data = CreateSkill(new ProbeSkill());
            Assert.That(data.IsValid(), Is.True);
            SkillRuntime runtime = data.CreateRuntime(owner);
            var targets = new SkillTargetSet();

            Assert.That(runtime.TryResolve(new SkillUseRequest(owner), targets), Is.True);
            Assert.That(runtime.Execute(targets), Is.EqualTo(SkillExecutionResult.Success));
            Assert.That(ProbeSkill.Instances[0].ExecutionCount, Is.EqualTo(1));
            Assert.That(runtime.Execute(targets), Is.EqualTo(SkillExecutionResult.Interrupted));
            Assert.That(ProbeSkill.Instances[0].ExecutionCount, Is.EqualTo(1));
            runtime.Reset();
        }

        /// <summary>앞선 효과가 성공했다면 뒤 효과 실패가 중복 실행을 유발하지 않아야 합니다.</summary>
        [Test]
        public void PartialSuccess_StillCommitsCooldown()
        {
            UnitObject owner = CreateUnit(0, position: Vector3.zero);
            SkillData data = CreateSkill(new ProbeSkill());
            SetField(data, "m_ExecutionMode", SkillExecutionMode.BehaviorThenEffects);
            SetField(data, "m_Effects", new List<SkillEffectDefinition> { new ExhaustedEffect() });
            SkillRuntime runtime = data.CreateRuntime(owner);
            var targets = new SkillTargetSet();
            runtime.TryResolve(new SkillUseRequest(owner), targets);

            Assert.That(runtime.Execute(targets), Is.EqualTo(SkillExecutionResult.Success));
            Assert.That(runtime.CurrentCooldown, Is.GreaterThan(0f));
            runtime.Reset();
        }

        /// <summary>쿨다운 중인 행동에도 전투 종료와 단 한 번의 해제가 전달됩니다.</summary>
        [Test]
        public void Lifecycle_IsDeliveredDuringCooldownAndResetIsIdempotent()
        {
            UnitObject owner = CreateUnit(0, position: Vector3.zero);
            SkillRuntime runtime = CreateSkill(new ProbeSkill()).CreateRuntime(owner);
            runtime.CommitPassiveSuccess();
            var context = new SkillTriggerContext
            {
                Receiver = new UnitHandle(owner),
                Trigger = SkillTriggerType.OnBattleEnd
            };

            runtime.HandleTrigger(context);
            runtime.Reset();
            runtime.Reset();
            runtime.Tick(1f);
            runtime.HandleTrigger(context);

            Assert.That(ProbeSkill.Instances[0].BattleEndCount, Is.EqualTo(1));
            Assert.That(ProbeSkill.Instances[0].RemovalCount, Is.EqualTo(1));
            Assert.That(runtime.Owner, Is.Null);
            Assert.That(runtime.CanUse, Is.False);
        }

        /// <summary>외부 유닛의 스킬 인스턴스는 대신 시전할 수 없습니다.</summary>
        [Test]
        public void Unit_RejectsForeignSkillRuntime()
        {
            UnitObject owner = CreateUnit(0, position: Vector3.zero);
            UnitObject other = CreateUnit(0, position: Vector3.right);
            SkillRuntime runtime = CreateSkill(new ProbeSkill()).CreateRuntime(owner);

            Assert.That(other.UseSkill(runtime, owner), Is.False);
            runtime.Reset();
        }

        /// <summary>원거리 스킬은 기본 공격 사거리 밖에서도 AI가 선택합니다.</summary>
        [Test]
        public void Decision_UsesSkillRangeIndependentlyFromBasicAttack()
        {
            UnitObject owner = CreateUnit(0, position: Vector3.zero);
            UnitObject enemy = CreateUnit(1, position: Vector3.right * 5f);
            SkillData skill = CreateSkill(null);
            SetField(skill, "m_Targeting", new SingleUnitSkillTargeting());
            SetField(skill, "m_Range", 6f);
            SetField(owner.UnitData, "m_SkillDatas", new List<SkillData> { skill });
            owner.Initialize(owner.UnitData, 0);

            Assert.That(DecisionSystem.Decide(owner, enemy, 0.9f).Type,
                Is.EqualTo(UnitIntentType.CastSkill));
        }

        /// <summary>Any 단일 대상은 아군만이 아니라 더 가까운 적도 선택합니다.</summary>
        [Test]
        public void SingleTarget_AnyCanSelectEnemy()
        {
            UnitObject owner = CreateUnit(0, position: Vector3.zero);
            UnitObject enemy = CreateUnit(1, position: Vector3.right);
            var targeting = new SingleUnitSkillTargeting();
            SetField(targeting, "m_Relation", SkillTargetRelation.Any);
            SetField(targeting, "m_IncludeSelf", false);
            SkillData data = CreateSkill(null);
            var targets = new SkillTargetSet();

            Assert.That(targeting.TryResolve(owner, data, new SkillUseRequest(), targets), Is.True);
            Assert.That(targets[0].Unit, Is.SameAs(enemy));
        }

        /// <summary>범위 타기팅은 양 팀을 수집하고 이동한 대상을 시전 완료 때 다시 검사합니다.</summary>
        [Test]
        public void AreaTarget_AnyRebuildsTargetsAtImpact()
        {
            UnitObject owner = CreateUnit(0, position: Vector3.zero);
            UnitObject enemy = CreateUnit(1, position: Vector3.right);
            var targeting = new AreaSkillTargeting();
            SetField(targeting, "m_Relation", SkillTargetRelation.Any);
            SetField(targeting, "m_CenterOnOwner", true);
            SkillData data = CreateSkill(null);
            var targets = new SkillTargetSet();
            targeting.TryResolve(owner, data, new SkillUseRequest(), targets);
            Assert.That(targets.Count, Is.EqualTo(2));

            enemy.transform.position = Vector3.right * 10f;
            Assert.That(targeting.Revalidate(owner, data, targets), Is.True);
            Assert.That(targets.Count, Is.EqualTo(1));
            Assert.That(targets[0].Unit, Is.SameAs(owner));
        }

        /// <summary>검색 거리 경계에 처음 발견한 적이 있어도 null 후보를 참조하지 않습니다.</summary>
        [Test]
        public void SpatialIndex_IncludesExactRangeBoundary()
        {
            UnitObject owner = CreateUnit(0, position: Vector3.zero);
            UnitObject enemy = CreateUnit(1, position: Vector3.right * 2f);
            var index = new UnitSpatialIndex();
            index.Rebuild(new[] { owner }, new[] { enemy });

            Assert.That(index.FindNearestEnemy(owner, 2f), Is.SameAs(enemy));
        }

        /// <summary>지속 피해로 사망하면서 상태 목록이 비워져도 이중 제거가 없어야 합니다.</summary>
        [Test]
        public void PeriodicDamage_DeathDuringTickSafelyClearsEffects()
        {
            UnitObject owner = CreateUnit(0, position: Vector3.zero);
            DebuffData status = ScriptableObject.CreateInstance<DebuffData>();
            m_Created.Add(status);
            var damage = new PeriodicDamageStatusBehavior();
            SetField(damage, "m_Damage", 10000f);
            SetField(status, "m_Behavior", damage);
            SetField(status, "m_Duration", 5f);
            owner.ApplyStatusEffect(status, owner);

            typeof(UnitObject).GetMethod("UpdateDataStatusEffects",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, new object[] { 1f });

            Assert.That(owner.IsDead, Is.True);
            Assert.That(owner.ActiveDataEffects, Is.Empty);
        }

        /// <summary>다양한 비율과 안전 영역에서도 제작 화면은 균일 배율로 내부에 들어갑니다.</summary>
        [TestCase(360, 640, 0, 0)]
        [TestCase(360, 800, 24, 34)]
        [TestCase(768, 1024, 0, 20)]
        [TestCase(1024, 768, 32, 24)]
        public void ResponsiveScale_FitsSafeArea(int width, int height, int top, int bottom)
        {
            Rect safeArea = UI_ResponsiveCanvas.ClampSafeArea(
                new Vector2(width, height), new Rect(0f, bottom, width, height - top - bottom));
            float scale = UI_ResponsiveCanvas.CalculateScale(new Vector2(1080f, 1920f), safeArea);

            Assert.That(1080f * scale, Is.LessThanOrEqualTo(safeArea.width + 0.001f));
            Assert.That(1920f * scale, Is.LessThanOrEqualTo(safeArea.height + 0.001f));
            Assert.That(scale, Is.GreaterThan(0f));
        }

        /// <summary>같은 이름의 세 번째 프리팹도 모호한 문자열 조회를 되살리지 않습니다.</summary>
        [Test]
        public void Pool_ThreeEqualNamesRemainAmbiguous()
        {
            var root = new GameObject("PoolRegressionRoot");
            m_Created.Add(root);
            var pool = new ObjectPoolingFactory<Transform>(root.transform);

            for (int i = 0; i < 3; i++)
            {
                var prefab = new GameObject("SharedName");
                m_Created.Add(prefab);
                Assert.That(pool.Register(prefab, PoolPolicy.Default()), Is.True);
            }

            Assert.That(pool.Spawn("SharedName", Vector3.zero), Is.Null);
            pool.Clear();
        }

        /// <summary>이벤트 폭주가 큐 용량을 넘으면 재귀 실행 대신 초과량을 기록합니다.</summary>
        [Test]
        public void EventQueue_OverflowDoesNotDispatchRecursively()
        {
            BattleSimulation simulation = BattleSimulation.EnsureExists();
            typeof(BattleSimulation).GetMethod("DrainSkillEvents",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(simulation, null);
            int overflowBefore = BattleSimulation.EventOverflowCount;
            var context = new SkillTriggerContext();

            for (int i = 0; i < 2049; i++)
            {
                BattleSimulation.EnqueueSkillEvent(in context);
            }

            Assert.That(BattleSimulation.EventOverflowCount, Is.EqualTo(overflowBefore + 1));
            typeof(BattleSimulation).GetMethod("DrainSkillEvents",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(simulation, null);
        }

        /// <summary>제거한 효과는 현재 콜백이 끝날 때까지 상태와 풀 대여를 보존합니다.</summary>
        [Test]
        public void RemovedEffect_IsNotReusedInsideItsCallback()
        {
            UnitObject owner = CreateUnit(0);
            var behavior = new RemoveAndReapply();
            BuffData data = CreateStatus(behavior);
            StatusEffectRuntime first = owner.ApplyStatusEffect(data);
            Invoke(owner, "SimulationTick", 0.05f);

            Assert.That(behavior.OwnerAfterRemoval, Is.SameAs(owner));
            Assert.That(behavior.Replacement, Is.Not.SameAs(first));
            Assert.That(first.Owner, Is.Null);
            Assert.That(owner.ActiveDataEffects.Count, Is.EqualTo(1));
        }

        /// <summary>영구 원본에도 유한 지속 시간 덮어쓰기가 적용됩니다.</summary>
        [Test]
        public void DurationOverride_ExpiresPermanentDefinition()
        {
            UnitObject owner = CreateUnit(0);
            BuffData data = CreateStatus(new ShieldStatusBehavior());
            SetField(data, "m_Duration", 0f);
            StatusEffectRuntime runtime = owner.ApplyStatusEffect(data, owner, 0.1f);
            Assert.That(runtime.IsPermanent, Is.False);
            Invoke(owner, "SimulationTick", 0.2f);
            Assert.That(owner.ActiveDataEffects, Is.Empty);
        }

        /// <summary>고정 피해는 방어력을 보호막 소모량에 더하지 않습니다.</summary>
        [Test]
        public void FixedDamage_ShieldConsumesOnlyIncomingDamage()
        {
            UnitObject target = CreateUnit(1, 50f);
            var shield = new ShieldStatusBehavior();
            SetField(shield, "m_ShieldAmount", 25f);
            BuffData data = CreateStatus(shield);
            SetField(data, "m_Duration", 0f);
            StatusEffectRuntime runtime = target.ApplyStatusEffect(data);
            var hit = new DamageContext { Target = target, Amount = 20f, IgnoreDefense = true };
            DamageResult result = target.ApplyDamageDetailed(in hit);
            Assert.That(result.HpLost, Is.Zero);
            Assert.That(result.ShieldAbsorbed, Is.EqualTo(20f));
            Assert.That(result.Killed, Is.False);
            Assert.That(runtime.FloatState, Is.EqualTo(5f));
            Assert.That(target.ApplyDamage(in hit), Is.EqualTo(15f));
            Invoke(target, "SimulationTick", 0.05f);
            Assert.That(target.ActiveDataEffects, Is.Empty);
        }

        /// <summary>방어·치명타를 계산한 피해에서 보호막을 차감합니다.</summary>
        [Test]
        public void CriticalDamage_AppliesDefenseBeforeShield()
        {
            UnitObject target = CreateUnit(1, 10f);
            var shield = new ShieldStatusBehavior();
            SetField(shield, "m_ShieldAmount", 20f);
            target.ApplyStatusEffect(CreateStatus(shield));
            var hit = new DamageContext { Target = target, Amount = 30f, IsCritical = true };
            Assert.That(target.ApplyDamage(in hit), Is.EqualTo(10f));
        }

        /// <summary>과잉 피해는 실제 감소 체력까지만 흡혈·로그에 전달됩니다.</summary>
        [Test]
        public void Overkill_ReturnsActualHealthLost()
        {
            UnitObject target = CreateUnit(1);
            float hp = target.CurrentHp;
            var hit = new DamageContext { Target = target, Amount = 10000f };
            DamageResult result = target.ApplyDamageDetailed(in hit);
            Assert.That(result.HpLost, Is.EqualTo(hp));
            Assert.That(result.ShieldAbsorbed, Is.Zero);
            Assert.That(result.Killed, Is.True);
        }

        /// <summary>보호막 알림의 중첩 피해로 사망해도 사망 처리는 한 번만 수행합니다.</summary>
        [Test]
        public void NestedDamage_DoesNotReportDeathTwice()
        {
            UnitObject target = CreateUnit(0);
            target.ApplyStatusEffect(CreateStatus(new ShieldStatusBehavior()));
            int deaths = 0;
            target.OnShieldAbsorb += KillDuringAbsorb;
            target.OnDied += RecordDeath;
            target.ApplyDamage(30f);
            Assert.That(deaths, Is.EqualTo(1));

            void KillDuringAbsorb(float absorbed)
            {
                target.ApplyDamage(10000f);
            }
            void RecordDeath(UnitObject attacker)
            {
                deaths++;
            }
        }

        /// <summary>무기 교체 후에도 능력치 증가 효과가 적용됩니다.</summary>
        [Test]
        public void WeaponOverride_ComposesWithBuff()
        {
            UnitObject owner = CreateUnit(0);
            owner.SetWeaponOverride(null, 30f, 2f, 4f);
            var modifier = new UnitStat { attackPower = 5f, attackSpeed = 1f, attackRange = 2f };
            Invoke(owner, "ApplyStatModifier", modifier, true);
            Assert.That(owner.CurrentAttackPower, Is.EqualTo(35f));
            Assert.That(owner.CurrentAttackRange, Is.EqualTo(6f));
        }

        /// <summary>범위 공격의 첫 대상이 제거되어도 나머지 대상이 피해를 받습니다.</summary>
        [Test]
        public void Explosion_KillsEveryCapturedTarget()
        {
            UnitObject source = CreateUnit(0);
            UnitObject first = CreateUnit(1);
            UnitObject second = CreateUnit(1);
            UnitObject third = CreateUnit(1);
            var effect = new SpawnProjectileSkillEffect();
            SetField(effect, "m_ExplosionRadius", 5f);
            var payload = new ProjectileImpactPayload(new UnitHandle(source), 0, 10000f, false, true, false, effect);
            effect.ApplyImpact(in payload, first, Vector3.zero);
            Assert.That(first.IsDead && second.IsDead && third.IsDead, Is.True);
        }

        /// <summary>등록 목록 앞쪽 유닛 제거가 현재 유닛의 중복 틱을 만들지 않습니다.</summary>
        [Test]
        public void SimulationMutation_DoesNotTickSurvivorTwice()
        {
            UnitObject victim = CreateUnit(1);
            UnitObject actor = CreateUnit(0);
            var behavior = new KillOther { Target = victim };
            actor.ApplyStatusEffect(CreateStatus(behavior));
            Invoke(BattleSimulation.EnsureExists(), "AdvanceSimulation", 0.05f);
            Assert.That(behavior.Ticks, Is.EqualTo(1));
        }

        /// <summary>느린 프레임에서도 모든 전투 로직은 최대 네 틱만 진행합니다.</summary>
        [Test]
        public void AcceptedClock_CapsLongFrameAndStopsAtResult()
        {
            Invoke(BattleSimulation.EnsureExists(), "AdvanceSimulation", 2f);
            Assert.That(BattleSimulation.ElapsedBattleTime, Is.EqualTo(0.2f).Within(0.001f));
            BattleSimulation.FreezeBattle();
            Invoke(BattleSimulation.EnsureExists(), "AdvanceSimulation", 2f);
            Assert.That(BattleSimulation.ElapsedBattleTime, Is.EqualTo(0.2f).Within(0.001f));
        }

        /// <summary>강제 이동은 공격 잠금을 끊고 일반 이동 명령보다 우선합니다.</summary>
        [Test]
        public void ForcedMovement_OverridesAttackAndAiDestination()
        {
            UnitObject owner = CreateUnit(0);
            var action = (UnitActionController)Get(owner, "m_ActionController");
            action.TryBeginAttack(2f);
            owner.ApplyForcedMovement(Vector3.right, 0.1f);
            owner.MoveTo(Vector3.left * 10f);
            Invoke(owner, "SimulationTick", 0.1f);
            Assert.That(owner.Runtime.Position.x, Is.EqualTo(1f).Within(0.01f));
            Assert.That(owner.IsAttacking, Is.False);
        }

        /// <summary>결과가 확정되면 체력과 상태 효과를 더 이상 변경하지 않습니다.</summary>
        [Test]
        public void FrozenBattle_RejectsLateDamageHealAndStatus()
        {
            UnitObject target = CreateUnit(0);
            target.ApplyDamage(20f);
            float hp = target.CurrentHp;
            BattleSimulation.FreezeBattle();
            Assert.That(target.ApplyDamage(100f), Is.Zero);
            Assert.That(target.Heal(100f), Is.Zero);
            Assert.That(target.ApplyStatusEffect(CreateStatus(new ShieldStatusBehavior())), Is.Null);
            Assert.That(target.CurrentHp, Is.EqualTo(hp));
        }

        /// <summary>원본 컬렉션과 외부 캐스팅으로 확정 결과를 바꿀 수 없습니다.</summary>
        [Test]
        public void ConfirmedData_IsImmutable()
        {
            var ticket = new RoundBetTicket();
            ticket.SetWager(100);
            Invoke(ticket, "MarkPlaced");
            Assert.Throws<InvalidOperationException>(ChangeWager);

            var rows = new List<SurvivingRowPrediction>();
            var result = new CombatResultSnapshot(Team.Red, 10f, 1, 0, rows, null);
            rows.Add(default);
            Assert.That(result.SurvivingRows.Count, Is.Zero);
            Assert.That(((ICollection<SurvivingRowPrediction>)result.SurvivingRows).IsReadOnly, Is.True);

            void ChangeWager()
            {
                ticket.SetWager(200);
            }
        }

        /// <summary>결과 화면 입력이 Enter 이전에 와도 완료 대기가 보존됩니다.</summary>
        [Test]
        public void ResultEarlyContinue_CompletesPreparedWait()
        {
            var root = new GameObject("ResultTest");
            m_Created.Add(root);
            ResultPhase phase = root.AddComponent<ResultPhase>();
            phase.InitializePhase(new RoundContext());
            Invoke(phase, "InitializeResult");
            Invoke(phase, "OnContinueClicked");
            var source = (AwaitableCompletionSource)Get(phase, "m_PhaseCompletionSource");
            Assert.That(source.Awaitable.GetAwaiter().IsCompleted, Is.True);
            Assert.That(phase.IsPhaseCompleted, Is.True);
        }

        /// <summary>적이 없는 실제 AI 갱신에서도 부상당한 아군에게 회복 스킬을 사용합니다.</summary>
        [Test]
        public void DecisionAgent_HealsWithoutEnemyTarget()
        {
            UnitObject owner = CreateUnit(0);
            UnitObject ally = CreateUnit(0);
            ally.ApplyDamage(30f);
            float before = ally.CurrentHp;
            SkillData skill = ScriptableObject.CreateInstance<SkillData>();
            m_Created.Add(skill);
            SetField(skill, "m_Targeting", new LowestHealthAllySkillTargeting());
            SetField(skill, "m_Effects", new List<SkillEffectDefinition> { new HealSkillEffect() });
            Invoke(owner, "AddRuntimeSkill", skill);

            AIData ai = ScriptableObject.CreateInstance<AIData>();
            m_Created.Add(ai);
            SetField(ai, "m_InitialSearchDelay", 0f);
            var agent = new UnitDecisionAgent(ai);
            agent.Initialize(owner);
            agent.UpdateAI(0.05f);

            Assert.That(ally.CurrentHp, Is.GreaterThan(before));
        }

        /// <summary>다음 라운드 준비 중에는 시작 버프가 적용되고 시간은 진행하지 않습니다.</summary>
        [Test]
        public void PrepareAfterFreeze_AllowsInitialEffectsWithoutAdvancingTime()
        {
            BattleSimulation.FreezeBattle();
            BattleSimulation.PrepareBattle();
            UnitObject owner = CreateUnit(0);
            Assert.That(owner.ApplyStatusEffect(CreateStatus(new ShieldStatusBehavior())), Is.Not.Null);
            Invoke(BattleSimulation.EnsureExists(), "AdvanceSimulation", 1f);
            Assert.That(BattleSimulation.ElapsedBattleTime, Is.Zero);
        }

        /// <summary>테스트 전용 상태 효과를 만듭니다.</summary>
        private BuffData CreateStatus(StatusEffectBehaviorDefinition behavior)
        {
            BuffData data = ScriptableObject.CreateInstance<BuffData>();
            m_Created.Add(data);
            SetField(data, "m_Behavior", behavior);
            SetField(data, "m_Duration", 10f);
            return data;
        }

        /// <summary>상속 계층의 직렬화 필드를 찾습니다.</summary>
        private static FieldInfo Field(object target, string name)
        {
            Type type = target.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    return field;
                }
                type = type.BaseType;
            }
            throw new MissingFieldException(name);
        }

        /// <summary>테스트 대상의 실행 상태를 읽습니다.</summary>
        private static object Get(object target, string name)
        {
            return Field(target, name).GetValue(target);
        }

        /// <summary>공개 API를 늘리지 않고 내부 실행 경계를 검증합니다.</summary>
        private static void Invoke(object target, string name, params object[] args)
        {
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        }

        private sealed class RemoveAndReapply : StatusEffectBehaviorDefinition
        {
            public UnitObject OwnerAfterRemoval;
            public StatusEffectRuntime Replacement;

            /// <summary>실행 중 자신을 제거한 뒤 같은 효과를 다시 적용합니다.</summary>
            public override void OnTick(StatusEffectRuntime runtime, float deltaTime)
            {
                UnitObject owner = runtime.Owner;
                StatusEffectData data = runtime.Data;
                owner.RemoveStatusEffect(runtime);
                OwnerAfterRemoval = runtime.Owner;
                Replacement = owner.ApplyStatusEffect(data);
            }
        }

        private sealed class KillOther : StatusEffectBehaviorDefinition
        {
            public UnitObject Target;
            public int Ticks;

            /// <summary>등록 목록 앞쪽 대상의 제거를 유발합니다.</summary>
            public override void OnTick(StatusEffectRuntime runtime, float deltaTime)
            {
                Ticks++;
                Target.ApplyDamage(10000f);
            }
        }

        /// <summary>기본 스탯의 테스트 유닛을 등록합니다.</summary>
        private UnitObject CreateUnit(int team, float defense = 0f, Vector3 position = default)
        {
            UnitData data = ScriptableObject.CreateInstance<UnitData>();
            m_Created.Add(data);
            UnitStat stat = UnitStat.Default;
            stat.defense = defense;
            SetField(data, "m_BaseStat", stat);
            var gameObject = new GameObject("ExtensibilityTestUnit");
            m_Created.Add(gameObject);
            gameObject.transform.position = position;
            gameObject.AddComponent<BoxCollider>();
            UnitObject unit = gameObject.AddComponent<UnitObject>();
            unit.Initialize(data, team);
            return unit;
        }

        /// <summary>공용 에셋을 건드리지 않는 스킬 설정을 준비합니다.</summary>
        private SkillData CreateSkill(SkillBehaviorDefinition behavior)
        {
            SkillData data = ScriptableObject.CreateInstance<SkillData>();
            m_Created.Add(data);
            SetField(data, "m_SkillName", "Regression");
            SetField(data, "m_Behavior", behavior);
            if (behavior != null)
            {
                SetField(data, "m_ExecutionMode", SkillExecutionMode.BehaviorOnly);
            }
            return data;
        }

        /// <summary>상속 계층의 직렬화 필드를 찾아 테스트 설정을 입력합니다.</summary>
        private static void SetField(object target, string name, object value)
        {
            Field(target, name).SetValue(target, value);
        }

        [Serializable]
        public sealed class ProbeSkill : SkillBehaviorDefinition
        {
            public static readonly List<ProbeSkill> Instances = new List<ProbeSkill>();

            public List<int> Values = new List<int>();
            public GameObject Reference;

            [NonSerialized] public float Elapsed;
            [NonSerialized] public int ExecutionCount;
            [NonSerialized] public int BattleEndCount;
            [NonSerialized] public int RemovalCount;

            public override bool UsesCustomTargeting => true;

            /// <summary>유닛마다 생성된 복사본을 관찰합니다.</summary>
            public override void OnCreated(SkillRuntime runtime)
            {
                Instances.Add(this);
            }

            /// <summary>같은 클래스에서 대상을 정합니다.</summary>
            public override bool TryResolveTargets(SkillRuntime runtime,
                in SkillUseRequest request, SkillTargetSet targets)
            {
                return targets.Add(runtime.Owner);
            }

            /// <summary>같은 클래스에서 대상 유효성을 검사합니다.</summary>
            public override bool RevalidateTargets(SkillRuntime runtime, SkillTargetSet targets)
            {
                return targets.Count == 1 && targets[0].IsAlive;
            }

            /// <summary>실행 횟수를 기록합니다.</summary>
            public override SkillExecutionResult Execute(in SkillEffectContext context)
            {
                ExecutionCount++;
                return SkillExecutionResult.Success;
            }

            /// <summary>유닛별 시간 상태를 기록합니다.</summary>
            public override void Tick(SkillRuntime runtime, float deltaTime)
            {
                Elapsed += deltaTime;
            }

            /// <summary>쿨다운 중 전투 종료 전달을 관찰합니다.</summary>
            public override void OnTrigger(SkillRuntime runtime, in SkillTriggerContext context)
            {
                if (context.Trigger == SkillTriggerType.OnBattleEnd)
                {
                    BattleEndCount++;
                }
            }

            /// <summary>정리 콜백 횟수를 기록합니다.</summary>
            public override void OnRemoved(SkillRuntime runtime)
            {
                RemovalCount++;
            }
        }

        [Serializable]
        private sealed class ExhaustedEffect : SkillEffectDefinition
        {
            /// <summary>앞 효과 적용 이후 자원 부족을 재현합니다.</summary>
            public override SkillExecutionResult Apply(in SkillEffectContext context)
            {
                return SkillExecutionResult.PoolExhausted;
            }
        }
    }
}
#endif
