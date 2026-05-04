using UnityEngine;

namespace XTD.Presentation
{
    public sealed class BattleStage2D : MonoBehaviour
    {
        [SerializeField] private Sprite backdropSprite;
        [SerializeField] private Sprite floorSprite;
        [SerializeField] private Sprite playerPortalSprite;
        [SerializeField] private Sprite enemyGateSprite;

        private SpriteRenderer backdropRenderer;
        private SpriteRenderer floorRenderer;
        private SpriteRenderer playerPortalRenderer;
        private SpriteRenderer enemyGateRenderer;

        private void Awake()
        {
            Build();
        }

        private void Start()
        {
            Build();
        }

        private void Build()
        {
            var camera = Camera.main;
            ConfigureCamera(camera);

            if (backdropSprite != null)
            {
                backdropRenderer = EnsureRenderer(backdropRenderer, "战场底图", backdropSprite, -120);
                FitToCamera(backdropRenderer, camera, 1.08f);
            }

            if (floorSprite != null)
            {
                floorRenderer = EnsureRenderer(floorRenderer, "战场地面", floorSprite, -110);
                FitToCamera(floorRenderer, camera, 1.08f);
                floorRenderer.color = new Color(1f, 1f, 1f, 0.72f);
            }

            playerPortalRenderer = ConfigureMarker(playerPortalRenderer, "我方阵眼", playerPortalSprite, new Vector3(0f, -3.62f, 0f), 0.95f, -15);
            enemyGateRenderer = ConfigureMarker(enemyGateRenderer, "敌方魔门", enemyGateSprite, new Vector3(0f, 3.32f, 0f), 0.92f, -15);
        }

        private static void ConfigureCamera(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.045f, 0.055f, 0.075f);
        }

        private SpriteRenderer ConfigureMarker(SpriteRenderer renderer, string objectName, Sprite sprite, Vector3 position, float height, int sortingOrder)
        {
            if (sprite == null)
            {
                if (renderer != null)
                {
                    renderer.gameObject.SetActive(false);
                }

                return renderer;
            }

            renderer = EnsureRenderer(renderer, objectName, sprite, sortingOrder);
            renderer.transform.localPosition = position;
            renderer.gameObject.SetActive(true);

            var bounds = sprite.bounds.size;
            var scale = bounds.y > 0f ? height / bounds.y : 1f;
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
            return renderer;
        }

        private SpriteRenderer EnsureRenderer(SpriteRenderer renderer, string objectName, Sprite sprite, int sortingOrder)
        {
            if (renderer == null)
            {
                var child = new GameObject(objectName);
                child.transform.SetParent(transform, false);
                renderer = child.AddComponent<SpriteRenderer>();
            }

            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static void FitToCamera(SpriteRenderer renderer, Camera camera, float coverage)
        {
            if (renderer == null || renderer.sprite == null)
            {
                return;
            }

            var targetHeight = camera != null && camera.orthographic ? camera.orthographicSize * 2f * coverage : 10f * coverage;
            var targetWidth = camera != null && camera.orthographic ? targetHeight * camera.aspect : targetHeight * 16f / 9f;
            var size = renderer.sprite.bounds.size;
            if (size.x <= 0f || size.y <= 0f)
            {
                return;
            }

            var scale = Mathf.Max(targetWidth / size.x, targetHeight / size.y);
            renderer.transform.localPosition = new Vector3(0f, 0f, 1f);
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
