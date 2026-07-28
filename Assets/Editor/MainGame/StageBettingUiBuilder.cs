#if UNITY_EDITOR
using System.Collections.Generic;
using InTheArena.MainGame;
using InTheArena.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace InTheArena.MainGame.Editor
{
    /// <summary>
    /// Stage/Betting 리팩터링 UI를 기존 프리팹과 MainGame/Debug 씬에 재현 가능하게 반영합니다.
    /// </summary>
    public static class StageBettingUiBuilder
    {
        private const string BettingPrefabPath = "Assets/Prefabs/UI/Panel/BettingPhaseUI.prefab";
        private const string ResultPrefabPath = "Assets/Prefabs/UI/Panel/UI_StageResultPanel.prefab";

        [MenuItem("Tools/In The Arena/Rebuild Stage Betting UI")]
        public static void Rebuild()
        {
            BuildBettingPrefab();
            GameObject resultPrefab = BuildStageResultPrefab();
            BindScene("Assets/Scenes/MainGame.unity", resultPrefab);
            BindScene("Assets/Scenes/Debug.unity", resultPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[StageBettingUiBuilder] Stage/Betting UI 생성 및 씬 연결 완료");
        }

        private static void BuildBettingPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BettingPrefabPath);
            try
            {
                Transform previous = root.transform.Find("BettingControls");
                if (previous != null) Object.DestroyImmediate(previous.gameObject);
                Transform legacy = root.transform.Find("Betting");
                if (legacy != null) legacy.gameObject.SetActive(false);

                GameObject controls = CreatePanel("BettingControls", root.transform, new Color(0.05f, 0.06f, 0.08f, 0.94f));
                RectTransform controlsRect = (RectTransform)controls.transform;
                controlsRect.anchorMin = new Vector2(0.04f, 0.02f);
                controlsRect.anchorMax = new Vector2(0.96f, 0.52f);
                controlsRect.offsetMin = Vector2.zero;
                controlsRect.offsetMax = Vector2.zero;

                var layout = controls.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(16, 16, 12, 12);
                layout.spacing = 7f;
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlHeight = true;
                layout.childControlWidth = true;
                layout.childForceExpandHeight = false;
                layout.childForceExpandWidth = true;

                GameObject summary = CreateRow("Summary", controls.transform, 48f);
                TMP_Text currentCall = CreateText("CurrentCallText", summary.transform, "500 Call");
                TMP_Text wagerCall = CreateText("WagerCallText", summary.transform, "500 Call");
                TMP_Text multiplier = CreateText("MultiplierText", summary.transform, "×0");
                TMP_Text payout = CreateText("EstimatedPayoutText", summary.transform, "0 Call");

                Slider wagerSlider = CreateSlider("WagerSlider", controls.transform);

                GameObject factionRoot = CreateRow("FactionBetRoot", controls.transform, 52f);
                CreateText("Label", factionRoot.transform, "Faction");
                Button red = CreateButton("RedButton", factionRoot.transform, "RED", new Color(0.8f, 0.2f, 0.2f));
                Button blue = CreateButton("BlueButton", factionRoot.transform, "BLUE", new Color(0.2f, 0.45f, 0.9f));
                Button draw = CreateButton("DrawButton", factionRoot.transform, "DRAW", new Color(0.45f, 0.45f, 0.45f));
                Button clear = CreateButton("ClearFactionButton", factionRoot.transform, "NONE", new Color(0.25f, 0.25f, 0.25f));

                GameObject timeRoot = CreateRow("RemainingTimeRoot", controls.transform, 52f);
                CreateText("Label", timeRoot.transform, "Time");
                Button[] timeButtons = CreateButtons(timeRoot.transform,
                    new[] { "0-5", "5-10", "10-15", "15-20", "20+" }, "Time");

                GameObject survivorRoot = CreateRow("SurvivingSlotsRoot", controls.transform, 52f);
                CreateText("Label", survivorRoot.transform, "Alive Slots");
                Button[] survivorButtons = CreateButtons(survivorRoot.transform,
                    new[] { "1", "2", "3", "4", "5", "6" }, "Survivor");

                GameObject oddEvenRoot = CreateRow("OddEvenRoot", controls.transform, 52f);
                CreateText("Label", oddEvenRoot.transform, "Alive Count");
                Button[] oddEvenButtons = CreateButtons(oddEvenRoot.transform,
                    new[] { "ODD", "EVEN" }, "OddEven");

                GameObject firstRoot = CreateRow("FirstEliminatedSlotRoot", controls.transform, 52f);
                CreateText("Label", firstRoot.transform, "First Out");
                Button[] firstButtons = CreateButtons(firstRoot.transform,
                    new[] { "1", "2", "3", "4", "5", "6" }, "FirstOut");

                TMP_Text validation = CreateText("ValidationText", controls.transform, string.Empty);
                validation.color = new Color(1f, 0.45f, 0.35f);
                validation.fontSize = 22f;
                validation.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;

                Button confirm = CreateButton("ConfirmBetButton", controls.transform, "CONFIRM BET", new Color(0.18f, 0.72f, 0.3f));
                confirm.gameObject.AddComponent<LayoutElement>().preferredHeight = 62f;

                PrefabUtility.SaveAsPrefabAsset(root, BettingPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject BuildStageResultPrefab()
        {
            GameObject root = new GameObject(
                "UI_StageResultPanel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(CanvasGroup),
                typeof(UI_StageResultPanel));
            RectTransform rootRect = (RectTransform)root.transform;
            Stretch(rootRect);

            GameObject background = CreatePanel("Background", root.transform, new Color(0.03f, 0.035f, 0.05f, 0.97f));
            Stretch((RectTransform)background.transform);

            var layout = background.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(90, 90, 360, 360);
            layout.spacing = 28f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            TMP_Text title = CreateText("TitleText", background.transform, "STAGE CLEAR");
            title.fontSize = 74f;
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 110f;
            TMP_Text current = CreateText("CurrentCallText", background.transform, "Current  0 Call");
            current.fontSize = 42f;
            current.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;
            TMP_Text target = CreateText("TargetCallText", background.transform, "Target  0 Call");
            target.fontSize = 42f;
            target.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;
            Button lobby = CreateButton("ReturnToLobbyButton", background.transform, "RETURN TO LOBBY", new Color(0.18f, 0.65f, 0.3f));
            lobby.gameObject.AddComponent<LayoutElement>().preferredHeight = 90f;

            var serialized = new SerializedObject(root.GetComponent<UI_StageResultPanel>());
            serialized.FindProperty("m_TitleText").objectReferenceValue = title;
            serialized.FindProperty("m_CurrentCallText").objectReferenceValue = current;
            serialized.FindProperty("m_TargetCallText").objectReferenceValue = target;
            serialized.FindProperty("m_ReturnToLobbyButton").objectReferenceValue = lobby;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ResultPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void BindScene(string scenePath, GameObject resultPrefab)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            BettingPhase bettingPhase = Object.FindAnyObjectByType<BettingPhase>(FindObjectsInactive.Include);
            GameObject bettingUi = GameObject.Find("BettingPhaseUI");
            if (bettingPhase != null && bettingUi != null)
            {
                BindBettingPhase(bettingPhase, bettingUi.transform);
            }

            UI_StageResultPanel existing = Object.FindAnyObjectByType<UI_StageResultPanel>(FindObjectsInactive.Include);
            if (existing == null)
            {
                UI_Root uiRoot = Object.FindAnyObjectByType<UI_Root>(FindObjectsInactive.Include);
                if (uiRoot != null)
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(resultPrefab, uiRoot.transform);
                    instance.name = "UI_StageResultPanel";
                    instance.SetActive(false);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void BindBettingPhase(BettingPhase phase, Transform uiRoot)
        {
            Transform controls = uiRoot.Find("BettingControls");
            if (controls == null) return;

            var serialized = new SerializedObject(phase);
            SetObject(serialized, "m_BettingCanvasGroup", uiRoot.GetComponent<CanvasGroup>());
            SetObject(serialized, "m_WagerSlider", controls.Find("WagerSlider")?.GetComponent<Slider>());
            SetObject(serialized, "m_CurrentCallText", FindComponent<TMP_Text>(controls, "Summary/CurrentCallText"));
            SetObject(serialized, "m_WagerCallText", FindComponent<TMP_Text>(controls, "Summary/WagerCallText"));
            SetObject(serialized, "m_MultiplierText", FindComponent<TMP_Text>(controls, "Summary/MultiplierText"));
            SetObject(serialized, "m_EstimatedPayoutText", FindComponent<TMP_Text>(controls, "Summary/EstimatedPayoutText"));
            SetObject(serialized, "m_FactionBetRoot", controls.Find("FactionBetRoot")?.gameObject);
            SetObject(serialized, "m_RedButton", FindComponent<Button>(controls, "FactionBetRoot/RedButton"));
            SetObject(serialized, "m_BlueButton", FindComponent<Button>(controls, "FactionBetRoot/BlueButton"));
            SetObject(serialized, "m_DrawButton", FindComponent<Button>(controls, "FactionBetRoot/DrawButton"));
            SetObject(serialized, "m_ClearFactionButton", FindComponent<Button>(controls, "FactionBetRoot/ClearFactionButton"));
            SetObject(serialized, "m_RemainingTimeRoot", controls.Find("RemainingTimeRoot")?.gameObject);
            SetArray(serialized, "m_RemainingTimeButtons", FindButtons(controls.Find("RemainingTimeRoot"), "Time", 5));
            SetObject(serialized, "m_SurvivingSlotsRoot", controls.Find("SurvivingSlotsRoot")?.gameObject);
            SetArray(serialized, "m_SurvivingSlotButtons", FindButtons(controls.Find("SurvivingSlotsRoot"), "Survivor", 6));
            SetObject(serialized, "m_OddEvenRoot", controls.Find("OddEvenRoot")?.gameObject);
            SetArray(serialized, "m_OddEvenButtons", FindButtons(controls.Find("OddEvenRoot"), "OddEven", 2));
            SetObject(serialized, "m_FirstEliminatedSlotRoot", controls.Find("FirstEliminatedSlotRoot")?.gameObject);
            SetArray(serialized, "m_FirstEliminatedSlotButtons", FindButtons(controls.Find("FirstEliminatedSlotRoot"), "FirstOut", 6));
            SetObject(serialized, "m_ValidationText", controls.Find("ValidationText")?.GetComponent<TMP_Text>());
            SetObject(serialized, "m_ConfirmBetButton", controls.Find("ConfirmBetButton")?.GetComponent<Button>());
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObject(SerializedObject serialized, string propertyName, Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null) property.objectReferenceValue = value;
        }

        private static void SetArray(SerializedObject serialized, string propertyName, IReadOnlyList<Button> buttons)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) return;
            property.arraySize = buttons.Count;
            for (int i = 0; i < buttons.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
        }

        private static T FindComponent<T>(Transform root, string path) where T : Component
        {
            return root.Find(path)?.GetComponent<T>();
        }

        private static Button[] FindButtons(Transform root, string prefix, int count)
        {
            var buttons = new Button[count];
            for (int i = 0; i < count; i++)
                buttons[i] = root?.Find($"{prefix}{i + 1}")?.GetComponent<Button>();
            return buttons;
        }

        private static Button[] CreateButtons(Transform parent, IReadOnlyList<string> labels, string prefix)
        {
            var result = new Button[labels.Count];
            for (int i = 0; i < labels.Count; i++)
                result[i] = CreateButton($"{prefix}{i + 1}", parent, labels[i], new Color(0.2f, 0.24f, 0.3f));
            return result;
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static GameObject CreateRow(string name, Transform parent, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;
            go.GetComponent<LayoutElement>().preferredHeight = height;
            return go;
        }

        private static TMP_Text CreateText(string name, Transform parent, string value)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 28f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            TMP_Text text = CreateText("Label", go.transform, label);
            text.fontSize = 22f;
            Stretch((RectTransform)text.transform);
            return button;
        }

        private static Slider CreateSlider(string name, Transform parent)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
            root.transform.SetParent(parent, false);
            root.GetComponent<LayoutElement>().preferredHeight = 48f;

            GameObject background = CreatePanel("Background", root.transform, new Color(0.15f, 0.16f, 0.2f));
            Stretch((RectTransform)background.transform, 0f, 0f, 0f, 0f);
            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            Stretch((RectTransform)fillArea.transform, 8f, 8f, 8f, 8f);
            GameObject fill = CreatePanel("Fill", fillArea.transform, new Color(0.2f, 0.8f, 0.35f));
            Stretch((RectTransform)fill.transform);
            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(root.transform, false);
            Stretch((RectTransform)handleArea.transform, 8f, 8f, 8f, 8f);
            GameObject handle = CreatePanel("Handle", handleArea.transform, Color.white);
            RectTransform handleRect = (RectTransform)handle.transform;
            handleRect.sizeDelta = new Vector2(28f, 44f);

            Slider slider = root.GetComponent<Slider>();
            slider.fillRect = (RectTransform)fill.transform;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private static void Stretch(RectTransform rect, float left = 0f, float right = 0f, float bottom = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
#endif
