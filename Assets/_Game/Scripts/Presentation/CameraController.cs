using UnityEngine;
using UnityEngine.InputSystem;

namespace Fulfilment.Presentation
{
    /// <summary>
    /// Orthographic map camera: drag with the right or middle mouse button (or WASD/arrows) to pan, scroll to zoom
    /// towards the cursor, F to fit the whole map. The view is clamped to the map like the player bounds in Lecture 1.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraController : MonoBehaviour
    {
        [SerializeField] private MapView map;
        [SerializeField] private float minSize = 6f;
        [SerializeField] private float zoomStep = 0.12f;
        [SerializeField] private float keyPanSpeed = 30f;
        [SerializeField] private float margin = 4f;

        [Header("HUD insets in reference pixels (same scaling as the HUD PanelSettings)")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1600f, 900f);
        [SerializeField, Range(0f, 1f)] private float match = 0.5f;
        [SerializeField] private float insetLeft = 300f;
        [SerializeField] private float insetRight = 340f;
        [SerializeField] private float insetTop = 56f;
        [SerializeField] private float insetBottom = 70f;

        private Camera cam;
        private Vector3 dragOriginWorld;
        private bool dragging;
        private bool fitted;

        public bool PointerBlocked { get; set; }

        private void Awake() => cam = GetComponent<Camera>();

        private void LateUpdate()
        {
            if (map == null || map.Size == Vector2.zero) return;
            ApplyViewport();
            if (!fitted) { Fit(); fitted = true; }

            Mouse mouse = Mouse.current;
            Keyboard keys = Keyboard.current;

            if (keys != null)
            {
                Vector2 dir = Vector2.zero;
                if (keys.aKey.isPressed || keys.leftArrowKey.isPressed) dir.x -= 1;
                if (keys.dKey.isPressed || keys.rightArrowKey.isPressed) dir.x += 1;
                if (keys.sKey.isPressed || keys.downArrowKey.isPressed) dir.y -= 1;
                if (keys.wKey.isPressed || keys.upArrowKey.isPressed) dir.y += 1;
                float scale = cam.orthographicSize / MaxSize();
                transform.position += (Vector3)(dir * keyPanSpeed * scale * Time.unscaledDeltaTime);
                if (keys.fKey.wasPressedThisFrame) Fit();
            }

            if (mouse != null)
            {
                bool dragButton = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
                Vector3 world = ScreenToWorld(mouse.position.ReadValue());
                if (dragButton && !dragging && !PointerBlocked) { dragging = true; dragOriginWorld = world; }
                if (!dragButton) dragging = false;
                if (dragging) transform.position += dragOriginWorld - world;

                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f && !PointerBlocked)
                {
                    Vector3 before = ScreenToWorld(mouse.position.ReadValue());
                    float factor = scroll > 0 ? 1f - zoomStep : 1f + zoomStep;
                    cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * factor, minSize, MaxSize());
                    transform.position += before - ScreenToWorld(mouse.position.ReadValue());
                }
            }

            Clamp();
        }

        public void SetLeftInset(float referencePixels) => insetLeft = referencePixels;

        /// <summary>Restricts the camera to the screen area not covered by HUD panels.</summary>
        private void ApplyViewport()
        {
            // UI Toolkit ScaleWithScreenSize + MatchWidthOrHeight: interpolate the width and height scale in log space.
            float logW = Mathf.Log(Screen.width / referenceResolution.x, 2f);
            float logH = Mathf.Log(Screen.height / referenceResolution.y, 2f);
            float scale = Mathf.Pow(2f, Mathf.Lerp(logW, logH, match));
            float x = insetLeft * scale / Screen.width;
            float y = insetBottom * scale / Screen.height;
            float w = 1f - x - insetRight * scale / Screen.width;
            float h = 1f - y - insetTop * scale / Screen.height;
            var rect = new Rect(x, y, Mathf.Max(0.1f, w), Mathf.Max(0.1f, h));
            if (cam.rect != rect) cam.rect = rect;
        }

        public void Fit()
        {
            cam.orthographicSize = MaxSize();
            transform.position = new Vector3(map.Size.x / 2f, map.Size.y / 2f, -10f);
        }

        private float MaxSize() => Mathf.Max(map.Size.y / 2f, map.Size.x / (2f * cam.aspect)) + margin;

        private Vector3 ScreenToWorld(Vector2 screen)
        {
            Vector3 w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -transform.position.z));
            w.z = 0f;
            return w;
        }

        private void Clamp()
        {
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            Vector3 p = transform.position;
            p.x = halfW * 2f >= map.Size.x + 2 * margin ? map.Size.x / 2f : Mathf.Clamp(p.x, halfW - margin, map.Size.x - halfW + margin);
            p.y = halfH * 2f >= map.Size.y + 2 * margin ? map.Size.y / 2f : Mathf.Clamp(p.y, halfH - margin, map.Size.y - halfH + margin);
            p.z = -10f;
            transform.position = p;
        }
    }
}
