#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

[DisallowMultipleComponent]
public class UIManager : Manager_Base
{
    public static UIManager Instance { get; private set; }

    [Header("Manager Settings")]
    [SerializeField] private ushort m_InitializationOrder = 100;
    public override ushort InitializationOrder => m_InitializationOrder;

    [Header("Pool Settings")]
    [SerializeField] private bool m_CreatePool;
    private UIObjectPoolingFactory m_Pool;
    // FX 관련 추가 후 수정
    //private ObjectPoolingFactory<UI_FX> m_PoolHud;

    [Header("UI Roots")]
    [SerializeField] private List<UI_Root> m_UiRoots = new List<UI_Root>();
    private readonly Dictionary<EUIObjectPoolingParent, UI_Root> m_BakedUiRoots = new Dictionary<EUIObjectPoolingParent, UI_Root>();
    private readonly List<GameObject> m_AllUiBaseObjects = new List<GameObject>();

    // [High Safety] GC 박싱 방지를 위한 제네릭 Dictionary 전환
    private readonly Dictionary<string, IUIBase> m_UiElementsByTypename = new Dictionary<string, IUIBase>();
    private readonly Dictionary<string, Transform> m_UiElementsByName = new Dictionary<string, Transform>();

    // 컨트롤 스택 제어
    private readonly List<List<UI_Base>> m_ControlStack = new List<List<UI_Base>>();
    private bool m_BindCallbacks;
    private bool m_IsScreenFaderOpened;

    public static UIObjectPoolingFactory Pool => Instance != null ? Instance.m_Pool : null;
    // FX 관련 추가 후 수정
    //public static ObjectPoolingFactory<UI_FX> Pool_FX_HUD => Instance != null ? Instance.m_PoolHud : null;

    public bool IsHidden { get; private set; }
    public bool IsPausePanelOpeningAllowed { get; private set; } = true;
    public List<List<UI_Base>> AllControlStack => m_ControlStack;

    public IList<UI_Base> CurrentControlStack
    {
        get
        {
            if (m_ControlStack.Count == 0) return null;
            return m_ControlStack[m_ControlStack.Count - 1];
        }
    }

    /// <summary>
    /// Manager_Base 1단계 초기화 : 싱글톤 설정 및 사전 바인딩
    /// </summary>
    public override bool Setup()
    {
        if (!ReferenceEquals(Instance, null) && Instance != null && Instance != this)
        {
            Debug.LogWarning("[UIManager] 중복 인스턴스가 발견되어 파괴합니다.");
            Destroy(gameObject);
            return false;
        }

        Instance = this;

        InitializeUiRoots();
        BindScreenFaderEvents();
        InitializePoolsIfNeeded();

        return true;
    }

    /// <summary>
    /// Manager_Base 2단계 초기화 : 매니저 메인 로직 가동
    /// </summary>
    protected override bool Init()
    {
        return true;
    }

    private void InitializeUiRoots()
    {
        foreach (var uiRoot in m_UiRoots)
        {
            if (uiRoot == null) continue;

            if (uiRoot.Type != EUIObjectPoolingParent.None && !m_BakedUiRoots.ContainsKey(uiRoot.Type))
            {
                m_BakedUiRoots.Add(uiRoot.Type, uiRoot);
            }
        }

        m_BindCallbacks = true;

        foreach (var uiRoot in m_UiRoots)
        {
            if (uiRoot == null) continue;

            uiRoot.OnControlAdded += OnControlAdded;
            uiRoot.OnControlRemoved += OnControlRemoved;

            int childCount = uiRoot.transform.childCount;
            for (int i = 0; i < childCount; ++i)
            {
                Transform child = uiRoot.transform.GetChild(i);
                if (child.TryGetComponent<IUIBase>(out var component))
                {
                    component.SetRoot(uiRoot);
                    m_AllUiBaseObjects.Add(child.gameObject);

                    string typeName = component.GetType().Name;
                    if (component.BIsSearchedByTypeHash && !m_UiElementsByTypename.ContainsKey(typeName))
                    {
                        m_UiElementsByTypename.Add(typeName, component);
                    }

                    if (!m_UiElementsByName.ContainsKey(child.name))
                    {
                        m_UiElementsByName.Add(child.name, child);
                    }

                    if (component.BIsOpened && component is UI_Base control && control.HasControl)
                    {
                        uiRoot.AddControl(control);
                    }
                }
            }
        }
    }

    private void BindScreenFaderEvents()
    {
        if (ScreenFader.Instance != null)
        {
            ScreenFader.Instance.OnOpened += OnScreenFaderOpened;
            ScreenFader.Instance.OnClosed += OnScreenFaderClosed;
        }
    }

    private void UnbindCallbacks()
    {
        if (!m_BindCallbacks) return;

        foreach (var uiRoot in m_UiRoots)
        {
            if (uiRoot == null) continue;
            uiRoot.OnControlAdded -= OnControlAdded;
            uiRoot.OnControlRemoved -= OnControlRemoved;
        }

        if (ScreenFader.Instance != null)
        {
            ScreenFader.Instance.OnOpened -= OnScreenFaderOpened;
            ScreenFader.Instance.OnClosed -= OnScreenFaderClosed;
        }

        m_BindCallbacks = false;
    }

    private void InitializePoolsIfNeeded()
    {
        if (!m_CreatePool) return;

        GameObject[] poolablePrefabs1 = Resources.LoadAll<GameObject>("UI");
        m_Pool = new UIObjectPoolingFactory();
        m_Pool.Initialize(this, poolablePrefabs1);

        GameObject[] poolablePrefabs2 = Resources.LoadAll<GameObject>("UI_FX_HUD");
        var hudRoot = GetRootFromType(EUIObjectPoolingParent.HUD);
        if (hudRoot != null)
        {
            // FX 관련 추가 후 수정
            //m_PoolHud = new ObjectPoolingFactory<UI_FX>();
            //m_PoolHud.Initialize(hudRoot.transform, poolablePrefabs2, string.Empty);
        }
    }

    public UI_Root GetRootFromType(EUIObjectPoolingParent parent)
    {
        return m_BakedUiRoots.TryGetValue(parent, out var root) ? root : null;
    }

    public T GetElement<T>() where T : class, IUIBase
    {
        string typeName = typeof(T).Name;
        return m_UiElementsByTypename.TryGetValue(typeName, out var element) ? element as T : null;
    }

    public Transform GetElement(string name)
    {
        return m_UiElementsByName.TryGetValue(name, out var transformElement) ? transformElement : null;
    }

    public void SetAllowPausePanelOpening(bool value) => IsPausePanelOpeningAllowed = value;

    public void CloseAllControl()
    {
        for (int i = m_ControlStack.Count - 1; i >= 0; --i)
        {
            for (int j = m_ControlStack[i].Count - 1; j >= 0; --j)
            {
                var control = m_ControlStack[i][j];
                if (control != null)
                {
                    control.Close();
                }
            }
        }
    }

    private void OnControlAdded(UI_Base control)
    {
        if (control == null) return;

        if (CurrentControlStack != null)
        {
            foreach (var currentControl in CurrentControlStack)
            {
                currentControl?.Disable();
            }
        }

        if (control.CombineTarget != null)
        {
            int targetIndex = -1;
            for (int i = m_ControlStack.Count - 1; i >= 0; --i)
            {
                for (int j = m_ControlStack[i].Count - 1; j >= 0; --j)
                {
                    if (m_ControlStack[i][j] == control.CombineTarget)
                    {
                        targetIndex = i;
                        break;
                    }
                }
            }

            if (targetIndex != -1)
            {
                if (control.CombineAtLastArray)
                    m_ControlStack[targetIndex].Add(control);
                else
                    m_ControlStack[targetIndex].Insert(0, control);
            }
            else
            {
                m_ControlStack.Add(new List<UI_Base> { control });
            }
        }
        else
        {
            m_ControlStack.Add(new List<UI_Base> { control });
        }

        if (!m_IsScreenFaderOpened && CurrentControlStack != null)
        {
            for (int i = 0; i < CurrentControlStack.Count; ++i)
            {
                CurrentControlStack[i]?.Enable();
            }
        }
    }

    private void OnControlRemoved(UI_Base control)
    {
        if (control == null) return;

        for (int i = m_ControlStack.Count - 1; i >= 0; --i)
        {
            for (int j = m_ControlStack[i].Count - 1; j >= 0; --j)
            {
                if (m_ControlStack[i][j] == control)
                {
                    m_ControlStack[i][j].Disable();
                    m_ControlStack[i].RemoveAt(j);
                }
            }

            if (m_ControlStack[i].Count == 0)
            {
                m_ControlStack.RemoveAt(i);
            }
        }

        if (!m_IsScreenFaderOpened && CurrentControlStack != null)
        {
            foreach (var currentControl in CurrentControlStack)
            {
                currentControl?.Enable();
            }
        }
    }

    private void OnScreenFaderOpened()
    {
        m_IsScreenFaderOpened = true;
        if (CurrentControlStack == null) return;

        foreach (var currentControl in CurrentControlStack)
        {
            currentControl?.Disable();
        }
    }

    private void OnScreenFaderClosed()
    {
        m_IsScreenFaderOpened = false;
        if (CurrentControlStack == null) return;

        foreach (var currentControl in CurrentControlStack)
        {
            currentControl?.Enable();
        }
    }

    public void Hide()
    {
        if (IsHidden) return;
        IsHidden = true;

        foreach (var uiRoot in m_UiRoots)
        {
            if (uiRoot != null && uiRoot.CanvasGroup != null)
            {
                uiRoot.CanvasGroup.alpha = 0.0f;
            }
        }
    }

    public void Show()
    {
        if (!IsHidden) return;
        IsHidden = false;

        foreach (var uiRoot in m_UiRoots)
        {
            if (uiRoot != null && uiRoot.CanvasGroup != null)
            {
                uiRoot.CanvasGroup.alpha = 1.0f;
            }
        }
    }

    /// <summary>
    /// Manager_Base의 자원 해제 구현
    /// </summary>
    public override void Release()
    {
        UnbindCallbacks();

        if (m_Pool != null)
        {
            m_Pool.Clear();
            m_Pool = null;
        }

        // FX 관련 추가 후 수정
        /*
        if (m_PoolHud != null)
        {
            m_PoolHud.Clear();
            m_PoolHud = null;
        }
        */

        // [High Safety] DOTween: 매니저 파괴 시 연결된 트윈 정지
        transform.DOKill();

        if (ReferenceEquals(Instance, this))
        {
            Instance = null;
        }

        base.Release();
    }

    protected override void OnDestroy()
    {
        Release();
        base.OnDestroy();
    }
}
#endif