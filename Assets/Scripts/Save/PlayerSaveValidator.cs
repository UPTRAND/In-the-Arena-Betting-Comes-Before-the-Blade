using System;

namespace InTheArena.Save
{
    public interface IClock
    {
        DateTime UtcNow { get; }
    }

    public class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    public static class PlayerSaveValidator
    {
        public const int CurrentSchemaVersion = 2;

        public static bool ValidateAndNormalize(PlayerSaveEnvelope envelope, IClock clock)
        {
            if (envelope == null) return false;

            if (envelope.schemaVersion > CurrentSchemaVersion)
            {
                UnityEngine.Debug.LogError($"[PlayerSaveValidator] 지원하지 않는 미래 스키마 버전({envelope.schemaVersion})입니다. 현재 지원 버전: {CurrentSchemaVersion}");
                return false;
            }

            if (envelope.payload == null)
            {
                envelope.payload = new PlayerSavePayload();
            }

            var payload = envelope.payload;
            payload.clearedStageNumber = Math.Max(0, payload.clearedStageNumber);
            payload.gold = Math.Max(0, payload.gold);
            payload.hearts = Math.Clamp(payload.hearts, 0, 5); // MaxHearts는 보통 SaveManager에 있지만 임시로 5로 고정 또는 Repository에서 검증
            payload.stars = Math.Max(0, payload.stars);

            long nowTicks = clock.UtcNow.Ticks;
            if (payload.lastHeartRecoveryUtcTicks > nowTicks)
            {
                TimeSpan diff = TimeSpan.FromTicks(payload.lastHeartRecoveryUtcTicks - nowTicks);
                if (diff.TotalMinutes > 5)
                {
                    UnityEngine.Debug.LogWarning($"[PlayerSaveValidator] 저장된 하트 회복 시간이 현재 시간보다 5분 이상 미래입니다. 차이: {diff.TotalMinutes}분. 현재 시간으로 리셋합니다.");
                }
                // 정책: 어떠한 미래 시간이든 항상 현재 시간으로 클램핑 (5분은 로깅 임계값일 뿐)
                payload.lastHeartRecoveryUtcTicks = nowTicks;
            }
            else if (payload.lastHeartRecoveryUtcTicks <= 0)
            {
                payload.lastHeartRecoveryUtcTicks = nowTicks;
            }

            return true;
        }
    }
}
