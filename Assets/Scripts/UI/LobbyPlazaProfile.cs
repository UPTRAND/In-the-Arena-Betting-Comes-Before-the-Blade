using System;
using System.Collections.Generic;
using UnityEngine;

namespace InTheArena.UI
{
    [Serializable]
    public sealed class LobbyNpcDefinition
    {
        public string Id;
        public LobbyNpcVisualData Visual;
    }

    [CreateAssetMenu(menuName = "In The Arena/Lobby/Plaza Profile")]
    public sealed class LobbyPlazaProfile : ScriptableObject
    {
        public int FirstStage = 1;
        public int LastStage = 5;
        public Sprite Background;
        public List<LobbyNpcDefinition> Residents = new List<LobbyNpcDefinition>();
        [Tooltip("Convex polygon in background UV coordinates; origin is bottom-left.")]
        public Vector2[] WalkArea = { new Vector2(.15f, .28f), new Vector2(.85f, .28f),
            new Vector2(.80f, .60f), new Vector2(.20f, .60f) };
        public Vector2 Speed = new Vector2(35f, 55f);
        public Vector2 WaitSeconds = new Vector2(2f, 5f);
        [Min(1f)] public float Separation = 70f;

        public bool IsValid => FirstStage > 0 && LastStage >= FirstStage &&
            ValidRange(Speed, .1f) && ValidRange(WaitSeconds, 0f) &&
            float.IsFinite(Separation) && Separation > 0f && LobbyPlazaGeometry.IsConvex(WalkArea);

        private static bool ValidRange(Vector2 value, float min) => float.IsFinite(value.x) &&
            float.IsFinite(value.y) && value.x >= min && value.y >= value.x;

        public static LobbyPlazaProfile Resolve(IReadOnlyList<LobbyPlazaProfile> profiles, int stage)
        {
            LobbyPlazaProfile last = null;
            if (profiles == null) return null;
            foreach (var profile in profiles)
            {
                if (profile == null || !profile.IsValid) continue;
                if (stage >= profile.FirstStage && stage <= profile.LastStage) return profile;
                if (last == null || profile.LastStage > last.LastStage) last = profile;
            }
            return last != null && stage > last.LastStage ? last : null;
        }
    }
}
