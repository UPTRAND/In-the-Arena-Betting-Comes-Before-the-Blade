#if UNITY_6000_0_OR_NEWER
using System;
using System.Threading;
using UnityEngine;

namespace InTheArena.MainGame
{
    /// <summary>
    /// 라운드 페이즈 기본 추상 클래스
    /// 각 페이즈(Betting, Battle, Result)는 이를 상속받아 구현
    /// </summary>
    public abstract class RoundPhaseBase : MonoBehaviour
    {
        protected RoundContext Context { get; private set; }
        public bool IsPhaseCompleted { get; protected set; }

        /// <summary>
        /// 페이즈 초기화
        /// </summary>
        /// <param name="context">라운드 컨텍스트</param>
        public virtual void InitializePhase(RoundContext context)
        {
            Context = context;
            IsPhaseCompleted = false;
        }

        /// <summary>
        /// 페이즈 준비 비동기 처리 (화면 전환 전 대기)
        /// </summary>
        /// <param name="token">취소 토큰</param>
#pragma warning disable 1998
        public virtual async Awaitable PreparePhaseAsync(CancellationToken token)
        {
            // 빈 구현체로 즉시 완료 반환
        }
#pragma warning restore 1998

        /// <summary>
        /// 페이즈 진입 및 실행 비동기 처리
        /// </summary>
        /// <param name="token">취소 토큰</param>
        public abstract Awaitable EnterPhaseAsync(CancellationToken token);

        /// <summary>
        /// 페이즈 종료 시 비동기 처리
        /// </summary>
        /// <param name="token">취소 토큰</param>
        public abstract Awaitable ExitPhaseAsync(CancellationToken token);

        /// <summary>
        /// 페이즈 완료 처리
        /// </summary>
        protected void CompletePhase()
        {
            IsPhaseCompleted = true;
        }
    }
}
#endif