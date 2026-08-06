#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using TMPro;
using InTheArena.UI;
using InTheArena.Unit;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.Editor.MainGame
{
    internal static class LobbyUnitPanelUiBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/UI/Panel/UI_LobbyUnitPanel.prefab";
        private const string FontPath = "Assets/Font/Galmuri9 SDF.asset";
        private const string DescriptionFolder = "Assets/ScriptableObject/UI/UnitDescription";
        private const string BuildRootName = "UnitButtonArea";
        private static readonly Color ButtonColor = new(0.96f, 0.47f, 0.12f, 1f);
        private static readonly Color ButtonOutlineColor = new(0.58f, 0.24f, 0.04f, 1f);
        private static TMP_FontAsset s_FontAsset;

        private static readonly UnitGroup[] Groups =
        {
            new(
                "\uc655\uad81 \uae30\uc0ac\ub2e8",
                new[]
                {
                    new UnitButton("Knight", "\uae30\uc0ac", "Assets/Sprites/Unit/BlueSide/MiniShieldMan.png", "MiniShieldMan_0", "Assets/ScriptableObject/Unit/Unit_Base/UnitData_Knight.asset"),
                    new UnitButton("Archer", "\uad81\uc218", "Assets/Sprites/Unit/BlueSide/MiniArcherMan.png", "MiniArcherMan_0", "Assets/ScriptableObject/Unit/Unit_Base/UnitData_Archer.asset"),
                    new UnitButton("Mage", "\ub9c8\ubc95\uc0ac", "Assets/Sprites/Unit/BlueSide/MiniMage.png", "MiniMage_0", "Assets/ScriptableObject/Unit/Unit_Base/UnitData_Wizard.asset"),
                    new UnitButton("Priest", "\uc0ac\uc81c", "Assets/Sprites/Unit/BlueSide/MiniArchMage.png", "MiniArchMage_0", "Assets/ScriptableObject/Unit/Unit_Base/UnitData_Prist.asset"),
                }),
            new(
                "\uc13c\ud2b8\ub7f4 \uce90\uc2ac",
                new[]
                {
                    new UnitButton("King", "\uc655", "Assets/Sprites/Unit/BlueSide/MiniKingMan.png", "MiniKingMan_0", "Assets/ScriptableObject/Unit/Unit_Base/UnitData_King.asset"),
                    new UnitButton("Peasant", "\ub18d\ubd80", "Assets/Sprites/Unit/BlueSide/MiniPeasant.png", "MiniPeasant_0", "Assets/ScriptableObject/Unit/Unit_Base/UnitData_Peasant.asset"),
                    new UnitButton("Thief", "\ub3c4\ub451", "Assets/Sprites/Unit/BlueSide/MiniThiefBlue.png", "MiniThiefBlue_0", "Assets/ScriptableObject/Unit/Unit_Base/UnitData_Thief.asset"),
                }),
            new(
                "\uc13c\ud2b8\ub7f4 \uc678\uacfd \ub9c8\uc744",
                new[]
                {
                    new UnitButton("Lumberjack", "\ub098\ubb34\uafbc", "Assets/Sprites/Unit/BlueSide/MiniLumberjackBlue.png", "MiniLumberjackBlue_0", "Assets/ScriptableObject/Unit/Unit_Base/UnitData_Lumberjack.asset"),
                    new UnitButton("Hunter", "\uc0ac\ub0e5\uafbc", "Assets/Sprites/Unit/BlueSide/MiniHunterBlue.png", "MiniHunterBlue_0", "Assets/ScriptableObject/Unit/Unit_Base/UnitData_Hunter.asset"),
                    new UnitButton("Blacksmith", "\ub300\uc7a5\uc7c1\uc774", "Assets/Sprites/Unit/BlueSide/MiniBlacksmithBlue.png", "MiniBlacksmithBlue_0", "Assets/ScriptableObject/Unit/Unit_Base/UnitData_Blacksmith.asset"),
                }),
        };

        [MenuItem("Tools/In The Arena/Rebuild Lobby Unit Button Layout")]
        public static void RebuildFromMenu()
        {
            Rebuild();
        }

        private static void Rebuild()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (prefabRoot == null)
                {
                    Debug.LogError($"Lobby unit panel prefab was not found: {PrefabPath}");
                    return;
                }

                Transform backgroundTransform = FindDirectChild(prefabRoot.transform, "Background");
                if (backgroundTransform == null || !backgroundTransform.TryGetComponent(out RectTransform background))
                {
                    Debug.LogError("Background was not found. UI_LobbyUnitPanel was not changed.");
                    return;
                }

                Transform oldButtonArea = FindDirectChild(background, BuildRootName);
                if (oldButtonArea != null)
                    UnityEngine.Object.DestroyImmediate(oldButtonArea.gameObject);

                CreateUnitList(background);
                BringDescriptionPopupsToFront(background);
                UpdateExistingDescriptionPopups(background);
                ConnectButtonsToExistingPopups(background);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"Rebuilt lobby unit button layout: {PrefabPath}");
            }
            finally
            {
                if (prefabRoot != null)
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void CreateUnitList(RectTransform background)
        {
            GameObject root = CreateUiObject(BuildRootName, background);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect, 0f, 0f, 0f, 0f);

            UnitGroup centralCastle = Groups[0];
            UnitGroup middleRegion = Groups[1];
            UnitGroup lowerRegion = Groups[2];

            CreateFixedTitle(rootRect, "CentralCastleTitle", centralCastle.Title, 0.875f, -20f);
            CreateFixedButton(rootRect, centralCastle.Units[0], 0.24f, 0.735f);
            CreateFixedButton(rootRect, centralCastle.Units[1], 0.50f, 0.735f);
            CreateFixedButton(rootRect, centralCastle.Units[2], 0.76f, 0.735f);
            CreateFixedButton(rootRect, centralCastle.Units[3], 0.24f, 0.570f);

            CreateFixedTitle(rootRect, "MiddleRegionTitle", middleRegion.Title, 0.430f);
            CreateFixedButton(rootRect, middleRegion.Units[0], 0.24f, 0.310f);
            CreateFixedButton(rootRect, middleRegion.Units[1], 0.50f, 0.310f);
            CreateFixedButton(rootRect, middleRegion.Units[2], 0.76f, 0.310f);

            CreateFixedTitle(rootRect, "LowerRegionTitle", lowerRegion.Title, 0.190f);
            CreateFixedButton(rootRect, lowerRegion.Units[0], 0.24f, 0.080f);
            CreateFixedButton(rootRect, lowerRegion.Units[1], 0.50f, 0.080f);
            CreateFixedButton(rootRect, lowerRegion.Units[2], 0.76f, 0.080f);
        }

        private static void CreateFixedTitle(Transform parent, string objectName, string title, float centerY, float positionY = 0f)
        {
            TMP_Text titleText = CreateText(objectName, parent, title, 50f, Color.black, TextAlignmentOptions.Center, FontStyles.Bold);
            RectTransform titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, centerY);
            titleRect.anchorMax = new Vector2(1f, centerY);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, positionY);
            titleRect.sizeDelta = new Vector2(0f, 68f);
        }

        private static void CreateFixedButton(Transform parent, UnitButton unit, float centerX, float centerY)
        {
            GameObject buttonObject = CreateUnitButton(parent, unit);
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(centerX, centerY);
            buttonRect.anchorMax = new Vector2(centerX, centerY);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = Vector2.zero;
            buttonRect.sizeDelta = new Vector2(72f, 72f);
            buttonRect.localScale = new Vector3(3f, 3f, 3f);
        }

        private static GameObject CreateUnitButton(Transform parent, UnitButton unit)
        {
            GameObject buttonObject = CreateUiObject($"{unit.Id}_Button", parent, typeof(Image), typeof(Button), typeof(Outline));
            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = ButtonColor;
            buttonImage.raycastTarget = true;

            Outline outline = buttonObject.GetComponent<Outline>();
            outline.effectColor = ButtonOutlineColor;
            outline.effectDistance = new Vector2(3f, -3f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = buttonImage;

            GameObject iconObject = CreateUiObject("Icon", buttonObject.transform, typeof(Image));
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            Anchor(iconRect, 0.25f, 0.47f, 0.75f, 0.88f, 0f, 0f, 0f, 0f);
            float iconScale = unit.Id is "Knight" or "Archer" ? 2.5f : 3f;
            iconRect.localScale = new Vector3(iconScale, iconScale, iconScale);
            iconRect.anchoredPosition = new Vector2(0f, unit.Id is "Knight" or "Archer" ? 11f : 17f);
            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = LoadSprite(unit.SpritePath, unit.SpriteName);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            TMP_Text label = CreateText("Label", buttonObject.transform, unit.DisplayName, 19f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            RectTransform labelRect = label.GetComponent<RectTransform>();
            Anchor(labelRect, 0f, 0.02f, 1f, 0.34f, 2f, 0f, -2f, 0f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = 19f;
            return buttonObject;
        }

        private static void ConnectButtonsToExistingPopups(RectTransform background)
        {
            Transform popupRoot = FindDirectChild(background, "UnitDescriptionPopups");
            if (popupRoot == null)
                return;

            foreach (UnitGroup group in Groups)
            {
                foreach (UnitButton unit in group.Units)
                {
                    Button button = FindDirectChildRecursive(background, $"{unit.Id}_Button")?.GetComponent<Button>();
                    GameObject popup = FindDirectChildRecursive(popupRoot, $"{unit.Id}_DescriptionPopup")?.gameObject;
                    if (button != null && popup != null)
                        UnityEventTools.AddBoolPersistentListener(button.onClick, popup.SetActive, true);
                }
            }
        }

        private static void BringDescriptionPopupsToFront(RectTransform background)
        {
            Transform popupRoot = FindDirectChild(background, "UnitDescriptionPopups");
            if (popupRoot != null)
                popupRoot.SetAsLastSibling();
        }

        private static void UpdateExistingDescriptionPopups(RectTransform background)
        {
            Transform popupRoot = FindDirectChild(background, "UnitDescriptionPopups");
            if (popupRoot == null)
                return;

            foreach (UnitGroup group in Groups)
            {
                foreach (UnitButton unit in group.Units)
                {
                    Transform popup = FindDirectChildRecursive(popupRoot, $"{unit.Id}_DescriptionPopup");
                    if (popup != null)
                        UpdateDescriptionPopup(popup, unit);
                }
            }
        }

        private static void UpdateDescriptionPopup(Transform popup, UnitButton unit)
        {
            UnitDescriptionData description = LoadDescription(unit);
            Transform panel = FindDirectChildRecursive(popup, "Panel");
            if (panel == null)
                return;

            if (panel is RectTransform panelRect)
                SetVerticalInspectorOffsets(panelRect, 100f, -100f);

            Transform closeButton = FindDirectChildRecursive(panel, "CloseButton");
            if (closeButton != null)
                UnityEngine.Object.DestroyImmediate(closeButton.gameObject);

            RectTransform unitIcon = FindDirectChildRecursive(panel, "UnitIcon") as RectTransform;
            if (unitIcon != null)
            {
                unitIcon.localScale = new Vector3(2f, 2f, 2f);
                SetVerticalInspectorOffsets(unitIcon, -93f, 93f);
                if (unitIcon.TryGetComponent(out Image unitIconImage))
                {
                    unitIconImage.sprite = description != null && description.UnitIcon != null
                        ? description.UnitIcon
                        : LoadSprite(unit.SpritePath, unit.SpriteName);
                    unitIconImage.preserveAspect = true;
                }
            }

            RectTransform skillIcon = FindDirectChildRecursive(panel, "SkillIcon") as RectTransform;
            if (skillIcon != null)
            {
                skillIcon.localScale = new Vector3(2f, 1f, 1f);
                SetInspectorOffsets(skillIcon, 55f, 390f, 45f, -290f);
                if (skillIcon.TryGetComponent(out Image skillIconImage))
                {
                    skillIconImage.sprite = description != null ? description.SkillIcon : null;
                    skillIconImage.color = skillIconImage.sprite != null
                        ? Color.white
                        : new Color(0.6f, 0.6f, 0.6f, 1f);
                    skillIconImage.preserveAspect = true;
                }
            }

            Transform header = FindDirectChildRecursive(panel, "Header");
            if (header is RectTransform headerRect)
                SetVerticalInspectorOffsets(headerRect, 0f, 0f);

            ApplyTextSize(header != null ? FindDirectChildRecursive(header, "Title") : null, 50f);
            ApplyText(FindDirectChildRecursive(panel, "UnitName"), GetUnitName(unit, description), 50f);
            ApplyText(FindDirectChildRecursive(panel, "Summary"), GetSummary(description), 40f);
            RemoveSkillType(panel);
            ApplyText(FindDirectChildRecursive(panel, "SkillName"), GetSkillName(description), 50f);
            ApplyText(FindDirectChildRecursive(panel, "SkillDescription"), GetSkillDescription(description), 35f);

            Transform backButton = FindDirectChildRecursive(panel, "BackButton");
            if (backButton != null)
                ApplyTextSize(FindDirectChildRecursive(backButton, "Label"), 50f);

            Transform statsTable = FindDirectChildRecursive(panel, "StatsTable");
            if (statsTable != null)
                UpdateStatsTable(statsTable, unit);
        }

        private static void ApplyTextSize(Transform target, float fontSize)
        {
            if (target == null || !target.TryGetComponent(out TMP_Text text))
                return;

            text.fontSize = fontSize;
            text.enableAutoSizing = false;
        }

        private static void ApplyText(Transform target, string value, float fontSize)
        {
            if (target == null || !target.TryGetComponent(out TMP_Text text))
                return;

            text.text = value;
            text.fontSize = fontSize;
            text.enableAutoSizing = false;
        }

        private static void RemoveSkillType(Transform panel)
        {
            Transform skillType = FindDirectChildRecursive(panel, "SkillType");
            if (skillType != null)
                UnityEngine.Object.DestroyImmediate(skillType.gameObject);
        }

        private static UnitDescriptionData LoadDescription(UnitButton unit)
        {
            return AssetDatabase.LoadAssetAtPath<UnitDescriptionData>($"{DescriptionFolder}/UnitDescription_{unit.Id}.asset");
        }

        private static UnitData LoadUnitData(UnitButton unit)
        {
            return AssetDatabase.LoadAssetAtPath<UnitData>(unit.UnitDataPath);
        }

        private static string GetUnitName(UnitButton unit, UnitDescriptionData description)
        {
            return description != null && !string.IsNullOrWhiteSpace(description.UnitName)
                ? description.UnitName
                : unit.DisplayName;
        }

        private static string GetSummary(UnitDescriptionData description)
        {
            return description != null && !string.IsNullOrWhiteSpace(description.Summary)
                ? description.Summary
                : "\uac04\ub2e8\ud55c \uc124\uba85\uc744 \uc785\ub825\ud558\uc138\uc694";
        }

        private static string GetSkillName(UnitDescriptionData description)
        {
            return description != null && !string.IsNullOrWhiteSpace(description.SkillName)
                ? description.SkillName
                : "\uc2a4\ud0ac \uc774\ub984";
        }

        private static string GetSkillDescription(UnitDescriptionData description)
        {
            return description != null && !string.IsNullOrWhiteSpace(description.SkillDescription)
                ? description.SkillDescription
                : "\uc2a4\ud0ac \uc124\uba85\uc744 \uc785\ub825\ud558\uc138\uc694";
        }

        private static void UpdateStatsTable(Transform statsTable, UnitButton unit)
        {
            UnitData data = LoadUnitData(unit);
            UnitStat stat = data != null ? data.BaseStat : UnitStat.Default;
            StatEntry[] entries =
            {
                new("HP", "\uccb4\ub825", FormatStat(stat.maxHp)),
                new("ATK", "\uacf5\uaca9\ub825", FormatStat(stat.attackPower)),
                new("DEF", "\ubc29\uc5b4\ub825", FormatStat(stat.defense)),
                new("AS", "\uacf5\uaca9 \uc18d\ub3c4", FormatStat(stat.attackSpeed)),
                new("MV", "\uc774\ub3d9\uc18d\ub3c4", FormatStat(stat.moveSpeed)),
                new("RG", "\uc0ac\uac70\ub9ac", FormatStat(stat.attackRange)),
            };

            foreach (StatEntry entry in entries)
            {
                Transform cell = FindStatCell(statsTable, entry.IconText);
                if (cell == null)
                    continue;

                ApplyText(FindDirectChildRecursive(cell, "IconText"), $"{entry.IconText}\nIMG", 20f);
                ApplyText(FindDirectChildRecursive(cell, "Label"), entry.Label, 20f);
                ApplyText(FindDirectChildRecursive(cell, "Value"), entry.Value, 20f);
            }

            TMP_Text[] texts = statsTable.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in texts)
            {
                text.fontSize = 20f;
                text.enableAutoSizing = false;
            }
        }

        private static Transform FindStatCell(Transform statsTable, string iconText)
        {
            for (int i = 0; i < statsTable.childCount; i++)
            {
                Transform child = statsTable.GetChild(i);
                if (child.name.StartsWith($"{iconText}_", StringComparison.Ordinal))
                    return child;
            }

            return null;
        }

        private static string FormatStat(float value)
        {
            return Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.0");
        }

        private static void SetVerticalInspectorOffsets(RectTransform rect, float top, float bottom)
        {
            rect.offsetMin = new Vector2(rect.offsetMin.x, bottom);
            rect.offsetMax = new Vector2(rect.offsetMax.x, -top);
        }

        private static void SetInspectorOffsets(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, float fontSize, Color color, TextAlignmentOptions alignment, FontStyles style)
        {
            GameObject textObject = CreateUiObject(name, parent, typeof(TextMeshProUGUI));
            TMP_Text tmp = textObject.GetComponent<TMP_Text>();
            TMP_FontAsset fontAsset = LoadFontAsset();
            if (fontAsset != null)
            {
                tmp.font = fontAsset;
                tmp.fontSharedMaterial = fontAsset.material;
            }

            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.fontStyle = style;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = true;
            return tmp;
        }

        private static TMP_FontAsset LoadFontAsset()
        {
            if (s_FontAsset == null)
                s_FontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            if (s_FontAsset == null)
                Debug.LogWarning($"Font asset was not found: {FontPath}");

            return s_FontAsset;
        }

        private static GameObject CreateUiObject(string name, Transform parent, params Type[] components)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(CanvasRenderer));
            gameObject.transform.SetParent(parent, false);

            foreach (Type component in components)
            {
                if (component == typeof(RectTransform) || component == typeof(CanvasRenderer))
                    continue;

                gameObject.AddComponent(component);
            }

            return gameObject;
        }

        private static Sprite LoadSprite(string assetPath, string spriteName)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is Sprite sprite && sprite.name == spriteName)
                    return sprite;
            }

            Debug.LogWarning($"Sprite was not found: {assetPath}/{spriteName}");
            return null;
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                    return child;
            }

            return null;
        }

        private static Transform FindDirectChildRecursive(Transform parent, string name)
        {
            Transform direct = FindDirectChild(parent, name);
            if (direct != null)
                return direct;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDirectChildRecursive(parent.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void Anchor(RectTransform rect, float minX, float minY, float maxX, float maxY, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        private readonly struct UnitGroup
        {
            public UnitGroup(string title, UnitButton[] units)
            {
                Title = title;
                Units = units;
            }

            public string Title { get; }
            public UnitButton[] Units { get; }
        }

        private readonly struct UnitButton
        {
            public UnitButton(string id, string displayName, string spritePath, string spriteName, string unitDataPath)
            {
                Id = id;
                DisplayName = displayName;
                SpritePath = spritePath;
                SpriteName = spriteName;
                UnitDataPath = unitDataPath;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public string SpritePath { get; }
            public string SpriteName { get; }
            public string UnitDataPath { get; }
        }

        private readonly struct StatEntry
        {
            public StatEntry(string iconText, string label, string value)
            {
                IconText = iconText;
                Label = label;
                Value = value;
            }

            public string IconText { get; }
            public string Label { get; }
            public string Value { get; }
        }
    }
}
#endif
