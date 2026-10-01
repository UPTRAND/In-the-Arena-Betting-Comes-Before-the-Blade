#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System.Reflection;
using InTheArena.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.MainGame.Editor
{
    public sealed class CombatHudHeightLimitTests
    {
        private const string PrefabPath = "Assets/Prefabs/UI/HUD/UI_BattlePhaseHUD.prefab";
        private static readonly string[] ContentNames =
        {
            "ItemAndSpeed_Area", "BallteSwap_Button", "BettingSwap_Button", "BettingHistory_Area"
        };

        [TestCase(1080f, 1920f)]
        [TestCase(1080f, 2340f)]
        [TestCase(1080f, 2400f)]
        [TestCase(1440f, 1920f)]
        public void ContentKeepsReferenceHeightAndBackgroundReachesScreenBottom(float width, float height)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var viewport = (RectTransform)root.transform;
                var hud = root.GetComponent<UI_BattlePhaseHUD>();
                float referenceHeight = Mathf.Min(height, width * 1920f / 1080f);
                viewport.sizeDelta = new Vector2(width, referenceHeight);
                Apply(hud);
                var content = new RectTransform[ContentNames.Length];
                var topDistances = new float[content.Length];
                var sizes = new Vector2[content.Length];
                for (int i = 0; i < content.Length; i++)
                {
                    content[i] = Find(root.transform, ContentNames[i]);
                    topDistances[i] = viewport.rect.yMax - MaxY(viewport, content[i]);
                    sizes[i] = content[i].rect.size;
                }

                RectTransform background = Find(root.transform, "BottomArea");
                RectTransform ornament = Find(root.transform, "Deko_Bottom");
                RectTransform left = Find(root.transform, "Deko_Left");
                RectTransform right = Find(root.transform, "Deko_Right");
                Vector2 backgroundSize = background.sizeDelta;
                Vector2 backgroundPosition = background.anchoredPosition;
                float ornamentBottom = MinY(viewport, ornament) - viewport.rect.yMin;
                float leftHeight = left.rect.height;
                float rightHeight = right.rect.height;
                var backgroundImage = background.GetComponent<Image>();
                Sprite originalSprite = backgroundImage.sprite;
                Color originalColor = backgroundImage.color;

                viewport.sizeDelta = new Vector2(width, height);
                Apply(hud);
                float extra = height - referenceHeight;
                for (int i = 0; i < content.Length; i++)
                {
                    Assert.That(viewport.rect.yMax - MaxY(viewport, content[i]), Is.EqualTo(topDistances[i]).Within(0.01f), content[i].name);
                    Assert.That(Vector2.Distance(content[i].rect.size, sizes[i]), Is.LessThan(0.01f), content[i].name + " size");
                    Assert.That(content[i].localScale, Is.EqualTo(Vector3.one));
                }
                Assert.That(background.anchoredPosition, Is.EqualTo(backgroundPosition));
                Assert.That(background.sizeDelta.x, Is.EqualTo(backgroundSize.x));
                Assert.That(background.rect.height, Is.EqualTo(backgroundSize.y + extra).Within(0.01f));
                Assert.That(MinY(viewport, background), Is.LessThanOrEqualTo(viewport.rect.yMin));
                Assert.That(MinY(viewport, ornament) - viewport.rect.yMin, Is.EqualTo(ornamentBottom).Within(0.01f));
                Assert.That(left.rect.height, Is.EqualTo(leftHeight + extra).Within(0.01f));
                Assert.That(right.rect.height, Is.EqualTo(rightHeight + extra).Within(0.01f));
                Assert.That(backgroundImage.sprite, Is.SameAs(originalSprite));
                Assert.That(backgroundImage.color, Is.EqualTo(originalColor));
                Assert.That(backgroundImage.enabled, Is.True);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void ResizeAndReopenAlwaysUseAuthoredValuesWithoutAccumulatingOffsets()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var viewport = (RectTransform)root.transform;
                var hud = root.GetComponent<UI_BattlePhaseHUD>();
                var items = Find(root.transform, "ItemAndSpeed_Area");
                var bottom = Find(root.transform, "BottomArea");
                var swap = Find(root.transform, "BallteSwap_Button");
                var ornament = Find(root.transform, "Deko_Bottom");
                Vector2 itemPosition = items.anchoredPosition;
                Vector2 bottomSize = bottom.sizeDelta;
                Vector2 swapPosition = swap.anchoredPosition;
                Vector2 ornamentPosition = ornament.anchoredPosition;
                viewport.sizeDelta = new Vector2(1080f, 1920f);
                Apply(hud);

                foreach (float height in new[] { 2340f, 2400f, 1920f, 2340f, 1920f })
                {
                    hud.Close();
                    viewport.sizeDelta = new Vector2(1080f, height);
                    hud.Open();
                    Apply(hud);
                    Apply(hud);
                    float extra = Mathf.Max(0f, height - 1920f);
                    Assert.That(items.anchoredPosition.y, Is.EqualTo(itemPosition.y + extra).Within(0.01f));
                    Assert.That(bottom.sizeDelta.y, Is.EqualTo(bottomSize.y + extra).Within(0.01f));
                    Assert.That(swap.anchoredPosition.y, Is.EqualTo(swapPosition.y + extra * 0.5f).Within(0.01f));
                    Assert.That(ornament.anchoredPosition.y, Is.EqualTo(ornamentPosition.y - extra * 0.5f).Within(0.01f));
                }
                Assert.That(items.anchoredPosition, Is.EqualTo(itemPosition));
                Assert.That(bottom.sizeDelta, Is.EqualTo(bottomSize));
                Assert.That(swap.anchoredPosition, Is.EqualTo(swapPosition));
                Assert.That(ornament.anchoredPosition, Is.EqualTo(ornamentPosition));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void Apply(UI_BattlePhaseHUD hud) => typeof(UI_BattlePhaseHUD)
            .GetMethod("RefreshLowerHudLayout", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, null);

        private static RectTransform Find(Transform root, string name)
        {
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == name) return rect;
            Assert.Fail("Missing authored HUD element: " + name);
            return null;
        }

        private static float MaxY(RectTransform viewport, RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float value = float.MinValue;
            foreach (Vector3 corner in corners) value = Mathf.Max(value, viewport.InverseTransformPoint(corner).y);
            return value;
        }

        private static float MinY(RectTransform viewport, RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float value = float.MaxValue;
            foreach (Vector3 corner in corners) value = Mathf.Min(value, viewport.InverseTransformPoint(corner).y);
            return value;
        }
    }
}
#endif
