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
                $"\uD604\uC7AC \uC2A4\uD14C\uC774\uC9C0 \uB09C\uC774\uB3C4: {stageData.TotalRounds}\uB77C\uC6B4\uB4DC / \uBAA9\uD45C {stageData.TargetCall} Call\n" +
                "\uB09C\uC774\uB3C4\uB294 \uC124\uC815 \uD31D\uC5C5\uC774 \uC544\uB2C8\uB77C \uAC01 StageData\uC758 \uB77C\uC6B4\uB4DC \uC218, \uBAA9\uD45C Call, \uC0AC\uC6A9 \uC720\uB2DB \uD480\uB85C \uACB0\uC815\uB429\uB2C8\uB2E4.",
                MessageType.Info);
        }
    }
}
#endif
