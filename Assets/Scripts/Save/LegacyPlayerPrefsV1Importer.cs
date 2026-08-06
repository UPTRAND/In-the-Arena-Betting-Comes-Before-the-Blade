using System.IO;
using UnityEngine;

namespace InTheArena.Save
{
    public enum LegacyImportStatus
    {
        Missing,
        Success,
        InvalidJson,
        IoFailure
    }

    public readonly struct LegacyImportResult
    {
        public LegacyImportStatus Status { get; }
        public LegacyPlayerDataV1 Data { get; }
        public string Error { get; }

        public LegacyImportResult(LegacyImportStatus status, LegacyPlayerDataV1 data = null, string error = null)
        {
            Status = status;
            Data = data;
            Error = error;
        }
    }

    public class LegacyPlayerPrefsV1Importer
    {
        private const string PlayerDataKey = "InTheArena.PlayerData.v1";
        
        private readonly string m_MarkerFilePath;
        private readonly IFileSystem m_FileSystem;

        public LegacyPlayerPrefsV1Importer(string markerFilePath, IFileSystem fileSystem = null)
        {
            m_MarkerFilePath = markerFilePath;
            m_FileSystem = fileSystem ?? new SystemFileSystem();
        }

        public bool HasLegacyData()
        {
            return !m_FileSystem.FileExists(m_MarkerFilePath) && PlayerPrefs.HasKey(PlayerDataKey);
        }

        public LegacyImportResult Import()
        {
            if (HasLegacyData())
            {
                string json = PlayerPrefs.GetString(PlayerDataKey);
                try
                {
                    var data = JsonUtility.FromJson<LegacyPlayerDataV1>(json);
                    if (data == null)
                    {
                        return new LegacyImportResult(LegacyImportStatus.InvalidJson, error: "Parsed data is null");
                    }
                    return new LegacyImportResult(LegacyImportStatus.Success, data);
                }
                catch (System.Exception ex)
                {
                    string error = $"[LegacyPlayerPrefsV1Importer] V1 JSON 파싱 실패: {ex.Message}";
                    Debug.LogError(error);
                    return new LegacyImportResult(LegacyImportStatus.InvalidJson, error: error);
                }
            }
            return new LegacyImportResult(LegacyImportStatus.Missing);
        }

        public bool TryMarkAsImported(out string error)
        {
            error = null;
            try
            {
                m_FileSystem.WriteAllText(m_MarkerFilePath, "imported");
                return true;
            }
            catch (System.Exception ex)
            {
                error = $"[LegacyPlayerPrefsV1Importer] 마커 파일 작성 실패: {ex.Message}";
                Debug.LogError(error);
                return false;
            }
        }
    }
}
