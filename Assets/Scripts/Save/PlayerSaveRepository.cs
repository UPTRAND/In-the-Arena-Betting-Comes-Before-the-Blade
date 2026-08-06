using UnityEngine;
using System.IO;

namespace InTheArena.Save
{
    public enum SaveLoadStatus
    {
        Success,
        RecoveredFromBackup,
        RecoveredFromBackupWithRepairWarning,
        Migrated,
        MigratedWithMarkerWarning,
        CreatedDefaults,
        UnsupportedFutureVersion,
        Corrupted,
        IoFailure
    }

    public class SaveLoadResult
    {
        public PlayerProgressState State { get; set; }
        public SaveLoadStatus Status { get; set; }
        public string Warning { get; set; }
    }

    public interface IPlayerSaveRepository
    {
        SaveLoadResult LoadOrCreate(PlayerProgressState defaultCandidate);
        bool TrySave(PlayerProgressState candidate, out string error);
    }

    public class PlayerSaveRepository : IPlayerSaveRepository
    {
        private readonly JsonFileSaveStorage m_Storage;
        private readonly LegacyPlayerPrefsV1Importer m_Importer;
        private readonly IClock m_Clock;

        private int m_CurrentRevision = 0;

        public PlayerSaveRepository(string saveDirectory, string fileName, IClock clock = null, IFileSystem fileSystem = null)
        {
            m_Storage = new JsonFileSaveStorage(saveDirectory, fileName, fileSystem);
            m_Importer = new LegacyPlayerPrefsV1Importer(Path.Combine(saveDirectory, "migration-v1.done"), fileSystem);
            m_Clock = clock ?? new SystemClock();
        }

        public SaveLoadResult LoadOrCreate(PlayerProgressState defaultCandidate)
        {
            var mainResult = m_Storage.LoadMain();
            var bakResult = m_Storage.LoadBackup();

            if (mainResult.Status == SaveFileReadStatus.UnsupportedFutureVersion ||
                bakResult.Status == SaveFileReadStatus.UnsupportedFutureVersion)
            {
                return new SaveLoadResult { Status = SaveLoadStatus.UnsupportedFutureVersion };
            }

            if (mainResult.Status == SaveFileReadStatus.IoFailure ||
                bakResult.Status == SaveFileReadStatus.IoFailure)
            {
                return new SaveLoadResult { Status = SaveLoadStatus.IoFailure };
            }

            PlayerSaveEnvelope envelope = null;
            SaveLoadStatus finalStatus = SaveLoadStatus.Success;
            string warning = null;

            bool mainValid = mainResult.Status == SaveFileReadStatus.Success;
            bool mainCorrupt = IsCorrupt(mainResult.Status);
            bool mainMissing = mainResult.Status == SaveFileReadStatus.Missing;

            bool bakValid = bakResult.Status == SaveFileReadStatus.Success;
            bool bakCorrupt = IsCorrupt(bakResult.Status);
            bool bakMissing = bakResult.Status == SaveFileReadStatus.Missing;

            if (mainValid)
            {
                envelope = mainResult.Envelope;
                finalStatus = SaveLoadStatus.Success;
            }
            else if ((mainCorrupt || mainMissing) && bakValid)
            {
                envelope = bakResult.Envelope;
                
                if (m_Storage.TryRepairMainFromBackup(out string repairError))
                {
                    finalStatus = SaveLoadStatus.RecoveredFromBackup;
                }
                else
                {
                    finalStatus = SaveLoadStatus.RecoveredFromBackupWithRepairWarning;
                    warning = repairError;
                }
            }
            else if (mainMissing && bakMissing)
            {
                if (m_Importer.HasLegacyData())
                {
                    var importResult = m_Importer.Import();
                    if (importResult.Status == LegacyImportStatus.Success)
                    {
                        var legacy = importResult.Data;
                        var migratedState = PlayerSaveMigrator.MigrateFromV1(legacy, m_Clock);
                        var tempEnv = new PlayerSaveEnvelope
                        {
                            schemaVersion = PlayerSaveValidator.CurrentSchemaVersion,
                            revision = 1,
                            savedAtUtcTicks = m_Clock.UtcNow.Ticks,
                            payload = migratedState
                        };

                        if (!PlayerSaveValidator.ValidateAndNormalize(tempEnv, m_Clock))
                        {
                            return new SaveLoadResult { Status = SaveLoadStatus.Corrupted };
                        }

                        if (m_Storage.AtomicSave(tempEnv, out string error))
                        {
                            m_CurrentRevision = 1;
                            var s = new PlayerProgressState();
                            s.CopyFromPayload(migratedState);

                            if (m_Importer.TryMarkAsImported(out string markerError))
                            {
                                return new SaveLoadResult { State = s, Status = SaveLoadStatus.Migrated };
                            }
                            else
                            {
                                return new SaveLoadResult { State = s, Status = SaveLoadStatus.MigratedWithMarkerWarning, Warning = markerError };
                            }
                        }
                        return new SaveLoadResult { Status = SaveLoadStatus.IoFailure };
                    }
                    else if (importResult.Status == LegacyImportStatus.InvalidJson)
                    {
                        return new SaveLoadResult { Status = SaveLoadStatus.Corrupted, Warning = "Legacy data exists but could not be parsed." };
                    }
                    else if (importResult.Status == LegacyImportStatus.IoFailure)
                    {
                        return new SaveLoadResult { Status = SaveLoadStatus.IoFailure };
                    }
                }

                var tempEnvDefault = new PlayerSaveEnvelope
                {
                    schemaVersion = PlayerSaveValidator.CurrentSchemaVersion,
                    revision = 1,
                    savedAtUtcTicks = m_Clock.UtcNow.Ticks,
                    payload = defaultCandidate != null ? defaultCandidate.ToPayload() : new PlayerSavePayload()
                };

                PlayerSaveValidator.ValidateAndNormalize(tempEnvDefault, m_Clock);

                if (m_Storage.AtomicSave(tempEnvDefault, out string saveError))
                {
                    m_CurrentRevision = 1;
                    var createdState = new PlayerProgressState();
                    createdState.CopyFromPayload(tempEnvDefault.payload);
                    return new SaveLoadResult { State = createdState, Status = SaveLoadStatus.CreatedDefaults };
                }
                return new SaveLoadResult { Status = SaveLoadStatus.IoFailure };
            }
            else
            {
                return new SaveLoadResult { Status = SaveLoadStatus.Corrupted };
            }

            if (envelope != null)
            {
                PlayerSaveValidator.ValidateAndNormalize(envelope, m_Clock);
                m_CurrentRevision = envelope.revision;
                var state = new PlayerProgressState();
                state.CopyFromPayload(envelope.payload);
                return new SaveLoadResult { State = state, Status = finalStatus, Warning = warning };
            }

            return new SaveLoadResult { Status = SaveLoadStatus.Corrupted };
        }

        private bool IsCorrupt(SaveFileReadStatus status)
        {
            return status == SaveFileReadStatus.InvalidJson ||
                   status == SaveFileReadStatus.EmptyChecksum ||
                   status == SaveFileReadStatus.ChecksumMismatch;
        }

        public bool TrySave(PlayerProgressState candidate, out string error)
        {
            error = null;
            m_CurrentRevision++;
            var env = new PlayerSaveEnvelope
            {
                schemaVersion = PlayerSaveValidator.CurrentSchemaVersion,
                revision = m_CurrentRevision,
                savedAtUtcTicks = m_Clock.UtcNow.Ticks,
                payload = candidate.ToPayload()
            };

            PlayerSaveValidator.ValidateAndNormalize(env, m_Clock);

            bool success = m_Storage.AtomicSave(env, out error);
            if (!success)
            {
                m_CurrentRevision--;
            }
            return success;
        }
    }
}
