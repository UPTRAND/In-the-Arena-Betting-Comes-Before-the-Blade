#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace InTheArena.MainGame.Editor
{
    [CustomEditor(typeof(StageData))]
    public sealed class StageDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var stageData = (StageData)target;
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                $"난이도 권장값: {stageData.PresetRoundCount} Round / {GetPresetTarget(stageData.Difficulty)} Call\n" +
                "프리셋 적용 후 Round 목록과 목표 Call은 콘텐츠에 맞게 수정할 수 있습니다.",
                MessageType.Info);

            if (GUILayout.Button("난이도 목표 Call 프리셋 적용"))
            {
                Undo.RecordObject(stageData, "Apply Stage Difficulty Preset");
                stageData.ApplyDifficultyPreset();
                EditorUtility.SetDirty(stageData);
            }

            if (stageData.TotalRounds != stageData.PresetRoundCount)
            {
                EditorGUILayout.HelpBox(
                    $"현재 Round 수({stageData.TotalRounds})가 난이도 권장값({stageData.PresetRoundCount})과 다릅니다. " +
                    "커스텀 스테이지라면 그대로 사용할 수 있습니다.",
                    MessageType.Warning);
            }
        }

        private static int GetPresetTarget(StageDifficulty difficulty)
        {
            return difficulty switch
            {
                StageDifficulty.Easy => 1200,
                StageDifficulty.Normal => 1800,
                StageDifficulty.Hard => 2400,
                _ => 1800
            };
        }
    }
}
#endif
