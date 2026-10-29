using System.Collections.Generic;
using Fulfilment.Simulation;
using UnityEngine;

namespace Fulfilment.Presentation
{
    /// <summary>One parcel icon on the map; also selectable.</summary>
    public sealed class ParcelView : MonoBehaviour, ISelectable
    {
        public SpriteRenderer Renderer { get; private set; }
        public int ShipmentId { get; set; } = -1;

        public string Title => $"Paket #{ShipmentId}";
        public Transform Anchor => transform;
        public bool IsAlive => this != null && gameObject.activeSelf && ShipmentId >= 0;

        public void Initialize(Material material)
        {
            Renderer = gameObject.AddComponent<SpriteRenderer>();
            Renderer.sprite = SpriteFactory.Get(IconShape.Parcel);
            Renderer.sortingOrder = 10;
            if (material != null) Renderer.sharedMaterial = material;
            var col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.7f;
        }

        public string Describe(SimulationEngine engine)
        {
            Shipment s = engine.ShipmentOf(ShipmentId);
            OrderRecord r = s == null ? null : engine.RecordOf(s.OrderId);
            if (s == null || r == null) return "Teslim edildi";

            int eta = s.ArrivalTick;
            double slack = r.Order.PromisedTick - eta;
            string status = slack >= 0 ? $"zamanında ({slack:0} dk pay)" : $"gecikecek ({-slack:0} dk)";
            return $"Sipariş #{r.Order.Id} · {r.Order.Priority}\n" +
                   $"{engine.World.Centers[s.CenterId].Code} → {engine.World.Regions[s.RegionId].Code}\n" +
                   $"Ürün satırı: {s.Lines.Count}, plan: {r.Plan.Kind}\n" +
                   $"Çıkış: {SimTime.Format(s.DispatchTick)}  Varış: {SimTime.Format(eta)}\n" +
                   $"Söz: {SimTime.Format(r.Order.PromisedTick)} → {status}";
        }
    }

    /// <summary>
    /// Moves a pooled icon along the A* route of every parcel in transit. Position comes from the simulated time,
    /// so parcels move at the same pace at any frame rate and speed setting; the first part of the trip is a small
    /// hop out of the center (a parabola, as in the Week 3 trajectory lab).
    /// </summary>
    public sealed class ParcelLayer : MonoBehaviour
    {
        private const float HopShare = 0.08f;
        private const float HopHeight = 1.2f;
        private const float IconScale = 0.85f;

        [SerializeField] private SimulationRunner runner;
        [SerializeField] private MapView map;

        private readonly List<ParcelView> pool = new List<ParcelView>();
        private int active;

        public int VisibleParcels => active;

        private void OnEnable()
        {
            if (runner != null) runner.EpisodeStarted += OnEpisodeStarted;
        }

        private void OnDisable()
        {
            if (runner != null) runner.EpisodeStarted -= OnEpisodeStarted;
        }

        private void OnEpisodeStarted(SimulationEngine engine) => HideFrom(0);

        private void LateUpdate()
        {
            SimulationEngine engine = runner != null ? runner.Engine : null;
            if (engine == null || map == null) return;

            double now = runner.SmoothTick;
            int used = 0;
            IReadOnlyList<Shipment> shipments = engine.ActiveShipments;
            for (int i = 0; i < shipments.Count; i++)
            {
                Shipment s = shipments[i];
                if (now < s.DispatchTick) continue; // still being picked and packed at the center

                ParcelView view = Get(used++);
                view.ShipmentId = s.Id;
                float t = (float)s.Progress(now);
                Vector3 position = MapGeometry.Along(map.Route(s.CenterId, s.RegionId), t);
                if (t < HopShare)
                {
                    float u = t / HopShare;
                    position.y += HopHeight * 4f * u * (1f - u);
                }
                view.transform.localPosition = position;
                view.Renderer.color = ColourFor(engine, s);
            }
            HideFrom(used);
        }

        private static Color ColourFor(SimulationEngine engine, Shipment s)
        {
            OrderRecord record = engine.RecordOf(s.OrderId);
            if (record == null) return Palette.Parcel;
            if (s.ArrivalTick > record.Order.PromisedTick) return Palette.Critical;
            return record.Order.Priority == OrderPriority.Express ? Palette.ParcelExpress : Palette.Parcel;
        }

        private ParcelView Get(int index)
        {
            while (pool.Count <= index)
            {
                var go = new GameObject("Parcel");
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * IconScale;
                var view = go.AddComponent<ParcelView>();
                view.Initialize(map.SpriteMaterial);
                pool.Add(view);
            }
            ParcelView v = pool[index];
            if (!v.gameObject.activeSelf) v.gameObject.SetActive(true);
            return v;
        }

        private void HideFrom(int index)
        {
            for (int i = index; i < pool.Count; i++)
            {
                if (!pool[i].gameObject.activeSelf) continue;
                pool[i].ShipmentId = -1;
                pool[i].gameObject.SetActive(false);
            }
            active = index;
        }
    }
}
