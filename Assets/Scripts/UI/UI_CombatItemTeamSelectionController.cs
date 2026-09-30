#if UNITY_6000_0_OR_NEWER
using System;
using InTheArena.MainGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    /// <summary>무승부 베팅에서 실제 효과를 받을 팀만 선택합니다.</summary>
    public sealed class UI_CombatItemTeamSelectionController : MonoBehaviour
    {
        [SerializeField] private TMP_Text m_Title;
        [SerializeField] private Button m_RedButton;
        [SerializeField] private Button m_BlueButton;
        [SerializeField] private Button m_CancelButton;
        [SerializeField] private Button m_Backdrop;
        private CombatPhase m_CombatPhase;
        private bool m_ButtonsBound;

        public bool IsSelecting { get; private set; }
        public event Action<Team> TeamConfirmed;
        public event Action SelectionCanceled;

        public bool BeginSelection(ItemType itemType, CombatPhase combatPhase)
        {
            if (IsSelecting || combatPhase == null ||
                (itemType != ItemType.Meteor && itemType != ItemType.Mercenary) ||
                !combatPhase.CanCommitGroundTargetItem() || !combatPhase.BeginItemCastingSlowMotion())
                return false;

            m_CombatPhase = combatPhase;
            IsSelecting = true;
            if (m_Title != null)
                m_Title.text = itemType == ItemType.Meteor ? "기절시킬 팀" : "용병을 투입할 팀";
            BindButtons();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            return true;
        }

        public void ChooseTeam(Team team)
        {
            if (!IsSelecting) return;
            if (m_CombatPhase == null || !m_CombatPhase.CanUseTeamItem(team))
            {
                CancelSelection();
                return;
            }
            FinishSelection();
            TeamConfirmed?.Invoke(team);
        }

        public void CancelSelection()
        {
            if (!IsSelecting) return;
            FinishSelection();
            SelectionCanceled?.Invoke();
        }

        public void AbortSelection() => FinishSelection();

        private void FinishSelection()
        {
            IsSelecting = false;
            CombatPhase phase = m_CombatPhase;
            m_CombatPhase = null;
            phase?.EndItemCastingSlowMotion();
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (IsSelecting && (m_CombatPhase == null || !m_CombatPhase.CanCommitGroundTargetItem()))
                CancelSelection();
        }

        private void OnDisable()
        {
            if (IsSelecting) CancelSelection();
        }

        private void OnDestroy()
        {
            if (IsSelecting) CancelSelection();
        }

        private void BindButtons()
        {
            if (m_ButtonsBound) return;
            if (m_RedButton != null) m_RedButton.onClick.AddListener(() => ChooseTeam(Team.Red));
            if (m_BlueButton != null) m_BlueButton.onClick.AddListener(() => ChooseTeam(Team.Blue));
            if (m_CancelButton != null) m_CancelButton.onClick.AddListener(CancelSelection);
            if (m_Backdrop != null) m_Backdrop.onClick.AddListener(CancelSelection);
            m_ButtonsBound = true;
        }

        public static UI_CombatItemTeamSelectionController Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = new GameObject("CombatItemTeamSelection", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image), typeof(Button));
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            root.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            panel.layer = root.layer;
            SetRect((RectTransform)panel.transform, Vector2.zero, new Vector2(660f, 320f));
            panel.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f);
            var controller = root.AddComponent<UI_CombatItemTeamSelectionController>();
            controller.m_Backdrop = root.GetComponent<Button>();
            controller.m_Title = CreateText(panel.transform, "Title", "아이템 적용 팀", new Vector2(0f, 100f), new Vector2(610f, 60f), font, 36f);
            controller.m_RedButton = CreateButton(panel.transform, "Red", "레드", new Vector2(-145f, 0f), new Vector2(240f, 80f), new Color(0.65f, 0.15f, 0.15f), font);
            controller.m_BlueButton = CreateButton(panel.transform, "Blue", "블루", new Vector2(145f, 0f), new Vector2(240f, 80f), new Color(0.15f, 0.3f, 0.7f), font);
            controller.m_CancelButton = CreateButton(panel.transform, "Cancel", "취소", new Vector2(0f, -105f), new Vector2(220f, 56f), new Color(0.3f, 0.32f, 0.36f), font);
            return controller;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Color color, TMP_FontAsset font)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            root.layer = parent.gameObject.layer;
            SetRect((RectTransform)root.transform, position, size);
            root.GetComponent<Image>().color = color;
            Button button = root.GetComponent<Button>();
            button.targetGraphic = root.GetComponent<Image>();
            CreateText(root.transform, "Label", label, Vector2.zero, size, font, 30f);
            return button;
        }

        private static TMP_Text CreateText(Transform parent, string name, string text, Vector2 position, Vector2 size, TMP_FontAsset font, float fontSize)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            root.transform.SetParent(parent, false);
            root.layer = parent.gameObject.layer;
            SetRect((RectTransform)root.transform, position, size);
            TMP_Text label = root.GetComponent<TMP_Text>();
            label.font = font != null ? font : TMP_Settings.defaultFontAsset;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
#endif
