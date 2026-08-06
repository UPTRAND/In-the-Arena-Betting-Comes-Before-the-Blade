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
        public static async Awaitable ToAwaitable(this AsyncOperation operation)
        {
            if (operation == null || operation.isDone)
            {
                return;
            }

            // 완료될 때까지 매 프레임 체크
            while (!operation.isDone)
            {
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
#endif
