#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;

namespace InTheArena.Util
{
    public interface ILoadingProgressSession : IDisposable
    {
        void Report(float progress);
        void Complete();
    }

    public interface ILoadingProgressService
    {
        float Progress { get; }
        bool IsLoading { get; }

        event Action<float> ProgressChanged;
        event Action<bool> LoadingStateChanged;

        ILoadingProgressSession BeginSession();
    }

    [DisallowMultipleComponent]
    public sealed class LoadingProgressService : MonoBehaviour, ILoadingProgressService
    {
        private static LoadingProgressService _instance;
        public static LoadingProgressService Instance
        {
            get
            {
                EnsureInstanceExists();
                return _instance;
            }
        }

        private float _progress;
        public float Progress => _progress;
        
        private bool _isLoading;
        public bool IsLoading => _isLoading;

        public event Action<float> ProgressChanged;
        public event Action<bool> LoadingStateChanged;

        private int _sessionSequence = 0;
        private int _activeSessionId = 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            EnsureInstanceExists();
        }

        private static void EnsureInstanceExists()
        {
            if (_instance != null) return;

            _instance = FindAnyObjectByType<LoadingProgressService>();
            
            if (_instance != null) return;

            var go = new GameObject("[LoadingProgressService]");
            _instance = go.AddComponent<LoadingProgressService>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (!ReferenceEquals(_instance, null) && _instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
        }

        public ILoadingProgressSession BeginSession()
        {
            if (_activeSessionId != 0)
            {
                throw new InvalidOperationException("A loading progress session is already active.");
            }

            _sessionSequence++;
            _activeSessionId = _sessionSequence;
            
            _progress = 0f;
            SetIsLoading(true);
            NotifyProgressChanged();

            return new LoadingProgressSession(this, _activeSessionId);
        }

        internal void Report(int sessionId, float progress)
        {
            if (_activeSessionId != sessionId || _activeSessionId == 0) return;

            // Invariant: Progress must be clamped between 0 and 1, and monotonically increasing
            float clamped = Mathf.Clamp01(progress);
            float nextProgress = Mathf.Max(_progress, clamped);

            if (Mathf.Approximately(nextProgress, _progress)) return;

            _progress = nextProgress;
            NotifyProgressChanged();
        }

        internal void Complete(int sessionId)
        {
            if (_activeSessionId != sessionId || _activeSessionId == 0) return;

            _progress = 1f;
            NotifyProgressChanged();
            // Note: We don't set IsLoading to false here. Dispose() will handle it.
        }

        internal void DisposeSession(int sessionId)
        {
            if (_activeSessionId != sessionId || _activeSessionId == 0) return;

            _activeSessionId = 0;
            SetIsLoading(false);
        }

        private void SetIsLoading(bool isLoading)
        {
            if (_isLoading != isLoading)
            {
                _isLoading = isLoading;
                LoadingStateChanged?.Invoke(_isLoading);
            }
        }

        private void NotifyProgressChanged()
        {
            ProgressChanged?.Invoke(_progress);
        }

        private class LoadingProgressSession : ILoadingProgressSession
        {
            private readonly LoadingProgressService _service;
            private readonly int _sessionId;
            private bool _disposed;

            public LoadingProgressSession(LoadingProgressService service, int sessionId)
            {
                _service = service;
                _sessionId = sessionId;
            }

            public void Report(float progress)
            {
                if (_disposed) return;
                _service.Report(_sessionId, progress);
            }

            public void Complete()
            {
                if (_disposed) return;
                _service.Complete(_sessionId);
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _service.DisposeSession(_sessionId);
            }
        }
    }
}
#endif
