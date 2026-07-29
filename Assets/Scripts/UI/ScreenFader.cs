#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

[DisallowMultipleComponent]
public class ScreenFader : MonoBehaviour
{
    public enum EFadingState
    {
        None,
        FadingOut,
        WaitForFadeIn,
        FadingIn,
        FadeEnd
    }

    [Header("UI Components")]
    [SerializeField] private CanvasGroup m_CanvasGroup;
    [SerializeField] private Image m_FadeScreenImage;
    [SerializeField] private Color m_DefaultFadeScreenColor = new Color32(34, 32, 52, 255);
    [SerializeField] private Image m_SaveProgressImage;

    [Header("Loading Screen Components")]
    [SerializeField] private CanvasGroup m_LoadingScreenImage;
    [SerializeField] private Image m_LoadingScreenBGImage;

    [Header("Debug Components")]
    [SerializeField] private TextMeshProUGUI m_DebugText;

    private EFadingState m_FadingState = EFadingState.None;
    private readonly Timer m_ColorChangeTimer = new Timer(1f, false);
    private readonly Timer m_FadeInTimer = new Timer(1f, false);
    private readonly Timer m_WarmUpTimer = new Timer(0f, false);
    private readonly Timer m_DebugTextTimer = new Timer(3f, false);

    private bool m_SmoothColorChange;
    private Color m_BeforeColor;
    private Color m_AfterColor;

    private float m_FadeSpeed = 1f;
    private bool m_AutoFadeIn = true;
    private bool m_ManualFadeInActionPerformed;

    private GameObject m_CurrentFadingObject;
    private GameObject m_CurrentLoadingScreen;
    private int m_CurrentLoadingScreenType = -1;

    private readonly Queue<string> m_DebugTextQueue = new Queue<string>();

    public static ScreenFader Instance { get; private set; }

    public EFadingState FadingState
    {
        get => m_FadingState;
        private set
        {
            if (m_FadingState == value) return;
            m_FadingState = value;

            bool isFading = m_FadingState != EFadingState.None;
            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.blocksRaycasts = isFading;
                m_CanvasGroup.interactable = isFading;
            }

            OnChangeFadeState?.Invoke(isFading);
        }
    }

    public bool IsFading => FadingState != EFadingState.None;
    public bool DemoBuild { get; private set; }
    public bool SampleBuild { get; private set; }
    public float SampleDemoBuildTime { get; private set; } = 1500f;
    public bool IsTestMode { get; private set; }

    private event Action OnFadeInAction;
    public event Action OnOpened;
    public event Action OnClosed;
    public event Action<bool> OnChangeFadeState;

    private void Awake()
    {
        if (!ReferenceEquals(Instance, null) && Instance != null && Instance != this)
        {
            Debug.LogWarning("[ScreenFader] 중복 인스턴스가 발견되어 파괴합니다.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (m_CanvasGroup != null)
        {
            m_CanvasGroup.alpha = 0f;
            m_CanvasGroup.blocksRaycasts = false;
            m_CanvasGroup.interactable = false;
        }

        FadingState = EFadingState.None;

        Application.logMessageReceived += OnLogMessageReceived;
        // TODO: 추후 SaveManager 구현 시 추가
        // SaveManager.OnSaveBegin += ShowSaveScreen;
        // SaveManager.OnSaveEnd += HideSaveScreen;

        ParseCommandLineArgs();
    }

    private void ParseCommandLineArgs()
    {
        try
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args == null) return;

            foreach (string arg in args)
            {
                string lowerArg = arg.ToLower();
                if (lowerArg == "-test_mode") IsTestMode = true;
                else if (lowerArg == "-demo_build") DemoBuild = true;

                string[] split = arg.Split('=', StringSplitOptions.None);
                if (split.Length == 2 && split[0].ToLower() == "-sample_build")
                {
                    SampleBuild = true;
                    if (float.TryParse(split[1], out float result))
                        SampleDemoBuildTime = result;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ScreenFader] 커맨드 라인 인자 파싱 실패: {ex.Message}");
        }
    }

    private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (!IsTestMode || (type != LogType.Error && type != LogType.Exception)) return;
        if (m_DebugText == null) return;

        m_DebugText.gameObject.SetActive(true);
        m_DebugTextQueue.Enqueue(condition);
        if (m_DebugTextQueue.Count > 5) m_DebugTextQueue.Dequeue();

        m_DebugText.text = string.Join("\n", m_DebugTextQueue.ToArray());
        m_DebugTextTimer.Reset();
    }

    private void Update()
    {
        float unscaledDeltaTime = Time.unscaledDeltaTime;

        if (m_DebugText != null && m_DebugText.gameObject.activeSelf && m_DebugTextTimer.Update(unscaledDeltaTime))
        {
            m_DebugText.gameObject.SetActive(false);
        }

        if (m_SmoothColorChange && m_FadeScreenImage != null)
        {
            m_ColorChangeTimer.Update(Time.deltaTime);
            m_FadeScreenImage.color = Color.Lerp(m_BeforeColor, m_AfterColor, m_ColorChangeTimer.Ratio);
            if (m_ColorChangeTimer.IsFinished)
            {
                m_SmoothColorChange = false;
            }
        }

        UpdateFadingState(unscaledDeltaTime);
    }

    private void UpdateFadingState(float unscaledDeltaTime)
    {
        if (m_CanvasGroup == null) return;

        switch (FadingState)
        {
            case EFadingState.FadingOut:
                m_CanvasGroup.alpha += unscaledDeltaTime * m_FadeSpeed;
                if (m_CanvasGroup.alpha >= 1f)
                {
                    m_CanvasGroup.alpha = 1f;
                    FadingState = EFadingState.WaitForFadeIn;
                }
                break;

            case EFadingState.WaitForFadeIn:
                if (m_FadeInTimer.Update(unscaledDeltaTime))
                {
                    if (!m_ManualFadeInActionPerformed)
                    {
                        m_ManualFadeInActionPerformed = true;
                        OnFadeInAction?.Invoke();
                    }

                    if (m_AutoFadeIn)
                    {
                        FadingState = EFadingState.FadingIn;
                    }
                }
                break;

            case EFadingState.FadingIn:
                if (m_WarmUpTimer.Duration <= 0f || m_WarmUpTimer.Update(unscaledDeltaTime))
                {
                    FadingState = EFadingState.FadeEnd;
                }
                break;

            case EFadingState.FadeEnd:
                m_CanvasGroup.alpha -= unscaledDeltaTime * m_FadeSpeed;
                if (m_CanvasGroup.alpha <= 0f)
                {
                    if (m_CurrentFadingObject != null)
                    {
                        Destroy(m_CurrentFadingObject);
                        m_CurrentFadingObject = null;
                    }

                    m_CanvasGroup.alpha = 0f;
                    FadingState = EFadingState.None;

                    if (m_FadeScreenImage != null)
                    {
                        m_FadeScreenImage.color = m_DefaultFadeScreenColor;
                    }

                    // 페이드 완결 시 UIManager에 화면 전환 완료 통보
                    OnClosed?.Invoke();
                }
                break;
        }
    }

    /// <summary>
    /// 페이드 아웃(화면 암전)을 시작합니다.
    /// </summary>
    public void FadeOut(Action fadeInAction, bool autoFadeIn = true, float waitTime = 1f, float fadingSpeed = 1f, float warmUpTime = 0f)
    {
        if (FadingState != EFadingState.None)
        {
            Debug.LogWarning("[ScreenFader] 이미 페이드 처리가 진행 중입니다.");
            return;
        }

        FadingState = EFadingState.FadingOut;
        m_FadeInTimer.Duration = waitTime;
        m_FadeInTimer.Reset();

        m_WarmUpTimer.Duration = warmUpTime;
        m_WarmUpTimer.Reset();

        m_AutoFadeIn = autoFadeIn;
        m_ManualFadeInActionPerformed = false;
        OnFadeInAction = fadeInAction;
        m_FadeSpeed = fadingSpeed;
        m_SmoothColorChange = false;

        // UIManager에 페이드 시작 통보 (UI 터치 입력 일시정지)
        OnOpened?.Invoke();
    }

    public void FadeIn()
    {
        if (FadingState == EFadingState.None || m_CanvasGroup == null) return;
        m_CanvasGroup.alpha = 1f;
        m_AutoFadeIn = true;
        FadingState = EFadingState.FadingIn;
    }

    public void FadeIn(float speed)
    {
        m_FadeSpeed = speed;
        FadeIn();
    }

    public void SetFadeColor(Color color, bool smooth = false)
    {
        if (m_FadeScreenImage == null) return;

        if (!smooth)
        {
            m_FadeScreenImage.color = color;
            m_SmoothColorChange = false;
        }
        else
        {
            m_SmoothColorChange = true;
            m_BeforeColor = m_FadeScreenImage.color;
            m_AfterColor = color;
            m_ColorChangeTimer.Duration = 1f;
            m_ColorChangeTimer.Reset();
        }
    }

    public void ShowSaveScreen()
    {
        if (m_SaveProgressImage != null) m_SaveProgressImage.enabled = true;
    }

    public void HideSaveScreen()
    {
        if (m_SaveProgressImage != null) m_SaveProgressImage.enabled = false;
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= OnLogMessageReceived;
        // TODO: 추후 SaveManager 구현 시 추가
        // SaveManager.OnSaveBegin -= ShowSaveScreen;
        // SaveManager.OnSaveEnd -= HideSaveScreen;

        // [High Safety] DOTween: UI 및 Fader 파괴 시 잔류 트윈 안전 제거
        transform.DOKill();
        if (m_CanvasGroup != null) m_CanvasGroup.DOKill();

        if (ReferenceEquals(Instance, this))
        {
            Instance = null;
        }
    }
}
#endif