#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using InTheArena.UI;
using UnityEditor;
using UnityEngine;

namespace InTheArena.MainGame.Editor
{
    public static class LobbyPlazaBuilder
    {
        public const string Folder = "Assets/ScriptableObject/UI/Plaza";
        public const string PrefabPath = "Assets/Prefabs/UI/Panel/UI_LobbyStagePanel.prefab";
        public static readonly string[] RegionIds = { "royal_knights", "central_castle", "outskirts" };

        [MenuItem("Tools/In The Arena/Build Lobby Plaza")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Build plaza in Edit mode.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/ScriptableObject/UI", "Plaza");
            var catalog = AssetDatabase.LoadAssetAtPath<LobbyUnitCatalogData>("Assets/ScriptableObject/UI/LobbyUnitCatalog.asset");
            if (catalog == null || catalog.Regions.Count != 3) throw new InvalidOperationException("Expected three source regions.");
            var profiles = new List<LobbyPlazaProfile>();
            var textures = new Dictionary<Texture2D, Texture2D>();
            try
            {
                for (int region = 0; region < 3; region++)
                {
                    string path = Folder + "/Plaza_" + RegionIds[region] + ".asset";
                    var profile = AssetDatabase.LoadAssetAtPath<LobbyPlazaProfile>(path);
                    if (profile == null)
                    {
                        profile = ScriptableObject.CreateInstance<LobbyPlazaProfile>();
                        profile.FirstStage = region * 5 + 1; profile.LastStage = region * 5 + 5;
                        string[] backgrounds = {
                            "Assets/Art/Stages/Stage01_RoyalKnights/Stage1Background.png",
                            "Assets/Art/Stages/Stage02_CentralCastle/Stage2Background.png",
                            "Assets/Art/Stages/Stage03_CentralOutskirtsVillage/Stage3Background.png" };
                        profile.Background = AssetDatabase.LoadAssetAtPath<Sprite>(backgrounds[region]);
                        float top = region == 2 ? .50f : .59f;
                        profile.WalkArea = new[] { new Vector2(.18f, .28f), new Vector2(.82f, .28f),
                            new Vector2(.78f, top), new Vector2(.22f, top) };
                        var units = catalog.Regions[region].Units;
                        for (int i = 0; i < 5; i++)
                        {
                            var unit = units[i % units.Count];
                            string visualPath = Folder + "/Visual_" + unit.UnitName + ".asset";
                            var visual = AssetDatabase.LoadAssetAtPath<LobbyNpcVisualData>(visualPath);
                            if (visual == null)
                            {
                                visual = ScriptableObject.CreateInstance<LobbyNpcVisualData>();
                                var animator = unit.UnitPrefab.GetComponentInChildren<Animator>(true);
                                var clips = animator.runtimeAnimatorController.animationClips;
                                var idle = clips.First(c => string.Equals(c.name, "Idle", StringComparison.OrdinalIgnoreCase));
                                var walk = clips.First(c => string.Equals(c.name, "Walk", StringComparison.OrdinalIgnoreCase));
                                visual.Idle = Extract(idle); visual.Walk = Extract(walk);
                                visual.FootPixel = MeasureFoot(visual.Idle.Frames[0], textures);
                                AssetDatabase.CreateAsset(visual, visualPath);
                            }
                            profile.Residents.Add(new LobbyNpcDefinition { Id = RegionIds[region] + "_resident_" + (i + 1).ToString("00"), Visual = visual });
                        }
                        AssetDatabase.CreateAsset(profile, path);
                    }
                    profiles.Add(profile);
                }
                BindPrefab(profiles.ToArray());
                AssetDatabase.SaveAssets();
                Debug.Log("[LobbyPlazaBuilder] Plaza profiles, extracted animations and lobby prefab are ready. Existing profile tuning is preserved.");
            }
            finally { foreach (var texture in textures.Values) UnityEngine.Object.DestroyImmediate(texture); }
        }

        public static LobbyNpcAnimation Extract(AnimationClip clip)
        {
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            var binding = bindings.First(b => b.type == typeof(SpriteRenderer) && b.propertyName == "m_Sprite");
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            var animation = new LobbyNpcAnimation {
                Frames = keys.Select(k => k.value as Sprite).ToArray(),
                Times = keys.Select(k => k.time).ToArray(), Duration = clip.length };
            if (!animation.IsValid) throw new InvalidOperationException("Invalid sprite animation: " + clip.name);
            return animation;
        }

        private static Vector2 MeasureFoot(Sprite sprite, Dictionary<Texture2D, Texture2D> textures)
        {
            if (!textures.TryGetValue(sprite.texture, out var readable))
            {
                var source = sprite.texture;
                var temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                var previous = RenderTexture.active;
                try
                {
                    Graphics.Blit(source, temporary); RenderTexture.active = temporary;
                    readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                    readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); readable.Apply();
                    textures.Add(source, readable);
                }
                finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temporary); }
            }
            Rect r = sprite.rect;
            for (int y = 0; y < r.height; y++)
                for (int x = 0; x < r.width; x++)
                    if (readable.GetPixel((int)r.x + x, (int)r.y + y).a > .1f) return new Vector2(r.width * .5f, y);
            throw new InvalidOperationException("Empty NPC sprite: " + sprite.name);
        }

        private static void BindPrefab(LobbyPlazaProfile[] profiles)
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var controller = root.GetComponentInChildren<LobbyPlazaController>(true);
                if (controller == null)
                {
                    var go = new GameObject("LobbyPlaza", typeof(RectTransform), typeof(CanvasGroup), typeof(LobbyPlazaController));
                    go.transform.SetParent(root.transform, false);
                    var rect = (RectTransform)go.transform;
                    rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                    controller = go.GetComponent<LobbyPlazaController>();
                }
                controller.transform.SetAsFirstSibling();
                Transform background = root.transform.Find("Background");
                if (background != null) controller.transform.SetSiblingIndex(background.GetSiblingIndex());
                var group = controller.GetComponent<CanvasGroup>();
                group.interactable = false; group.blocksRaycasts = false;
                var serialized = new SerializedObject(controller);
                var references = serialized.FindProperty("m_Profiles"); references.arraySize = profiles.Length;
                for (int i = 0; i < profiles.Length; i++) references.GetArrayElementAtIndex(i).objectReferenceValue = profiles[i];
                var controls = serialized.FindProperty("m_LowerControls"); controls.arraySize = 2;
                string[] names = { "StartButton", "Chest_Button" };
                for (int i = 0; i < names.Length; i++)
                    controls.GetArrayElementAtIndex(i).objectReferenceValue = root.GetComponentsInChildren<RectTransform>(true).First(t => t.name == names[i]);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var panel = new SerializedObject(root.GetComponent<UI_LobbyStagePanel>());
                panel.FindProperty("m_Plaza").objectReferenceValue = controller;
                panel.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
#endif
