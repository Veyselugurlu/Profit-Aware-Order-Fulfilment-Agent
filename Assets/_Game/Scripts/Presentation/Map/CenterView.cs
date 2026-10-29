using System.Text;
using Fulfilment.Simulation;
using UnityEngine;

namespace Fulfilment.Presentation
{
    public enum CenterState
    {
        Normal,
        LowStock,
        OutOfStock
    }

    /// <summary>
    /// A fulfilment center on the map: warehouse icon, a ring whose colour shows stock health and a bar
    /// under it that grows with the picking queue.
    /// </summary>
    public sealed class CenterView : MonoBehaviour, ISelectable
    {
        private const float QueueBarMaxMinutes = 120f;
        private const int OutOfStockSkus = 2;
        private const float LowStockShare = 0.2f;

        private SpriteRenderer icon;
        private SpriteRenderer ring;
        private SpriteRenderer barBack;
        private SpriteRenderer bar;

        public FulfilmentCenter Center { get; private set; }
        public CenterState State { get; private set; }
        public int SkusBelowSafety { get; private set; }
        public int SkusOut { get; private set; }
        public float BacklogMinutes { get; private set; }

        public string Title => $"{Center.Code} · {Center.Name}";
        public Transform Anchor => transform;
        public bool IsAlive => this != null;

        public void Initialize(FulfilmentCenter center, Material material)
        {
            Center = center;
            name = center.Code;
            transform.localPosition = MapGeometry.CellCenter(center.Cell);

            ring = Child("Ring", IconShape.Ring, 3.0f, 5, material);
            icon = Child("Icon", IconShape.Warehouse, 2.2f, 6, material);
            icon.color = Palette.Center;
            barBack = Child("QueueBack", IconShape.Square, 1f, 6, material);
            barBack.transform.localPosition = new Vector3(0f, -1.75f, 0f);
            barBack.transform.localScale = new Vector3(2.4f, 0.28f, 1f);
            barBack.color = Palette.WithAlpha(Palette.Block, 0.9f);
            bar = Child("Queue", IconShape.Square, 1f, 7, material);

            var col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = 1.4f;
            Refresh(null);
        }

        private SpriteRenderer Child(string childName, IconShape shape, float scale, int order, Material material)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Get(shape);
            sr.sortingOrder = order;
            if (material != null) sr.sharedMaterial = material;
            return sr;
        }

        public void Refresh(SimulationEngine engine)
        {
            if (engine != null)
            {
                int below = 0, empty = 0;
                for (int s = 0; s < engine.World.Skus.Count; s++)
                {
                    int onHand = engine.Inventory.OnHand(Center.Id, s);
                    if (onHand == 0) empty++;
                    if (engine.Costs.IsBelowSafety(Center.Id, s, onHand)) below++;
                }
                SkusBelowSafety = below;
                SkusOut = empty;
                BacklogMinutes = (float)engine.Queues.Backlog(Center.Id, engine.Tick);
                State = empty >= OutOfStockSkus ? CenterState.OutOfStock
                    : below >= LowStockShare * engine.World.Skus.Count ? CenterState.LowStock
                    : CenterState.Normal;
            }

            ring.color = State == CenterState.OutOfStock ? Palette.Critical
                : State == CenterState.LowStock ? Palette.Warning
                : Palette.Ok;

            float fill = Mathf.Clamp01(BacklogMinutes / QueueBarMaxMinutes);
            bar.transform.localScale = new Vector3(Mathf.Max(0.001f, 2.4f * fill), 0.28f, 1f);
            bar.transform.localPosition = new Vector3(-1.2f + 1.2f * fill, -1.75f, 0f);
            bar.color = fill < 0.33f ? Palette.Ok : fill < 0.66f ? Palette.Warning : Palette.Critical;
        }

        public string Describe(SimulationEngine engine)
        {
            Refresh(engine);
            string state = State == CenterState.OutOfStock ? "ürün tükendi"
                : State == CenterState.LowStock ? "düşük stok" : "normal";
            var sb = new StringBuilder();
            sb.AppendLine($"Durum: {state}");
            sb.AppendLine($"Güvenlik stoğu altındaki ürün: {SkusBelowSafety}/{engine.World.Skus.Count}");
            sb.AppendLine($"Tükenen ürün: {SkusOut}");
            sb.AppendLine($"Paketleme kuyruğu: {BacklogMinutes:0} dk");
            sb.AppendLine($"Kapasite: {Center.CapacityPerHour} paket/saat");
            int pending = 0;
            foreach (ReplenishmentOrder r in engine.Replenishment.Pending)
                if (r.CenterId == Center.Id) pending += r.Quantity;
            sb.Append($"Yoldaki yenileme: {pending} adet");
            return sb.ToString();
        }
    }
}
