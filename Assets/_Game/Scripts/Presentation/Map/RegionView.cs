using System.Linq;
using Fulfilment.Simulation;
using UnityEngine;

namespace Fulfilment.Presentation
{
    /// <summary>A customer region: pin icon sized by its demand weight.</summary>
    public sealed class RegionView : MonoBehaviour, ISelectable
    {
        public CustomerRegion Region { get; private set; }

        public string Title => $"{Region.Code} · {Region.Name}";
        public Transform Anchor => transform;
        public bool IsAlive => this != null;

        public void Initialize(CustomerRegion region, Material material)
        {
            Region = region;
            name = region.Code;
            transform.localPosition = MapGeometry.CellCenter(region.Cell);

            // Disc sized by demand weight with a white centre; order pulses spread around it concentrically.
            float scale = 0.9f + 0.45f * (float)region.DemandWeight;
            Icon("Disc", IconShape.Disc, scale, Palette.Region, 6, material);
            Icon("Centre", IconShape.Disc, scale * 0.35f, Palette.Neutral, 7, material);

            var col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.5f * scale;
        }

        private void Icon(string childName, IconShape shape, float scale, Color colour, int order, Material material)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Get(shape);
            sr.color = colour;
            sr.sortingOrder = order;
            if (material != null) sr.sharedMaterial = material;
        }

        public string Describe(SimulationEngine engine)
        {
            var orders = engine.Records.Where(r => r.Order.RegionId == Region.Id).ToList();
            int open = orders.Count(r => r.Status == OrderStatus.Shipping);
            int late = orders.Count(r => r.IsComplete && r.IsLate);
            int lost = orders.Count(r => r.Evaluation.LostUnits > 0);
            double nearestKm = engine.World.Centers.Min(c => engine.Distances.DistanceKm(c.Id, Region.Id));
            return $"Talep ağırlığı: {Region.DemandWeight:0.00}\n" +
                   $"Sipariş: {orders.Count} (yolda {open})\n" +
                   $"Geç teslim: {late}\n" +
                   $"Eksik/kayıp ürünlü sipariş: {lost}\n" +
                   $"En yakın depoya yol: {nearestKm:0.0} km";
        }
    }
}
