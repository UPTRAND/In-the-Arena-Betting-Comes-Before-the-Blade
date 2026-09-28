using System.Collections.Generic;
using UnityEngine;

namespace InTheArena.UI
{
    public static class LobbyPlazaGeometry
    {
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        public static bool IsConvex(IReadOnlyList<Vector2> p)
        {
            if (p == null || p.Count < 3) return false;
            float sign = 0;
            for (int i = 0; i < p.Count; i++)
            {
                if (!float.IsFinite(p[i].x) || !float.IsFinite(p[i].y)) return false;
                Vector2 edge = p[(i + 1) % p.Count] - p[i];
                if (edge.sqrMagnitude < .00000001f) return false;
                for (int j = 0; j < p.Count; j++)
                {
                    float c = Cross(edge, p[j] - p[i]);
                    if (Mathf.Abs(c) < .000001f) continue;
                    if (sign == 0) sign = Mathf.Sign(c);
                    else if (c * sign < 0) return false;
                }
            }
            return sign != 0;
        }

        public static bool Contains(IReadOnlyList<Vector2> p, Vector2 point)
        {
            if (p == null || p.Count < 3) return false;
            float sign = 0;
            for (int i = 0; i < p.Count; i++)
            {
                float c = Cross(p[(i + 1) % p.Count] - p[i], point - p[i]);
                if (Mathf.Abs(c) < .001f) continue;
                if (sign == 0) sign = Mathf.Sign(c);
                else if (c * sign < 0) return false;
            }
            return true;
        }

        public static Vector2 Clamp(IReadOnlyList<Vector2> p, Vector2 point)
        {
            if (Contains(p, point)) return point;
            Vector2 best = p[0];
            float distance = float.MaxValue;
            for (int i = 0; i < p.Count; i++)
            {
                Vector2 a = p[i], d = p[(i + 1) % p.Count] - a;
                Vector2 candidate = a + d * Mathf.Clamp01(Vector2.Dot(point - a, d) / Mathf.Max(d.sqrMagnitude, .000001f));
                float next = (candidate - point).sqrMagnitude;
                if (next < distance) { best = candidate; distance = next; }
            }
            return best;
        }

        public static Vector2 Sample(IReadOnlyList<Vector2> p, System.Random random)
        {
            float total = 0f;
            for (int i = 1; i < p.Count - 1; i++) total += Mathf.Abs(Cross(p[i] - p[0], p[i + 1] - p[0]));
            float target = (float)random.NextDouble() * total;
            int triangle = p.Count - 2;
            for (int i = 1; i < p.Count - 1; i++)
            {
                target -= Mathf.Abs(Cross(p[i] - p[0], p[i + 1] - p[0]));
                if (target <= 0) { triangle = i; break; }
            }
            float u = Mathf.Sqrt((float)random.NextDouble()), v = (float)random.NextDouble();
            return (1 - u) * p[0] + u * (1 - v) * p[triangle] + u * v * p[triangle + 1];
        }

        // Sutherland-Hodgman clipping preserves convexity, including a resized viewport.
        public static List<Vector2> ClipToRect(IReadOnlyList<Vector2> polygon, Rect rect)
        {
            var result = new List<Vector2>(polygon);
            if (rect.width <= 0 || rect.height <= 0) { result.Clear(); return result; }
            for (int side = 0; side < 4 && result.Count > 0; side++)
            {
                var input = result;
                result = new List<Vector2>();
                Vector2 previous = input[input.Count - 1];
                float pd = Distance(previous, rect, side);
                foreach (var current in input)
                {
                    float cd = Distance(current, rect, side);
                    if ((cd >= 0) != (pd >= 0)) result.Add(Vector2.Lerp(previous, current, pd / (pd - cd)));
                    if (cd >= 0) result.Add(current);
                    previous = current; pd = cd;
                }
            }
            for (int i = result.Count - 1; i >= 0 && result.Count > 1; i--)
                if ((result[i] - result[(i + 1) % result.Count]).sqrMagnitude < .000001f) result.RemoveAt(i);
            return result;
        }

        private static float Distance(Vector2 p, Rect r, int side)
        {
            switch (side)
            {
                case 0: return p.x - r.xMin;
                case 1: return r.xMax - p.x;
                case 2: return p.y - r.yMin;
                default: return r.yMax - p.y;
            }
        }
    }
}
