using UnityEngine;
using UnityEngine.InputSystem;

namespace Fulfilment.Presentation
{
    /// <summary>
    /// Left click picks the center, region or parcel under the cursor (screen → world, then Physics2D.OverlapPoint,
    /// the same pattern as the Week 6 lab) and keeps a highlight ring on it.
    /// </summary>
    public sealed class SelectionController : MonoBehaviour
    {
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private MapView map;
        [SerializeField] private SimulationRunner runner;

        private SpriteRenderer highlight;
        private int selectedShipment = -1;

        public ISelectable Current { get; private set; }
        public bool PointerBlocked { get; set; }

        private void Start()
        {
            var go = new GameObject("SelectionHighlight");
            go.transform.SetParent(transform, false);
            highlight = go.AddComponent<SpriteRenderer>();
            highlight.sprite = SpriteFactory.Get(IconShape.Ring);
            highlight.color = Palette.Selection;
            highlight.sortingOrder = 20;
            if (map != null && map.SpriteMaterial != null) highlight.sharedMaterial = map.SpriteMaterial;
            go.SetActive(false);

            if (runner != null) runner.EpisodeStarted += _ => Clear();
        }

        public void Clear() => Current = null;

        public void Select(ISelectable target)
        {
            Current = target;
            selectedShipment = target is ParcelView parcel ? parcel.ShipmentId : -1;
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerBlocked)
            {
                Vector2 p = mouse.position.ReadValue();
                Vector3 world = sceneCamera.ScreenToWorldPoint(new Vector3(p.x, p.y, -sceneCamera.transform.position.z));
                Collider2D hit = Physics2D.OverlapPoint(world);
                Current = hit != null ? hit.GetComponentInParent<ISelectable>() : null;
                selectedShipment = Current is ParcelView parcel ? parcel.ShipmentId : -1;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Current = null;

            // Parcel icons are pooled: drop the selection when the icon is reused for another shipment.
            if (Current != null && (!Current.IsAlive || (Current is ParcelView pv && pv.ShipmentId != selectedShipment)))
                Current = null;

            bool show = Current != null;
            if (highlight.gameObject.activeSelf != show) highlight.gameObject.SetActive(show);
            if (show)
            {
                highlight.transform.position = Current.Anchor.position;
                float pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f);
                highlight.transform.localScale = Vector3.one * (Current is ParcelView ? 1.4f : 3.6f) * pulse;
            }
        }
    }
}
