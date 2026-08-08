using System.IO;
using UnityEditor;
using UnityEngine;
using InTheArena.MainGame; // For StageManager

public class SaveDebugWindow : EditorWindow
{
    private int m_InputNextStage = 1;
    private int m_InputStars = 0;
    private int m_InputHearts = SaveManager.MaxHearts;

    [MenuItem("Tools/Debug/Save Data")]
    public static void ShowWindow()
    {
        var window = GetWindow<SaveDebugWindow>("Save Debug Tool");
        window.Show();
    }

    private void OnEnable()
    {
        if (SaveManager.Instance != null &&
            SaveManager.Instance.Availability == SaveAvailability.Ready)
        {
            m_InputNextStage = SaveManager.Instance.ClearedStageNumber + 1;
            m_InputStars = SaveManager.Instance.Stars;
            m_InputHearts = SaveManager.Instance.Hearts;
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

        // Save 삭제는 SaveManager가 없어도 가능해야 함
        DrawDeleteSaveSection();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.Space();

        SaveManager save = SaveManager.Instance;

        if (save == null)
        {
            EditorGUILayout.HelpBox(
                "SaveManager is not available. Start Play Mode to modify runtime save values.",
                MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Save Status", save.Availability.ToString());
        EditorGUILayout.LabelField("Gold", save.Gold.ToString());
        EditorGUILayout.LabelField("Tickets / Hearts", $"{save.Hearts}/{SaveManager.MaxHearts}");
        EditorGUILayout.LabelField("Stars", save.Stars.ToString());
        EditorGUILayout.LabelField("Cleared", $"Stage {save.ClearedStageNumber}");
        EditorGUILayout.LabelField("Next Stage", $"Stage {save.ClearedStageNumber + 1}");

        // 기존 Gold, Stage, Stars UI...

        EditorGUILayout.BeginHorizontal();

        m_InputHearts = EditorGUILayout.IntField(
            "Tickets / Hearts",
            m_InputHearts);

        if (GUILayout.Button("Apply", GUILayout.Width(80)))
        {
            SetHearts(save, m_InputHearts);
        }

        EditorGUILayout.EndHorizontal();
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

    private void SetHearts(SaveManager save, int value)
    {
        int hearts = Mathf.Clamp(value, 0, SaveManager.MaxHearts);

        if (!save.DebugTryModifyState(
                state => state.SetHearts(hearts),
                out string error))
        {
            Debug.LogError(
                $"[SaveDebug] Failed to set Hearts: {error}");
            return;
        }

        Debug.Log(
            $"[SaveDebug] Successfully set Hearts to {hearts}.");
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

    private void DrawDeleteSaveSection()
    {
        GUI.color = Color.red;

        GUI.enabled = !Application.isPlaying;

        if (GUILayout.Button("DELETE SAVE DATA", GUILayout.Height(40)))
        {
            DeleteSaveFiles();
        }

        GUI.enabled = true;
        GUI.color = Color.white;

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Stop Play Mode to delete the save files.",
                MessageType.Info);
        }
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
