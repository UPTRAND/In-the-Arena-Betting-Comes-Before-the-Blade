using System;

namespace InTheArena.Save
{
    [Serializable]
    public sealed class PlayerSaveEnvelope
    {
        public int schemaVersion;
        public int revision;
        public long savedAtUtcTicks;
        public PlayerSavePayload payload;
        public string checksum;
    }
}
