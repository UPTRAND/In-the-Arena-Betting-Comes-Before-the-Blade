#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using InTheArena.Unit;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnitType = InTheArena.Unit.Unit;

namespace InTheArena.EditorTests
{
    public sealed class TeamUnitSpriteTests
    {
        private static readonly MethodInfo ResolveTeamSpriteMethod = typeof(UnitType).GetMethod(
            "ResolveTeamSprite",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo TeamField = typeof(UnitType).GetField(
            "m_Team",
            BindingFlags.Instance | BindingFlags.NonPublic);

        [TestCase("Archer", "MiniArcherMan")]
        [TestCase("Knight", "MiniShieldMan")]
        [TestCase("Wizard", "MiniMage")]
        [TestCase("Prist", "MiniArchMage")]
        public void UnitPrefab_MapsEveryBlueFrameToItsRedVariant(string unitName, string sheetName)
        {
            string bluePath = $"Assets/Sprites/Unit/BlueSide/{sheetName}.png";
            string redPath = $"Assets/Sprites/Unit/RedSide/{sheetName}Red.png";
            Sprite[] blueSprites = LoadSprites(bluePath);
            Sprite[] redSprites = LoadSprites(redPath);

            Assert.That(blueSprites.Length, Is.GreaterThan(0));
            Assert.That(redSprites.Length, Is.EqualTo(blueSprites.Length));
            Assert.That(((TextureImporter)AssetImporter.GetAtPath(redPath)).spritePixelsPerUnit,
                Is.EqualTo(((TextureImporter)AssetImporter.GetAtPath(bluePath)).spritePixelsPerUnit));

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Prefabs/Unit/Unit_{unitName}.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<SpriteRenderer>().sprite, Is.Not.Null);
            AssertAnimationSpritesAreLinked(unitName);

            SerializedProperty redTeamSprites = new SerializedObject(prefab.GetComponent<UnitType>())
                .FindProperty("m_RedTeamSprites");
            Assert.That(redTeamSprites.arraySize, Is.EqualTo(redSprites.Length));
            for (int i = 0; i < redSprites.Length; i++)
            {
                Assert.That(blueSprites[i].name, Is.EqualTo($"{sheetName}_{i}"));
                Assert.That(redSprites[i].name, Is.EqualTo($"{sheetName}Red_{i}"));
                Assert.That(redTeamSprites.GetArrayElementAtIndex(i).objectReferenceValue,
                    Is.SameAs(redSprites[i]));
            }

            UnitType instance = UnityEngine.Object.Instantiate(prefab).GetComponent<UnitType>();
            try
            {
                Assert.That(ResolveTeamSpriteMethod, Is.Not.Null);
                Assert.That(TeamField, Is.Not.Null);
                TeamField.SetValue(instance, 0);
                Assert.That(ResolveTeamSpriteMethod.Invoke(instance, new object[] { blueSprites[^1] }),
                    Is.SameAs(redSprites[^1]));
                TeamField.SetValue(instance, 1);
                Assert.That(ResolveTeamSpriteMethod.Invoke(instance, new object[] { blueSprites[^1] }),
                    Is.SameAs(blueSprites[^1]));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance.gameObject);
            }
        }

        private static Sprite[] LoadSprites(string path)
            => AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .OrderBy(sprite => FrameIndex(sprite.name))
                .ToArray();

        private static int FrameIndex(string spriteName)
            => int.Parse(spriteName[(spriteName.LastIndexOf('_') + 1)..]);

        private static void AssertAnimationSpritesAreLinked(string unitName)
        {
            foreach (string guid in AssetDatabase.FindAssets(
                         "t:AnimationClip", new[] { $"Assets/Animator/Unit/{unitName}" }))
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    AssetDatabase.GUIDToAssetPath(guid));
                foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                foreach (ObjectReferenceKeyframe frame in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                    Assert.That(frame.value, Is.Not.Null, $"{unitName}/{clip.name} has a missing sprite.");
            }
        }
    }
}
#endif
