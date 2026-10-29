using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Fulfilment.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fulfilment.Presentation
{
    /// <summary>
    /// Right sidebar. With nothing selected it follows the latest decision; a selected parcel or region shows the
    /// decision behind that order; a selected center shows the center. A decision is explained candidate by
    /// candidate: for the Utility AI a stacked bar of weighted criterion scores, for baselines their rule and
    /// the raw numbers they compare.
    /// </summary>
    public sealed class DecisionPanel
    {
        private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

        public static readonly Dictionary<string, (string label, Color colour)> CriterionStyle = new Dictionary<string, (string, Color)>
        {
            ["cost"] = ("Maliyet", Palette.Hex("4FC3F7")),
            ["time"] = ("Süre", Palette.Hex("FFD54F")),
            ["stock"] = ("Stok", Palette.Hex("66BB6A")),
            ["split"] = ("Split", Palette.Hex("BA68C8")),
            ["fill"] = ("Doluluk", Palette.Hex("FF8A65"))
        };

        private readonly VisualElement root;
        private readonly Label title;
        private readonly Label subtitle;
        private readonly Label policyLine;
        private readonly Label rule;
        private readonly VisualElement legend;
        private readonly VisualElement rows;
        private readonly Label body;
        private OrderRecord shown;
        private string shownTitle;
        private OrderStatus shownStatus;

        public VisualElement Root => root;

        public DecisionPanel()
        {
            root = new VisualElement();
            root.AddToClassList("panel");
            root.AddToClassList("info-panel");

            title = Add(new Label(), "info-title");
            subtitle = Add(new Label(), "info-sub");
            policyLine = Add(new Label(), "policy-line");
            rule = Add(new Label(), "rule");
            legend = Add(new VisualElement(), "legend");
            rows = Add(new VisualElement(), "candidate-list");
            body = Add(new Label(), "info-body");
            var hint = Add(new Label("Seçim yokken son kararı gösterir. Pakete veya bölgeye tıklayınca o siparişin kararı, depoya tıklayınca depo bilgisi açılır. Esc: seçimi kaldır."), "hint");
            hint.pickingMode = PickingMode.Ignore;
        }

        private T Add<T>(T element, string cls) where T : VisualElement
        {
            element.AddToClassList(cls);
            root.Add(element);
            return element;
        }

        public void Refresh(SimulationEngine engine, ISelectable selected)
        {
            if (selected is CenterView center)
            {
                ShowText(center.Title, center.Describe(engine));
                return;
            }

            OrderRecord record = selected switch
            {
                ParcelView parcel => OrderOf(engine, parcel),
                RegionView region => engine.Records.LastOrDefault(r => r.Order.RegionId == region.Region.Id),
                _ => engine.Records.Count > 0 ? engine.Records[engine.Records.Count - 1] : null
            };

            string header = selected == null ? "Son karar" : selected.Title;
            if (record == null)
            {
                ShowText(header, selected is RegionView ? "Bu bölgeden henüz sipariş gelmedi." : "Henüz sipariş yok. Simülasyonu başlatın.");
                return;
            }
            if (record == shown && header == shownTitle && record.Status == shownStatus) return; // nothing changed
            ShowDecision(engine, record, header);
        }

        private static OrderRecord OrderOf(SimulationEngine engine, ParcelView parcel)
        {
            Shipment s = engine.ShipmentOf(parcel.ShipmentId);
            return s == null ? null : engine.RecordOf(s.OrderId);
        }

        private void ShowText(string heading, string text)
        {
            shown = null;
            shownTitle = null;
            title.text = heading;
            subtitle.text = "";
            policyLine.text = "";
            rule.text = "";
            legend.Clear();
            rows.Clear();
            body.text = text;
        }

        private void ShowDecision(SimulationEngine engine, OrderRecord record, string header)
        {
            shown = record;
            shownTitle = header;
            shownStatus = record.Status;
            Order o = record.Order;
            World w = engine.World;

            title.text = $"{header} · Sipariş #{o.Id}";
            subtitle.text = $"{w.Regions[o.RegionId].Code} {w.Regions[o.RegionId].Name} · {(o.Priority == OrderPriority.Express ? "Ekspres" : "Standart")} · " +
                            $"{o.Lines.Count} ürün, {o.TotalUnits} adet\nGeliş {SimTime.Format(o.CreatedTick)} · söz {SimTime.Format(o.PromisedTick)} · {Status(record)}";
            DecisionTrace trace = record.Trace;
            policyLine.text = $"Politika: {trace?.Policy ?? engine.Policy.Name}";
            rule.text = trace?.Rule ?? "";
            if (!string.IsNullOrEmpty(trace?.Note)) rule.text += "\n" + trace.Note;
            body.text = "";

            legend.Clear();
            if (trace != null && trace.HasScores)
                foreach (string key in trace.Criteria)
                {
                    var chip = new Label($"{Name(key)} {trace.Weights[trace.Criteria.ToList().IndexOf(key)] * 100:0}%");
                    chip.AddToClassList("legend-chip");
                    chip.style.borderLeftColor = Colour(key);
                    legend.Add(chip);
                }

            rows.Clear();
            IEnumerable<int> order = Enumerable.Range(0, record.Candidates.Count);
            order = trace != null && trace.HasScores
                ? order.OrderByDescending(i => trace.Totals[i])
                : order.OrderBy(i => i == record.ChosenIndex ? 0 : 1);
            foreach (int i in order.Take(6))
                rows.Add(CandidateRow(engine, record, trace, i));
        }

        private VisualElement CandidateRow(SimulationEngine engine, OrderRecord record, DecisionTrace trace, int i)
        {
            FulfilmentPlan plan = record.Candidates[i];
            PlanEvaluation e = record.CandidateEvaluations[i];
            bool chosen = i == record.ChosenIndex;

            var row = new VisualElement();
            row.AddToClassList("candidate");
            row.EnableInClassList("chosen", chosen);

            var head = new VisualElement();
            head.AddToClassList("candidate-head");
            var name = new Label((chosen ? "✔ " : "") + PlanLabel(engine.World, plan, e));
            name.AddToClassList("candidate-name");
            head.Add(name);
            if (trace != null && trace.HasScores)
            {
                var total = new Label(trace.Totals[i].ToString("0.00", Tr));
                total.AddToClassList("candidate-total");
                head.Add(total);
            }
            row.Add(head);

            if (trace != null && trace.HasScores)
            {
                var bar = new VisualElement();
                bar.AddToClassList("utility-bar");
                for (int k = 0; k < trace.Criteria.Count; k++)
                {
                    var seg = new VisualElement();
                    seg.AddToClassList("utility-seg");
                    seg.style.width = Length.Percent((float)(trace.Contribution(i, k) * 100.0));
                    seg.style.backgroundColor = Colour(trace.Criteria[k]);
                    seg.tooltip = $"{Name(trace.Criteria[k])}: puan {trace.Scores[i][k]:0.00} × ağırlık {trace.Weights[k]:0.00}";
                    bar.Add(seg);
                }
                row.Add(bar);
            }

            var detail = new Label($"Kargo {e.ShippingCost.ToString("N0", Tr)} TL · pay {SlackText(e)} · {e.PackageCount} paket" +
                                   (e.LostUnits > 0 ? $" · {e.LostUnits} eksik" : "") +
                                   (e.OpportunityCost > 0.5 ? $" · fırsat {e.OpportunityCost.ToString("N0", Tr)} TL" : ""));
            detail.AddToClassList("candidate-detail");
            row.Add(detail);
            return row;
        }

        public static string PlanLabel(World world, FulfilmentPlan plan, PlanEvaluation e)
        {
            if (e.PackageCount == 0) return "Hiçbir depoda yok";
            string centers = string.Join(" + ", plan.Centers().OrderBy(c => c).Select(c => world.Centers[c].Code));
            string label = plan.Kind == PlanKind.SingleCenter ? $"{centers} tek depo" : $"Split: {centers}";
            return e.LostUnits > 0 ? $"{label} (eksik)" : label;
        }

        private static string SlackText(PlanEvaluation e) =>
            e.SlackMinutes >= 0 ? $"{e.SlackMinutes:0} dk" : $"{-e.SlackMinutes:0} dk geç";

        private static string Status(OrderRecord r)
        {
            switch (r.Status)
            {
                case OrderStatus.Lost: return "kayıp";
                case OrderStatus.Delivered: return r.IsLate ? $"geç teslim ({r.LateMinutes:0} dk)" : "zamanında teslim";
                default: return "yolda";
            }
        }

        public static string Name(string key) => CriterionStyle.TryGetValue(key, out var s) ? s.label : key;
        public static Color Colour(string key) => CriterionStyle.TryGetValue(key, out var s) ? s.colour : Color.gray;
    }
}
