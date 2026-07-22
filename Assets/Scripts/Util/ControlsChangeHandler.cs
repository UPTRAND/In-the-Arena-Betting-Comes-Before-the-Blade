#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class ControlsChangeHandler : MonoBehaviour
{
    [SerializeField] private PlayerInput m_PlayerInput;

    public static ControlsChangeHandler Current { get; private set; }

    public PlayerInput PlayerInput => m_PlayerInput;

    /// <summary>
    /// 모바일 터치 전용 환경에서는 UI 요소의 자동 선택/포커스 하이라이트를 사용하지 않습니다.
    /// </summary>
    public bool UseDefaultSelectable => false;

    public event Action<PlayerInput> OnControlsChanged;

    private void Awake()
    {
        // [High Safety] 싱글톤 중복 검사 및 가짜 Null 차단
        if (!ReferenceEquals(Current, null) && Current != null && Current != this)
        {
            Debug.LogWarning("[ControlsChangeHandler] 중복 인스턴스가 발견되어 파괴합니다.");
            Destroy(this);
            return;
        }

        Current = this;

        if (m_PlayerInput == null)
        {
            m_PlayerInput = GetComponent<PlayerInput>();
        }
    }

    private void OnEnable()
    {
        if (m_PlayerInput != null)
        {
            m_PlayerInput.onControlsChanged += HandleOnControlsChanged;
            if (!string.IsNullOrEmpty(m_PlayerInput.currentControlScheme))
            {
                HandleOnControlsChanged(m_PlayerInput);
            }
        }
    }

    private void OnDisable()
    {
        if (m_PlayerInput != null)
        {
            m_PlayerInput.onControlsChanged -= HandleOnControlsChanged;
        }
    }

    public void HandleOnControlsChanged(PlayerInput input)
    {
        if (input == null) return;

        OnControlsChanged?.Invoke(input);
    }

    private void OnDestroy()
    {
        if (ReferenceEquals(Current, this))
        {
            Current = null;
        }
    }
}
#endif