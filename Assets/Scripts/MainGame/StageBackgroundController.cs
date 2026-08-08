#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace InTheArena.MainGame
{
    [DisallowMultipleComponent]
    public sealed class StageBackgroundController : MonoBehaviour
    {
        private const string StageBackgroundName = "StageBackground";
        private const string PrimaryBackgroundName = "pixel_background_elven-hall_bg";
        private static readonly string[] LayerBackgroundNames =
        {
            "pixel_background_elven-hall_1",
            "pixel_background_elven-hall_2",
            "pixel_background_elven-hall_3",
        };

        [SerializeField] private SpriteRenderer m_PrimaryBackgroundRenderer;
        [SerializeField] private GameObject[] m_LayerBackgrounds;

        public void Apply(StageData stageData)
        {
            RestoreDefaultBackgrounds();
        }

        public static void ApplyToScene(StageData stageData)
        {
            ShowBattleBackgrounds();
        }

        public static void ShowBattleBackgrounds()
        {
            StageBackgroundController controller = FindFirstObjectByType<StageBackgroundController>(FindObjectsInactive.Include);
            if (controller != null)
            {
                controller.RestoreDefaultBackgrounds();
                return;
            }

            GameObject primary = GameObject.Find(PrimaryBackgroundName);
            if (primary != null)
                primary.SetActive(true);

            foreach (string layerName in LayerBackgroundNames)
            {
                GameObject layer = GameObject.Find(layerName);
                if (layer != null)
                    layer.SetActive(true);
            }

            RemoveStageBackground();
        }

        public static void HideBattleBackgrounds()
        {
            StageBackgroundController controller = FindFirstObjectByType<StageBackgroundController>(FindObjectsInactive.Include);
            if (controller != null)
            {
                controller.HideDefaultBackgrounds();
                return;
            }

            GameObject primary = GameObject.Find(PrimaryBackgroundName);
            if (primary != null)
                primary.SetActive(false);

            foreach (string layerName in LayerBackgroundNames)
            {
                GameObject layer = GameObject.Find(layerName);
                if (layer != null)
                    layer.SetActive(false);
            }

            RemoveStageBackground();
        }

        private void EnsureReferences()
        {
            if (m_PrimaryBackgroundRenderer == null)
                m_PrimaryBackgroundRenderer = FindPrimaryRenderer();

            if (m_LayerBackgrounds == null || m_LayerBackgrounds.Length == 0)
            {
                m_LayerBackgrounds = new GameObject[LayerBackgroundNames.Length];
                for (int i = 0; i < LayerBackgroundNames.Length; i++)
                    m_LayerBackgrounds[i] = GameObject.Find(LayerBackgroundNames[i]);
            }
        }

        private void RestoreDefaultBackgrounds()
        {
            EnsureReferences();

            RemoveStageBackground();

            if (m_PrimaryBackgroundRenderer != null)
                m_PrimaryBackgroundRenderer.gameObject.SetActive(true);

            if (m_LayerBackgrounds == null)
                return;

            foreach (GameObject layer in m_LayerBackgrounds)
            {
                if (layer != null)
                    layer.SetActive(true);
            }
        }

        private void HideDefaultBackgrounds()
        {
            EnsureReferences();

            RemoveStageBackground();

            if (m_PrimaryBackgroundRenderer != null)
                m_PrimaryBackgroundRenderer.gameObject.SetActive(false);

            if (m_LayerBackgrounds == null)
                return;

            foreach (GameObject layer in m_LayerBackgrounds)
            {
                if (layer != null)
                    layer.SetActive(false);
            }
        }

        private static SpriteRenderer FindPrimaryRenderer()
        {
            GameObject primary = GameObject.Find(PrimaryBackgroundName);
            return primary != null ? primary.GetComponent<SpriteRenderer>() : null;
        }

        private static void RemoveStageBackground()
        {
            GameObject stageBackground = GameObject.Find(StageBackgroundName);
            if (stageBackground == null)
                return;

            if (Application.isPlaying)
            {
                Destroy(stageBackground);
            }
            else
            {
                DestroyImmediate(stageBackground);
            }
        }
    }
}
#endif
