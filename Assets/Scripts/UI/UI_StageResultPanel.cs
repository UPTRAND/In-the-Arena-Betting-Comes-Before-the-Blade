#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using System.Threading;
using DG.Tweening;
using InTheArena.MainGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    public enum StageResultPanelMode { Saving, SaveFailed, ClearCompleted, GameOver, GivenUp }

    [DisallowMultipleComponent]
    public sealed class UI_StageResultPanel : UI_Base
    {
        [Header("Legacy")]
        [SerializeField] private TMP_Text m_TitleText;
        [SerializeField] private TMP_Text m_CurrentCallText;
        [SerializeField] private TMP_Text m_TargetCallText;
        [SerializeField] private Button m_ReturnToLobbyButton;
        [SerializeField] private TMP_Text m_ErrorText;
        [SerializeField] private Button m_RetryButton;
        [SerializeField] private Button m_GiveUpButton;
        [SerializeField] private TMP_Text m_GiveUpButtonText;

        [Header("Round Result")]
        [SerializeField] private TMP_Text m_FinalColText;
        [SerializeField] private Image m_FinalColImage;
        [SerializeField] private RectTransform m_RoundCheckGroup;
        [SerializeField] private GameObject m_RoundCheckBoxTemplate;

        private static readonly Color ProfitBoxColor = new Color(0.13725491f, 1f, 0.13725491f);
        private static readonly Color LossBoxColor = new Color(1f, 0.39215687f, 0.39215687f);
        private static readonly Color NeutralBoxColor = new Color(0.77254903f, 0.77254903f, 0.77254903f);
        private static readonly Color ProfitTextColor = new Color(0.46666667f, 0.74509805f, 0.5058824f);
        private static readonly Color LossTextColor = new Color(0.74509805f, 0.4862745f, 0.46666667f);
        private static readonly Color NeutralTextColor = new Color(0.6901961f, 0.6901961f, 0.6901961f);

        private readonly List<RoundCheckView> m_RoundChecks = new List<RoundCheckView>();
        private readonly List<BetSettlement> m_Settlements = new List<BetSettlement>();
        private AwaitableCompletionSource m_CompletionSource;
        private Sequence m_ResultSequence;
        private bool m_IsEventSubscribed;
        private bool m_IsGiveUpConfirming;
        private int m_InitialCall;

        protected override void Awake()
        {
            base.Awake();
            ResolveReferences();
            if (m_ReturnToLobbyButton != null) m_ReturnToLobbyButton.onClick.AddListener(OnReturnToLobbyClicked);
            if (m_RetryButton != null) m_RetryButton.onClick.AddListener(OnRetryClicked);
            if (m_GiveUpButton != null) m_GiveUpButton.onClick.AddListener(OnGiveUpClicked);
        }

        private void OnEnable() => SubscribeEvent();
        private void OnDisable()
        {
            CancelResultAnimation();
            UnsubscribeEvent();
        }

        public void Prepare(bool isClear, int initialCall, IReadOnlyList<BetSettlement> settlements)
        {
            ResolveReferences();
            CancelResultAnimation();
            ClearRoundChecks();
            m_InitialCall = Mathf.Max(0, initialCall);
            m_Settlements.Clear();
            if (settlements != null) m_Settlements.AddRange(settlements);
            ResetResultDisplay();
            m_CompletionSource = new AwaitableCompletionSource();

            if (!gameObject.activeSelf) Open(); else Enable();
            SubscribeEvent();
            SetMode(isClear ? StageResultPanelMode.ClearCompleted : StageResultPanelMode.GameOver);
        }

        public void PlayResultAnimation()
        {
            CancelResultAnimation();
            ResetResultDisplay();
            BuildRoundChecks();
            if (m_RoundChecks.Count == 0) return;

            int runningCall = m_InitialCall;
            m_ResultSequence = DOTween.Sequence().SetTarget(this).SetUpdate(true);
            foreach (RoundCheckView roundCheck in m_RoundChecks)
            {
                int fromCall = runningCall;
                int nextCall = checked(fromCall + roundCheck.NetChange);
                m_ResultSequence.AppendInterval(0.09f);
                m_ResultSequence.AppendCallback(roundCheck.Show);
                m_ResultSequence.Append(roundCheck.PlayReveal());
                m_ResultSequence.Join(DOTween.To(
                    () => fromCall,
                    value => SetFinalCall(value),
                    nextCall,
                    0.3f).SetEase(Ease.OutCubic));
                if (roundCheck.NetChange > 0 && m_FinalColImage != null)
                    m_ResultSequence.Join(m_FinalColImage.transform.DOPunchScale(Vector3.one * 0.12f, 0.28f, 4, 0.65f));
                runningCall = nextCall;
            }
        }

        public void SetMode(StageResultPanelMode mode, string errorStr = null)
        {
            m_IsGiveUpConfirming = false;
            if (m_ErrorText != null) m_ErrorText.gameObject.SetActive(mode == StageResultPanelMode.Saving || mode == StageResultPanelMode.SaveFailed || mode == StageResultPanelMode.GivenUp);
            if (m_RetryButton != null) m_RetryButton.gameObject.SetActive(mode == StageResultPanelMode.SaveFailed);
            if (m_GiveUpButton != null) m_GiveUpButton.gameObject.SetActive(mode == StageResultPanelMode.SaveFailed);
            if (m_ReturnToLobbyButton != null) m_ReturnToLobbyButton.gameObject.SetActive(mode == StageResultPanelMode.ClearCompleted || mode == StageResultPanelMode.GameOver);

            switch (mode)
            {
                case StageResultPanelMode.ClearCompleted:
                    SetTitle("STAGE CLEAR", new Color(0.3f, 1f, 0.45f));
                    EnableInput();
                    EnableCompletionInput();
                    break;
                case StageResultPanelMode.GameOver:
                    SetTitle("Game Over", new Color(1f, 0.3f, 0.3f));
                    EnableInput();
                    EnableCompletionInput();
                    break;
                case StageResultPanelMode.Saving:
                    if (m_ErrorText != null) m_ErrorText.text = "Saving...";
                    DisableCompletionInput();
                    break;
                case StageResultPanelMode.SaveFailed:
                    if (m_ErrorText != null) m_ErrorText.text = $"Save failed.\n{errorStr}";
                    if (m_GiveUpButtonText != null) m_GiveUpButtonText.text = "Give up";
                    DisableCompletionInput();
                    EnableInput();
                    break;
                case StageResultPanelMode.GivenUp:
                    if (m_ErrorText != null) m_ErrorText.text = "Returning to lobby...";
                    DisableCompletionInput();
                    DisableInput();
                    break;
            }
        }

        public void EnableInput()
        {
            if (CanvasGroup == null) return;
            CanvasGroup.interactable = true;
            CanvasGroup.blocksRaycasts = true;
        }

        public void DisableInput()
        {
            if (CanvasGroup == null) return;
            CanvasGroup.interactable = false;
            CanvasGroup.blocksRaycasts = false;
        }

        public async Awaitable WaitForCompletionAsync(CancellationToken token)
        {
            AwaitableCompletionSource source = m_CompletionSource ?? throw new System.InvalidOperationException("Prepare must be called before waiting.");
            using CancellationTokenRegistration registration = token.Register(static state => ((AwaitableCompletionSource)state).TrySetResult(), source);
            await source.Awaitable;
            token.ThrowIfCancellationRequested();
        }

        public override void OnClosed()
        {
            CancelResultAnimation();
            ClearRoundChecks();
            m_CompletionSource?.TrySetResult();
            m_CompletionSource = null;
            UnsubscribeEvent();
            base.OnClosed();
        }

        protected override void OnDestroy()
        {
            CancelResultAnimation();
            ClearRoundChecks();
            if (m_ReturnToLobbyButton != null) m_ReturnToLobbyButton.onClick.RemoveListener(OnReturnToLobbyClicked);
            if (m_RetryButton != null) m_RetryButton.onClick.RemoveListener(OnRetryClicked);
            if (m_GiveUpButton != null) m_GiveUpButton.onClick.RemoveListener(OnGiveUpClicked);
            m_CompletionSource?.TrySetResult();
            UnsubscribeEvent();
            base.OnDestroy();
        }

        private void BuildRoundChecks()
        {
            ClearRoundChecks();
            if (m_RoundCheckGroup == null || m_RoundCheckBoxTemplate == null) return;
            m_RoundCheckBoxTemplate.SetActive(false);
            foreach (BetSettlement settlement in m_Settlements)
            {
                GameObject item = Instantiate(m_RoundCheckBoxTemplate, m_RoundCheckGroup);
                item.name = "RoundCheck_Box_Result";
                var roundCheck = new RoundCheckView(item, settlement.NetChange);
                roundCheck.Initialize();
                m_RoundChecks.Add(roundCheck);
            }
        }

        private void ClearRoundChecks()
        {
            foreach (RoundCheckView roundCheck in m_RoundChecks)
            {
                roundCheck.Kill();
                if (roundCheck.Root != null) Destroy(roundCheck.Root);
            }
            m_RoundChecks.Clear();
        }

        private void ResetResultDisplay()
        {
            SetFinalCall(m_InitialCall);
            if (m_FinalColImage != null) m_FinalColImage.transform.localScale = Vector3.one;
            if (m_RoundCheckBoxTemplate != null) m_RoundCheckBoxTemplate.SetActive(false);
        }

        private void SetFinalCall(int value)
        {
            if (m_FinalColText != null) m_FinalColText.text = $"{Mathf.Max(0, value)} Col";
        }

        private void CancelResultAnimation()
        {
            if (m_ResultSequence != null && m_ResultSequence.IsActive()) m_ResultSequence.Kill(false);
            m_ResultSequence = null;
            m_FinalColImage?.transform.DOKill();
            m_FinalColText?.DOKill();
            foreach (RoundCheckView roundCheck in m_RoundChecks) roundCheck.Kill();
        }

        private void ResolveReferences()
        {
            m_FinalColText ??= m_CurrentCallText ?? FindDescendant(transform, "FinalCol_Text")?.GetComponent<TMP_Text>();
            m_FinalColImage ??= FindDescendant(transform, "FinalCol_Image")?.GetComponent<Image>();
            m_RoundCheckGroup ??= FindDescendant(transform, "RoundCheck_Group") as RectTransform;
            m_RoundCheckBoxTemplate ??= FindDescendant(m_RoundCheckGroup, "RoundCheck_Box")?.gameObject;
        }

        private void SetTitle(string text, Color color)
        {
            if (m_TitleText == null) return;
            m_TitleText.text = text;
            m_TitleText.color = color;
        }

        private void SubscribeEvent()
        {
            if (m_IsEventSubscribed || StageManager.Instance == null) return;
            StageManager.Instance.OnStageClearCommitStateChanged += HandleCommitStateChanged;
            m_IsEventSubscribed = true;
        }

        private void UnsubscribeEvent()
        {
            if (!m_IsEventSubscribed || StageManager.Instance == null) return;
            StageManager.Instance.OnStageClearCommitStateChanged -= HandleCommitStateChanged;
            m_IsEventSubscribed = false;
        }

        private void HandleCommitStateChanged(StageClearCommitState state)
        {
            if (state == StageClearCommitState.Failed) SetMode(StageResultPanelMode.SaveFailed, StageManager.Instance.LastStageClearSaveError);
            else if (state == StageClearCommitState.Saving) SetMode(StageResultPanelMode.Saving);
            else if (state == StageClearCommitState.Committed) SetMode(StageResultPanelMode.ClearCompleted);
            else if (state == StageClearCommitState.GivenUp) SetMode(StageResultPanelMode.GivenUp);
        }

        private void OnReturnToLobbyClicked()
        {
            if (m_ReturnToLobbyButton != null && m_ReturnToLobbyButton.gameObject.activeSelf)
            {
                DisableInput();
                m_CompletionSource?.TrySetResult();
            }
        }

        private void OnRetryClicked()
        {
            m_IsGiveUpConfirming = false;
            if (StageManager.Instance != null && m_RetryButton != null && m_RetryButton.gameObject.activeSelf)
                StageManager.Instance.RetryStageClearSave();
        }

        private void OnGiveUpClicked()
        {
            if (!m_IsGiveUpConfirming)
            {
                m_IsGiveUpConfirming = true;
                if (m_ErrorText != null) m_ErrorText.text = "Give up stage clear save?";
                if (m_GiveUpButtonText != null) m_GiveUpButtonText.text = "Confirm give up";
            }
            else if (StageManager.Instance != null && m_GiveUpButton != null && m_GiveUpButton.gameObject.activeSelf)
                StageManager.Instance.GiveUpStageClearSave();
        }

        private void DisableCompletionInput()
        {
            if (m_ReturnToLobbyButton != null) m_ReturnToLobbyButton.interactable = false;
        }

        private void EnableCompletionInput()
        {
            if (m_ReturnToLobbyButton != null) m_ReturnToLobbyButton.interactable = true;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindDescendant(root.GetChild(i), objectName);
                if (result != null) return result;
            }
            return null;
        }

        private sealed class RoundCheckView
        {
            private readonly Image m_BoxImage;
            private readonly TMP_Text m_AddColText;
            private readonly RectTransform m_RectTransform;
            public GameObject Root { get; }
            public int NetChange { get; }

            public RoundCheckView(GameObject root, int netChange)
            {
                Root = root;
                NetChange = netChange;
                m_BoxImage = root.GetComponent<Image>();
                m_AddColText = FindDescendant(root.transform, "AddCol_Text")?.GetComponent<TMP_Text>();
                m_RectTransform = root.transform as RectTransform;
            }

            public void Initialize()
            {
                Root.SetActive(true);
                if (m_BoxImage != null) m_BoxImage.color = NetChange > 0 ? ProfitBoxColor : NetChange < 0 ? LossBoxColor : NeutralBoxColor;
                if (m_AddColText != null)
                {
                    m_AddColText.color = NetChange > 0 ? ProfitTextColor : NetChange < 0 ? LossTextColor : NeutralTextColor;
                    m_AddColText.text = NetChange > 0 ? $"+{NetChange} Col" : NetChange < 0 ? $"-{Mathf.Abs(NetChange)} Col" : "0 Col";
                }
                GetCanvasGroup().alpha = 0f;
                if (m_RectTransform != null) m_RectTransform.localScale = Vector3.one * 0.82f;
            }

            public void Show() => Root.SetActive(true);

            public Tween PlayReveal()
            {
                Sequence sequence = DOTween.Sequence().SetTarget(Root);
                sequence.Join(GetCanvasGroup().DOFade(1f, 0.2f));
                if (m_RectTransform != null)
                {
                    sequence.Join(m_RectTransform.DOScale(1f, 0.24f).SetEase(Ease.OutBack));
                    sequence.Append(m_RectTransform.DOPunchScale(Vector3.one * 0.08f, 0.14f, 3, 0.7f));
                }
                return sequence;
            }

            public void Kill()
            {
                if (Root != null) GetCanvasGroup()?.DOKill();
                m_RectTransform?.DOKill();
            }

            private CanvasGroup GetCanvasGroup()
            {
                CanvasGroup group = Root.GetComponent<CanvasGroup>();
                return group != null ? group : Root.AddComponent<CanvasGroup>();
            }
        }
    }
}
#endif
