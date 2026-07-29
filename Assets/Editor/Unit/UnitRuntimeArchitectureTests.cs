#if UNITY_EDITOR
using InTheArena.Camera;
using InTheArena.Unit;
using NUnit.Framework;
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
    }
}
#endif
