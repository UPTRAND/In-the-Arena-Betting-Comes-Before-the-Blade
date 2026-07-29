#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using InTheArena.MainGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InTheArena.UI
{
    public sealed class UI_LobbyStagePanel : UI_Base
    {
        [SerializeField] private TMP_Text m_LevelText;
        [SerializeField] private Button m_StartButton;
        [SerializeField] private List<StageData> m_StageDatas = new List<StageData>();
        private StageData m_Target;
        protected override void Awake() { base.Awake(); m_StartButton.onClick.AddListener(StartStage); }
        public override void OnOpened() { base.OnOpened(); Refresh(); }
        public void Refresh()
        {
            int next = (SaveManager.Instance != null && SaveManager.Instance.Data != null ? SaveManager.Instance.Data.clearedStageNumber : 0) + 1;
            m_LevelText.text = $"레벨 {next}";
            m_Target = m_StageDatas.Find(stage => stage != null && stage.StageNum == next);
        }
        private void StartStage()
        {
            Refresh();
            if (m_Target == null) { Debug.Log($"[Lobby] {m_LevelText.text}은 준비 중입니다."); return; }
            SaveManager save = SaveManager.Instance;
            if (save == null || !save.TrySpendHeart()) { Debug.Log($"[Lobby] 하트가 부족합니다. 다음 하트까지 {save?.GetRemainingHeartTime():mm\\:ss}"); return; }
            if (StageManager.Instance == null) { Debug.LogError("[Lobby] StageManager를 찾을 수 없습니다."); return; }
            _ = StageManager.Instance.StartStageAsync(m_Target);
        }
    }
}
#endif
