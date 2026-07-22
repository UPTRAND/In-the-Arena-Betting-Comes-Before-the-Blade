#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public class UI_Base : MonoBehaviour, IUIBase
{
    private RectTransform m_RectTransform;

    [Header("Control Settings")]
    [Tooltip("UI가 화면에 열렸을 때 조작권을 가져갈지 여부")]
    [SerializeField] private bool m_HasControl;

    [Tooltip("안드로이드 백 버튼 동작 시 중앙 매니저가 이 UI를 닫을 수 있는지 여부")]
    [SerializeField] private bool m_CanCloseControlWithBackButton = true;

    [Tooltip("플레이어 조작 관련 UI 여부")]
    [SerializeField] private bool m_BIsPlayerUI;

    #region Properties for External Managers
    public bool HasControl => m_HasControl;
    public bool CanCloseControlWithBackButton => m_CanCloseControlWithBackButton;
    public bool IsPlayerUI => m_BIsPlayerUI;

    public RectTransform rectTransform
    {
        get
        {
            // [High Safety] 유니티 커스텀 == null 검사를 통해 C++ 네이티브 오버헤드 최소화
            if (m_RectTransform == null)
            {
                m_RectTransform = transform as RectTransform;
            }
            return m_RectTransform;
        }
    }

    public CanvasGroup CanvasGroup { get; private set; }
    public UI_Root ParentRoot { get; private set; }
    public bool BIsOpened => gameObject.activeSelf;
    public bool IsControlEnabled { get; private set; }
    public virtual bool BIsSearchedByTypeHash => true;

    public UI_Base CombineTarget { get; private set; }
    public bool CombineAtLastArray { get; private set; }
    #endregion

    protected virtual void Awake()
    {
        CanvasGroup = GetComponent<CanvasGroup>();
    }

    public void SetRoot(UI_Root parent) => ParentRoot = parent;

    public void ControlCombine(UI_Base attachTarget, bool lastArray)
    {
        CombineTarget = attachTarget;
        CombineAtLastArray = lastArray;
    }

    public void Open()
    {
        if (BIsOpened) return;

        gameObject.SetActive(true);
        OnOpened();

        if (m_HasControl && ParentRoot != null)
        {
            ParentRoot.AddControl(this);
        }

        CombineTarget = null;
    }

    public virtual void OnOpened() { }

    public void AddControlToParent()
    {
        if (ParentRoot != null) ParentRoot.AddControl(this);
    }

    public void RemoveControlFromParent()
    {
        if (ParentRoot != null) ParentRoot.RemoveControl(this);
    }

    public virtual void Close()
    {
        if (!BIsOpened) return;

        // [High Safety] DOTween: UI 닫힘 시 해당 렌더링 요소에 걸린 애니메이션 종료
        KillActiveTweens();

        gameObject.SetActive(false);
        OnClosed();

        if (m_HasControl && ParentRoot != null)
        {
            ParentRoot.RemoveControl(this);
        }

        CombineTarget = null;
    }

    public virtual void OnClosed() { }

    public void Enable()
    {
        IsControlEnabled = true;

        if (CanvasGroup != null)
        {
            CanvasGroup.interactable = true;
            CanvasGroup.blocksRaycasts = true;
        }

        OnControlEnabled();
    }

    public void Disable()
    {
        IsControlEnabled = false;

        if (CanvasGroup != null)
        {
            CanvasGroup.interactable = false;
            CanvasGroup.blocksRaycasts = false;
        }

        KillActiveTweens();
        OnControlDisabled();
    }

    protected virtual void OnControlEnabled() { }
    protected virtual void OnControlDisabled() { }

    /// <summary>
    /// UI에 종속된 DOTween 트윈 인스턴스를 안전하게 정지합니다.
    /// </summary>
    private void KillActiveTweens()
    {
        transform.DOKill();
        if (CanvasGroup != null)
        {
            CanvasGroup.DOKill();
        }
    }

    protected virtual void OnDestroy()
    {
        // [High Safety] DOTween: 객체 파괴 시 메모리 누수 방지
        KillActiveTweens();
    }
}
#endif