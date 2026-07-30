#if UNITY_EDITOR
using InTheArena.Camera;
using InTheArena.MainGame;
using InTheArena.Unit;
using NUnit.Framework;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace InTheArena.Editor.Unit
{
    public sealed class UnitRuntimeArchitectureTests
    {
        [TearDown]
        public void TearDown()
        {
            UnitRegistry.Clear();
        }

        [Test]
        public void StatusEffectRuntime_StateIsIsolatedPerUnit()
        {
            BuffData data = ScriptableObject.CreateInstance<BuffData>();
            var first = new StatusEffectRuntime();
            var second = new StatusEffectRuntime();

            first.Initialize(data, null, null, 3f);
            second.Initialize(data, null, null, 3f);
            first.Stacks = 3;
            first.CustomTimer = 2f;

            Assert.That(second.Stacks, Is.EqualTo(1));
            Assert.That(second.CustomTimer, Is.EqualTo(0f));
            Object.DestroyImmediate(data);
        }

        [Test]
        public void ActionController_HitDoesNotReplaceAttackOrCast()
        {
            var controller = new UnitActionController();
            controller.Reset();

            Assert.That(controller.TryBeginAttack(0.3f), Is.True);
            Assert.That(controller.State, Is.EqualTo(UnitActionState.Attack));
            controller.Tick(0.1f);
            Assert.That(controller.State, Is.EqualTo(UnitActionState.Attack));
            controller.Tick(0.2f);
            Assert.That(controller.State, Is.EqualTo(UnitActionState.Idle));

            Assert.That(controller.TryBeginCast(1f), Is.True);
            controller.Tick(1f);
            Assert.That(controller.State, Is.EqualTo(UnitActionState.Casting));
            controller.CompleteCast();
            Assert.That(controller.State, Is.EqualTo(UnitActionState.Idle));
        }

        [Test]
        public void ActionController_StunAndDeathInterruptProtectedActions()
        {
            var controller = new UnitActionController();
            controller.Reset();
            controller.TryBeginAttack(0.3f);

            controller.SetStunned(true);
            Assert.That(controller.State, Is.EqualTo(UnitActionState.Stunned));
            controller.SetStunned(false);
            Assert.That(controller.State, Is.EqualTo(UnitActionState.Idle));

            controller.TryBeginCast(1f);
            controller.MarkDead();
            Assert.That(controller.State, Is.EqualTo(UnitActionState.Dead));
            Assert.That(controller.TryBeginAttack(0.3f), Is.False);
        }

        [Test]
        public void SpatialIndex_NearestMatchesExpectedEnemy()
        {
            UnitData data = CreateUnitData();
            InTheArena.Unit.Unit owner = CreateUnit(data, 0, Vector3.zero);
            InTheArena.Unit.Unit near = CreateUnit(data, 1, new Vector3(1.25f, 0f, 0.25f));
            InTheArena.Unit.Unit far = CreateUnit(data, 1, new Vector3(4f, 0f, 0f));
            var index = new UnitSpatialIndex();
            index.Rebuild(
                new[] { owner },
                new[] { near, far });

            InTheArena.Unit.Unit result = index.FindNearestEnemy(owner, 0f);

            Assert.That(result, Is.SameAs(near));
            Object.DestroyImmediate(owner.gameObject);
            Object.DestroyImmediate(near.gameObject);
            Object.DestroyImmediate(far.gameObject);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void EngagementSlots_AreUniqueAndReleasedWithOwner()
        {
            UnitData data = CreateUnitData();
            InTheArena.Unit.Unit target = CreateUnit(data, 1, Vector3.zero);
            InTheArena.Unit.Unit first = CreateUnit(data, 0, new Vector3(-2f, 0f, 0f));
            InTheArena.Unit.Unit second = CreateUnit(data, 0, new Vector3(2f, 0f, 0f));

            Vector3 firstPosition = UnitRegistry.GetEngagementPosition(first, target);
            Vector3 secondPosition = UnitRegistry.GetEngagementPosition(second, target);
            Assert.That(firstPosition, Is.Not.EqualTo(secondPosition));

            EngagementSlotSystem.Release(first);
            Vector3 reacquired = UnitRegistry.GetEngagementPosition(first, target);
            Assert.That(float.IsNaN(reacquired.x), Is.False);
            Object.DestroyImmediate(target.gameObject);
            Object.DestroyImmediate(first.gameObject);
            Object.DestroyImmediate(second.gameObject);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void EngagementSlots_HighUnitIdStillUsesInnermostAvailableRing()
        {
            UnitData data = CreateUnitData();
            InTheArena.Unit.Unit target = CreateUnit(data, 1, Vector3.zero);
            InTheArena.Unit.Unit owner = CreateUnit(data, 0, new Vector3(-2f, 0f, 0f));
            typeof(InTheArena.Unit.Unit).GetMethod(
                    "AssignSimulationId",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(owner, new object[] { 96 });

            Vector3 position = UnitRegistry.GetEngagementPosition(owner, target);
            float expectedRadius = EngagementSlotSystem.GetContactDistance(owner, target);

            Assert.That(
                Vector3.Distance(position, target.GroundPosition),
                Is.EqualTo(expectedRadius).Within(0.001f));
            Object.DestroyImmediate(target.gameObject);
            Object.DestroyImmediate(owner.gameObject);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void EngagementSlot_StartsOnTheOwnersApproachSide()
        {
            UnitData data = CreateUnitData();
            InTheArena.Unit.Unit target = CreateUnit(data, 1, Vector3.zero);
            InTheArena.Unit.Unit owner =
                CreateUnit(data, 0, new Vector3(-4f, 0f, 0f));

            Vector3 position = UnitRegistry.GetEngagementPosition(owner, target);

            Assert.That(position.x, Is.LessThan(0f));
            Assert.That(Mathf.Abs(position.z), Is.LessThan(0.001f));
            Object.DestroyImmediate(target.gameObject);
            Object.DestroyImmediate(owner.gameObject);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void DecisionSystem_MeleeCanAttackFromItsContactSlot()
        {
            UnitData data = CreateUnitData();
            UnitStat stat = UnitStat.Default;
            stat.attackRange = 1f;
            typeof(UnitData).GetField(
                    "m_BaseStat",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(data, stat);
            InTheArena.Unit.Unit owner = CreateUnit(data, 0, Vector3.zero);
            float contactDistance =
                data.VisualRadius * 2f + EngagementSlotSystem.ContactPadding;
            InTheArena.Unit.Unit target =
                CreateUnit(data, 1, new Vector3(contactDistance, 0f, 0f));

            UnitIntent intent = DecisionSystem.Decide(owner, target, 0.9f);

            Assert.That(intent.Type, Is.EqualTo(UnitIntentType.BasicAttack));
            Object.DestroyImmediate(target.gameObject);
            Object.DestroyImmediate(owner.gameObject);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void DecisionSystem_RangedDoesNotMoveInsideNominalAttackRange()
        {
            UnitData sourceArcherData = AssetDatabase.LoadAssetAtPath<UnitData>(
                "Assets/ScriptableObject/Unit/Unit_Base/UnitData_Archer.asset");
            Assert.That(sourceArcherData, Is.Not.Null);
            UnitData archerData = Object.Instantiate(sourceArcherData);
            typeof(UnitData).GetField(
                    "m_SkillDatas",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(archerData, new System.Collections.Generic.List<SkillData>());
            InTheArena.Unit.Unit archer = CreateUnit(archerData, 0, Vector3.zero);
            InTheArena.Unit.Unit target =
                CreateUnit(CreateUnitData(), 1, new Vector3(2.9f, 0f, 0f));

            UnitIntent intent = DecisionSystem.Decide(archer, target, 0.9f);

            Assert.That(intent.Type, Is.EqualTo(UnitIntentType.BasicAttack));
            target.transform.position = new Vector3(1f, 0f, 0f);
            typeof(InTheArena.Unit.Unit).GetField(
                    "m_SimulationPosition",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, target.transform.position);
            intent = DecisionSystem.Decide(archer, target, 0.9f);
            Assert.That(intent.Type, Is.EqualTo(UnitIntentType.BasicAttack));
            UnitData targetData = target.UnitData;
            Object.DestroyImmediate(archer.gameObject);
            Object.DestroyImmediate(target.gameObject);
            Object.DestroyImmediate(archerData);
            Object.DestroyImmediate(targetData);
        }

        [Test]
        public void ReciprocalKnightTargets_EnterCombatInsteadOfDriftingTogether()
        {
            UnitData sourceKnightData = AssetDatabase.LoadAssetAtPath<UnitData>(
                "Assets/ScriptableObject/Unit/Unit_Base/UnitData_Knight.asset");
            Assert.That(sourceKnightData, Is.Not.Null);
            UnitData knightData = Object.Instantiate(sourceKnightData);
            typeof(UnitData).GetField(
                    "m_SkillDatas",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(knightData, new System.Collections.Generic.List<SkillData>());
            typeof(UnitData).GetField(
                    "m_StartingStatusEffects",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(
                    knightData,
                    new System.Collections.Generic.List<StatusEffectData>());
            InTheArena.Unit.Unit first =
                CreateUnit(knightData, 0, new Vector3(-2f, 0f, 0f));
            InTheArena.Unit.Unit second =
                CreateUnit(knightData, 1, new Vector3(2f, 0f, 0f));
            UnitRegistry.Register(first);
            UnitRegistry.Register(second);
            first.SetAIActive(true);
            second.SetAIActive(true);
            MethodInfo tick = typeof(InTheArena.Unit.Unit).GetMethod(
                "SimulationTick",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo frame = typeof(InTheArena.Unit.Unit).GetMethod(
                "SimulationFrame",
                BindingFlags.Instance | BindingFlags.NonPublic);

            for (int i = 0; i < 200 && !first.IsDead && !second.IsDead; i++)
            {
                UnitRegistry.RebuildSpatialIndex();
                tick.Invoke(first, new object[] { 0.05f });
                tick.Invoke(second, new object[] { 0.05f });
                frame.Invoke(first, new object[] { 0.05f, 1f });
                frame.Invoke(second, new object[] { 0.05f, 1f });
                if (first.CurrentHp < first.MaxHp || second.CurrentHp < second.MaxHp)
                    break;
            }

            Assert.That(
                first.CurrentHp < first.MaxHp || second.CurrentHp < second.MaxHp,
                Is.True,
                $"Reciprocal melee targets never entered attack range. " +
                $"distance={Vector3.Distance(first.GroundPosition, second.GroundPosition):F2}, " +
                $"first={first.GroundPosition}, second={second.GroundPosition}, " +
                $"firstState={first.ActionState}/{first.AI?.CurrentState}, " +
                $"secondState={second.ActionState}/{second.AI?.CurrentState}");
            Object.DestroyImmediate(first.gameObject);
            Object.DestroyImmediate(second.gameObject);
            Object.DestroyImmediate(knightData);
        }

        [Test]
        public void Knight_StationaryTargetAtEngagementSlotReceivesDamage()
        {
            UnitData sourceKnightData = AssetDatabase.LoadAssetAtPath<UnitData>(
                "Assets/ScriptableObject/Unit/Unit_Base/UnitData_Knight.asset");
            Assert.That(sourceKnightData, Is.Not.Null);
            UnitData knightData = Object.Instantiate(sourceKnightData);
            typeof(UnitData).GetField(
                    "m_SkillDatas",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(knightData, new System.Collections.Generic.List<SkillData>());
            typeof(UnitData).GetField(
                    "m_StartingStatusEffects",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(
                    knightData,
                    new System.Collections.Generic.List<StatusEffectData>());
            InTheArena.Unit.Unit knight =
                CreateUnit(knightData, 0, new Vector3(-2f, 0f, 0f));
            InTheArena.Unit.Unit target =
                CreateUnit(knightData, 1, Vector3.zero);
            UnitRegistry.Register(knight);
            UnitRegistry.Register(target);
            knight.SetAIActive(true);
            target.SetAIActive(false);
            MethodInfo tick = typeof(InTheArena.Unit.Unit).GetMethod(
                "SimulationTick",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo frame = typeof(InTheArena.Unit.Unit).GetMethod(
                "SimulationFrame",
                BindingFlags.Instance | BindingFlags.NonPublic);

            for (int i = 0; i < 200 && target.CurrentHp >= target.MaxHp; i++)
            {
                UnitRegistry.RebuildSpatialIndex();
                tick.Invoke(knight, new object[] { 0.05f });
                frame.Invoke(knight, new object[] { 0.05f, 1f });
            }

            Assert.That(
                target.CurrentHp,
                Is.LessThan(target.MaxHp),
                $"Knight stopped without attacking. " +
                $"distance={Vector3.Distance(knight.GroundPosition, target.GroundPosition):F2}, " +
                $"state={knight.ActionState}/{knight.AI?.CurrentState}");
            Object.DestroyImmediate(knight.gameObject);
            Object.DestroyImmediate(target.gameObject);
            Object.DestroyImmediate(knightData);
        }

        [Test]
        public void DecisionAgent_RetainsValidTargetAndReacquiresAfterPoolVersionChanges()
        {
            UnitData data = CreateUnitData();
            AIData aiData = ScriptableObject.CreateInstance<AIData>();
            InTheArena.Unit.Unit owner = CreateUnit(data, 0, Vector3.zero);
            InTheArena.Unit.Unit retained = CreateUnit(data, 1, new Vector3(2f, 0f, 0f));
            InTheArena.Unit.Unit farther = CreateUnit(data, 1, new Vector3(4f, 0f, 0f));
            UnitRegistry.Register(owner);
            UnitRegistry.Register(retained);
            UnitRegistry.Register(farther);
            var agent = new UnitDecisionAgent(aiData);
            agent.Initialize(owner);
            agent.UpdateAI(0.1f);
            agent.UpdateAI(0.05f);
            Assert.That(agent.CurrentTarget, Is.SameAs(retained));

            InTheArena.Unit.Unit closer = CreateUnit(data, 1, new Vector3(1f, 0f, 0f));
            UnitRegistry.Register(closer);
            agent.UpdateAI(0.05f);
            Assert.That(agent.CurrentTarget, Is.SameAs(retained));

            retained.Initialize(data, 1);
            agent.UpdateAI(0.05f);
            Assert.That(agent.CurrentTarget, Is.SameAs(closer));

            Object.DestroyImmediate(owner.gameObject);
            Object.DestroyImmediate(retained.gameObject);
            Object.DestroyImmediate(farther.gameObject);
            Object.DestroyImmediate(closer.gameObject);
            Object.DestroyImmediate(aiData);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void BattleConfig_CopiesSpawnPlanInput()
        {
            var plans = new[]
            {
                new SpawnPlan(null, Team.Red, 2, new Vector3(1f, 0f, 3f))
            };
            var config = new BattleConfig(plans, RoundRule.None);
            plans[0] = new SpawnPlan(null, Team.Blue, 5, Vector3.zero);

            Assert.That(config.SpawnPlans[0].Team, Is.EqualTo(Team.Red));
            Assert.That(config.SpawnPlans[0].CellIndex, Is.EqualTo(2));
            Assert.That(config.SpawnPlans[0].Position, Is.EqualTo(new Vector3(1f, 0f, 3f)));
        }

        [Test]
        public void CameraPose_PortraitAspectKeepsFiniteDistance()
        {
            CameraSettings settings = ScriptableObject.CreateInstance<CameraSettings>();
            var cameraObject = new GameObject("CameraTest");
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.aspect = 9f / 16f;
            Bounds bounds = new Bounds(Vector3.zero, new Vector3(14f, 2f, 6f));

            CameraPose pose = CameraFramingCalculator.CalculatePose(bounds, camera, settings, 2f);

            Assert.That(float.IsNaN(pose.Position.x), Is.False);
            Assert.That(float.IsInfinity(pose.Position.magnitude), Is.False);
            Assert.That(pose.FieldOfView, Is.EqualTo(settings.FieldOfView));
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void CameraPose_UsesConfiguredFortyFiveDegreePitch()
        {
            CameraSettings settings = ScriptableObject.CreateInstance<CameraSettings>();
            var cameraObject = new GameObject("CameraTest");
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();

            CameraPose pose = CameraFramingCalculator.CalculatePose(
                new Bounds(Vector3.zero, new Vector3(10f, 1f, 6f)),
                camera,
                settings);

            Assert.That(Quaternion.Angle(
                pose.Rotation,
                Quaternion.Euler(settings.CameraAngleX, settings.CameraAngleY, 0f)), Is.LessThan(0.01f));
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void CameraPose_CompactBoundsNeverZoomsInsideMinimumDistance()
        {
            CameraSettings settings = ScriptableObject.CreateInstance<CameraSettings>();
            var cameraObject = new GameObject("CameraTest");
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.aspect = 9f / 16f;

            CameraPose pose = CameraFramingCalculator.CalculatePose(
                new Bounds(Vector3.zero, Vector3.one * 0.25f),
                camera,
                settings,
                settings.FramingPadding);

            Assert.That(GetGroundDistance(pose), Is.EqualTo(14f).Within(0.01f));
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void CameraPose_WideBoundsNeverExceedsMaximumDistance()
        {
            CameraSettings settings = ScriptableObject.CreateInstance<CameraSettings>();
            var cameraObject = new GameObject("CameraTest");
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.aspect = 9f / 16f;

            CameraPose pose = CameraFramingCalculator.CalculatePose(
                new Bounds(Vector3.zero, new Vector3(100f, 2f, 20f)),
                camera,
                settings,
                settings.FramingPadding);

            Assert.That(GetGroundDistance(pose), Is.EqualTo(60f).Within(0.01f));
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void CameraPose_PortraitBoundsStayInsideConfiguredSafeArea()
        {
            CameraSettings settings = ScriptableObject.CreateInstance<CameraSettings>();
            var cameraObject = new GameObject("CameraTest");
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.aspect = 9f / 16f;
            Bounds bounds = new Bounds(
                new Vector3(0f, 0.5f, 0f),
                new Vector3(8f, 1f, 4f));
            CameraPose pose = CameraFramingCalculator.CalculatePose(
                bounds,
                camera,
                settings,
                settings.FramingPadding);
            camera.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            camera.fieldOfView = pose.FieldOfView;

            Vector3 min = bounds.min - new Vector3(
                settings.FramingPadding,
                settings.FramingPadding * 0.5f,
                settings.FramingPadding);
            Vector3 max = bounds.max + new Vector3(
                settings.FramingPadding,
                settings.FramingPadding * 0.5f,
                settings.FramingPadding);
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        Vector3 viewport = camera.WorldToViewportPoint(new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z));
                        Assert.That(viewport.z, Is.GreaterThan(0f));
                        Assert.That(viewport.x, Is.InRange(
                            settings.SafeMarginHorizontal - 0.001f,
                            1f - settings.SafeMarginHorizontal + 0.001f));
                        Assert.That(viewport.y, Is.InRange(
                            settings.SafeMarginVertical - 0.001f,
                            1f - settings.SafeMarginVertical + 0.001f));
                    }
                }
            }

            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void FinalEliminationPose_CanZoomInsideNormalMinimum()
        {
            CameraSettings settings = ScriptableObject.CreateInstance<CameraSettings>();

            CameraPose pose = CameraFramingCalculator.CalculateFinalEliminationPose(
                new Bounds(new Vector3(3f, 0.5f, -2f), Vector3.one),
                settings);

            Assert.That(GetGroundDistance(pose), Is.EqualTo(8f).Within(0.01f));
            Assert.That(GetGroundDistance(pose), Is.LessThan(settings.MinFramingDistance));
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void UnitDeath_NormalDeathHidesImmediately()
        {
            UnitData data = CreateUnitData();
            InTheArena.Unit.Unit unit = CreateUnit(data);

            KillUnit(unit);

            Assert.That(unit.IsDead, Is.True);
            Assert.That(unit.gameObject.activeSelf, Is.False);
            Object.DestroyImmediate(unit.gameObject);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void UnitDeath_FinalPresentationCanHoldAndReleaseVisual()
        {
            UnitData data = CreateUnitData();
            InTheArena.Unit.Unit unit = CreateUnit(data);
            MethodInfo holdMethod = typeof(InTheArena.Unit.Unit).GetMethod(
                "HoldDeathPresentation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo completeMethod = typeof(InTheArena.Unit.Unit).GetMethod(
                "CompleteDeathPresentation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            unit.OnDied += _ => holdMethod.Invoke(unit, null);

            KillUnit(unit);

            Assert.That(unit.IsDead, Is.True);
            Assert.That(unit.gameObject.activeSelf, Is.True);
            completeMethod.Invoke(unit, null);
            Assert.That(unit.gameObject.activeSelf, Is.False);
            Object.DestroyImmediate(unit.gameObject);
            Object.DestroyImmediate(data);
        }

        private static UnitData CreateUnitData()
        {
            UnitData data = ScriptableObject.CreateInstance<UnitData>();
            typeof(UnitData).GetField(
                    "m_BaseStat",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(data, UnitStat.Default);
            return data;
        }

        private static InTheArena.Unit.Unit CreateUnit(UnitData data)
            => CreateUnit(data, 0, Vector3.zero);

        private static InTheArena.Unit.Unit CreateUnit(UnitData data, int team, Vector3 position)
        {
            var unitObject = new GameObject("UnitTest");
            unitObject.SetActive(false);
            unitObject.transform.position = position;
            unitObject.AddComponent<BoxCollider>();
            var unit = unitObject.AddComponent<InTheArena.Unit.Unit>();
            unit.Initialize(data, team);
            unitObject.SetActive(true);
            return unit;
        }

        private static void KillUnit(InTheArena.Unit.Unit unit)
        {
            typeof(InTheArena.Unit.Unit).GetField(
                    "m_CurrentHp",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(unit, 0f);
            typeof(InTheArena.Unit.Unit).GetMethod(
                    "Die",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(unit, new object[] { null });
        }

        private static float GetGroundDistance(CameraPose pose)
        {
            Vector3 forward = pose.Rotation * Vector3.forward;
            return -pose.Position.y / forward.y;
        }
    }
}
#endif
