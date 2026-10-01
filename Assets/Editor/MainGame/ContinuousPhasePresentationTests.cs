#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using InTheArena.Camera;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using InTheArena.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.MainGame.Editor
{
    public sealed class ContinuousPhasePresentationTests
    {
        [TestCase(1080f, 1920f)]
        [TestCase(1080f, 2340f)]
        [TestCase(1440f, 1920f)]
        public void BettingPreparationPreservesAuthoredLayout(float width, float height)
        {
            GameObject root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/Panel/UI_BettingPhase.prefab");
            var owner = new GameObject("ContinuousPhaseTest");
            var stage = ScriptableObject.CreateInstance<StageData>();
            var round = ScriptableObject.CreateInstance<RoundData>();
            var poolOwner = PoolManager.Instance == null ? new GameObject("TestPools") : null;
            var previousManager = RoundManager.Instance;
            try
            {
                if (poolOwner != null) poolOwner.AddComponent<PoolManager>().Setup();
                UseContinuousManager(owner);
                ((RectTransform)root.transform).sizeDelta = new Vector2(width, height);
                CanvasGroup content = root.transform.Find("BettingContent").GetComponent<CanvasGroup>();
                var layout = CaptureLayout(content.transform);
                var phase = owner.AddComponent<BettingPhase>();
                SetField(phase, "m_BettingContentCanvasGroup", content);
                SetField(phase, "m_BettingCanvasGroup", content);
                stage.RoundDatas.Add(round);
                var context = new RoundContext();
                context.InitializeStage(stage);
                context.SetRoundData(stage, 0);
                phase.InitializePhase(context);
                phase.PreparePhaseAsync(CancellationToken.None).GetAwaiter().GetResult();
                AssertLayout(layout);
                Assert.That(content.interactable, Is.False);
                phase.ExitPhaseAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            finally
            {
                Object.DestroyImmediate(owner);
                SetManager(previousManager);
                if (poolOwner != null) Object.DestroyImmediate(poolOwner);
                Object.DestroyImmediate(round);
                Object.DestroyImmediate(stage);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [TestCase(1080f, 1920f)]
        [TestCase(1080f, 2340f)]
        [TestCase(1440f, 1920f)]
        public void ResultPreparationAndExitPreserveAuthoredPanelAndBackdrop(float width, float height)
        {
            GameObject root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/Panel/ResultPhaseUI.prefab");
            var owner = new GameObject("ContinuousResultTest");
            var previousManager = RoundManager.Instance;
            try
            {
                UseContinuousManager(owner);
                ((RectTransform)root.transform).sizeDelta = new Vector2(width, height);
                RectTransform panel = Find(root.transform, "ResultWin");
                RectTransform backdrop = Find(root.transform, "BackGround");
                var layout = CaptureLayout(root.transform);
                var phase = owner.AddComponent<ResultPhase>();
                SetField(phase, "m_ResultUi", root.GetComponent<UI_ResultPhase>());
                phase.InitializePhase(new RoundContext
                {
                    BetTicket = new RoundBetTicket(),
                    CombatResult = new CombatResultSnapshot(Team.Red, 10f, 1, 0, null, null),
                    Settlement = new BetSettlement(true, 100, 2, 200, null)
                });
                phase.PreparePhaseAsync(CancellationToken.None).GetAwaiter().GetResult();
                Assert.That(ContinuousPhasePresentation.GetResultPanel(root.transform), Is.SameAs(panel));
                // Result summaries/list rows intentionally animate; the authored outer layout must stay intact.
                AssertLayout(layout, panel, backdrop);
                foreach (Graphic graphic in backdrop.GetComponentsInChildren<Graphic>(true))
                    Assert.That(graphic.enabled, Is.True, graphic.name);
                Vector2 resting = panel.anchoredPosition;
                panel.anchoredPosition += Vector2.down * 2000f;
                phase.ExitPhaseAsync(CancellationToken.None).GetAwaiter().GetResult();
                Assert.That(panel.anchoredPosition, Is.EqualTo(resting));
                AssertLayout(layout, panel, backdrop);
            }
            finally
            {
                Object.DestroyImmediate(owner);
                SetManager(previousManager);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void ArenaFramingSurvivesHiddenCombatHudAndClearsEntireScreen()
        {
            var go = new GameObject("ViewportTest", typeof(UnityEngine.Camera), typeof(CameraViewportProvider));
            try
            {
                var provider = go.GetComponent<CameraViewportProvider>();
                provider.SetPersistentCameraRect(ContinuousPhasePresentation.ArenaViewport);
                Assert.That(provider.TryGetTargetCameraRect(out Rect cameraRect), Is.True);
                Assert.That(cameraRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
                Assert.That(provider.GetEffectiveViewportRect(), Is.EqualTo(ContinuousPhasePresentation.ArenaViewport));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [TestCase(1080f, 1920f)]
        [TestCase(1080f, 2340f)]
        [TestCase(1440f, 1920f)]
        public void CenteredResultSlideClearsEntireViewport(float width, float height)
        {
            GameObject root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/Panel/ResultPhaseUI.prefab");
            try
            {
                var viewport = (RectTransform)root.transform;
                viewport.sizeDelta = new Vector2(width, height);
                var panel = Find(root.transform, "ResultWin");
                Vector2 resting = panel.anchoredPosition;
                var calculation = typeof(ContinuousPhasePresentation).GetMethod("GetHiddenPosition", BindingFlags.Static | BindingFlags.NonPublic);
                Vector2 hidden = (Vector2)calculation.Invoke(null, new object[] { panel, resting });
                panel.anchoredPosition = hidden;
                var corners = new Vector3[4];
                panel.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                    Assert.That(viewport.InverseTransformPoint(corner).y, Is.LessThan(viewport.rect.yMin));
                // Re-entering during an interrupted slide must use the same resting position.
                Assert.That((Vector2)calculation.Invoke(null, new object[] { panel, resting }), Is.EqualTo(hidden));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static RectTransform Find(Transform root, string name)
        {
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == name) return rect;
            Assert.Fail("Missing authored rectangle: " + name);
            return null;
        }

        private static void UseContinuousManager(GameObject owner) => SetManager(owner.AddComponent<RoundManager>());

        private static void SetManager(RoundManager manager) => typeof(RoundManager)
            .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, manager);

        private static void SetField(object owner, string name, object value) => owner.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);

        private static Dictionary<RectTransform, string> CaptureLayout(Transform root)
        {
            var snapshots = new Dictionary<RectTransform, string>();
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
                snapshots.Add(rect, Layout(rect));
            return snapshots;
        }

        private static string Layout(RectTransform rect) => JsonUtility.ToJson(new RectLayout
        {
            anchorMin = rect.anchorMin, anchorMax = rect.anchorMax, pivot = rect.pivot,
            position = rect.anchoredPosition, size = rect.sizeDelta, scale = rect.localScale,
            active = rect.gameObject.activeSelf,
            imageEnabled = rect.GetComponent<Image>()?.enabled ?? false,
            imageRaycast = rect.GetComponent<Image>()?.raycastTarget ?? false
        });

        private static void AssertLayout(Dictionary<RectTransform, string> snapshots, params RectTransform[] selected)
        {
            if (selected.Length == 0)
                foreach (var pair in snapshots) Assert.That(Layout(pair.Key), Is.EqualTo(pair.Value), pair.Key.name);
            else
                foreach (RectTransform rect in selected) Assert.That(Layout(rect), Is.EqualTo(snapshots[rect]), rect.name);
        }

        [System.Serializable]
        private struct RectLayout
        {
            public Vector2 anchorMin, anchorMax, pivot, position, size;
            public Vector3 scale;
            public bool active, imageEnabled, imageRaycast;
        }
    }
}
#endif
