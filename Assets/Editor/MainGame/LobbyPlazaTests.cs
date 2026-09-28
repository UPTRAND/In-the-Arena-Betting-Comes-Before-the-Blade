#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using InTheArena.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.MainGame.Editor
{
    public sealed class LobbyPlazaTests
    {
        private static readonly Vector2[] Area = { new Vector2(-300, -200), new Vector2(300, -200),
            new Vector2(250, 250), new Vector2(-250, 250) };
        private static LobbyPlazaProfile[] Profiles => LobbyPlazaBuilder.RegionIds.Select(id =>
            AssetDatabase.LoadAssetAtPath<LobbyPlazaProfile>(LobbyPlazaBuilder.Folder + "/Plaza_" + id + ".asset")).ToArray();

        [TestCase(1, 0)] [TestCase(5, 0)] [TestCase(6, 1)] [TestCase(10, 1)]
        [TestCase(11, 2)] [TestCase(15, 2)] [TestCase(16, 2)] [TestCase(999, 2)]
        public void ProgressSelectsExpectedProfile(int stage, int region)
        {
            var profiles = Profiles;
            Assert.That(profiles.All(p => p != null), Is.True);
            Assert.That(LobbyPlazaProfile.Resolve(profiles, stage), Is.SameAs(profiles[region]));
        }

        [Test]
        public void ResidentIdsAreGloballyUniqueAndVisualsValid()
        {
            var ids = new HashSet<string>();
            foreach (var p in Profiles)
            {
                Assert.That(p.IsValid, Is.True);
                Assert.That(p.Background, Is.Not.Null);
                Assert.That(p.Residents.Count, Is.EqualTo(5));
                foreach (var npc in p.Residents)
                {
                    Assert.That(ids.Add(npc.Id), Is.True, npc.Id);
                    Assert.That(npc.Visual.IsValid, Is.True, npc.Id);
                    Assert.That(npc.Visual.FootPixel.y, Is.GreaterThanOrEqualTo(0));
                }
            }
        }

        [Test]
        public void AnimationsRetainSourceFrameTimes()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animator/Unit/Knight/Walk.anim");
            var extracted = LobbyPlazaBuilder.Extract(clip);
            var stored = AssetDatabase.LoadAssetAtPath<LobbyNpcVisualData>(LobbyPlazaBuilder.Folder + "/Visual_Knight.asset").Walk;
            CollectionAssert.AreEqual(extracted.Frames, stored.Frames);
            CollectionAssert.AreEqual(extracted.Times, stored.Times);
            Assert.That(stored.Duration, Is.EqualTo(clip.length));
            for (int i = 0; i < stored.Frames.Length; i++) Assert.That(stored.Sample(stored.Times[i]), Is.SameAs(stored.Frames[i]));
            Assert.That(stored.Sample(stored.Duration), Is.SameAs(stored.Frames[0]));
        }

        [TestCase(1080, 1920)] [TestCase(1080, 2340)] [TestCase(1440, 1920)]
        public void BackgroundPolygonClipsToVisibleSafeSpace(int width, int height)
        {
            float scale = Mathf.Max(width / 1080f, height / 1920f);
            Vector2 size = new Vector2(1080, 1920) * scale;
            var safe = Rect.MinMaxRect(-width / 2f + 70, -height / 2f + 480, width / 2f - 70, height / 2f - 330);
            foreach (var profile in Profiles)
            {
                var mapped = profile.WalkArea.Select(uv => Vector2.Scale(uv - Vector2.one * .5f, size)).ToArray();
                var clipped = LobbyPlazaGeometry.ClipToRect(mapped, safe);
                Assert.That(LobbyPlazaGeometry.IsConvex(clipped), Is.True);
                var random = new System.Random(15);
                for (int i = 0; i < 500; i++)
                {
                    Vector2 point = LobbyPlazaGeometry.Sample(clipped, random);
                    Assert.That(LobbyPlazaGeometry.Contains(mapped, point), Is.True);
                    Assert.That(point.x, Is.InRange(safe.xMin, safe.xMax));
                    Assert.That(point.y, Is.InRange(safe.yMin, safe.yMax));
                }
            }
        }

        [Test]
        public void InvalidOrEmptyGeometryIsRejected()
        {
            Assert.That(LobbyPlazaGeometry.IsConvex(new[] { Vector2.zero, Vector2.one, Vector2.right, Vector2.up }), Is.False);
            Assert.That(LobbyPlazaGeometry.IsConvex(new[] { Vector2.zero, Vector2.right, Vector2.right * 2 }), Is.False);
            Assert.That(LobbyPlazaGeometry.ClipToRect(Area, new Rect(1000, 1000, 10, 10)), Is.Empty);
            var invalid = ScriptableObject.CreateInstance<LobbyPlazaProfile>();
            try
            {
                invalid.Speed = new Vector2(float.NaN, 55);
                Assert.That(LobbyPlazaProfile.Resolve(new[] { invalid }, 1), Is.Null);
            }
            finally { Object.DestroyImmediate(invalid); }
        }

        [Test]
        public void ClippingExactlyThroughPolygonVertexKeepsUsableArea()
        {
            var diamond = new[] { new Vector2(0, -100), new Vector2(100, 0), new Vector2(0, 100), new Vector2(-100, 0) };
            var clipped = LobbyPlazaGeometry.ClipToRect(diamond, new Rect(0, -100, 100, 200));
            Assert.That(LobbyPlazaGeometry.IsConvex(clipped), Is.True);
            Assert.That(LobbyPlazaGeometry.Contains(clipped, new Vector2(20, 0)), Is.True);
        }

        [Test]
        public void CrowdedResidentsStayInsideAndEventuallyMove()
        {
            var random = new System.Random(7);
            var residents = Enumerable.Range(0, 10).Select(i => new LobbyNpcMotion(Vector2.zero, 35 + i * 2,
                new Vector2(2, 5), random)).ToArray();
            float[] traveled = new float[residents.Length];
            var positions = new List<Vector2>();
            for (int frame = 0; frame < 3600; frame++)
            {
                positions.Clear(); positions.AddRange(residents.Select(r => r.Position));
                for (int i = 0; i < residents.Length; i++)
                {
                    residents[i].Tick(1f / 30f, Area, LobbyNpcMotion.Avoidance(i, positions, 70f));
                    traveled[i] += Vector2.Distance(positions[i], residents[i].Position);
                    Assert.That(LobbyPlazaGeometry.Contains(Area, residents[i].Position), Is.True);
                }
            }
            Assert.That(traveled.Min(), Is.GreaterThan(500f));
        }

        [Test]
        public void BlockedWalkerReroutesAndLongFrameIsCapped()
        {
            var motion = new LobbyNpcMotion(Vector2.zero, 50, Vector2.zero, new System.Random(123));
            // The first zero-distance arrival starts the normal wait/destination cycle.
            for (int i = 0; i < 200; i++) motion.Tick(.05f, Area, Vector2.zero);
            Vector2 old = motion.Position;
            motion.Tick(1000f, Area, Vector2.zero);
            Assert.That(Vector2.Distance(old, motion.Position), Is.LessThanOrEqualTo(2.501f));
            var blocker = new LobbyNpcMotion(Vector2.zero, 50, new Vector2(.01f, .01f), new System.Random(5));
            for (int i = 0; i < 200; i++)
            {
                Vector2 cancel = -(blocker.Destination - blocker.Position).normalized * 50f;
                blocker.Tick(.05f, Area, cancel);
            }
            Assert.That(blocker.RerouteCount, Is.GreaterThan(0));
        }

        [Test]
        public void PrefabBindsPlazaBelowControlsWithoutInterceptingInput()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPlazaBuilder.PrefabPath);
            var plaza = prefab.GetComponentInChildren<LobbyPlazaController>(true);
            Assert.That(plaza, Is.Not.Null);
            Assert.That(plaza.transform.GetSiblingIndex(), Is.GreaterThan(prefab.transform.Find("Background").GetSiblingIndex()));
            Assert.That(plaza.transform.GetSiblingIndex(), Is.LessThan(prefab.transform.Find("StartButton").GetSiblingIndex()));
            Assert.That(plaza.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            var panel = new SerializedObject(prefab.GetComponent<UI_LobbyStagePanel>());
            Assert.That(panel.FindProperty("m_Plaza").objectReferenceValue, Is.SameAs(plaza));
            var settings = new SerializedObject(plaza);
            Assert.That(settings.FindProperty("m_Profiles").arraySize, Is.EqualTo(3));
            Assert.That(settings.FindProperty("m_LowerControls").arraySize, Is.EqualTo(2));
            Assert.That(plaza.GetComponentsInChildren<Graphic>(true), Is.Empty);
        }
    }
}
#endif
