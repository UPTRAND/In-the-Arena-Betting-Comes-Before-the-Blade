#if UNITY_EDITOR
using InTheArena.Camera;
using InTheArena.Unit;
using NUnit.Framework;
using System.Reflection;
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
        {
            var unitObject = new GameObject("UnitTest");
            unitObject.SetActive(false);
            unitObject.AddComponent<BoxCollider>();
            var unit = unitObject.AddComponent<InTheArena.Unit.Unit>();
            unit.Initialize(data, 0);
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
