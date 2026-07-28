#if UNITY_6000_0_OR_NEWER
using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InTheArena.MainGame
{
    /// <summary>
    /// Unity 6 Awaitable 지원 확장을 위한 헬퍼 클래스
    /// </summary>
    public static class AwaitableExtensions
    {
        /// <summary>
        /// AsyncOperation을 Awaitable로 변환
        /// </summary>
        public static Awaitable ToAwaitable(this AsyncOperation operation)
        {
            var awaitable = new AwaitableCompletionSource();
            
            if (operation.isDone)
            {
                awaitable.TrySetResult();
                return awaitable.Awaitable;
            }

            // 완료될 때까지 매 프레임 체크
            var routine = WaitForCompletion(operation, awaitable);
            return awaitable.Awaitable;
        }

        private static async Awaitable WaitForCompletion(AsyncOperation operation, AwaitableCompletionSource source)
        {
            while (!operation.isDone)
            {
                await Awaitable.NextFrameAsync();
            }
            source.TrySetResult();
        }
    }
}
#endif