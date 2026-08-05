#if UNITY_EDITOR
using InTheArena.Camera;
using InTheArena.MainGame;
using InTheArena.Unit;
using NUnit.Framework;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

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
                new SpawnPlan(null, Team.Red, 2, 4, new Vector3(1f, 0f, 3f))
            };
            var config = new BattleConfig(plans, RoundRule.None);
            plans[0] = new SpawnPlan(null, Team.Blue, 5, 1, Vector3.zero);

            Assert.That(config.SpawnPlans[0].Team, Is.EqualTo(Team.Red));
            Assert.That(config.SpawnPlans[0].CellIndex, Is.EqualTo(2));
            Assert.That(config.SpawnPlans[0].UnitNumber, Is.EqualTo(4));
            Assert.That(config.SpawnPlans[0].Position, Is.EqualTo(new Vector3(1f, 0f, 3f)));
        }

        [Test]
        public void CameraPose_PortraitAspectKeepsFiniteDistance()
        {
            CameraSettings settings = ScriptableObject.CreateInstance<CameraSettings>();
            var cameraObject = new GameObject("CameraTest");
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.aspect = 9f / 16f;
            camera.orthographic = false;
            settings.ProjMode = ProjectionMode.PerspectiveLegacy;
            Bounds bounds = new Bounds(Vector3.zero, new Vector3(14f, 2f, 6f));

            CameraPose pose = CameraFramingCalculator.CalculatePose(bounds, camera, new Rect(0,0,1,1), settings, 2f);

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
                new Rect(0,0,1,1),
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
                new Rect(0,0,1,1),
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
                new Rect(0,0,1,1),
                settings,
                settings.FramingPadding);

            Assert.That(GetGroundDistance(pose), Is.EqualTo(60f).Within(0.01f));
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(settings);
        }

        [Test]
        [Ignore("Known Issue: PerspectiveLegacy portrait bounds constraint is broken in 9:16 aspect. Will be tracked in a separate issue.")]
        public void CameraPose_PortraitBoundsStayInsideConfiguredSafeArea()
        {
            CameraSettings settings = ScriptableObject.CreateInstance<CameraSettings>();
            var cameraObject = new GameObject("CameraTest");
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.aspect = 9f / 16f;
            settings.ProjMode = ProjectionMode.PerspectiveLegacy;
            Bounds bounds = new Bounds(
                new Vector3(0f, 0.5f, 0f),
                new Vector3(8f, 1f, 4f));
            CameraPose pose = CameraFramingCalculator.CalculatePose(
                bounds,
                camera,
                new Rect(0,0,1,1),
                settings,
                settings.FramingPadding);
            camera.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            camera.fieldOfView = pose.FieldOfView;
            camera.orthographicSize = pose.OrthographicSize;

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
        public void CameraSettings_DefaultValuesMatchOrthographicAutoFramingContract()
        {
            CameraSettings settings = ScriptableObject.CreateInstance<CameraSettings>();

            Assert.That(settings.MinOrthographicSize, Is.EqualTo(3f));
            Assert.That(settings.FramingPadding, Is.EqualTo(0.75f));
            Assert.That(settings.SafeMarginHorizontal, Is.EqualTo(0.025f));
            Assert.That(settings.SafeMarginVertical, Is.EqualTo(0.06f));
            Assert.That(settings.DistanceDeadZone, Is.EqualTo(0.1f));
            Assert.That(settings.FinalEliminationOrthographicSize, Is.EqualTo(4f));

            Object.DestroyImmediate(settings);
        }

        [Test]
        public void FinalEliminationPose_UsesDedicatedOrthographicSize()
        {
            CameraSettings settings = ScriptableObject.CreateInstance<CameraSettings>();
            settings.ProjMode = ProjectionMode.Orthographic;
            var cameraObject = new GameObject("CameraTest");
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();

            CameraPose pose = CameraFramingCalculator.CalculateFinalEliminationPose(
                new Bounds(new Vector3(3f, 0.5f, -2f), Vector3.one),
                camera,
                new Rect(0,0,1,1),
                settings);

            Assert.That(pose.OrthographicSize, Is.EqualTo(4f).Within(0.001f));
            Assert.That(pose.OrthographicSize, Is.Not.EqualTo(settings.MinOrthographicSize));
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void CameraPose_OrthographicRespectsPartialViewport()
        {
            CameraSettings settings = ScriptableObject.CreateInstance<CameraSettings>();
            settings.ProjMode = ProjectionMode.Orthographic;
            var cameraObject = new GameObject("CameraTest");
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.aspect = 1f;

            Bounds bounds = new Bounds(Vector3.zero, new Vector3(10f, 0f, 10f));

            CameraPose fullPose = CameraFramingCalculator.CalculatePose(
                bounds, camera, new Rect(0f, 0f, 1f, 1f), settings, 0f);

            CameraPose partialPose = CameraFramingCalculator.CalculatePose(
                bounds, camera, new Rect(0f, 0.5f, 1f, 0.5f), settings, 0f);

            Assert.That(partialPose.OrthographicSize, Is.GreaterThan(fullPose.OrthographicSize));

            Object.DestroyImmediate(cameraObject);
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

        [Test]
        public void CameraPose_OrthographicViewSpaceCornersRespectPaddingAndSafeMargins()
        {
            CameraSettings settings = ScriptableObject.CreateInstance<CameraSettings>();
            var cameraObject = new GameObject("CameraTest");
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.aspect = 9f / 16f;
            camera.orthographic = true;
            settings.ProjMode = ProjectionMode.Orthographic;
            Bounds bounds = new Bounds(
                new Vector3(0f, 0.5f, 0f),
                new Vector3(8f, 1f, 4f));
            CameraPose pose = CameraFramingCalculator.CalculatePose(
                bounds,
                camera,
                new Rect(0,0,1,1),
                settings,
                settings.FramingPadding);
            camera.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            camera.fieldOfView = pose.FieldOfView;
            camera.orthographicSize = pose.OrthographicSize;

            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            Vector3[] points = new Vector3[] {
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, max.y, max.z)
            };

            foreach (var p in points)
            {
                Vector3 vp = camera.WorldToViewportPoint(p);
                float horizontalPadding = settings.FramingPadding /
                    (2f * pose.OrthographicSize * camera.aspect);
                float verticalPadding = settings.FramingPadding /
                    (2f * pose.OrthographicSize);
                Assert.That(vp.x, Is.GreaterThanOrEqualTo(settings.SafeMarginHorizontal + horizontalPadding - 0.01f), $"Point {p} violates left padding: {vp.x}");
                Assert.That(vp.x, Is.LessThanOrEqualTo(1f - settings.SafeMarginHorizontal - horizontalPadding + 0.01f), $"Point {p} violates right padding: {vp.x}");
                Assert.That(vp.y, Is.GreaterThanOrEqualTo(settings.SafeMarginVertical + verticalPadding - 0.01f), $"Point {p} violates bottom padding: {vp.y}");
                Assert.That(vp.y, Is.LessThanOrEqualTo(1f - settings.SafeMarginVertical - verticalPadding + 0.01f), $"Point {p} violates top padding: {vp.y}");
                Assert.That(vp.z, Is.GreaterThanOrEqualTo(camera.nearClipPlane), $"Point {p} behind camera: {vp.z}");
            }

            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Constraint_VerticalSafeArea_LimitsOrthographicSize()
        {
            var (cam, safeArea, settings) = SetupConstraintTest();
            typeof(BackgroundCameraSafeArea).GetField("m_Size", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(safeArea, new Vector2(10f, 5f));
            cam.aspect = 1f; // w:h = 1:1
            var constraint = new CameraBackgroundConstraint();
            var pose = new CameraPose(new Vector3(0, 0, -10f), Quaternion.identity, 45f, 10f); // desired 10
            var result = constraint.ConstrainPose(pose, cam, safeArea, new Rect(0,0,1,1), settings, Vector2.zero);

            // max ortho height = (5 - 0.01 * 2) / 2 = 2.49
            Assert.That(result.OrthographicSize, Is.EqualTo(2.49f).Within(0.0001f));

            CleanupConstraintTest(cam, safeArea, settings);
        }

        [Test]
        public void Constraint_HorizontalSafeArea_LimitsOrthographicSize()
        {
            var (cam, safeArea, settings) = SetupConstraintTest();
            typeof(BackgroundCameraSafeArea).GetField("m_Size", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(safeArea, new Vector2(5f, 10f));
            cam.aspect = 2f; // w:h = 2:1
            var constraint = new CameraBackgroundConstraint();
            var pose = new CameraPose(new Vector3(0, 0, -10f), Quaternion.identity, 45f, 10f);
            var result = constraint.ConstrainPose(pose, cam, safeArea, new Rect(0,0,1,1), settings, Vector2.zero);

            // max ortho width = (5 - 0.01 * 2), aspect 2, orthoSize = 4.98 / 4 = 1.245
            Assert.That(result.OrthographicSize, Is.EqualTo(1.245f).Within(0.0001f));

            CleanupConstraintTest(cam, safeArea, settings);
        }

        [Test]
        public void Constraint_MaximumSafeSize_DoesNotFallbackToDesiredSize()
        {
            var (camera, safeArea, settings) = SetupConstraintTest();

            typeof(BackgroundCameraSafeArea).GetField("m_Size", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(safeArea, new Vector2(10f, 5f));
            camera.aspect = 1f;

            var desired = new CameraPose(
                new Vector3(0f, 0f, -10f),
                Quaternion.identity,
                45f,
                10f);

            var constraint = new CameraBackgroundConstraint();

            CameraPose result = constraint.ConstrainPose(
                desired,
                camera,
                safeArea,
                new Rect(0f, 0f, 1f, 1f),
                settings,
                Vector2.zero);

            Assert.That(
                result.OrthographicSize,
                Is.EqualTo(2.49f).Within(0.0001f));

            Assert.That(
                result.OrthographicSize,
                Is.LessThan(desired.OrthographicSize));

            CleanupConstraintTest(camera, safeArea, settings);
        }

        [Test]
        public void Constraint_ViewportOffset_ClampsPositionCorrectly()
        {
            var (cam, safeArea, settings) = SetupConstraintTest();
            typeof(BackgroundCameraSafeArea).GetField("m_Size", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(safeArea, new Vector2(10f, 10f));
            cam.aspect = 1f;
            var constraint = new CameraBackgroundConstraint();
            var pose = new CameraPose(new Vector3(5f, 5f, -10f), Quaternion.identity, 45f, 2f); // target at (5,5)
            // viewport at top right quarter (0.5, 0.5, 0.5, 0.5)
            var result = constraint.ConstrainPose(pose, cam, safeArea, new Rect(0.5f, 0.5f, 0.5f, 0.5f), settings, Vector2.zero);

            Assert.That(result.Position.x, Is.LessThan(5f));
            Assert.That(result.Position.y, Is.LessThan(5f));

            CleanupConstraintTest(cam, safeArea, settings);
        }

        [Test]
        public void Constraint_OrthographicBounds_MathematicallyExposed()
        {
            var (cam, safeArea, settings) = SetupConstraintTest();
            typeof(BackgroundCameraSafeArea).GetField("m_Size", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(safeArea, new Vector2(10f, 10f));
            cam.aspect = 1f;
            var constraint = new CameraBackgroundConstraint();
            var pose = new CameraPose(new Vector3(0, 0, -10f), Quaternion.identity, 45f, 4f);
            var result = constraint.ConstrainPose(pose, cam, safeArea, new Rect(0,0,1,1), settings, Vector2.zero);

            // Check mathematically
            float w = 2f * result.OrthographicSize * cam.aspect;
            float h = 2f * result.OrthographicSize;
            float marginX = w * 0.5f;
            float marginY = h * 0.5f;

            Assert.That(result.Position.x + marginX, Is.LessThanOrEqualTo(5f + 0.05f));
            Assert.That(result.Position.x - marginX, Is.GreaterThanOrEqualTo(-5f - 0.05f));
            Assert.That(result.Position.y + marginY, Is.LessThanOrEqualTo(5f + 0.05f));
            Assert.That(result.Position.y - marginY, Is.GreaterThanOrEqualTo(-5f - 0.05f));

            CleanupConstraintTest(cam, safeArea, settings);
        }

        [Test]
        public void Constraint_PerspectiveLegacy_IsBypassed()
        {
            var (cam, safeArea, settings) = SetupConstraintTest();
            settings.ProjMode = ProjectionMode.PerspectiveLegacy;
            var constraint = new CameraBackgroundConstraint();
            var pose = new CameraPose(new Vector3(100f, 100f, -10f), Quaternion.identity, 45f, 10f);
            var result = constraint.ConstrainPose(pose, cam, safeArea, new Rect(0,0,1,1), settings, Vector2.zero);

            Assert.That(result.Position, Is.EqualTo(pose.Position)); // bypassed

            CleanupConstraintTest(cam, safeArea, settings);
        }

        [Test]
        public void Constraint_ShakePadding_ReducesEffectiveSafeArea()
        {
            var (cam, safeArea, settings) = SetupConstraintTest();
            typeof(BackgroundCameraSafeArea).GetField("m_Size", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(safeArea, new Vector2(10f, 10f));
            cam.aspect = 1f;
            var constraint = new CameraBackgroundConstraint();
            var pose = new CameraPose(new Vector3(0, 0, -10f), Quaternion.identity, 45f, 5f);

            var resultWithShake = constraint.ConstrainPose(pose, cam, safeArea, new Rect(0,0,1,1), settings, new Vector2(1f, 1f));
            var resultNoShake = constraint.ConstrainPose(pose, cam, safeArea, new Rect(0,0,1,1), settings, Vector2.zero);

            Assert.That(resultWithShake.OrthographicSize, Is.LessThan(resultNoShake.OrthographicSize));

            CleanupConstraintTest(cam, safeArea, settings);
        }

        [Test]
        public void Constraint_DifferentViewportSizes_ProduceDifferentLimits()
        {
            var constraint = new CameraBackgroundConstraint();
            var (cam, safeArea, settings) = SetupConstraintTest();
            typeof(CameraSettings).GetField("m_MaxOrthographicSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(settings, 100f);
            cam.aspect = 1f;

            var pose = new CameraPose(new Vector3(0, 0, -10f), Quaternion.identity, 45f, 50f);
            var result1 = constraint.ConstrainPose(pose, cam, safeArea, new Rect(0,0,1,1), settings, Vector2.zero);
            var result2 = constraint.ConstrainPose(pose, cam, safeArea, new Rect(0.5f,0.5f,0.5f,0.5f), settings, Vector2.zero);
            Assert.That(result1.OrthographicSize, Is.Not.EqualTo(result2.OrthographicSize));
            CleanupConstraintTest(cam, safeArea, settings);
        }

        [Test]
        public void Constraint_ContextChange_InvalidatesLastValidPose()
        {
            var (cam, safeArea, settings) = SetupConstraintTest();
            typeof(BackgroundCameraSafeArea).GetField("m_Size", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(safeArea, new Vector2(10f, 10f));
            cam.aspect = 1f;
            var constraint = new CameraBackgroundConstraint();

            var validPose = new CameraPose(new Vector3(0, 0, -10f), Quaternion.identity, 45f, 2f);
            var result1 = constraint.ConstrainPose(validPose, cam, safeArea, new Rect(0,0,1,1), settings, Vector2.zero);

            var invalidPose = new CameraPose(new Vector3(float.NaN, 0, -10f), Quaternion.identity, 45f, 2f);
            var result2 = constraint.ConstrainPose(invalidPose, cam, safeArea, new Rect(0,0,1,1), settings, Vector2.zero);
            Assert.That(result2.Position, Is.EqualTo(result1.Position)); // Fallback used

            cam.aspect = 10f; // Soft geometry change, forces verify pose to fail with result1's clamping
            LogAssert.Expect(LogType.Warning, new Regex("Background coverage priority overrides MinOrthographicSize"));
            var result3 = constraint.ConstrainPose(invalidPose, cam, safeArea, new Rect(0,0,1,1), settings, Vector2.zero);
            Assert.That(float.IsNaN(result3.Position.x), Is.True); // Context changed & VerifyPose failed, fallback cleared

            CleanupConstraintTest(cam, safeArea, settings);
        }

        [Test]
        public void Constraint_MinimumConflict_UsesBackgroundSafeMaximum()
        {
            LogAssert.Expect(LogType.Warning, new Regex("Background coverage priority overrides MinOrthographicSize"));
            var (cam, safeArea, settings) = SetupConstraintTest();
            typeof(BackgroundCameraSafeArea).GetField("m_Size", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(safeArea, new Vector2(0.1f, 0.1f));
            typeof(CameraSettings).GetField("m_MinOrthographicSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(settings, 8f);
            cam.aspect = 1f;
            var constraint = new CameraBackgroundConstraint();
            var pose = new CameraPose(new Vector3(10f, 10f, -10f), Quaternion.identity, 45f, 5f);
            var result = constraint.ConstrainPose(pose, cam, safeArea, new Rect(0,0,1,1), settings, Vector2.zero);

            Assert.That(result.OrthographicSize, Is.EqualTo(0.04f).Within(0.0001f));
            Assert.That(result.OrthographicSize, Is.LessThan(settings.MinOrthographicSize));
            Assert.That(result.Position, Is.Not.EqualTo(pose.Position));

            CleanupConstraintTest(cam, safeArea, settings);
        }

        [Test]
        public void CameraSettings_OrthographicTransform_UsesConfiguredAnglesAndDistance()
        {
            var settings = ScriptableObject.CreateInstance<CameraSettings>();
            typeof(CameraSettings).GetField("m_CameraAngleX", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(settings, 45f);
            typeof(CameraSettings).GetField("m_CameraAngleY", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(settings, 0f);
            typeof(CameraSettings).GetField("m_CameraDistance", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(settings, 18f);
            Vector3 target = new Vector3(2f, 7f, -3f);

            var (position, rotation) = settings.CalculateOrthographicTransform(target);
            Quaternion expectedRotation = Quaternion.Euler(45f, 0f, 0f);
            Vector3 expectedTarget = new Vector3(target.x, 0f, target.z);
            Vector3 expectedPosition = expectedTarget - expectedRotation * Vector3.forward * 18f;

            Assert.That(Quaternion.Angle(rotation, expectedRotation), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(position, expectedPosition), Is.LessThan(0.0001f));

            Object.DestroyImmediate(settings);
        }

        [Test]
        public void CameraPose_OrthographicCompactBoundsClampsToConfiguredMinimum()
        {
            CameraSettings settings = ScriptableObject.CreateInstance<CameraSettings>();
            settings.ProjMode = ProjectionMode.Orthographic;
            var cameraObject = new GameObject("CameraTest");
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.aspect = 16f / 9f;

            CameraPose pose = CameraFramingCalculator.CalculatePose(
                new Bounds(Vector3.zero, Vector3.one * 0.1f),
                camera,
                new Rect(0f, 0f, 1f, 1f),
                settings,
                settings.FramingPadding);

            Assert.That(pose.OrthographicSize, Is.EqualTo(3f).Within(0.001f));
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void CameraPose_OrthographicWiderBoundsIncreaseSizeThenCanReturnToMinimum()
        {
            CameraSettings settings = ScriptableObject.CreateInstance<CameraSettings>();
            settings.ProjMode = ProjectionMode.Orthographic;
            var cameraObject = new GameObject("CameraTest");
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.aspect = 16f / 9f;

            CameraPose compactPose = CameraFramingCalculator.CalculatePose(
                new Bounds(Vector3.zero, Vector3.one * 0.1f), camera,
                new Rect(0f, 0f, 1f, 1f), settings, settings.FramingPadding);
            CameraPose widePose = CameraFramingCalculator.CalculatePose(
                new Bounds(Vector3.zero, new Vector3(16f, 2f, 6f)), camera,
                new Rect(0f, 0f, 1f, 1f), settings, settings.FramingPadding);
            CameraPose compactAgainPose = CameraFramingCalculator.CalculatePose(
                new Bounds(Vector3.zero, Vector3.one * 0.1f), camera,
                new Rect(0f, 0f, 1f, 1f), settings, settings.FramingPadding);

            Assert.That(widePose.OrthographicSize, Is.GreaterThan(compactPose.OrthographicSize));
            Assert.That(compactAgainPose.OrthographicSize, Is.EqualTo(3f).Within(0.001f));
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Constraint_FootprintCache_IsInvalidatedOnAspectChange()
        {
            var (cam, safeArea, settings) = SetupConstraintTest();
            typeof(BackgroundCameraSafeArea).GetField("m_Size", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(safeArea, new Vector2(10f, 10f));
            cam.aspect = 1f;
            var constraint = new CameraBackgroundConstraint();
            var pose = new CameraPose(new Vector3(0, 0, -10f), Quaternion.identity, 45f, 5f);
            var result1 = constraint.ConstrainPose(pose, cam, safeArea, new Rect(0,0,1,1), settings, Vector2.zero);

            cam.aspect = 2f;
            var result2 = constraint.ConstrainPose(pose, cam, safeArea, new Rect(0,0,1,1), settings, Vector2.zero);

            Assert.That(result1.OrthographicSize, Is.Not.EqualTo(result2.OrthographicSize));

            CleanupConstraintTest(cam, safeArea, settings);
        }

        [Test]
        public void Constraint_InvalidSafeArea_FailsValidation()
        {
            LogAssert.Expect(LogType.Error, new Regex("Safe Area.*"));
            var (cam, safeArea, settings) = SetupConstraintTest();
            safeArea.transform.localScale = new Vector3(1f, 2f, 1f); // non-uniform
            Assert.That(safeArea.ValidateConfiguration(true), Is.False);
            CleanupConstraintTest(cam, safeArea, settings);
        }

        [Test]
        public void Constraint_PlaneParallel_FailsConstraint()
        {
            LogAssert.Expect(LogType.Error, new Regex("카메라 시선과 Safe Area 평면이.*"));
            var (cam, safeArea, settings) = SetupConstraintTest();
            safeArea.transform.rotation = Quaternion.Euler(0, 90, 0);
            var constraint = new CameraBackgroundConstraint();
            var pose = new CameraPose(new Vector3(0, 0, -10f), Quaternion.identity, 45f, 5f);
            var result = constraint.ConstrainPose(pose, cam, safeArea, new Rect(0,0,1,1), settings, Vector2.zero);

            Assert.That(result.Position, Is.EqualTo(pose.Position));

            CleanupConstraintTest(cam, safeArea, settings);
        }

        [Test]
        public void Constraint_NonOrthographicCamera_Blocked()
        {
            LogAssert.Expect(LogType.Error, new Regex("Orthographic Camera.*"));
            var (cam, safeArea, settings) = SetupConstraintTest();
            cam.orthographic = false;
            var constraint = new CameraBackgroundConstraint();
            var pose = new CameraPose(new Vector3(10f, 10f, -10f), Quaternion.identity, 45f, 5f);
            var result = constraint.ConstrainPose(pose, cam, safeArea, new Rect(0,0,1,1), settings, Vector2.zero);

            Assert.That(result.Position, Is.EqualTo(pose.Position));

            CleanupConstraintTest(cam, safeArea, settings);
        }

        [Test]
        public void Constraint_ResetState_ClearsLastValidPose()
        {
            var (cam, safeArea, settings) = SetupConstraintTest();
            typeof(BackgroundCameraSafeArea).GetField("m_Size", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(safeArea, new Vector2(10f, 10f));
            cam.aspect = 1f;
            var constraint = new CameraBackgroundConstraint();

            var validPose = new CameraPose(new Vector3(0, 0, -10f), Quaternion.identity, 45f, 2f);
            constraint.ConstrainPose(validPose, cam, safeArea, new Rect(0,0,1,1), settings, Vector2.zero);

            constraint.ResetState();

            var invalidPose = new CameraPose(new Vector3(float.NaN, 0, -10f), Quaternion.identity, 45f, 2f);
            var result = constraint.ConstrainPose(invalidPose, cam, safeArea, new Rect(0,0,1,1), settings, Vector2.zero);

            Assert.That(float.IsNaN(result.Position.x), Is.True);

            CleanupConstraintTest(cam, safeArea, settings);
        }

        private (UnityEngine.Camera cam, BackgroundCameraSafeArea safeArea, CameraSettings settings) SetupConstraintTest()
        {
            var go = new GameObject("Test");
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            var cam = go.AddComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            var safeArea = go.AddComponent<BackgroundCameraSafeArea>();
            safeArea.ValidateConfiguration(false);
            var settings = ScriptableObject.CreateInstance<CameraSettings>();
            settings.ProjMode = ProjectionMode.Orthographic;
            typeof(CameraSettings).GetField("m_MinOrthographicSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(settings, 1f);
            return (cam, safeArea, settings);
        }

        private void CleanupConstraintTest(UnityEngine.Camera cam, BackgroundCameraSafeArea safeArea, CameraSettings settings)
        {
            Object.DestroyImmediate(cam.gameObject);
            Object.DestroyImmediate(settings);
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

        [Test]
        public void Constraint_Rotated45_VerifiesBoundsCorrectly()
        {
            var (cam, safeArea, settings) = SetupConstraintTest();
            // Safe Area 크기 설정
            typeof(BackgroundCameraSafeArea).GetField("m_Size", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(safeArea, new Vector2(20f, 20f));

            // X축 45도 회전된 Safe Area 설정
            safeArea.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            cam.aspect = 1f;

            var constraint = new CameraBackgroundConstraint();
            // X축 45도 기울여진 카메라 (Safe Area와 평행)
            var rot = Quaternion.Euler(45f, 0f, 0f);
            // 의도하는 위치를 벗어난 곳에 배치해 봅니다 (위로 100). 이 위치는 Clamp 되어야 합니다.
            var pose = new CameraPose(rot * new Vector3(100f, 100f, -10f), rot, 45f, 50f);

            var result = constraint.ConstrainPose(pose, cam, safeArea, new Rect(0,0,1,1), settings, Vector2.zero);

            // maxAllowed 계산 시: width, height는 aspect = 1이므로 2, 2. (회전된 평면에 대해 평행이므로 직교 뷰포트 그대로)
            // effRect 폭 = 20, 높이 = 20
            // maxAllowed = (20 - 0.02) / 2 = 9.99
            Assert.That(result.OrthographicSize, Is.EqualTo(9.99f).Within(0.0001f));

            // Clamp 확인: 100f, 100f로 설정했으므로 가장자리(pivotMaxX, pivotMaxY)에 걸려야 합니다.
            // pivotMaxX = 10 - 1 * 9.99 - 0.01 = 0
            // pivotMaxY = 10 - 1 * 9.99 - 0.01 = 0
            // 따라서 중앙 위치가 (0, 0) 로컬 좌표계로 Clamp 되어야 함
            Vector2 localHit = safeArea.GetLocalPoint(result.Position + rot * Vector3.forward * 10f); // 10f is distance
            Assert.That(localHit.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(localHit.y, Is.EqualTo(0f).Within(0.0001f));

            CleanupConstraintTest(cam, safeArea, settings);
        }

#if UNITY_6000_0_OR_NEWER
        [Test]
        public void CameraViewportProvider_NormalizeScreenRect_ReturnsExpectedRect()
        {
            // 화면 크기가 1920x1080일 때
            Rect pixelRect = new Rect(0, 0, 1920f, 1080f);

            // UI가 중앙 50% 영역(960x540)을 덮고, 오프셋이 좌하단(480, 270)에서 우상단(1440, 810)일 경우
            Vector2 minScreen = new Vector2(480f, 270f);
            Vector2 maxScreen = new Vector2(1440f, 810f);

            Rect result = InTheArena.Camera.CameraViewportProvider.NormalizeScreenRect(minScreen, maxScreen, pixelRect);

            Assert.That(result.x, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(result.width, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(result.height, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void CameraViewportProvider_NormalizeScreenRect_HandlesInvalidValues()
        {
            Rect pixelRect = new Rect(0, 0, 1920f, 1080f);

            // NaN 입력
            Vector2 minScreen = new Vector2(float.NaN, 270f);
            Vector2 maxScreen = new Vector2(1440f, 810f);
            Rect result1 = InTheArena.Camera.CameraViewportProvider.NormalizeScreenRect(minScreen, maxScreen, pixelRect);
            Assert.That(result1, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));

            // 0 너비/높이 픽셀 렉트 (배치 모드 또는 비활성 상태)
            Rect zeroRect = new Rect(0, 0, 0, 0);
            minScreen = new Vector2(0f, 0f);
            maxScreen = new Vector2(100f, 100f);
            Rect result2 = InTheArena.Camera.CameraViewportProvider.NormalizeScreenRect(minScreen, maxScreen, zeroRect);
            Assert.That(result2, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
        }
#endif

        private static float GetGroundDistance(CameraPose pose)
        {
            Vector3 forward = pose.Rotation * Vector3.forward;
            return -pose.Position.y / forward.y;
        }
    }
}
#endif
