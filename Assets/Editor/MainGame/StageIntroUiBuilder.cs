#if UNITY_EDITOR
using System;
using System.Linq;
using InTheArena.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.MainGame.Editor
{
    public static class StageIntroUiBuilder
    {
        private const string BettingPrefabPath = "Assets/Prefabs/UI/Panel/UI_BettingPhase.prefab";
        private const string SpriteSheetPath = "Assets/Sprites/UI/Icon/BattleIntro.png";

        [MenuItem("Tools/In The Arena/Build Stage Intro UI")]
        public static void Build()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(BettingPrefabPath);
            try
            {
                BuildPrefabContents(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, BettingPrefabPath);
                Debug.Log("[StageIntroUiBuilder] Stage opening intro UI was rebuilt successfully.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void BuildPrefabContents(GameObject prefabRoot)
        {
            RectTransform rootRect = prefabRoot.transform as RectTransform;
            if (rootRect == null)
                throw new InvalidOperationException("UI_BettingPhase root must use RectTransform.");

            RectTransform bettingContent = FindChild(rootRect, "BettingContent") as RectTransform;
            RectTransform topBar = FindChild(rootRect, "TopBar") as RectTransform;
            TMP_Text fontSource = FindChild(rootRect, "RoundInfo_Text")?.GetComponent<TMP_Text>();
            if (bettingContent == null || topBar == null || fontSource == null)
                throw new InvalidOperationException("Required betting UI hierarchy or font source is missing.");

            DestroyExisting(rootRect, "StageIntroVisual");
            DestroyExisting(rootRect, "StageIntroInputBlocker");

            Sprite redSword = LoadSprite("BattleIntro_0");
            Sprite blueSword = LoadSprite("BattleIntro_1");
            Sprite shield = LoadSprite("BattleIntro_2");

            RectTransform visualRoot = CreateRect("StageIntroVisual", rootRect);
            Stretch(visualRoot);
            visualRoot.SetSiblingIndex(0);

            RectTransform cameraArea = CreateRect("CameraArea", visualRoot);
            cameraArea.anchorMin = new Vector2(0f, 0.375f);
            cameraArea.anchorMax = new Vector2(1f, 0.875f);
            cameraArea.offsetMin = Vector2.zero;
            cameraArea.offsetMax = Vector2.zero;

            RectTransform emblemRoot = CreateRect("Emblem", cameraArea);
            Center(emblemRoot, new Vector2(594f, 487f));

            Image redImage = CreateImage("RedSword", emblemRoot, redSword, false);
            Image blueImage = CreateImage("BlueSword", emblemRoot, blueSword, false);
            Image shieldImage = CreateImage("Shield", emblemRoot, shield, false);
            Center(redImage.rectTransform, new Vector2(478f, 478f));
            Center(blueImage.rectTransform, new Vector2(478f, 478f));
            Center(shieldImage.rectTransform, new Vector2(312f, 312f));
            redImage.rectTransform.SetSiblingIndex(0);
            blueImage.rectTransform.SetSiblingIndex(1);
            shieldImage.rectTransform.SetSiblingIndex(2);

            RectTransform infoArea = CreateRect("StageInfoArea", visualRoot);
            infoArea.anchorMin = Vector2.zero;
            infoArea.anchorMax = new Vector2(1f, 0.375f);
            infoArea.offsetMin = Vector2.zero;
            infoArea.offsetMax = Vector2.zero;
            Image infoBackground = infoArea.gameObject.AddComponent<Image>();
            infoBackground.color = Color.black;
            infoBackground.raycastTarget = false;

            RectTransform infoRoot = CreateRect("StageInfo", infoArea);
            Center(infoRoot, new Vector2(920f, 310f));
            TMP_Text stageText = CreateText("StageText", infoRoot, fontSource, 64f, FontStyles.Bold);
            TMP_Text roundText = CreateText("RoundText", infoRoot, fontSource, 44f, FontStyles.Bold);
            TMP_Text targetText = CreateText("TargetCallText", infoRoot, fontSource, 44f, FontStyles.Bold);
            PlaceTopAnchored(stageText.rectTransform, 0f, 90f);
            PlaceTopAnchored(roundText.rectTransform, -105f, 70f);
            PlaceTopAnchored(targetText.rectTransform, -190f, 70f);
            stageText.text = "스테이지 1";
            roundText.text = "라운드 횟수 5";
            targetText.text = "목표 콜 1500";

            RectTransform blockerRect = CreateRect("StageIntroInputBlocker", rootRect);
            Stretch(blockerRect);
            Image blockerImage = blockerRect.gameObject.AddComponent<Image>();
            blockerImage.color = new Color(0f, 0f, 0f, 0.001f);
            blockerImage.raycastTarget = true;
            Button blockerButton = blockerRect.gameObject.AddComponent<Button>();
            blockerButton.transition = Selectable.Transition.None;
            blockerButton.targetGraphic = blockerImage;

            UI_StageIntro intro = visualRoot.gameObject.AddComponent<UI_StageIntro>();
            SerializedObject serializedIntro = new SerializedObject(intro);
            SetReference(serializedIntro, "m_VisualRoot", visualRoot);
            SetReference(serializedIntro, "m_CameraArea", cameraArea);
            SetReference(serializedIntro, "m_InfoArea", infoArea);
            SetReference(serializedIntro, "m_EmblemRoot", emblemRoot);
            SetReference(serializedIntro, "m_RedSwordRect", redImage.rectTransform);
            SetReference(serializedIntro, "m_BlueSwordRect", blueImage.rectTransform);
            SetReference(serializedIntro, "m_ShieldRect", shieldImage.rectTransform);
            SetReference(serializedIntro, "m_InfoRoot", infoRoot.gameObject);
            SetReference(serializedIntro, "m_StageText", stageText);
            SetReference(serializedIntro, "m_RoundText", roundText);
            SetReference(serializedIntro, "m_TargetCallText", targetText);
            SetReference(serializedIntro, "m_InputBlocker", blockerRect.gameObject);
            SetReference(serializedIntro, "m_InputButton", blockerButton);
            serializedIntro.ApplyModifiedPropertiesWithoutUndo();

            CanvasGroup contentGroup = bettingContent.GetComponent<CanvasGroup>();
            if (contentGroup == null)
                throw new InvalidOperationException("BettingContent requires CanvasGroup.");
            contentGroup.alpha = 0f;
            contentGroup.interactable = false;
            contentGroup.blocksRaycasts = false;

            bettingContent.SetSiblingIndex(1);
            topBar.SetSiblingIndex(2);
            blockerRect.SetSiblingIndex(3);
            infoRoot.gameObject.SetActive(false);
            visualRoot.gameObject.SetActive(false);
            blockerRect.gameObject.SetActive(false);

            EditorUtility.SetDirty(prefabRoot);
        }

        private static Sprite LoadSprite(string spriteName)
        {
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(SpriteSheetPath)
                .OfType<Sprite>()
                .FirstOrDefault(candidate => candidate.name == spriteName);
            return sprite != null
                ? sprite
                : throw new InvalidOperationException($"Sprite '{spriteName}' was not found in {SpriteSheetPath}.");
        }

        private static TMP_Text CreateText(
            string name,
            RectTransform parent,
            TMP_Text fontSource,
            float fontSize,
            FontStyles fontStyle)
        {
            RectTransform rect = CreateRect(name, parent);
            TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = fontSource.font;
            text.fontSharedMaterial = fontSource.fontSharedMaterial;
            text.color = Color.white;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private static Image CreateImage(
            string name,
            RectTransform parent,
            Sprite sprite,
            bool raycastTarget)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = parent.gameObject.layer;
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        private static void PlaceTopAnchored(RectTransform rect, float y, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(920f, height);
        }

        private static Transform FindChild(Transform root, string objectName)
        {
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChild(root.GetChild(i), objectName);
                if (found != null) return found;
            }
            return null;
        }

        private static void DestroyExisting(Transform root, string objectName)
        {
            Transform existing = FindChild(root, objectName);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }
        }

        private static void SetReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException($"Serialized property '{propertyName}' was not found.");
            property.objectReferenceValue = value;
        }
    }
}
#endif
