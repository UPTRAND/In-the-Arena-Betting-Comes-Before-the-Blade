
using System;
using UnityEngine;

#nullable disable
public abstract class Manager_Base : MonoBehaviour, IComparable<Manager_Base>
{
    [Tooltip("매니저 초기화 순서 (낮을수록 먼저 초기화됩니다)")]
    public virtual ushort InitializationOrder { get; protected set; }

    /// <summary>
    /// 매니저의 초기화 완료 상태를 추적하여 중복 초기화를 방지합니다.
    /// </summary>
    public bool IsInitialized { get; private set; }

    public virtual bool Setup() => true;

    protected abstract bool Init();

    /// <summary>
    /// 외부에서 호출할 때 사용하는 안전한 초기화 래퍼
    /// </summary>
    public bool TryInitialize()
    {
        if (IsInitialized) return true;

        if (!Setup())
        {
            Debug.LogError($"[{gameObject.name}] ManagerBase Setup Failed.");
            return false;
        }

        IsInitialized = Init();
        return IsInitialized;
    }

    public virtual void Release()
    {
        IsInitialized = false;
    }

    /// <summary>
    /// MonoBehaviour 파괴 시 안전성 보장
    /// </summary>
    protected virtual void OnDestroy()
    {
        if (IsInitialized)
        {
            Release();
        }
    }

    public int CompareTo(Manager_Base other)
    {
        // 레퍼런스 비교
        if (object.ReferenceEquals(this, other))
        {
            return 0;
        }

        // 비교 대상이 null일 경우
        if (object.ReferenceEquals(other, null))
        {
            return 1; // null보다 other가 더 뒤로 가도록 설정
        }

        // 초기화 순서 비교
        int orderCompare = InitializationOrder.CompareTo(other.InitializationOrder);
        if (orderCompare != 0)
        {
            return orderCompare;
        }

        return this.GetEntityId().CompareTo(other.GetEntityId());
    }
}