#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
public static class ClearSaveDataTools
{
    [MenuItem("Tools/Clear Save Data")]
    public static void ClearSaveData()
    {
        PlayerPrefs.DeleteKey("InTheArena.PlayerData.v1");
        // 또는 전체 삭제 시: PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("<color=green>[SaveManager]</color> 세이브 데이터가 초기화되었습니다.");
    }
}
#endif