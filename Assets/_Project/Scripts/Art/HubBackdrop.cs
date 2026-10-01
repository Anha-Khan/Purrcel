using UnityEngine;

namespace CatCourier.Art
{
    /// <summary>Gives the camera-free Hub scene a painted, resolution-independent view.</summary>
    public sealed class HubBackdrop : MonoBehaviour
    {
        private Camera hubCamera;
        private Sprite backgroundSprite;
        private Sprite panelSprite;
        private Texture2D panelTexture;
        private SpriteRenderer background;
        private SpriteRenderer panel;

        private void Awake()
        {
            hubCamera = GetComponentInChildren<Camera>(true);
            if (hubCamera == null)
            {
                var cameraObject = new GameObject("Hub Camera");
                cameraObject.transform.SetParent(transform, false);
                cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
                cameraObject.tag = "MainCamera";
                hubCamera = cameraObject.AddComponent<Camera>();
            }
            hubCamera.orthographic = true;
            hubCamera.orthographicSize = 5f;
            hubCamera.clearFlags = CameraClearFlags.SolidColor;
            hubCamera.backgroundColor = new Color(0.12f, 0.16f, 0.19f);

            backgroundSprite = Resources.Load<Sprite>("HubCourierSquare");
            if (backgroundSprite != null)
            {
                background = CreateRenderer("Painted Courier Square", backgroundSprite, -100);
            }

            panelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            panelTexture.SetPixel(0, 0, new Color(0.045f, 0.09f, 0.13f, 0.85f));
            panelTexture.Apply();
            panelSprite = Sprite.Create(panelTexture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 1f);
            panel = CreateRenderer("Readable Menu Panel", panelSprite, -90);
            FitToScreen();
        }

        private SpriteRenderer CreateRenderer(string name, Sprite sprite, int order)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void LateUpdate() => FitToScreen();

        private void FitToScreen()
        {
            if (hubCamera == null) return;
            var height = hubCamera.orthographicSize * 2f;
            var width = height * hubCamera.aspect;
            if (background != null && backgroundSprite != null)
            {
                var spriteSize = backgroundSprite.bounds.size;
                var scale = Mathf.Max(width / spriteSize.x, height / spriteSize.y);
                background.transform.localScale = Vector3.one * scale;
                // Portrait-leaning Game views crop a landscape painting. Favor
                // its courier cat on the right while the left is under the UI.
                var extraWidth = Mathf.Max(0f, spriteSize.x * scale - width);
                background.transform.localPosition = new Vector3(-extraWidth * 0.45f, 0f, 0f);
            }
            if (panel != null)
            {
                var panelWidth = width * 0.63f;
                panel.transform.localScale = new Vector3(panelWidth, height, 1f);
                panel.transform.localPosition = new Vector3((panelWidth - width) * 0.5f, 0f, 0f);
            }
        }

        private void OnDestroy()
        {
            if (panelSprite != null) Destroy(panelSprite);
            if (panelTexture != null) Destroy(panelTexture);
        }
    }
}
