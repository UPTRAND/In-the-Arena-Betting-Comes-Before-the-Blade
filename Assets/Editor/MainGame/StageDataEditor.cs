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
                $"선택된 프리셋: {stageData.DefaultDifficulty} / {stageData.PresetRoundCount}라운드 / " +
                $"{StageData.GetPresetTargetCall(stageData.DefaultDifficulty)} Call\n" +
                "실행 난이도는 설정 팝업에서 선택합니다. 각 스테이지는 공통 7개 라운드를 가지고, 난이도별 사용 라운드 수만 달라집니다.",
                MessageType.Info);

            if (GUILayout.Button("선택 난이도에 목표 Call/라운드 수 프리셋 적용"))
            {
                Undo.RecordObject(stageData, "스테이지 난이도 프리셋 적용");
                stageData.ApplyDifficultyPreset();
                EditorUtility.SetDirty(stageData);
            }
        }
    }
}
#endif
