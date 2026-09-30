using System.Collections.Generic;
using System;
using UnityEngine;

namespace InTheArena.UI
{
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class LobbyPlazaController : MonoBehaviour
    {
        [SerializeField] private LobbyPlazaProfile[] m_Profiles;
        [SerializeField] private RectTransform[] m_LowerControls;
        private readonly List<LobbyNpcView> m_Residents = new List<LobbyNpcView>();
        private readonly List<Vector2> m_Positions = new List<Vector2>();
        private readonly List<LobbyNpcView> m_Sorted = new List<LobbyNpcView>();
        private readonly Vector3[] m_Corners = new Vector3[4];
        private readonly System.Random m_Random = new System.Random();
        private readonly HashSet<string> m_Warnings = new HashSet<string>();
        private List<Vector2> m_Area = new List<Vector2>();
        private LobbyPlazaProfile m_Profile;
        private RectTransform m_Background;
        private RectTransform m_Rect;
        private CanvasGroup m_Group;
        private Rect m_LastBackgroundRect;
        private Rect m_LastSafeRect;
        private bool m_LayoutDirty = true;
        private bool m_Ready;
        private bool m_Paused;
        private bool m_Focused = true;
        private bool m_SkipFrame = true;
        private bool m_Spawned;
        private float m_SideMargin = 70f;
        private float m_HeadMargin = 130f;

        public IReadOnlyList<LobbyNpcView> Residents => m_Residents;
        public IReadOnlyList<Vector2> ActiveArea => m_Area;
        public LobbyPlazaProfile CurrentProfile => m_Profile;
        public event Action ResidentsChanged;

        public void SetContext(int stage, RectTransform background, bool ready)
        {
            EnsureReferences();
            m_Ready = ready;
            if (m_Background != background) { m_Background = background; m_LayoutDirty = true; }
            if (!ready) { m_Group.alpha = 0; return; }
            LobbyPlazaProfile next = LobbyPlazaProfile.Resolve(m_Profiles, stage);
            if (next == null) Warn("No valid plaza profile for stage " + stage);
            if (next == m_Profile) return;
            foreach (var resident in m_Residents)
            {
                resident.gameObject.SetActive(false);
                Destroy(resident.gameObject);
            }
            m_Residents.Clear(); m_Sorted.Clear();
            ResidentsChanged?.Invoke();
            m_Profile = next; m_LayoutDirty = true; m_Spawned = false;
            m_SideMargin = 70f; m_HeadMargin = 130f;
            if (m_Profile != null && m_Profile.Residents != null)
                foreach (var npc in m_Profile.Residents)
                    if (npc?.Visual != null && npc.Visual.IsValid)
                    {
                        MeasureVisual(npc.Visual, npc.Visual.Idle);
                        MeasureVisual(npc.Visual, npc.Visual.Walk);
                    }
        }

        private void MeasureVisual(LobbyNpcVisualData visual, LobbyNpcAnimation animation)
        {
            foreach (var sprite in animation.Frames)
            {
                m_SideMargin = Mathf.Max(m_SideMargin, Mathf.Max(visual.FootPixel.x, sprite.rect.width - visual.FootPixel.x) * visual.PixelScale);
                m_HeadMargin = Mathf.Max(m_HeadMargin, (sprite.rect.height - visual.FootPixel.y) * visual.PixelScale);
            }
        }

        private void EnsureReferences()
        {
            if (m_Rect == null) m_Rect = (RectTransform)transform;
            if (m_Group == null) m_Group = GetComponent<CanvasGroup>();
            m_Group.interactable = false; m_Group.blocksRaycasts = false;
        }

        private void OnEnable()
        {
            EnsureReferences(); m_SkipFrame = true; m_LayoutDirty = true;
            // Focus/pause callbacks can be missed while the Stage tab is inactive.
            m_Focused = Application.isFocused; m_Paused = false;
        }
        private void OnApplicationPause(bool paused) { m_Paused = paused; m_SkipFrame = true; }
        private void OnApplicationFocus(bool focused) { m_Focused = focused; m_SkipFrame = true; }

        private void LateUpdate()
        {
            if (!m_Ready || m_Profile == null || m_Background == null) { m_Group.alpha = 0; return; }
            UpdateArea();
            bool visible = m_Area.Count >= 3;
            m_Group.alpha = visible ? 1f : 0f;
            if (!visible) return;
            if (!m_Spawned) Spawn();
            if (m_Paused || !m_Focused) return;
            if (m_SkipFrame) { m_SkipFrame = false; return; }
            Simulate(Mathf.Min(Time.unscaledDeltaTime, .05f));
        }

        private void UpdateArea()
        {
            Rect background = LocalBounds(m_Background);
            Rect safe = m_Rect.rect;
            var canvas = GetComponentInParent<Canvas>();
            UnityEngine.Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_Rect, Screen.safeArea.min, camera, out Vector2 screenMin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_Rect, Screen.safeArea.max, camera, out Vector2 screenMax);
            safe = Rect.MinMaxRect(Mathf.Max(safe.xMin, screenMin.x), Mathf.Max(safe.yMin, screenMin.y),
                Mathf.Min(safe.xMax, screenMax.x), Mathf.Min(safe.yMax, screenMax.y));
            // Reserve room for the entire sprite, not just its foot anchor.
            safe.xMin += m_SideMargin; safe.xMax -= m_SideMargin; safe.yMin += 20f; safe.yMax -= m_HeadMargin + 200f;
            if (m_LowerControls != null)
                foreach (var control in m_LowerControls)
                    if (control != null && control.gameObject.activeInHierarchy)
                        safe.yMin = Mathf.Max(safe.yMin, LocalBounds(control).yMax + 30f);
            if (!m_LayoutDirty && background == m_LastBackgroundRect && safe == m_LastSafeRect) return;
            m_LayoutDirty = false; m_LastBackgroundRect = background; m_LastSafeRect = safe;
            var mapped = new List<Vector2>();
            foreach (Vector2 uv in m_Profile.WalkArea)
                mapped.Add(background.min + Vector2.Scale(uv, background.size));
            m_Area = LobbyPlazaGeometry.ClipToRect(mapped, safe);
            if (m_Area.Count >= 3 && !LobbyPlazaGeometry.IsConvex(m_Area)) m_Area.Clear();
            if (m_Area.Count < 3) return;
            foreach (var resident in m_Residents)
            {
                resident.Motion.Constrain(m_Area);
                resident.Present(0f);
            }
        }

        private Rect LocalBounds(RectTransform target)
        {
            target.GetWorldCorners(m_Corners);
            Vector2 min = m_Rect.InverseTransformPoint(m_Corners[0]);
            Vector2 max = min;
            for (int i = 1; i < 4; i++)
            {
                Vector2 point = m_Rect.InverseTransformPoint(m_Corners[i]);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void Spawn()
        {
            m_Spawned = true;
            if (m_Profile.Residents == null) return;
            var ids = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var definition in m_Profile.Residents)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id) || definition.Visual == null ||
                    !definition.Visual.IsValid || !ids.Add(definition.Id))
                { Warn("Skipped invalid or duplicate NPC in " + m_Profile.name); continue; }
                Vector2 position = Vector2.zero;
                bool found = false;
                for (int attempt = 0; attempt < 100; attempt++)
                {
                    position = LobbyPlazaGeometry.Sample(m_Area, m_Random);
                    found = true;
                    foreach (var resident in m_Residents)
                        if (Vector2.Distance(position, resident.Motion.Position) < m_Profile.Separation) { found = false; break; }
                    if (found) break;
                }
                if (!found) { Warn("Insufficient spawn space for " + definition.Id); continue; }
                var go = new GameObject(definition.Id, typeof(RectTransform), typeof(LobbyNpcView));
                go.transform.SetParent(transform, false);
                var view = go.GetComponent<LobbyNpcView>();
                float speed = Mathf.Lerp(m_Profile.Speed.x, m_Profile.Speed.y, (float)m_Random.NextDouble());
                view.Initialize(definition, new LobbyNpcMotion(position, speed, m_Profile.WaitSeconds, m_Random));
                m_Residents.Add(view); m_Sorted.Add(view);
            }
            SortDepth();
            ResidentsChanged?.Invoke();
        }

        private void Simulate(float dt)
        {
            m_Positions.Clear();
            foreach (var resident in m_Residents) m_Positions.Add(resident.Motion.Position);
            for (int i = 0; i < m_Residents.Count; i++)
            {
                var resident = m_Residents[i];
                resident.Motion.Tick(dt, m_Area, LobbyNpcMotion.Avoidance(i, m_Positions, m_Profile.Separation));
                resident.Present(dt);
            }
            SortDepth();
        }

        private void SortDepth()
        {
            m_Sorted.Sort((a, b) =>
            {
                int order = b.Motion.Position.y.CompareTo(a.Motion.Position.y);
                return order != 0 ? order : string.CompareOrdinal(a.NpcId, b.NpcId);
            });
            for (int i = 0; i < m_Sorted.Count; i++)
                if (m_Sorted[i].transform.GetSiblingIndex() != i) m_Sorted[i].transform.SetSiblingIndex(i);
        }

        private void Warn(string message)
        {
            if (m_Warnings.Add(message)) Debug.LogWarning("[LobbyPlaza] " + message, this);
        }
    }
}
