#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// MonoBehaviour 없이 스킬 쿨타임, UI 진행도, 주기적 타이머를 관리하는 직렬화 가능 타이머 클래스입니다.
/// </summary>
[Serializable]
public class Timer
{
    [FormerlySerializedAs("time")]
    [Tooltip("타이머 목표 시간 (초)")]
    [SerializeField] private float m_Duration = 1f;

    [FormerlySerializedAs("resetOnTime")]
    [Tooltip("시간 만료 시 0초로 자동 리셋할지 여부")]
    [SerializeField] private bool m_ResetOnTime = true;

    [FormerlySerializedAs("activeOnce")]
    [Tooltip("1회만 발동하고 이후 업데이트를 중지할지 여부")]
    [SerializeField] private bool m_ActiveOnce = false;

    private float m_CurrentTime;
    private int m_ActiveCount;

    #region Properties
    /// <summary>
    /// 타이머 목표 시간 (초)
    /// </summary>
    public float Duration
    {
        get => m_Duration;
        set => m_Duration = Mathf.Max(0f, value);
    }

    /// <summary>
    /// 현재 경과 시간 (초)
    /// </summary>
    public float CurrentTime
    {
        get => m_CurrentTime;
        set => m_CurrentTime = Mathf.Clamp(value, 0f, m_Duration);
    }

    /// <summary>
    /// 타이머 진행 비율 (0.0 ~ 1.0)
    /// </summary>
    public float Ratio
    {
        get
        {
            if (m_Duration <= 0f) return 1f; // [High Safety] 0 나눔(NaN) 예외 원천 차단
            return Mathf.Clamp01(m_CurrentTime / m_Duration);
        }
        set
        {
            m_CurrentTime = (m_Duration <= 0f) ? 0f : m_Duration * Mathf.Clamp01(value);
        }
    }

    /// <summary>
    /// 타이머 만료 여부
    /// </summary>
    public bool IsFinished => m_Duration > 0f ? m_CurrentTime >= m_Duration : true;

    public bool ResetOnTime
    {
        get => m_ResetOnTime;
        set => m_ResetOnTime = value;
    }

    public bool ActiveOnce
    {
        get => m_ActiveOnce;
        set => m_ActiveOnce = value;
    }
    #endregion

    public Timer() : this(1f) { }

    public Timer(float duration) : this(duration, true) { }

    public Timer(float duration, bool resetOnTime, bool activeOnce = false)
    {
        m_Duration = Mathf.Max(0f, duration);
        m_ResetOnTime = resetOnTime;
        m_ActiveOnce = activeOnce;
        m_CurrentTime = 0f;
        m_ActiveCount = 0;
    }

    /// <summary>
    /// 현재 경과 시간을 직접 설정합니다.
    /// </summary>
    public void SetCurrentTime(float currentTime)
    {
        m_CurrentTime = Mathf.Clamp(currentTime, 0f, m_Duration);
    }

    public float GetRemainingTime()
    {
        return Mathf.Max(0f, m_Duration - m_CurrentTime);
    }

    /// <summary>
    /// 경과 시간을 즉시 0초로 초기화합니다.
    /// </summary>
    public void Reset()
    {
        m_CurrentTime = 0f;
        m_ActiveCount = 0;
    }

    /// <summary>
    /// 매 프레임 시간을 누적하며 만료 여부를 검사합니다.
    /// </summary>
    /// <param name="deltaTime">Time.deltaTime 또는 Time.unscaledDeltaTime</param>
    /// <returns>타이머 만료 시 true 반환</returns>
    public bool Update(float deltaTime)
    {
        if (deltaTime <= 0f) return IsFinished;

        if (!m_ActiveOnce || m_ActiveCount <= 0)
        {
            m_CurrentTime += deltaTime;

            if (m_CurrentTime >= m_Duration)
            {
                m_CurrentTime = m_ResetOnTime ? 0f : m_Duration;
                m_ActiveCount++;
                return true;
            }
        }

        return false;
    }
}
#endif