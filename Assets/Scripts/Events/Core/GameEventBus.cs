using System;
using System.Collections.Generic;
using UnityEngine;

namespace InTheArena.Events.Core
{
    public interface IGameEvent
    {
        string EventId { get; }
        long OccurredAtUtcTicks { get; }
    }

    public interface IGameEventBus
    {
        void Publish<TEvent>(TEvent eventData) where TEvent : IGameEvent;
        IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent;
    }

    /// <summary>
    /// 이벤트를 타입별 FIFO 순서로 전달하며 재진입 발행을 안전하게 대기열에 넣습니다.
    /// </summary>
    public sealed class GameEventBus : IGameEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> m_Handlers = new Dictionary<Type, List<Delegate>>();
        private readonly Queue<IGameEvent> m_PendingEvents = new Queue<IGameEvent>();
        private bool m_IsPublishing;

        /// <summary>
        /// 이벤트를 등록 순서대로 전달합니다. 한 구독자의 예외는 다른 구독자를 막지 않습니다.
        /// </summary>
        public void Publish<TEvent>(TEvent eventData) where TEvent : IGameEvent
        {
            if (eventData == null)
            {
                throw new ArgumentNullException(nameof(eventData));
            }

            m_PendingEvents.Enqueue(eventData);
            if (m_IsPublishing)
            {
                return;
            }

            m_IsPublishing = true;
            try
            {
                while (m_PendingEvents.Count > 0)
                {
                    Dispatch(m_PendingEvents.Dequeue());
                }
            }
            finally
            {
                m_IsPublishing = false;
            }
        }

        /// <summary>
        /// 지정 타입의 이벤트 처리기를 등록하고 해제 가능한 핸들을 반환합니다.
        /// </summary>
        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            Type eventType = typeof(TEvent);
            if (!m_Handlers.TryGetValue(eventType, out List<Delegate> handlers))
            {
                handlers = new List<Delegate>();
                m_Handlers.Add(eventType, handlers);
            }

            handlers.Add(handler);
            return new Subscription(this, eventType, handler);
        }

        /// <summary>
        /// 현재 타입의 구독자 스냅샷에 이벤트를 순서대로 전달합니다.
        /// </summary>
        private void Dispatch(IGameEvent eventData)
        {
            Type eventType = eventData.GetType();
            if (!m_Handlers.TryGetValue(eventType, out List<Delegate> handlers))
            {
                return;
            }

            Delegate[] snapshot = handlers.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    snapshot[i].DynamicInvoke(eventData);
                }
                catch (Exception exception)
                {
                    Exception reported = exception.InnerException ?? exception;
                    Debug.LogException(reported);
                }
            }
        }

        /// <summary>
        /// 지정 타입에서 처리기 하나를 제거하고 빈 목록을 정리합니다.
        /// </summary>
        private void Unsubscribe(Type eventType, Delegate handler)
        {
            if (!m_Handlers.TryGetValue(eventType, out List<Delegate> handlers))
            {
                return;
            }

            handlers.Remove(handler);
            if (handlers.Count == 0)
            {
                m_Handlers.Remove(eventType);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private GameEventBus m_Owner;
            private readonly Type m_EventType;
            private readonly Delegate m_Handler;

            /// <summary>
            /// 해제에 필요한 버스, 타입과 처리기를 보관합니다.
            /// </summary>
            public Subscription(GameEventBus owner, Type eventType, Delegate handler)
            {
                m_Owner = owner;
                m_EventType = eventType;
                m_Handler = handler;
            }

            /// <summary>
            /// 등록한 처리기를 한 번만 해제합니다.
            /// </summary>
            public void Dispose()
            {
                if (m_Owner == null)
                {
                    return;
                }

                m_Owner.Unsubscribe(m_EventType, m_Handler);
                m_Owner = null;
            }
        }
    }
}
