using System;
using UnityEngine;

namespace InTheArena.UI
{
    [Serializable]
    public sealed class LobbyNpcAnimation
    {
        public Sprite[] Frames = Array.Empty<Sprite>();
        public float[] Times = Array.Empty<float>();
        public float Duration;

        public bool IsValid
        {
            get
            {
                if (Frames == null || Times == null || Frames.Length == 0 || Frames.Length != Times.Length ||
                    !float.IsFinite(Duration) || Duration <= 0f) return false;
                for (int i = 0; i < Frames.Length; i++)
                    if (Frames[i] == null || !float.IsFinite(Times[i]) || Times[i] < 0f || Times[i] > Duration ||
                        (i > 0 && Times[i] < Times[i - 1])) return false;
                return Times[0] == 0f;
            }
        }

        public Sprite Sample(float time)
        {
            time = Mathf.Repeat(time, Duration);
            for (int i = Times.Length - 1; i >= 0; i--)
                if (time >= Times[i]) return Frames[i];
            return Frames[0];
        }
    }

    [CreateAssetMenu(menuName = "In The Arena/Lobby/NPC Visual")]
    public sealed class LobbyNpcVisualData : ScriptableObject
    {
        public LobbyNpcAnimation Idle = new LobbyNpcAnimation();
        public LobbyNpcAnimation Walk = new LobbyNpcAnimation();
        [Tooltip("Opaque foot position in source sprite pixels, measured from bottom-left.")]
        public Vector2 FootPixel;
        [Min(0.1f)] public float PixelScale = 5f;
        public bool FacesRight = true;
        public bool IsValid => Idle != null && Walk != null && Idle.IsValid && Walk.IsValid &&
            float.IsFinite(PixelScale) && PixelScale > 0f && float.IsFinite(FootPixel.x) && float.IsFinite(FootPixel.y);
    }
}
