using System;
using System.IO;
using UnityEngine;

namespace InTheArena.Save
{
    public enum SaveFileReadStatus
    {
        Success,
        Missing,
        InvalidJson,
        EmptyChecksum,
        ChecksumMismatch,
        UnsupportedFutureVersion,
        IoFailure
    }

    public readonly struct SaveFileReadResult
    {
        public SaveFileReadStatus Status { get; }
        public PlayerSaveEnvelope Envelope { get; }
        public Exception Exception { get; }

        public SaveFileReadResult(SaveFileReadStatus status, PlayerSaveEnvelope envelope = null, Exception exception = null)
        {
            Status = status;
            Envelope = envelope;
            Exception = exception;
        }
    }

    public class JsonFileSaveStorage
    {
        private readonly string m_MainFilePath;
        private readonly string m_TmpFilePath;
        private readonly string m_BakFilePath;
        private readonly IFileSystem m_FileSystem;

        public JsonFileSaveStorage(string saveDirectory, string fileName, IFileSystem fileSystem = null)
        {
            m_FileSystem = fileSystem ?? new SystemFileSystem();
            m_MainFilePath = Path.Combine(saveDirectory, fileName);
            m_TmpFilePath = Path.Combine(saveDirectory, fileName + ".tmp");
            m_BakFilePath = Path.Combine(saveDirectory, fileName + ".bak");
        }

        public SaveFileReadResult LoadMain() => LoadFile(m_MainFilePath);
        public SaveFileReadResult LoadBackup() => LoadFile(m_BakFilePath);

        private SaveFileReadResult LoadFile(string path)
        {
            if (!m_FileSystem.FileExists(path)) return new SaveFileReadResult(SaveFileReadStatus.Missing);
            try
            {
                string json = m_FileSystem.ReadAllText(path);
                var env = JsonUtility.FromJson<PlayerSaveEnvelope>(json);
                if (env == null || env.payload == null)
                {
                    return new SaveFileReadResult(SaveFileReadStatus.InvalidJson);
                }

                if (env.schemaVersion > PlayerSaveValidator.CurrentSchemaVersion)
                {
                    return new SaveFileReadResult(SaveFileReadStatus.UnsupportedFutureVersion, env);
                }

                if (string.IsNullOrWhiteSpace(env.checksum))
                {
                    return new SaveFileReadResult(SaveFileReadStatus.EmptyChecksum, env);
                }

                string computed = ComputeChecksum(env);
                if (env.checksum != computed)
                {
                    return new SaveFileReadResult(SaveFileReadStatus.ChecksumMismatch, env);
                }

                return new SaveFileReadResult(SaveFileReadStatus.Success, env);
            }
            catch (ArgumentException ex)
            {
                Debug.LogError($"[JsonFileSaveStorage] Invalid JSON in {path}: {ex.Message}");
                return new SaveFileReadResult(SaveFileReadStatus.InvalidJson, exception: ex);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[JsonFileSaveStorage] Failed to read {path}: {ex.Message}");
                return new SaveFileReadResult(SaveFileReadStatus.IoFailure, exception: ex);
            }
        }

        public bool AtomicSave(PlayerSaveEnvelope envelope, out string error)
        {
            error = null;
            try
            {
                string dir = Path.GetDirectoryName(m_MainFilePath);
                try
                {
                    m_FileSystem.CreateDirectory(dir);
                }
                catch (Exception ex)
                {
                    error = $"[JsonFileSaveStorage] Failed to create directory: {ex.Message}";
                    return false;
                }

                envelope.checksum = string.Empty;
                envelope.checksum = ComputeChecksum(envelope);

                string json = JsonUtility.ToJson(envelope, true);

                using (var fs = m_FileSystem.OpenWrite(m_TmpFilePath))
                using (var sw = new StreamWriter(fs, System.Text.Encoding.UTF8))
                {
                    sw.Write(json);
                    sw.Flush();
                    if (fs is FileStream realFs) realFs.Flush(true);
                }

                var verify = LoadFile(m_TmpFilePath);
                if (verify.Status != SaveFileReadStatus.Success)
                {
                    error = $"[JsonFileSaveStorage] Tmp file verification failed with status: {verify.Status}";
                    Debug.LogError(error);
                    return false;
                }

                bool hadMain = m_FileSystem.FileExists(m_MainFilePath);

                if (hadMain)
                {
                    try
                    {
                        m_FileSystem.Replace(m_TmpFilePath, m_MainFilePath, m_BakFilePath, true);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        if (!TryFallbackReplace(ref error)) return false;
                    }
                    catch (IOException)
                    {
                        if (!TryFallbackReplace(ref error)) return false;
                    }
                }
                else
                {
                    m_FileSystem.Move(m_TmpFilePath, m_MainFilePath);
                }

                var finalMain = LoadFile(m_MainFilePath);
                if (finalMain.Status != SaveFileReadStatus.Success)
                {
                    error = $"[JsonFileSaveStorage] Final main verification failed: {finalMain.Status}";
                    Debug.LogError(error);

                    if (hadMain)
                    {
                        if (m_FileSystem.FileExists(m_BakFilePath))
                        {
                            var verifyBackup = LoadFile(m_BakFilePath);
                            if (verifyBackup.Status == SaveFileReadStatus.Success)
                            {
                                try
                                {
                                    m_FileSystem.Copy(m_BakFilePath, m_MainFilePath, true);
                                    var reVerify = LoadFile(m_MainFilePath);
                                    if (reVerify.Status != SaveFileReadStatus.Success)
                                    {
                                        error += " Critical Recovery Failure: Restored main verification failed.";
                                    }
                                }
                                catch (Exception restoreEx)
                                {
                                    error += $" Critical Recovery Failure: Restoring backup failed ({restoreEx.Message}).";
                                }
                            }
                            else
                            {
                                error += " Critical Recovery Failure: Backup invalid.";
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            m_FileSystem.Move(m_MainFilePath, m_MainFilePath + $".invalid_{DateTime.UtcNow.Ticks}");
                        }
                        catch (Exception isolateEx)
                        {
                            error += $" Failed to isolate invalid main: {isolateEx.Message}";
                        }
                    }

                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"[JsonFileSaveStorage] AtomicSave failed: {ex.Message}";
                Debug.LogError(error);
                try { m_FileSystem.Delete(m_TmpFilePath); } catch (Exception cleanupEx) { Debug.LogWarning($"Failed to cleanup tmp: {cleanupEx}"); }
                return false;
            }
        }

        public bool TryRepairMainFromBackup(out string error)
        {
            error = null;
            if (!m_FileSystem.FileExists(m_BakFilePath))
            {
                error = "Backup file not found.";
                return false;
            }

            string corruptMain = m_MainFilePath + ".corrupt";
            string recoveryTmp = m_MainFilePath + ".recovery.tmp";

            if (m_FileSystem.FileExists(m_MainFilePath))
            {
                try
                {
                    m_FileSystem.Copy(m_MainFilePath, corruptMain, true);
                }
                catch (Exception ex)
                {
                    error = $"Failed to preserve corrupt main: {ex.Message}";
                    return false;
                }
            }

            try
            {
                m_FileSystem.Copy(m_BakFilePath, recoveryTmp, true);
            }
            catch (Exception ex)
            {
                error = $"Failed to copy backup to recovery tmp: {ex.Message}";
                return false;
            }

            var verifyRecovery = LoadFile(recoveryTmp);
            if (verifyRecovery.Status != SaveFileReadStatus.Success)
            {
                error = $"Recovery tmp verification failed: {verifyRecovery.Status}";
                return false;
            }

            try
            {
                m_FileSystem.Copy(recoveryTmp, m_MainFilePath, true);
            }
            catch (Exception ex)
            {
                error = $"Failed to replace main with recovery tmp: {ex.Message}";
                return false;
            }

            var verifyMain = LoadFile(m_MainFilePath);
            if (verifyMain.Status != SaveFileReadStatus.Success)
            {
                error = $"Main verification after recovery failed: {verifyMain.Status}";
                return false;
            }

            try { m_FileSystem.Delete(recoveryTmp); } catch (Exception ex) { Debug.LogWarning($"Failed to cleanup recovery tmp: {ex}"); }
            return true;
        }

        private bool TryFallbackReplace(ref string error)
        {
            string oldMain = m_MainFilePath + ".old";
            if (m_FileSystem.FileExists(m_MainFilePath))
            {
                try { m_FileSystem.Copy(m_MainFilePath, oldMain, true); }
                catch (Exception e)
                {
                    error = $"[JsonFileSaveStorage] Fallback failed to create oldMain: {e.Message}";
                    try { m_FileSystem.Delete(m_TmpFilePath); } catch (Exception ex) { Debug.LogWarning($"Failed to cleanup tmp: {ex}"); }
                    return false;
                }
            }

            try { m_FileSystem.Copy(m_TmpFilePath, m_MainFilePath, true); }
            catch (Exception e)
            {
                error = $"[JsonFileSaveStorage] Fallback replace failed during copy: {e.Message}.";
                if (TryRestoreOldMain(oldMain, ref error))
                {
                    error += " Original main restored successfully.";
                    try { m_FileSystem.Delete(m_TmpFilePath); } catch (Exception ex) { error += $" [WARNING] Failed to cleanup tmp: {ex.Message}"; }
                }
                else
                {
                    error += " Critical Recovery Failure: original main could not be restored.";
                }
                return false;
            }

            var verifyMain = LoadFile(m_MainFilePath);
            if (verifyMain.Status == SaveFileReadStatus.Success)
            {
                bool backupRotated = false;
                if (m_FileSystem.FileExists(oldMain))
                {
                    try
                    {
                        m_FileSystem.Copy(oldMain, m_BakFilePath, true);
                        var verifyBackup = LoadFile(m_BakFilePath);
                        if (verifyBackup.Status == SaveFileReadStatus.Success)
                        {
                            backupRotated = true;
                        }
                        else
                        {
                            error = $"[WARNING] Backup rotation verification failed: {verifyBackup.Status}";
                            Debug.LogWarning(error);
                        }
                    }
                    catch (Exception ex)
                    {
                        error = $"[WARNING] Backup rotation failed: {ex.Message}";
                        Debug.LogWarning(error);
                    }
                }
                else
                {
                    backupRotated = true;
                }

                try { m_FileSystem.Delete(m_TmpFilePath); } catch (Exception ex) { Debug.LogWarning($"Failed to cleanup tmp: {ex}"); }

                if (backupRotated && m_FileSystem.FileExists(oldMain))
                {
                    try { m_FileSystem.Delete(oldMain); } catch (Exception ex) { Debug.LogWarning($"Failed to cleanup old: {ex}"); }
                }

                return true;
            }
            else
            {
                error = $"[JsonFileSaveStorage] Fallback replacement verification failed ({verifyMain.Status}).";
                if (TryRestoreOldMain(oldMain, ref error))
                {
                    error += " Original main restored successfully.";
                    try { m_FileSystem.Delete(m_TmpFilePath); } catch (Exception ex) { error += $" [WARNING] Failed to cleanup tmp: {ex.Message}"; }
                }
                else
                {
                    error += " Critical Recovery Failure: original main could not be restored.";
                }
                return false;
            }
        }

        private bool TryRestoreOldMain(string oldMain, ref string error)
        {
            if (!m_FileSystem.FileExists(oldMain))
            {
                error += " Original main backup was not found.";
                return false;
            }

            try
            {
                m_FileSystem.Copy(oldMain, m_MainFilePath, true);
            }
            catch (Exception restoreEx)
            {
                error += $" Failed to restore oldMain: {restoreEx.Message}";
                return false;
            }

            var verifyRestored = LoadFile(m_MainFilePath);
            if (verifyRestored.Status != SaveFileReadStatus.Success)
            {
                error += $" Restored main verification failed ({verifyRestored.Status}). oldMain preserved.";
                return false;
            }

            try
            {
                m_FileSystem.Delete(oldMain);
            }
            catch (Exception cleanupEx)
            {
                error += $" [WARNING] Original main was restored, but oldMain cleanup failed: {cleanupEx.Message}";
            }

            return true;
        }

        public static string ComputeChecksum(PlayerSaveEnvelope env)
        {
            string payloadJson = env.schemaVersion < 3
                ? JsonUtility.ToJson(LegacyChecksumPayload.From(env.payload))
                : JsonUtility.ToJson(env.payload);
            string raw = $"{env.schemaVersion}_{env.revision}_{env.savedAtUtcTicks}_{payloadJson}";

            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }

        [Serializable]
        private sealed class LegacyChecksumPayload
        {
            public int clearedStageNumber;
            public int gold;
            public int hearts;
            public int stars;
            public long lastHeartRecoveryUtcTicks;

            public static LegacyChecksumPayload From(PlayerSavePayload payload)
            {
                if (payload == null)
                {
                    return new LegacyChecksumPayload();
                }

                return new LegacyChecksumPayload
                {
                    clearedStageNumber = payload.clearedStageNumber,
                    gold = payload.gold,
                    hearts = payload.hearts,
                    stars = payload.stars,
                    lastHeartRecoveryUtcTicks = payload.lastHeartRecoveryUtcTicks
                };
            }
        }
    }
}
