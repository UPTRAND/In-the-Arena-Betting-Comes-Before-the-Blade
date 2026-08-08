#if UNITY_6000_0_OR_NEWER
using UnityEngine;

namespace InTheArena.MainGame
{
    [DisallowMultipleComponent]
    public sealed class StageBackgroundController : MonoBehaviour
    {
        private const string PrimaryBackgroundName = "pixel_background_elven-hall_bg";
        private static readonly string[] LayerBackgroundNames =
        {
            "pixel_background_elven-hall_1",
            "pixel_background_elven-hall_2",
            "pixel_background_elven-hall_3",
        };

        [SerializeField] private SpriteRenderer m_PrimaryBackgroundRenderer;
        [SerializeField] private GameObject[] m_LayerBackgrounds;

        private SpriteRenderer m_StageBackgroundRenderer;

        public void Apply(StageData stageData)
        {
            if (stageData == null || stageData.BackgroundSprite == null)
                return;

            EnsureReferences();

            if (m_PrimaryBackgroundRenderer == null)
            {
                Debug.LogWarning("[StageBackgroundController] Primary background renderer was not found.");
                return;
            }

            EnsureStageBackgroundRenderer();
            if (m_StageBackgroundRenderer == null)
                return;

            m_StageBackgroundRenderer.sprite = stageData.BackgroundSprite;
            m_StageBackgroundRenderer.gameObject.SetActive(true);
            m_PrimaryBackgroundRenderer.gameObject.SetActive(false);

            if (m_LayerBackgrounds == null)
                return;

            foreach (GameObject layer in m_LayerBackgrounds)
            {
                if (layer != null)
                    layer.SetActive(false);
            }
        }

        public static void ApplyToScene(StageData stageData)
        {
            StageBackgroundController controller = FindFirstObjectByType<StageBackgroundController>(FindObjectsInactive.Include);
            if (controller != null)
            {
                controller.Apply(stageData);
                return;
            }

            SpriteRenderer renderer = FindPrimaryRenderer();
            if (renderer == null || stageData == null || stageData.BackgroundSprite == null)
                return;

            SpriteRenderer stageRenderer = CreateStageBackgroundRenderer(renderer);
            stageRenderer.sprite = stageData.BackgroundSprite;
            stageRenderer.gameObject.SetActive(true);
            renderer.gameObject.SetActive(false);
            DisableLayerBackgrounds();
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

            GameObject stageBackground = GameObject.Find("StageBackground");
            if (stageBackground != null)
                stageBackground.SetActive(false);
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

            if (m_StageBackgroundRenderer != null)
                m_StageBackgroundRenderer.gameObject.SetActive(false);

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

        private void EnsureStageBackgroundRenderer()
        {
            if (m_StageBackgroundRenderer != null)
                return;

            m_StageBackgroundRenderer = CreateStageBackgroundRenderer(m_PrimaryBackgroundRenderer);
        }

        private static SpriteRenderer CreateStageBackgroundRenderer(SpriteRenderer source)
        {
            GameObject stageBackground = new("StageBackground");
            Transform stageTransform = stageBackground.transform;
            Transform sourceTransform = source.transform;
            stageTransform.SetParent(sourceTransform.parent, false);
            stageTransform.SetLocalPositionAndRotation(sourceTransform.localPosition, sourceTransform.localRotation);
            stageTransform.localScale = sourceTransform.localScale;

            SpriteRenderer renderer = stageBackground.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = source.sharedMaterial;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder;
            renderer.drawMode = source.drawMode;
            renderer.size = source.size;
            renderer.color = source.color;
            renderer.flipX = source.flipX;
            renderer.flipY = source.flipY;
            renderer.maskInteraction = source.maskInteraction;
            renderer.spriteSortPoint = source.spriteSortPoint;
            return renderer;
        }

        private static SpriteRenderer FindPrimaryRenderer()
        {
            GameObject primary = GameObject.Find(PrimaryBackgroundName);
            return primary != null ? primary.GetComponent<SpriteRenderer>() : null;
        }

        private static void DisableLayerBackgrounds()
        {
            foreach (string layerName in LayerBackgroundNames)
            {
                GameObject layer = GameObject.Find(layerName);
                if (layer != null)
                    layer.SetActive(false);
            }
        }
    }
}
#endif
