using System.Collections.Generic;
using UnityEngine;

namespace InTheArena.UI
{
    // No scene, save, animation or combat dependencies: coordinates are plaza-local pixels.
    public sealed class LobbyNpcMotion
    {
        public Vector2 Position { get; private set; }
        public Vector2 Destination { get; private set; }
        public bool Walking { get; private set; }
        public bool FacingRight { get; private set; } = true;
        public int RerouteCount { get; private set; }
        private readonly System.Random m_Random;
        private readonly float m_Speed;
        private readonly Vector2 m_WaitRange;
        private float m_Wait;
        private float m_ProgressTime;
        private float m_LastDistance;

        public LobbyNpcMotion(Vector2 position, float speed, Vector2 waitRange, System.Random random)
        {
            Position = Destination = position;
            m_Speed = speed; m_WaitRange = waitRange; m_Random = random;
            m_Wait = RandomWait();
        }

        public void Constrain(IReadOnlyList<Vector2> area)
        {
            Position = LobbyPlazaGeometry.Clamp(area, Position);
            Destination = LobbyPlazaGeometry.Clamp(area, Destination);
        }

        public void Tick(float dt, IReadOnlyList<Vector2> area, Vector2 avoidance)
        {
            dt = Mathf.Clamp(dt, 0f, .05f);
            if (dt == 0) return;
            Vector2 old = Position;
            if (m_Wait <= 0f && Vector2.Distance(Position, Destination) < 3f) PickDestination(area);
            if (m_Wait > 0)
            {
                m_Wait -= dt;
                if (m_Wait <= 0) PickDestination(area);
            }
            else
            {
                Vector2 delta = Vector2.ClampMagnitude(Destination - Position, m_Speed * dt);
                Position = LobbyPlazaGeometry.Clamp(area, Position + delta + avoidance * dt);
                float distance = Vector2.Distance(Position, Destination);
                if (distance < 3f) { m_Wait = RandomWait(); m_ProgressTime = 0; }
                else
                {
                    m_ProgressTime += dt;
                    if (m_ProgressTime >= 2f)
                    {
                        if (m_LastDistance - distance < m_Speed * .2f) { PickDestination(area); RerouteCount++; }
                        m_LastDistance = Vector2.Distance(Position, Destination);
                        m_ProgressTime = 0;
                    }
                }
            }
            Vector2 movement = Position - old;
            Walking = movement.sqrMagnitude > .0001f;
            if (Mathf.Abs(movement.x) > .01f) FacingRight = movement.x > 0;
        }

        private float RandomWait() => Mathf.Lerp(m_WaitRange.x, m_WaitRange.y, (float)m_Random.NextDouble());
        private void PickDestination(IReadOnlyList<Vector2> area)
        {
            for (int i = 0; i < 12; i++)
            {
                Destination = LobbyPlazaGeometry.Sample(area, m_Random);
                if (Vector2.Distance(Position, Destination) >= 80f) break;
            }
            m_LastDistance = Vector2.Distance(Position, Destination);
            m_ProgressTime = 0;
        }

        public static Vector2 Avoidance(int index, IReadOnlyList<Vector2> positions, float separation)
        {
            Vector2 force = Vector2.zero;
            for (int j = 0; j < positions.Count; j++)
            {
                if (j == index) continue;
                Vector2 delta = positions[index] - positions[j];
                float distance = delta.magnitude;
                if (distance >= separation) continue;
                Vector2 direction = distance > .01f ? delta / distance : (index < j ? Vector2.left : Vector2.right);
                force += direction * (1 - distance / separation) * 65f;
            }
            return Vector2.ClampMagnitude(force, 65f);
        }
    }
}
