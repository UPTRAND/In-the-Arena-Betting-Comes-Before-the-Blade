#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using InTheArena.Quests;
using InTheArena.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.MainGame.Editor
{
    /// <summary>
    /// 퀘스트 콘텐츠 에셋과 위치를 직접 편집할 수 있는 uGUI 프리팹을 재현 가능하게 생성합니다.
    /// </summary>
    public static class QuestMailUiBuilder
    {
        private const string QuestResourceFolder = "Assets/Resources/Quest";
        private const string UiResourceFolder = "Assets/Resources/UI/Quest";
        private const string FontPath = "Assets/Font/Galmuri9 SDF.asset";

        private static TMP_FontAsset s_Font;

        [MenuItem("Tools/In The Arena/Rebuild Quest and Mail System Assets")]
        /// <summary>
        /// 확정 콘텐츠 에셋과 편집 가능한 퀘스트·우편 프리팹을 다시 생성합니다.
        /// </summary>
        public static void Rebuild()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder(QuestResourceFolder);
            EnsureFolder("Assets/Resources/UI");
            EnsureFolder(UiResourceFolder);

            s_Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (s_Font == null)
            {
                s_Font = TMP_Settings.defaultFontAsset;
            }

            BuildQuestContentAssets();

            QuestSpeechBubbleView bubblePrefab = BuildSpeechBubblePrefab();
            QuestListRowView questRowPrefab = BuildQuestRowPrefab();
            MailListRowView mailRowPrefab = BuildMailRowPrefab();

            BuildQuestInfoPopup();
            BuildQuestListPopup(questRowPrefab);
            BuildMailboxPopup(mailRowPrefab);
            BuildLobbyHud(bubblePrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[QuestMailUiBuilder] 퀘스트·우편 콘텐츠와 편집 가능한 uGUI 프리팹 생성을 완료했습니다.");
        }

        /// <summary>일일 3종·긴급 2종 정의와 카탈로그 에셋을 생성합니다.</summary>
        private static void BuildQuestContentAssets()
        {
            List<QuestDefinition> daily = new List<QuestDefinition>();
            List<QuestDefinition> emergency = new List<QuestDefinition>();

            daily.Add(CreateOrUpdateDefinition(
                "daily_double_bet",
                QuestKind.Daily,
                "이중 배팅",
                "{parameter} 팀에 베팅하여 {target}회 적중",
                QuestObjectiveKind.DoubleBet,
                new[] { 3, 5, 7 },
                1800));
            daily.Add(CreateOrUpdateDefinition(
                "daily_big_bettor",
                QuestKind.Daily,
                "큰손 배팅",
                "확정 베팅액 {target} Col 누적",
                QuestObjectiveKind.BigBettor,
                new[] { 3000, 4000, 5000 },
                1800));
            daily.Add(CreateOrUpdateDefinition(
                "daily_precise_bet",
                QuestKind.Daily,
                "정밀 배팅",
                "{parameter} 베팅 {target}회 적중",
                QuestObjectiveKind.PreciseBet,
                new[] { 3, 5, 7 },
                1800));

            emergency.Add(CreateOrUpdateDefinition(
                "emergency_raise_stakes",
                QuestKind.Emergency,
                "판돈 올리기",
                "스테이지 {target}회 연속 클리어",
                QuestObjectiveKind.ConsecutiveStageClear,
                new[] { 3, 4, 5 },
                1800));
            emergency.Add(CreateOrUpdateDefinition(
                "emergency_wager",
                QuestKind.Emergency,
                "내기",
                "승리 정산 지급액 {target} Col 획득",
                QuestObjectiveKind.WinningPayout,
                new[] { 3500, 4500, 5500 },
                1800));

            string catalogPath = QuestResourceFolder + "/QuestCatalog.asset";
            QuestCatalog catalog = AssetDatabase.LoadAssetAtPath<QuestCatalog>(catalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<QuestCatalog>();
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }

            catalog.ConfigureForEditor(daily, emergency);
            EditorUtility.SetDirty(catalog);
        }

        private static QuestDefinition CreateOrUpdateDefinition(
            string definitionId,
            QuestKind kind,
            string title,
            string description,
            QuestObjectiveKind objective,
            int[] targets,
            int durationSeconds)
        {
            string path = QuestResourceFolder + "/" + definitionId + ".asset";
            QuestDefinition definition = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<QuestDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            definition.ConfigureForEditor(
                definitionId,
                kind,
                title,
                description,
                objective,
                targets,
                durationSeconds);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        /// <summary>NPC 머리 위에 표시할 임시 말풍선 프리팹을 생성합니다.</summary>
        private static QuestSpeechBubbleView BuildSpeechBubblePrefab()
        {
            GameObject root = CreateUiObject("QuestSpeechBubble", null, typeof(Image), typeof(Button), typeof(QuestSpeechBubbleView));
            RectTransform rect = root.GetComponent<RectTransform>();
            SetFixed(rect, new Vector2(150f, 64f));

            Image background = root.GetComponent<Image>();
            background.color = new Color(0.20f, 0.52f, 0.84f, 0.96f);
            Button button = root.GetComponent<Button>();
            button.targetGraphic = background;

            TMP_Text label = CreateText("Label", rect, "퀘스트", 24f, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 8f, 8f, 5f, 5f);

            QuestSpeechBubbleView view = root.GetComponent<QuestSpeechBubbleView>();
            SetReference(view, "m_Button", button);
            SetReference(view, "m_Label", label);
            SetReference(view, "m_Background", background);

            return SavePrefab(root, UiResourceFolder + "/QuestSpeechBubble.prefab")
                .GetComponent<QuestSpeechBubbleView>();
        }

        /// <summary>퀘스트 목록에서 재사용할 행 프리팹을 생성합니다.</summary>
        private static QuestListRowView BuildQuestRowPrefab()
        {
            GameObject root = CreateUiObject("QuestListRow", null, typeof(Image), typeof(Button), typeof(LayoutElement), typeof(QuestListRowView));
            RectTransform rect = root.GetComponent<RectTransform>();
            SetFixed(rect, new Vector2(800f, 112f));

            Image background = root.GetComponent<Image>();
            background.color = new Color(0.13f, 0.16f, 0.22f, 0.96f);
            Button button = root.GetComponent<Button>();
            button.targetGraphic = background;
            root.GetComponent<LayoutElement>().preferredHeight = 112f;

            TMP_Text title = CreateText("Title", rect, "[일일] 퀘스트 · 1등급", 26f, TextAlignmentOptions.MidlineLeft);
            SetAnchored(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 24f), new Vector2(450f, 44f));
            TMP_Text progress = CreateText("Progress", rect, "0 / 3", 23f, TextAlignmentOptions.Center);
            SetAnchored(progress.rectTransform, new Vector2(0.63f, 0.5f), new Vector2(0.63f, 0.5f), Vector2.zero, new Vector2(180f, 44f));
            TMP_Text status = CreateText("Status", rect, "진행 중", 21f, TextAlignmentOptions.Center);
            SetAnchored(status.rectTransform, new Vector2(0.88f, 0.5f), new Vector2(0.88f, 0.5f), Vector2.zero, new Vector2(180f, 44f));

            QuestListRowView view = root.GetComponent<QuestListRowView>();
            SetReference(view, "m_Button", button);
            SetReference(view, "m_TitleText", title);
            SetReference(view, "m_ProgressText", progress);
            SetReference(view, "m_StatusText", status);

            return SavePrefab(root, UiResourceFolder + "/QuestListRow.prefab").GetComponent<QuestListRowView>();
        }

        /// <summary>우편함에서 재사용할 행 프리팹을 생성합니다.</summary>
        private static MailListRowView BuildMailRowPrefab()
        {
            GameObject root = CreateUiObject("MailListRow", null, typeof(Image), typeof(LayoutElement), typeof(MailListRowView));
            RectTransform rect = root.GetComponent<RectTransform>();
            SetFixed(rect, new Vector2(800f, 190f));
            root.GetComponent<Image>().color = new Color(0.14f, 0.16f, 0.20f, 0.97f);
            root.GetComponent<LayoutElement>().preferredHeight = 190f;

            TMP_Text title = CreateText("Title", rect, "우편 제목", 28f, TextAlignmentOptions.MidlineLeft);
            SetAnchored(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -18f), new Vector2(590f, 42f));
            title.rectTransform.pivot = new Vector2(0f, 1f);

            TMP_Text body = CreateText("Body", rect, "우편 내용", 20f, TextAlignmentOptions.TopLeft);
            SetAnchored(body.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -66f), new Vector2(590f, 64f));
            body.rectTransform.pivot = new Vector2(0f, 1f);
            body.textWrappingMode = TextWrappingModes.Normal;

            TMP_Text reward = CreateText("Reward", rect, "골드 300", 22f, TextAlignmentOptions.MidlineLeft);
            SetAnchored(reward.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 18f), new Vector2(560f, 42f));
            reward.rectTransform.pivot = new Vector2(0f, 0f);

            Button claim = CreateButton("ClaimButton", rect, "수령", new Color(0.73f, 0.47f, 0.16f, 1f), out TMP_Text claimText);
            SetAnchored(claim.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-96f, 0f), new Vector2(150f, 72f));

            MailListRowView view = root.GetComponent<MailListRowView>();
            SetReference(view, "m_TitleText", title);
            SetReference(view, "m_BodyText", body);
            SetReference(view, "m_RewardText", reward);
            SetReference(view, "m_ClaimButtonText", claimText);
            SetReference(view, "m_ClaimButton", claim);

            return SavePrefab(root, UiResourceFolder + "/MailListRow.prefab").GetComponent<MailListRowView>();
        }

        /// <summary>목표·진행·기한·보상과 완료 확인을 표시할 정보 프리팹을 생성합니다.</summary>
        private static void BuildQuestInfoPopup()
        {
            GameObject root = CreatePopupRoot("UI_QuestInfoPopup", typeof(UI_QuestInfoPopup), out RectTransform panel);
            UI_QuestInfoPopup popup = root.GetComponent<UI_QuestInfoPopup>();

            TMP_Text kind = CreateText("Kind", panel, "일일 퀘스트", 28f, TextAlignmentOptions.Center);
            PlaceTop(kind.rectTransform, -62f, 52f, 760f);
            TMP_Text title = CreateText("Title", panel, "퀘스트 · 1등급", 42f, TextAlignmentOptions.Center);
            PlaceTop(title.rectTransform, -125f, 78f, 760f);
            TMP_Text objective = CreateText("Objective", panel, "목표 설명", 30f, TextAlignmentOptions.Center);
            PlaceTop(objective.rectTransform, -260f, 140f, 740f);
            objective.textWrappingMode = TextWrappingModes.Normal;
            TMP_Text progress = CreateText("Progress", panel, "진행 0 / 3", 32f, TextAlignmentOptions.Center);
            PlaceTop(progress.rectTransform, -425f, 70f, 700f);
            TMP_Text reward = CreateText("Reward", panel, "보상: 영구 골드 300", 28f, TextAlignmentOptions.Center);
            PlaceTop(reward.rectTransform, -515f, 60f, 700f);
            TMP_Text remaining = CreateText("Remaining", panel, "남은 시간 23:59:59", 26f, TextAlignmentOptions.Center);
            PlaceTop(remaining.rectTransform, -600f, 56f, 700f);
            TMP_Text status = CreateText("Status", panel, "진행 중", 27f, TextAlignmentOptions.Center);
            PlaceTop(status.rectTransform, -680f, 64f, 700f);

            Button confirm = CreateButton("ConfirmButton", panel, "완료 확인", new Color(0.28f, 0.66f, 0.34f, 1f), out TMP_Text confirmText);
            SetAnchored(confirm.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 145f), new Vector2(500f, 92f));
            Button close = CreateButton("CloseButton", panel, "닫기", new Color(0.30f, 0.33f, 0.40f, 1f), out _);
            SetAnchored(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(500f, 72f));

            SetReference(popup, "m_KindText", kind);
            SetReference(popup, "m_TitleText", title);
            SetReference(popup, "m_ObjectiveText", objective);
            SetReference(popup, "m_ProgressText", progress);
            SetReference(popup, "m_RewardText", reward);
            SetReference(popup, "m_RemainingText", remaining);
            SetReference(popup, "m_StatusText", status);
            SetReference(popup, "m_ConfirmButtonText", confirmText);
            SetReference(popup, "m_ConfirmButton", confirm);
            SetReference(popup, "m_CloseButton", close);
            SetControlSettings(popup, true);

            root.SetActive(false);
            SavePrefab(root, UiResourceFolder + "/UI_QuestInfoPopup.prefab");
        }

        /// <summary>NPC 미배정 항목도 접근 가능한 목록 프리팹을 생성합니다.</summary>
        private static void BuildQuestListPopup(QuestListRowView rowPrefab)
        {
            GameObject root = CreatePopupRoot("UI_QuestListPopup", typeof(UI_QuestListPopup), out RectTransform panel);
            UI_QuestListPopup popup = root.GetComponent<UI_QuestListPopup>();

            TMP_Text title = CreateText("Title", panel, "퀘스트", 42f, TextAlignmentOptions.Center);
            PlaceTop(title.rectTransform, -42f, 70f, 760f);
            TMP_Text unassigned = CreateText("Unassigned", panel, "담당 NPC 없음: 0", 23f, TextAlignmentOptions.Center);
            PlaceTop(unassigned.rectTransform, -116f, 48f, 760f);

            RectTransform content = CreateScrollArea("QuestScroll", panel, out _);
            SetAnchored(content.parent as RectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(800f, 850f));
            TMP_Text empty = CreateText("Empty", panel, "표시할 퀘스트가 없습니다.", 28f, TextAlignmentOptions.Center);
            SetAnchored(empty.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 80f));

            Button close = CreateButton("CloseButton", panel, "닫기", new Color(0.30f, 0.33f, 0.40f, 1f), out _);
            SetAnchored(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 45f), new Vector2(480f, 74f));

            SetReference(popup, "m_ContentRoot", content);
            SetReference(popup, "m_RowPrefab", rowPrefab);
            SetReference(popup, "m_EmptyText", empty);
            SetReference(popup, "m_UnassignedText", unassigned);
            SetReference(popup, "m_CloseButton", close);
            SetControlSettings(popup, true);

            root.SetActive(false);
            SavePrefab(root, UiResourceFolder + "/UI_QuestListPopup.prefab");
        }

        /// <summary>개별 수령과 미발송 재시도를 제공할 우편함 프리팹을 생성합니다.</summary>
        private static void BuildMailboxPopup(MailListRowView rowPrefab)
        {
            GameObject root = CreatePopupRoot("UI_MailboxPopup", typeof(UI_MailboxPopup), out RectTransform panel);
            UI_MailboxPopup popup = root.GetComponent<UI_MailboxPopup>();

            TMP_Text title = CreateText("Title", panel, "우편함", 42f, TextAlignmentOptions.Center);
            PlaceTop(title.rectTransform, -42f, 70f, 760f);
            TMP_Text pending = CreateText("Pending", panel, "미발송 완료 보상: 0", 23f, TextAlignmentOptions.MidlineLeft);
            SetAnchored(pending.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(54f, -120f), new Vector2(500f, 50f));
            pending.rectTransform.pivot = new Vector2(0f, 1f);
            Button retry = CreateButton("RetryPendingButton", panel, "재시도", new Color(0.73f, 0.47f, 0.16f, 1f), out _);
            SetAnchored(retry.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-110f, -120f), new Vector2(170f, 58f));

            RectTransform content = CreateScrollArea("MailScroll", panel, out _);
            SetAnchored(content.parent as RectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -48f), new Vector2(800f, 820f));
            TMP_Text empty = CreateText("Empty", panel, "도착한 우편이 없습니다.", 28f, TextAlignmentOptions.Center);
            SetAnchored(empty.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 80f));

            Button close = CreateButton("CloseButton", panel, "닫기", new Color(0.30f, 0.33f, 0.40f, 1f), out _);
            SetAnchored(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 45f), new Vector2(480f, 74f));

            SetReference(popup, "m_ContentRoot", content);
            SetReference(popup, "m_RowPrefab", rowPrefab);
            SetReference(popup, "m_EmptyText", empty);
            SetReference(popup, "m_PendingText", pending);
            SetReference(popup, "m_RetryPendingButton", retry);
            SetReference(popup, "m_CloseButton", close);
            SetControlSettings(popup, true);

            root.SetActive(false);
            SavePrefab(root, UiResourceFolder + "/UI_MailboxPopup.prefab");
        }

        /// <summary>로비 진입 버튼·배지·말풍선 루트를 포함한 HUD 프리팹을 생성합니다.</summary>
        private static void BuildLobbyHud(QuestSpeechBubbleView bubblePrefab)
        {
            GameObject root = CreateUiObject("UI_QuestLobbyHud", null, typeof(CanvasGroup), typeof(UI_QuestLobbyHud));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect, 0f, 0f, 0f, 0f);

            RectTransform bubbles = CreateUiObject("BubbleRoot", rootRect).GetComponent<RectTransform>();
            Stretch(bubbles, 0f, 0f, 0f, 0f);
            CanvasGroup bubbleGroup = bubbles.gameObject.AddComponent<CanvasGroup>();
            bubbleGroup.ignoreParentGroups = false;

            // LobbyHeader의 설정 버튼 중심선에 맞추고 설정 버튼 아래로 우편, 퀘스트 순서로 배치합니다.
            RectTransform toolbar = CreateUiObject("Toolbar", rootRect, typeof(Image)).GetComponent<RectTransform>();
            SetAnchored(toolbar, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-111f, -286f), new Vector2(112f, 168f));
            Image toolbarImage = toolbar.gameObject.GetComponent<Image>();
            toolbarImage.color = Color.clear;
            toolbarImage.raycastTarget = false;

            Button mailbox = CreateButton("MailboxButton", toolbar, "우편", new Color(0.73f, 0.47f, 0.16f, 1f), out _);
            SetAnchored(mailbox.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 45f), new Vector2(112f, 78f));
            TMP_Text mailBadge = CreateBadge("MailBadge", mailbox.transform as RectTransform);

            Button quests = CreateButton("QuestButton", toolbar, "퀘스트", new Color(0.20f, 0.52f, 0.84f, 1f), out _);
            SetAnchored(quests.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -45f), new Vector2(112f, 78f));
            TMP_Text questBadge = CreateBadge("QuestBadge", quests.transform as RectTransform);

            UI_QuestLobbyHud hud = root.GetComponent<UI_QuestLobbyHud>();
            SetReference(hud, "m_QuestListButton", quests);
            SetReference(hud, "m_MailboxButton", mailbox);
            SetReference(hud, "m_QuestBadgeText", questBadge);
            SetReference(hud, "m_MailBadgeText", mailBadge);
            SetReference(hud, "m_BubbleRoot", bubbles);
            SetReference(hud, "m_BubblePrefab", bubblePrefab);
            SetControlSettings(hud, false);

            root.SetActive(false);
            SavePrefab(root, UiResourceFolder + "/UI_QuestLobbyHud.prefab");
        }

        /// <summary>공통 전체 화면 차단 영역과 중앙 패널을 가진 팝업 루트를 만듭니다.</summary>
        private static GameObject CreatePopupRoot(string name, Type componentType, out RectTransform panel)
        {
            GameObject root = CreateUiObject(name, null, typeof(CanvasGroup), componentType);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect, 0f, 0f, 0f, 0f);

            Image blocker = root.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0.70f);
            blocker.raycastTarget = true;

            panel = CreateUiObject("Panel", rootRect, typeof(Image)).GetComponent<RectTransform>();
            SetAnchored(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 1250f));
            panel.gameObject.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.14f, 0.98f);
            return root;
        }

        /// <summary>세로 목록용 마스크·뷰포트·콘텐츠 계층을 만듭니다.</summary>
        private static RectTransform CreateScrollArea(string name, RectTransform parent, out ScrollRect scrollRect)
        {
            RectTransform viewport = CreateUiObject(name, parent, typeof(Image), typeof(RectMask2D), typeof(ScrollRect)).GetComponent<RectTransform>();
            viewport.gameObject.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.07f, 0.82f);

            RectTransform content = CreateUiObject("Content", viewport, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect = viewport.GetComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            return content;
        }

        /// <summary>임시 단색 이미지와 TMP 라벨을 가진 버튼을 만듭니다.</summary>
        private static Button CreateButton(string name, Transform parent, string label, Color color, out TMP_Text labelText)
        {
            GameObject gameObject = CreateUiObject(name, parent, typeof(Image), typeof(Button));
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            Button button = gameObject.GetComponent<Button>();
            button.targetGraphic = image;

            labelText = CreateText("Label", gameObject.transform as RectTransform, label, 24f, TextAlignmentOptions.Center);
            Stretch(labelText.rectTransform, 8f, 8f, 6f, 6f);
            return button;
        }

        /// <summary>버튼 위에 배치할 원형 수량 배지를 만듭니다.</summary>
        private static TMP_Text CreateBadge(string name, RectTransform parent)
        {
            RectTransform backgroundRect = CreateUiObject(name, parent, typeof(Image)).GetComponent<RectTransform>();
            Image background = backgroundRect.GetComponent<Image>();
            background.color = new Color(0.88f, 0.18f, 0.16f, 1f);
            SetAnchored(backgroundRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(34f, 34f));

            TMP_Text badgeText = CreateText("Value", backgroundRect, "0", 18f, TextAlignmentOptions.Center);
            Stretch(badgeText.rectTransform, 0f, 0f, 0f, 0f);
            return badgeText;
        }

        /// <summary>프로젝트 기본 폰트를 적용한 TMP 텍스트 오브젝트를 만듭니다.</summary>
        private static TMP_Text CreateText(string name, Transform parent, string value, float fontSize, TextAlignmentOptions alignment)
        {
            GameObject gameObject = CreateUiObject(name, parent, typeof(TextMeshProUGUI));
            TMP_Text text = gameObject.GetComponent<TMP_Text>();
            text.font = s_Font;
            text.fontSize = fontSize;
            text.text = value;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        /// <summary>RectTransform과 요청 컴포넌트를 가진 UI 오브젝트를 만듭니다.</summary>
        private static GameObject CreateUiObject(string name, Transform parent, params Type[] components)
        {
            List<Type> types = new List<Type> { typeof(RectTransform) };
            if (components != null)
            {
                types.AddRange(components);
            }

            GameObject gameObject = new GameObject(name, types.ToArray());
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
                gameObject.layer = parent.gameObject.layer;
            }
            return gameObject;
        }

        /// <summary>임시 계층을 지정 경로의 프리팹으로 저장하고 씬 오브젝트를 정리합니다.</summary>
        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>UI_Base의 제어 스택 사용 여부를 직렬화 프로퍼티에 설정합니다.</summary>
        private static void SetControlSettings(UI_Base control, bool hasControl)
        {
            SerializedObject serialized = new SerializedObject(control);
            serialized.FindProperty("m_HasControl").boolValue = hasControl;
            serialized.FindProperty("m_CanCloseControlWithBackButton").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>프리팹 컴포넌트의 직렬화 참조를 이름으로 연결합니다.</summary>
        private static void SetReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Serialized property was not found: {target.GetType().Name}.{propertyName}");
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Assets 아래의 중첩 폴더가 없으면 순서대로 생성합니다.</summary>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        /// <summary>RectTransform을 부모에 늘리고 네 방향 여백을 설정합니다.</summary>
        private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>RectTransform의 앵커를 중앙에 두고 고정 크기를 설정합니다.</summary>
        private static void SetFixed(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }

        /// <summary>앵커, 위치와 크기를 명시적으로 설정합니다.</summary>
        private static void SetAnchored(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>패널 상단 기준으로 지정한 Y 위치와 크기에 요소를 배치합니다.</summary>
        private static void PlaceTop(RectTransform rect, float y, float height, float width)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
#endif
