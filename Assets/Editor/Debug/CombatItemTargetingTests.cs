#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using InTheArena.Battlefield;
using InTheArena.MainGame;
using InTheArena.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CombatItemTargetingTests
{
    [Test]
    public void BattlefieldArea_RaycastAcceptsOnlyColliderAndNormalizesY()
    {
        GameObject areaObject = new GameObject("TestBattlefield");
        GameObject cameraObject = new GameObject("TestCamera");

        try
        {
            BoxCollider collider = areaObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0f, 0f);
            collider.size = new Vector3(10f, 0.2f, 4f);
            areaObject.transform.position = new Vector3(0f, 2f, 0f);
            BattlefieldArea area = areaObject.AddComponent<BattlefieldArea>();

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 10f;
            camera.aspect = 1f;
            camera.pixelRect = new Rect(0f, 0f, 100f, 100f);
            camera.transform.position = new Vector3(0f, 10f, 0f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            Assert.That(
                area.TryGetGroundPosition(
                    camera,
                    new Vector2(50f, 50f),
                    out Vector3 inside),
                Is.True);
            Assert.That(inside.y, Is.EqualTo(2f).Within(0.001f));
            Assert.That(inside.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(inside.z, Is.EqualTo(0f).Within(0.001f));

            Assert.That(
                area.TryGetGroundPosition(
                    camera,
                    new Vector2(99f, 50f),
                    out _),
                Is.False);
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(areaObject);
        }
    }

    [Test]
    public void BattlefieldArea_FormationPaddingKeepsAllThreeMercenariesInside()
    {
        GameObject areaObject = new GameObject("TestBattlefield");

        try
        {
            BoxCollider collider = areaObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(14f, 0.2f, 6f);
            BattlefieldArea area = areaObject.AddComponent<BattlefieldArea>();

            Vector3 center = area.ClampPosition(
                new Vector3(100f, 0f, 100f),
                1f);
            Vector3 knight = center;
            Vector3 archer = center + new Vector3(0.5f, 0f, -0.5f);
            Vector3 wizard = center + new Vector3(-0.5f, 0f, -0.5f);

            Assert.That(area.ContainsPosition(knight, 0.5f), Is.True);
            Assert.That(area.ContainsPosition(archer, 0.5f), Is.True);
            Assert.That(area.ContainsPosition(wizard, 0.5f), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(areaObject);
        }
    }

    [Test]
    public void CombatPhase_ItemCastingSlowMotionBlocksToggleAndRestoresSelectedSpeed()
    {
        GameObject phaseObject = new GameObject("TestCombatPhase");
        float previousTimeScale = Time.timeScale;

        try
        {
            CombatPhase phase = phaseObject.AddComponent<CombatPhase>();
            typeof(CombatPhase)
                .GetField("m_CurrentSpeed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(phase, 2f);
            Time.timeScale = 2f;

            Assert.That(phase.BeginItemCastingSlowMotion(), Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(phase.DisplaySpeed, Is.EqualTo(0.25f).Within(0.0001f));

            phase.ToggleCombatSpeed();
            Assert.That(Time.timeScale, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(phase.CurrentSpeed, Is.EqualTo(2f).Within(0.0001f));

            phase.EndItemCastingSlowMotion();
            Assert.That(Time.timeScale, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(phase.DisplaySpeed, Is.EqualTo(2f).Within(0.0001f));
        }
        finally
        {
            Time.timeScale = previousTimeScale;
            Object.DestroyImmediate(phaseObject);
        }
    }

    [Test]
    public void CombatItemTargetingController_OtherPointerUpDoesNotCancelActivePointer()
    {
        GameObject phaseObject = new GameObject("TestCombatPhase");
        GameObject slotObject = new GameObject("SelectedSlot", typeof(RectTransform));
        GameObject controllerObject = new GameObject(
            "TargetingController",
            typeof(RectTransform),
            typeof(Image));
        GameObject eventSystemObject = new GameObject("TestEventSystem");
        float previousTimeScale = Time.timeScale;

        try
        {
            CombatPhase phase = phaseObject.AddComponent<CombatPhase>();
            UI_CombatItemTargetingController controller =
                controllerObject.AddComponent<UI_CombatItemTargetingController>();
            EventSystem eventSystem = eventSystemObject.AddComponent<EventSystem>();
            int canceledCount = 0;
            controller.TargetCanceled += () => canceledCount++;

            Assert.That(
                controller.BeginTargeting(
                    ItemType.Meteor,
                    phase,
                    (RectTransform)slotObject.transform,
                    null),
                Is.True);

            PointerEventData down = new PointerEventData(eventSystem)
            {
                pointerId = 11,
                position = new Vector2(100f, 100f)
            };
            PointerEventData otherPointerUp = new PointerEventData(eventSystem)
            {
                pointerId = 12,
                position = new Vector2(100f, 100f)
            };

            controller.OnPointerDown(down);
            controller.OnPointerUp(otherPointerUp);

            Assert.That(controller.State, Is.Not.EqualTo(CombatItemTargetingState.Idle));
            Assert.That(canceledCount, Is.EqualTo(0));

            controller.OnPointerUp(down);
            Assert.That(canceledCount, Is.EqualTo(1));
        }
        finally
        {
            Time.timeScale = previousTimeScale;
            Object.DestroyImmediate(eventSystemObject);
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(slotObject);
            Object.DestroyImmediate(phaseObject);
        }
    }
}
#endif
