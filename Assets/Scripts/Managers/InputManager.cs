#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using DG.Tweening;

[DisallowMultipleComponent]
public class InputManager : Manager_Base
{
    public static InputManager Instance { get; private set; }

    [Header("Manager Settings")]
    [SerializeField] private ushort m_InitializationOrder = 20;
    public override ushort InitializationOrder => m_InitializationOrder;

    [Header("Raycast Settings")]
    [Tooltip("스킬 드래그 타겟팅 시 탐지할 3D 월드 지면 레이어")]
    [SerializeField] private LayerMask m_GroundLayerMask;

    [Header("Touch FX Settings")]
    [Tooltip("터치 시 스폰할 FX 프리팹 키 이름 (UI_FX_HUD 풀 내 프리팹 명칭)")]
    [SerializeField] private string m_TouchFxKey = "FX_TouchPoint";
    [SerializeField] private bool m_EnableTouchFx = true;

    // 스킬 드래그 상태 관리
    private bool m_IsDraggingSkill;
    private bool m_IsTargetingArmed;
    private int m_ArmedSessionId = -1;
    private int m_CurrentDraggingSkillId = -1;
    private Camera m_CachedMainCamera;

    #region Events
    /// <summary>
    /// 스킬 드래그 타겟팅 시작 (스킬 ID, 화면 좌표)
    /// </summary>
    public event Action<int, int, Vector2> OnSkillDragBegan;

    /// <summary>
    /// 스킬 드래그 위치 업데이트 (스킬 ID, 화면 좌표, 3D 월드 지면 좌표, Valid 여부)
    /// </summary>
    public event Action<int, int, Vector2, Vector3, bool> OnSkillDragUpdated;

    /// <summary>
    /// 스킬 드래그 종료/발동 (스킬 ID, 화면 좌표, 3D 월드 지면 좌표, 취소 여부)
    /// </summary>
    public event Action<int, int, Vector2, Vector3, bool, bool> OnSkillDragEnded;
    #endregion

    public Camera MainCamera
    {
        get
        {
            if (m_CachedMainCamera == null)
            {
                m_CachedMainCamera = Camera.main;
            }
            return m_CachedMainCamera;
        }
    }

    public override bool Setup()
    {
        // [High Safety] 유니티 가짜 Null 및 중복 인스턴스 검사
        if (!ReferenceEquals(Instance, null) && Instance != null && Instance != this)
        {
            Debug.LogWarning("[InputManager] 중복 인스턴스가 발견되어 파괴합니다.");
            Destroy(gameObject);
            return false;
        }

        Instance = this;
        return true;
    }

    protected override bool Init()
    {
        m_CachedMainCamera = Camera.main;

        // [New Input System] 모바일 터치 트래킹을 위한 EnhancedTouch 활성화
        if (!EnhancedTouchSupport.enabled)
        {
            EnhancedTouchSupport.Enable();
        }

        return true;
    }

    private void Update()
    {
        if (!IsInitialized) return;

        HandleTouchInput();
        HandleAndroidBackButton();
    }

    /// <summary>
    /// New Input System의 EnhancedTouch API를 사용한 안드로이드 멀티 터치 및 스킬 드래그 처리
    /// </summary>
    private void HandleTouchInput()
    {
        var activeTouches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
        if (activeTouches.Count == 0) return;

        for (int i = 0; i < activeTouches.Count; i++)
        {
            var touch = activeTouches[i];

            // 1. 터치 시작 시 FX 스폰 (UI 요소 터치가 아닐 때만)
            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                if (m_EnableTouchFx && !IsPointerOverUIObject(touch.screenPosition))
                {
                    // TODO : UI_FX 구현 시 수정
                    // SpawnTouchFx(touch.screenPosition);
                }
            }

            // 2. 스킬 드래그 타겟팅 상태 업데이트
            if (m_IsTargetingArmed && touch.phase == UnityEngine.InputSystem.TouchPhase.Began && !IsPointerOverUIObject(touch.screenPosition))
            {
                m_IsTargetingArmed = false;
                StartSkillDrag(m_CurrentDraggingSkillId, touch.screenPosition);
                return;
            }

            if (m_IsDraggingSkill)
            {
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved || touch.phase == UnityEngine.InputSystem.TouchPhase.Stationary)
                {
                    UpdateSkillDrag(touch.screenPosition);
                }
                else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended || touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    EndSkillDrag(touch.screenPosition, touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled);
                }
            }
        }
    }

    /// <summary>
    /// UI 요소를 터치 중인지 판별 (uGUI Raycast 감지)
    /// </summary>
    private bool IsPointerOverUIObject(Vector2 screenPosition)
    {
        if (EventSystem.current == null) return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }

    /*
    // TODO : UI_FX 구현 시 수정
    /// <summary>
    /// UIManager의 Pool_FX_HUD를 통한 터치 이펙트 스폰
    /// </summary>
    private void SpawnTouchFx(Vector2 screenPosition)
    {
        var pool = UIManager.Pool_FX_HUD;
        if (pool == null || string.IsNullOrEmpty(m_TouchFxKey)) return;

        Vector3 worldPos = MainCamera != null
            ? MainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 10f))
            : (Vector3)screenPosition;

        pool.Spawn(m_TouchFxKey, worldPos);
    }
    */

    #region Skill Drag Targeting API
    /// <summary>
    /// HUD 스킬 버튼의 OnPointerDown/OnBeginDrag에서 호출하여 월드 타겟팅 시작
    /// </summary>
    public void ArmSkillTargeting(int skillId, int sessionId)
    {
        m_IsTargetingArmed = true;
        m_ArmedSessionId = sessionId;
        m_CurrentDraggingSkillId = skillId;
    }

    public void CancelSkillDrag()
    {
        if (m_IsDraggingSkill)
        {
            EndSkillDrag(Vector2.zero, true);
        }
        else
        {
            m_IsTargetingArmed = false;
            m_ArmedSessionId = -1;
            m_CurrentDraggingSkillId = -1;
        }
    }

    public void StartSkillDrag(int skillId, Vector2 screenPosition)
    {
        m_IsDraggingSkill = true;
        m_CurrentDraggingSkillId = skillId;

        OnSkillDragBegan?.Invoke(skillId, m_ArmedSessionId, screenPosition);
        UpdateSkillDrag(screenPosition);
    }

    private void UpdateSkillDrag(Vector2 screenPosition)
    {
        if (!m_IsDraggingSkill) return;

        bool hitGround = RaycastGroundPosition(screenPosition, out Vector3 worldPos);
        bool isValid = hitGround && !IsPointerOverUIObject(screenPosition);
        OnSkillDragUpdated?.Invoke(m_CurrentDraggingSkillId, m_ArmedSessionId, screenPosition, worldPos, isValid);
    }

    private void EndSkillDrag(Vector2 screenPosition, bool isCanceled)
    {
        if (!m_IsDraggingSkill) return;

        bool hitGround = RaycastGroundPosition(screenPosition, out Vector3 worldPos);
        bool isValid = hitGround && !IsPointerOverUIObject(screenPosition);

        int skillId = m_CurrentDraggingSkillId;
        int sessionId = m_ArmedSessionId;

        m_IsDraggingSkill = false;
        m_IsTargetingArmed = false;
        m_CurrentDraggingSkillId = -1;
        m_ArmedSessionId = -1;

        OnSkillDragEnded?.Invoke(skillId, sessionId, screenPosition, worldPos, isCanceled, isValid);
    }

    /// <summary>
    /// 화면 터치 좌표에서 3D 월드 지면(Ground)으로 레이캐스트 투사
    /// </summary>
    public bool RaycastGroundPosition(Vector2 screenPosition, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;
        if (MainCamera == null) return false;

        Ray ray = MainCamera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f, m_GroundLayerMask, QueryTriggerInteraction.Collide))
        {
            worldPosition = hit.point;
            return true;
        }

        return false;
    }
    #endregion

    /// <summary>
    /// [New Input System] 안드로이드 시스템 백 버튼 처리
    /// Input System 패키지에서는 안드로이드 백 버튼이 Keyboard.current.escapeKey로 매핑됩니다.
    /// </summary>
    private void HandleAndroidBackButton()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            var uiManager = UIManager.Instance;
            if (uiManager == null || uiManager.CurrentControlStack == null) return;

            var controlStack = uiManager.CurrentControlStack;
            if (controlStack.Count == 0) return;

            for (int i = controlStack.Count - 1; i >= 0; i--)
            {
                var topUI = controlStack[i];
                if (topUI != null && topUI.CanCloseControlWithBackButton && topUI.BIsOpened)
                {
                    topUI.Close();
                    break;
                }
            }
        }
    }

    public override void Release()
    {
        m_IsDraggingSkill = false;
        m_CurrentDraggingSkillId = -1;

        OnSkillDragBegan = null;
        OnSkillDragUpdated = null;
        OnSkillDragEnded = null;

        if (EnhancedTouchSupport.enabled)
        {
            EnhancedTouchSupport.Disable();
        }

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