#if UNITY_6000_0_OR_NEWER
using System.Threading;
using UnityEngine;

public abstract class RoundPhaseBase : MonoBehaviour
{
    protected RoundContext Context { get; private set; }
    public bool IsPhaseCompleted { get; protected set; }

    public virtual void InitializePhase(RoundContext context)
    {
        Context = context;
        IsPhaseCompleted = false;
    }

    /// <summary>
    /// 페이즈 진입 시 실행되는 비동기 초기화 메서드
    /// </summary>
    public abstract Awaitable EnterPhaseAsync(CancellationToken token);

    /// <summary>
    /// 페이즈 종료 시 리소스 정리를 수행하는 비동기 메서드
    /// </summary>
    public abstract Awaitable ExitPhaseAsync(CancellationToken token);
}
#endif