using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Fulfilment.AI;
using Fulfilment.Simulation;
using UnityEngine.UIElements;

namespace Fulfilment.Presentation
{
    /// <summary>
    /// Left sidebar: the scenario panel (load level, regime, policy, seed, stock level and split penalty; applied by
    /// rebuilding the episode, no code change needed) and the live inventory panel with one card per center and
    /// low-stock warnings.
    /// </summary>
    public sealed class SidePanel
    {
        private const int WarningsPerCenter = 3;
        private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

        private readonly SimulationRunner runner;
        private readonly VisualElement root;
        private readonly DropdownField load;
        private readonly DropdownField regime;
        private readonly DropdownField policy;
        private readonly IntegerField seed;
        private readonly Slider stock;
        private readonly Slider split;
        private readonly VisualElement cards;
        private readonly List<CenterCard> centerCards = new List<CenterCard>();

        private sealed class CenterCard
        {
            public VisualElement Root;
            public Label State;
            public VisualElement HealthFill;
            public Label Stats;
            public Label Warnings;
        }

        public VisualElement Root => root;

        public SidePanel(SimulationRunner runner)
        {
            this.runner = runner;
            ConfigSet cfg = runner.Configs;

            root = new VisualElement();
            root.AddToClassList("panel");
            root.AddToClassList("side-panel");

            root.Add(Heading("Senaryo"));
            load = Dropdown("Yük", cfg.Scenarios.Select(s => s.displayName).ToList(), cfg.Scenarios.ToList().FindIndex(s => s.code == runner.Scenario));
            regime = Dropdown("Rejim", cfg.Regimes.Select(r => r.displayName).ToList(), cfg.Regimes.ToList().FindIndex(r => r.code == runner.Regime));
            policy = Dropdown("Politika", PolicyFactory.Names.ToList(), PolicyFactory.Names.ToList().IndexOf(runner.Policy));
            seed = new IntegerField("Seed") { value = runner.Seed };
            seed.AddToClassList("field");
            root.Add(seed);

            stock = SliderWithValue("Stok seviyesi", 0.5f, 1.5f, runner.StockMultiplier, v => $"×{v:0.00}");
            split = SliderWithValue("Split cezası", 0f, 120f, runner.SplitPenalty, v => $"{v:0} TL");

            var apply = new Button(Apply) { text = "Uygula ve yeniden başlat" };
            apply.AddToClassList("tool-button");
            apply.AddToClassList("primary");
            apply.AddToClassList("apply-button");
            root.Add(apply);

            root.Add(Heading("Stok durumu"));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("stock-scroll");
            cards = scroll.contentContainer;
            root.Add(scroll);
        }

        private static Label Heading(string text)
        {
            var l = new Label(text);
            l.AddToClassList("section-title");
            return l;
        }

        private DropdownField Dropdown(string label, List<string> choices, int index)
        {
            var d = new DropdownField(label, choices, System.Math.Max(0, index));
            d.AddToClassList("field");
            root.Add(d);
            return d;
        }

        private Slider SliderWithValue(string label, float low, float high, float value, System.Func<float, string> format)
        {
            var s = new Slider(label, low, high) { value = value };
            s.AddToClassList("field");
            var valueLabel = new Label(format(value));
            valueLabel.AddToClassList("slider-value");
            s.Add(valueLabel);
            s.RegisterValueChangedCallback(e => valueLabel.text = format(e.newValue));
            root.Add(s);
            return s;
        }

        /// <summary>Shows the settings of the running episode (it may have been started from the command line).</summary>
        public void SyncFromRunner()
        {
            ConfigSet cfg = runner.Configs;
            load.index = System.Math.Max(0, cfg.Scenarios.ToList().FindIndex(s => s.code == runner.Scenario));
            regime.index = System.Math.Max(0, cfg.Regimes.ToList().FindIndex(r => r.code == runner.Regime));
            policy.index = System.Math.Max(0, PolicyFactory.Names.ToList().IndexOf(runner.Policy));
            seed.SetValueWithoutNotify(runner.Seed);
            stock.value = runner.StockMultiplier;
            split.value = runner.SplitPenalty;
        }

        private void Apply()
        {
            ConfigSet cfg = runner.Configs;
            runner.SetParameters(stock.value, split.value);
            runner.Configure(cfg.Scenarios[load.index].code, cfg.Regimes[regime.index].code, PolicyFactory.Names[policy.index], seed.value);
        }

        /// <summary>Called when an episode starts (the map is rebuilt): one card per center.</summary>
        public void BuildCards(SimulationEngine engine)
        {
            cards.Clear();
            centerCards.Clear();
            foreach (FulfilmentCenter c in engine.World.Centers)
            {
                var card = new CenterCard { Root = new VisualElement() };
                card.Root.AddToClassList("center-card");

                var head = new VisualElement();
                head.AddToClassList("card-head");
                var name = new Label($"{c.Code} {c.Name}");
                name.AddToClassList("card-name");
                card.State = new Label();
                card.State.AddToClassList("state-chip");
                head.Add(name);
                head.Add(card.State);
                card.Root.Add(head);

                var health = new VisualElement();
                health.AddToClassList("health-bar");
                card.HealthFill = new VisualElement();
                card.HealthFill.AddToClassList("health-fill");
                health.Add(card.HealthFill);
                card.Root.Add(health);

                card.Stats = new Label();
                card.Stats.AddToClassList("card-stats");
                card.Root.Add(card.Stats);
                card.Warnings = new Label();
                card.Warnings.AddToClassList("card-warnings");
                card.Root.Add(card.Warnings);

                cards.Add(card.Root);
                centerCards.Add(card);
            }
        }

        public void Refresh(SimulationEngine engine, MapView map)
        {
            if (map == null || map.Centers.Count != centerCards.Count) return;
            int skus = engine.World.Skus.Count;
            for (int c = 0; c < centerCards.Count; c++)
            {
                CenterView view = map.Centers[c];
                CenterCard card = centerCards[c];
                float healthy = 1f - view.SkusBelowSafety / (float)skus;

                card.State.text = view.State == CenterState.OutOfStock ? "Tükendi" : view.State == CenterState.LowStock ? "Düşük stok" : "Normal";
                card.State.EnableInClassList("warn", view.State == CenterState.LowStock);
                card.State.EnableInClassList("bad", view.State == CenterState.OutOfStock);
                card.Root.EnableInClassList("alert", view.State != CenterState.Normal);
                card.HealthFill.style.width = Length.Percent(healthy * 100f);
                card.HealthFill.EnableInClassList("warn", healthy < 0.8f);
                card.HealthFill.EnableInClassList("bad", healthy < 0.5f);
                card.Stats.text = $"Güvenlik altı {view.SkusBelowSafety}/{skus} · Tükenen {view.SkusOut} · Kuyruk {view.BacklogMinutes:0} dk";

                // Lowest coverage SKUs of this center: on hand / safety stock.
                var low = Enumerable.Range(0, skus)
                    .Select(s => (sku: s, onHand: engine.Inventory.OnHand(c, s), safety: engine.Costs.SafetyStock(c, s)))
                    .Where(x => x.safety > 0 && x.onHand < x.safety)
                    .OrderBy(x => x.onHand / x.safety)
                    .Take(WarningsPerCenter)
                    .Select(x => $"⚠ {engine.World.Skus[x.sku].Code} {engine.World.Skus[x.sku].Name}: {x.onHand}/{x.safety.ToString("0", Tr)}")
                    .ToList();
                card.Warnings.text = low.Count > 0 ? string.Join("\n", low) : "Uyarı yok";
                card.Warnings.EnableInClassList("none", low.Count == 0);
            }
        }
    }
}
