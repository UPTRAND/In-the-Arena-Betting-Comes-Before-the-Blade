using UnityEngine;

namespace InTheArena.Save
{
    public static class PlayerSaveMigrator
    {
        public static PlayerSavePayload MigrateFromV1(LegacyPlayerDataV1 v1Data, IClock clock)
        {
            if (v1Data == null) return null;

            if (v1Data.itemCounts != null && v1Data.itemCounts.Length > 0)
            {
                Debug.Log("[PlayerSaveMigrator] Legacy V1 itemCounts는 현재 게임 디자인에서 영구 아이템 인벤토리를 사용하지 않으므로 V2로 이전하지 않습니다. (폐기됨)");
            }

            var payload = new PlayerSavePayload
            {
                clearedStageNumber = v1Data.clearedStageNumber,
                gold = v1Data.gold,
                hearts = v1Data.hearts,
                stars = v1Data.stars,
                lastHeartRecoveryUtcTicks = v1Data.lastHeartRecoveryUtcTicks
            };

            // Normalize before saving so we don't save out-of-bounds legacy data
            var tempEnv = new PlayerSaveEnvelope { payload = payload };
            PlayerSaveValidator.ValidateAndNormalize(tempEnv, clock);

            return tempEnv.payload;
        }
    }
}
