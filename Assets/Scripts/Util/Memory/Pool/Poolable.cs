#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using DG.Tweening;

[DisallowMultipleComponent]
public abstract class Poolable : MonoBehaviour
{
    [Header("Pool Configuration")]
    [Tooltip("기본 풀 생성 개수")]
    [SerializeField] private int m_PoolSize = 10;

    public int poolSize
    {
        get => m_PoolSize;
        set => m_PoolSize = value;
    }

    /// <summary>
    /// 현재 오브젝트가 씬 상에 스폰되어 활성화된 상태인지 추적합니다.
    /// </summary>
    public bool IsSpawned { get; private set; }

    private Transform m_CachedTransform;
    public Transform CachedTransform
    {
        get
        {
            if (m_CachedTransform == null)
            {
                m_CachedTransform = transform;
            }
            return m_CachedTransform;
        }
    }

    /// <summary>
    /// 오브젝트가 풀에서 꺼내져 활성화될 때 호출되는 초기화 메서드입니다.
    /// </summary>
    public abstract void OnSpawn();

    /// <summary>
    /// 오브젝트가 풀로 반환되어 비활성화될 때 호출되는 정리 메서드입니다.
    /// </summary>
    public abstract void OnDespawn();

    /// <summary>
    /// 풀 팩토리에서 GameObject의 활성화 상태 및 스폰 상태 플래그를 변경합니다.
    /// </summary>
    public virtual void GameObjectSetActive(bool value)
    {
        IsSpawned = value;
        gameObject.SetActive(value);
    }

    /// <summary>
    /// 오브젝트를 안전하게 풀로 반환합니다. (중복 호출 차단 및 DOTween 잔여 트윈 해제)
    /// </summary>
    public virtual void Despawn()
    {
        // 이미 Despawn 상태이거나 비활성화된 경우 이중 호출 차단
        if (!IsSpawned && !gameObject.activeSelf) return;

        IsSpawned = false;

        // [High Safety] DOTween: 풀로 반환되기 전 진행 중인 트윈 정지 (안드로이드 백그라운드/Null 에러 방지)
        KillActiveTweens();

        try
        {
            OnDespawn();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        gameObject.SetActive(false);
    }

    /// <summary>
    /// 바인딩된 DOTween 애니메이션을 안전하게 정지(Kill)합니다.
    /// </summary>
    protected virtual void KillActiveTweens()
    {
        CachedTransform.DOKill();
    }

    protected virtual void OnDestroy()
    {
        IsSpawned = false;
        KillActiveTweens();
    }
}
#endif