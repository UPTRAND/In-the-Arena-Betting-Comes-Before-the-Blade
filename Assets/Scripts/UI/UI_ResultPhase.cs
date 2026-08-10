#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using DG.Tweening;
using InTheArena.MainGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    [DisallowMultipleComponent]
    public sealed class UI_ResultPhase : UI_Base
    {
        [Header("Result Summary")]
        [SerializeField] private RectTransform m_MyBet;
        [SerializeField] private TMP_Text m_BetText;
        [SerializeField] private RectTransform m_MyOdds;
        [SerializeField] private TMP_Text m_OddsText;
        [SerializeField] private RectTransform m_MyResult;
        [SerializeField] private TMP_Text m_ResultText;
        [SerializeField] private Button m_ContinueButton;

        [Header("Betting Result List")]
        [SerializeField] private RectTransform m_BettingsGroup;
        [SerializeField] private GameObject m_BettingBoxTemplate;

        private readonly List<BetEntry> m_Entries = new List<BetEntry>(4);
        private readonly List<BettingBoxView> m_BettingBoxes = new List<BettingBoxView>(4);

        private RoundBetTicket m_Ticket;
        private CombatResultSnapshot m_CombatResult;
        private BetSettlement m_Settlement;
        private Sequence m_ResultSequence;
        private Sequence m_FinalResultSequence;
        private Vector2 m_MyBetStartPosition;
        private Vector2 m_MyOddsStartPosition;
        private bool m_HasSummaryStartPositions;
        private bool m_ResultSfxPlayed;

        public event Action ContinueClicked;
        public event Action PayoutRevealCompleted;

        public RectTransform MyResultTransform => m_MyResult;

        protected override void Awake()
        {
            base.Awake();
            ResolveReferences();
            CacheSummaryStartPositions();
            if (m_ContinueButton != null)
                m_ContinueButton.onClick.AddListener(OnContinueButtonClicked);
        }

        public void Configure(RoundBetTicket ticket, CombatResultSnapshot combatResult, BetSettlement settlement)
        {
            CancelResultAnimation();
            m_Ticket = ticket;
            m_CombatResult = combatResult;
            m_Settlement = settlement;
            m_ResultSfxPlayed = false;
            if (m_ContinueButton != null)
                m_ContinueButton.interactable = true;
        }

        public void Refresh()
        {
            ResolveReferences();
            CacheSummaryStartPositions();
            CancelResultAnimation();
            BuildEntries();
            BuildBettingBoxes();
            ResetSummaryDisplay();
        }

        public void PlayResultAnimation()
        {
            CancelResultAnimation();
            if (m_Entries.Count == 0)
            {
                ShowFinalResult();
                return;
            }

            bool oddsStopped = false;
            m_ResultSequence = DOTween.Sequence().SetTarget(this).SetUpdate(true);
            for (int i = 0; i < m_BettingBoxes.Count; i++)
            {
                BettingBoxView box = m_BettingBoxes[i];
                BetEntry entry = m_Entries[i];
                bool updateOdds = !oddsStopped;
                int displayedOdds = 0;
                if (updateOdds)
                {
                    if (entry.IsCorrect)
                        displayedOdds = 1 << (i + 1);
                    else
                        oddsStopped = true;
                }

                m_ResultSequence.AppendInterval(i == 0 ? 0.08f : 0.12f);
                m_ResultSequence.AppendCallback(() =>
                {
                    box.Reveal();
                    if (updateOdds) UpdateOdds(displayedOdds);
                });
                m_ResultSequence.Append(box.PlayRevealTween());
                if (updateOdds && m_MyOdds != null)
                    m_ResultSequence.Append(m_MyOdds.DOPunchScale(Vector3.one * 0.12f, 0.16f, 4, 0.65f));
            }

            m_ResultSequence.AppendInterval(0.35f);
            m_ResultSequence.Append(BuildSummaryExitTween());
            m_ResultSequence.AppendInterval(0.08f);
            m_ResultSequence.AppendCallback(ShowFinalResult);
        }

        public void CancelResultAnimation()
        {
            KillSequence(ref m_ResultSequence);
            KillSequence(ref m_FinalResultSequence);
            foreach (BettingBoxView box in m_BettingBoxes)
                box.KillTween();
            m_MyBet?.DOKill();
            m_MyOdds?.DOKill();
            m_MyResult?.DOKill();
            m_ResultText?.DOKill();
        }

        private void BuildEntries()
        {
            m_Entries.Clear();
            if (m_Ticket == null || m_CombatResult == null) return;

            if (m_Ticket.Faction != FactionPrediction.NotSelected)
                m_Entries.Add(new BetEntry($"\uC2B9\uB9AC\uD560 \uD300 \u00B7 {FormatFaction(m_Ticket.Faction)}", MatchesFaction(m_Ticket.Faction, m_CombatResult.Winner)));

            if (m_Ticket.RemainingTime.HasValue)
                m_Entries.Add(new BetEntry($"\uC885\uB8CC \uC2DC\uAC04 \u00B7 {FormatRemainingTime(m_Ticket.RemainingTime.Value)}", m_Ticket.RemainingTime.Value == BetSettlementService.ClassifyRemainingTime(m_CombatResult.RemainingTime)));

            if (m_Ticket.OddEven.HasValue)
            {
                bool isEven = m_CombatResult.TotalAliveCount % 2 == 0;
                bool correct = m_Ticket.OddEven.Value == (isEven ? OddEvenPrediction.Even : OddEvenPrediction.Odd);
                m_Entries.Add(new BetEntry($"\uD640\uC9DD \u00B7 {FormatOddEven(m_Ticket.OddEven.Value)}", correct));
            }

            if (m_Ticket.FirstEliminatedColumn.HasValue)
                m_Entries.Add(new BetEntry($"\uCCAB \uC804\uBA78 \uC5F4 \u00B7 {FormatFirstEliminatedColumn(m_Ticket.FirstEliminatedColumn.Value)}", m_Ticket.FirstEliminatedColumn == m_CombatResult.FirstEliminatedColumn));

            if (m_Ticket.SurvivingRow.HasValue)
                m_Entries.Add(new BetEntry($"\uB9C8\uC9C0\uB9C9 \uC0DD\uC874 \uD589 \u00B7 {FormatSurvivingRow(m_Ticket.SurvivingRow.Value)}", m_CombatResult.SurvivingRows.Contains(m_Ticket.SurvivingRow.Value)));

            if (m_Entries.Count > 4)
                m_Entries.RemoveRange(4, m_Entries.Count - 4);
        }

        private void BuildBettingBoxes()
        {
            ClearBettingBoxes();
            if (m_BettingsGroup == null || m_BettingBoxTemplate == null) return;

            m_BettingBoxTemplate.SetActive(false);
            foreach (BetEntry entry in m_Entries)
            {
                GameObject boxObject = Instantiate(m_BettingBoxTemplate, m_BettingsGroup);
                boxObject.name = "Betting_Box_Result";
                var box = new BettingBoxView(boxObject, entry);
                box.Initialize();
                m_BettingBoxes.Add(box);
            }
        }

        private void ClearBettingBoxes()
        {
            foreach (BettingBoxView box in m_BettingBoxes)
            {
                box.KillTween();
                if (box.Root != null) Destroy(box.Root);
            }
            m_BettingBoxes.Clear();
        }

        private void ResetSummaryDisplay()
        {
            if (m_BetText != null) m_BetText.text = $"{m_Ticket?.WagerCall ?? 0} Col";
            UpdateOdds(0);
            if (m_ResultText != null) m_ResultText.text = "0 Col";

            ResetSummaryTransform(m_MyBet, m_MyBetStartPosition);
            ResetSummaryTransform(m_MyOdds, m_MyOddsStartPosition);
            if (m_MyResult != null)
            {
                m_MyResult.gameObject.SetActive(false);
                m_MyResult.localScale = Vector3.one;
                GetCanvasGroup(m_MyResult).alpha = 1f;
            }
        }

        private void UpdateOdds(int multiplier)
        {
            if (m_OddsText != null) m_OddsText.text = $"\u00D7{multiplier}";
        }

        private Tween BuildSummaryExitTween()
        {
            if (m_MyBet == null || m_MyOdds == null) return DOTween.Sequence();
            Sequence sequence = DOTween.Sequence();
            sequence.Join(m_MyBet.DOAnchorPos(Vector2.zero, 0.28f).SetEase(Ease.InOutQuad));
            sequence.Join(m_MyOdds.DOAnchorPos(Vector2.zero, 0.28f).SetEase(Ease.InOutQuad));
            sequence.Join(m_MyBet.DOScale(0.82f, 0.28f).SetEase(Ease.InBack));
            sequence.Join(m_MyOdds.DOScale(0.82f, 0.28f).SetEase(Ease.InBack));
            sequence.Join(GetCanvasGroup(m_MyBet).DOFade(0f, 0.22f));
            sequence.Join(GetCanvasGroup(m_MyOdds).DOFade(0f, 0.22f));
            return sequence;
        }

        private void ShowFinalResult()
        {
            if (m_MyResult == null) return;
            if (!m_ResultSfxPlayed && m_Settlement != null)
            {
                m_ResultSfxPlayed = true;
                SoundManager.Instance?.PlaySfx(
                    m_Settlement.IsWin ? SfxIds.BettingWin : SfxIds.BettingFail);
            }

            int payout = Mathf.Max(0, m_Settlement?.PayoutCall ?? 0);
            m_MyResult.gameObject.SetActive(true);
            m_MyResult.localScale = Vector3.one * 0.82f;
            CanvasGroup group = GetCanvasGroup(m_MyResult);
            group.alpha = 0f;

            KillSequence(ref m_FinalResultSequence);
            m_FinalResultSequence = DOTween.Sequence().SetTarget(m_MyResult).SetUpdate(true);
            m_FinalResultSequence.Join(group.DOFade(1f, 0.2f));
            m_FinalResultSequence.Join(m_MyResult.DOScale(1f, 0.32f).SetEase(Ease.OutBack));
            m_FinalResultSequence.Join(DOTween.To(() => 0, value =>
            {
                if (m_ResultText != null) m_ResultText.text = $"{value} Col";
            }, payout, payout > 0 ? 0.65f : 0.15f).SetEase(Ease.OutCubic));
            m_FinalResultSequence.OnComplete(() => PayoutRevealCompleted?.Invoke());
        }

        private void ResolveReferences()
        {
            m_MyBet ??= FindDescendant(transform, "MyBet") as RectTransform;
            m_BetText ??= FindDescendant(transform, "Bet_text")?.GetComponent<TMP_Text>();
            m_MyOdds ??= FindDescendant(transform, "MyOdds") as RectTransform;
            m_OddsText ??= FindDescendant(transform, "Odds_Text")?.GetComponent<TMP_Text>();
            m_MyResult ??= FindDescendant(transform, "MyResult") as RectTransform;
            m_ResultText ??= FindDescendant(transform, "Result_Text")?.GetComponent<TMP_Text>();
            m_BettingsGroup ??= FindDescendant(transform, "Bettins_Group") as RectTransform;
            m_BettingBoxTemplate ??= FindDescendant(m_BettingsGroup, "Betting_Box")?.gameObject;
            m_ContinueButton ??= FindButton("Btn_Next") ?? FindFirstButton(transform);
        }

        private void CacheSummaryStartPositions()
        {
            if (m_HasSummaryStartPositions || m_MyBet == null || m_MyOdds == null) return;
            m_MyBetStartPosition = m_MyBet.anchoredPosition;
            m_MyOddsStartPosition = m_MyOdds.anchoredPosition;
            m_HasSummaryStartPositions = true;
        }

        private static void ResetSummaryTransform(RectTransform target, Vector2 position)
        {
            if (target == null) return;
            target.gameObject.SetActive(true);
            target.anchoredPosition = position;
            target.localScale = Vector3.one;
            GetCanvasGroup(target).alpha = 1f;
        }

        private Button FindButton(string objectName)
        {
            Transform target = FindDescendant(transform, objectName);
            return target != null ? target.GetComponent<Button>() : null;
        }

        private static Button FindFirstButton(Transform root) => root == null ? null : root.GetComponentInChildren<Button>(true);

        private static CanvasGroup GetCanvasGroup(Component target)
        {
            CanvasGroup group = target.GetComponent<CanvasGroup>();
            return group != null ? group : target.gameObject.AddComponent<CanvasGroup>();
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDescendant(root.GetChild(i), objectName);
                if (found != null) return found;
            }
            return null;
        }

        private static void KillSequence(ref Sequence sequence)
        {
            if (sequence != null && sequence.IsActive()) sequence.Kill(false);
            sequence = null;
        }

        private static bool MatchesFaction(FactionPrediction prediction, Team winner) => prediction switch
        {
            FactionPrediction.Red => winner == Team.Red,
            FactionPrediction.Blue => winner == Team.Blue,
            FactionPrediction.Draw => winner == Team.None,
            _ => false
        };

        private static string FormatFaction(FactionPrediction prediction) => prediction switch
        {
            FactionPrediction.Red => "\uB808\uB4DC",
            FactionPrediction.Blue => "\uBE14\uB8E8",
            FactionPrediction.Draw => "\uBB34\uC2B9\uBD80",
            _ => "\uBBF8\uC120\uD0DD"
        };

        private static string FormatRemainingTime(RemainingTimePrediction prediction) => prediction switch
        {
            RemainingTimePrediction.Seconds0To5 => "0~5\uCD08",
            RemainingTimePrediction.Seconds5To10 => "5~10\uCD08",
            RemainingTimePrediction.Seconds10To15 => "10~15\uCD08",
            RemainingTimePrediction.Seconds15To20 => "15~20\uCD08",
            _ => "20\uCD08 \uC774\uC0C1"
        };

        private static string FormatOddEven(OddEvenPrediction prediction) => prediction == OddEvenPrediction.Odd ? "\uD640" : "\uC9DD";

        private static string FormatFirstEliminatedColumn(FirstEliminatedColumnPrediction prediction) => prediction switch
        {
            FirstEliminatedColumnPrediction.RedFront => "\uB808\uB4DC / \uC804\uC5F4",
            FirstEliminatedColumnPrediction.RedBack => "\uB808\uB4DC / \uD6C4\uC5F4",
            FirstEliminatedColumnPrediction.BlueFront => "\uBE14\uB8E8 / \uC804\uC5F4",
            FirstEliminatedColumnPrediction.BlueBack => "\uBE14\uB8E8 / \uD6C4\uC5F4",
            _ => "-"
        };

        private static string FormatSurvivingRow(SurvivingRowPrediction prediction) => prediction switch
        {
            SurvivingRowPrediction.RedRow1 => "\uB808\uB4DC / 1\uD589",
            SurvivingRowPrediction.RedRow2 => "\uB808\uB4DC / 2\uD589",
            SurvivingRowPrediction.RedRow3 => "\uB808\uB4DC / 3\uD589",
            SurvivingRowPrediction.BlueRow1 => "\uBE14\uB8E8 / 1\uD589",
            SurvivingRowPrediction.BlueRow2 => "\uBE14\uB8E8 / 2\uD589",
            SurvivingRowPrediction.BlueRow3 => "\uBE14\uB8E8 / 3\uD589",
            _ => "-"
        };

        private void OnContinueButtonClicked()
        {
            SoundManager.Instance?.PlaySfx(SfxIds.ButtonPositive);
            CancelResultAnimation();
            ContinueClicked?.Invoke();
        }

        private void OnDisable() => CancelResultAnimation();

        protected override void OnDestroy()
        {
            CancelResultAnimation();
            if (m_ContinueButton != null) m_ContinueButton.onClick.RemoveListener(OnContinueButtonClicked);
            ContinueClicked = null;
            PayoutRevealCompleted = null;
            base.OnDestroy();
        }

        private readonly struct BetEntry
        {
            public readonly string Label;
            public readonly bool IsCorrect;

            public BetEntry(string label, bool isCorrect)
            {
                Label = label;
                IsCorrect = isCorrect;
            }
        }

        private sealed class BettingBoxView
        {
            private readonly TMP_Text m_Label;
            private readonly GameObject m_CorrectImage;
            private readonly GameObject m_WrongImage;
            private readonly CanvasGroup m_CanvasGroup;
            private readonly RectTransform m_RectTransform;
            private readonly BetEntry m_Entry;

            public GameObject Root { get; }

            public BettingBoxView(GameObject root, BetEntry entry)
            {
                Root = root;
                m_Entry = entry;
                m_Label = FindDescendant(root.transform, "Betting_Text")?.GetComponent<TMP_Text>();
                m_CorrectImage = FindDescendant(root.transform, "Correct_Image")?.gameObject;
                m_WrongImage = FindDescendant(root.transform, "Wrong _Image")?.gameObject;
                m_CanvasGroup = GetCanvasGroup(root.transform);
                m_RectTransform = root.transform as RectTransform;
            }

            public void Initialize()
            {
                if (m_Label != null) m_Label.text = m_Entry.Label;
                if (m_CorrectImage != null) m_CorrectImage.SetActive(false);
                if (m_WrongImage != null) m_WrongImage.SetActive(false);
                m_CanvasGroup.alpha = 0f;
                if (m_RectTransform != null) m_RectTransform.localScale = Vector3.one * 0.88f;
                Root.SetActive(false);
            }

            public void Reveal()
            {
                Root.SetActive(true);
                if (m_CorrectImage != null) m_CorrectImage.SetActive(m_Entry.IsCorrect);
                if (m_WrongImage != null) m_WrongImage.SetActive(!m_Entry.IsCorrect);
            }

            public Tween PlayRevealTween()
            {
                Sequence sequence = DOTween.Sequence().SetTarget(Root);
                sequence.Join(m_CanvasGroup.DOFade(1f, 0.2f));
                if (m_RectTransform != null)
                    sequence.Join(m_RectTransform.DOScale(1f, 0.24f).SetEase(Ease.OutBack));
                return sequence;
            }

            public void KillTween()
            {
                m_RectTransform?.DOKill();
                m_CanvasGroup?.DOKill();
            }
        }
    }
}
#endif
