using System.IO;
using UnityEditor;
using UnityEngine;
using InTheArena.MainGame; // For StageManager

public class SaveDebugWindow : EditorWindow
{
    private int m_InputNextStage = 1;
    private int m_InputStars = 0;

    [MenuItem("Tools/Debug/Save Data")]
    public static void ShowWindow()
    {
        var window = GetWindow<SaveDebugWindow>("Save Debug Tool");
        window.Show();
    }

    private void OnEnable()
    {
        // Load initial values to populate input fields nicely if possible
        if (SaveManager.Instance != null && SaveManager.Instance.Availability == SaveAvailability.Ready)
        {
            m_InputNextStage = SaveManager.Instance.ClearedStageNumber + 1;
            m_InputStars = SaveManager.Instance.Stars;
        }
    }

    private void OnInspectorUpdate()
    {
        // Repaint at 10 frames per second to keep values updated while in Play Mode
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Save Debug Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        SaveManager save = SaveManager.Instance;

        if (save == null)
        {
            EditorGUILayout.HelpBox("SaveManager is not available in the current scene.", MessageType.Warning);
            return;
        }

        // 1. Current Status Display
        EditorGUILayout.LabelField("Save Status", save.Availability.ToString());
        EditorGUILayout.LabelField("Gold", save.Gold.ToString());
        EditorGUILayout.LabelField("Stars", save.Stars.ToString());
        EditorGUILayout.LabelField("Cleared", $"Stage {save.ClearedStageNumber}");
        EditorGUILayout.LabelField("Next Stage", $"Stage {save.ClearedStageNumber + 1}");
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.Space();

        // 2. Gold Modification
        if (GUILayout.Button("Gold +5000", GUILayout.Height(30)))
        {
            AddGold5000(save);
        }

        EditorGUILayout.Space();

        // 3. Next Stage Modification
        EditorGUILayout.BeginHorizontal();
        m_InputNextStage = EditorGUILayout.IntField("Next Stage", m_InputNextStage);
        if (GUILayout.Button("Apply", GUILayout.Width(80)))
        {
            SetNextStage(save, m_InputNextStage);
        }
        EditorGUILayout.EndHorizontal();

        // 4. Stars Modification
        EditorGUILayout.BeginHorizontal();
        m_InputStars = EditorGUILayout.IntField("Stars", m_InputStars);
        if (GUILayout.Button("Apply", GUILayout.Width(80)))
        {
            SetStars(save, m_InputStars);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.Space();

        // 5. Delete Save Data
        GUI.color = Color.red;
        GUI.enabled = !Application.isPlaying; // Prevent deletion during play mode
        if (GUILayout.Button("DELETE SAVE DATA", GUILayout.Height(40)))
        {
            DeleteSaveFiles();
        }
        GUI.enabled = true;
        GUI.color = Color.white;

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Save Clear is disabled during Play Mode.", MessageType.Info);
        }
    }

    private void AddGold5000(SaveManager save)
    {
        const int amount = 5000;

        if (!save.DebugTryModifyState(state => state.SetGold(state.Gold + amount), out string error))
        {
            Debug.LogError($"[SaveDebug] Failed to add gold: {error}");
            return;
        }

        StageManager stage = StageManager.Instance;
        if (stage != null && stage.IsStageRunning && stage.PlayerState != null)
        {
            stage.PlayerState.Gold += amount;
            Debug.Log($"[SaveDebug] Added 5000 Gold to StagePlayerState as well.");
        }
        
        Debug.Log($"[SaveDebug] Successfully added 5000 Gold.");
    }

    private void SetNextStage(SaveManager save, int stageNumber)
    {
        int nextStage = Mathf.Max(1, stageNumber);

        if (!save.DebugTryModifyState(state => state.SetClearedStageNumber(nextStage - 1), out string error))
        {
            Debug.LogError($"[SaveDebug] Failed to set next stage: {error}");
            return;
        }
        
        Debug.Log($"[SaveDebug] Successfully set Next Stage to {nextStage} (ClearedStageNumber = {nextStage - 1}).");
    }

    private void SetStars(SaveManager save, int value)
    {
        if (!save.DebugTryModifyState(state => state.SetStars(Mathf.Max(0, value)), out string error))
        {
            Debug.LogError($"[SaveDebug] Failed to set stars: {error}");
            return;
        }
        
        Debug.Log($"[SaveDebug] Successfully set Stars to {Mathf.Max(0, value)}.");
    }

    private void DeleteSaveFiles()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[SaveDebug] Save Clear is only allowed in Edit Mode.");
            return;
        }

        bool confirm = EditorUtility.DisplayDialog(
            "세이브 데이터 삭제 확인",
            "정말 세이브 데이터를 삭제하시겠습니까?\n\nplayer-data.json 및 백업 데이터가 모두 삭제됩니다.",
            "Delete",
            "Cancel"
        );

        if (!confirm) return;

        string directory = Path.Combine(Application.persistentDataPath, "Save");

        if (Directory.Exists(directory))
        {
            string[] files = Directory.GetFiles(directory, "player-data.json*");

            foreach (string file in files)
            {
                File.Delete(file);
                Debug.Log($"[SaveDebug] Deleted file: {file}");
            }

            string migrationMarker = Path.Combine(directory, "migration-v1.done");
            if (File.Exists(migrationMarker))
            {
                File.Delete(migrationMarker);
                Debug.Log($"[SaveDebug] Deleted migration marker.");
            }
        }

        PlayerPrefs.DeleteKey("InTheArena.PlayerData.v1");
        PlayerPrefs.Save();
        
        Debug.Log("[SaveDebug] Save data deletion complete.");
    }
}
