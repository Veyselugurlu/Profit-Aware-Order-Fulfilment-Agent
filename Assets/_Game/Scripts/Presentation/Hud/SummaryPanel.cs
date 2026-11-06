using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Fulfilment.AI;
using Fulfilment.Experiments;
using Fulfilment.Simulation;
using UnityEngine.UIElements;

namespace Fulfilment.Presentation
{
    /// <summary>
    /// End-of-episode summary: the KPIs of the episode that just finished next to every other policy run headless
    /// on the same setup (same seed, same orders, same overrides), the best value per row marked, and buttons to
    /// replay the same episode with another policy.
    /// </summary>
    public sealed class SummaryPanel
    {
        private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

        private sealed class Row
        {
            public string Label;
            public Func<KpiTracker, double> Value;
            public Func<double, string> Format;
            public bool HigherIsBetter;
        }

        private static readonly Row[] Rows =
        {
            new Row { Label = "Kâr (TL)", Value = k => k.Profit, Format = v => v.ToString("N0", Tr), HigherIsBetter = true },
            new Row { Label = "Toplam maliyet (TL)", Value = k => k.TotalCost, Format = v => v.ToString("N0", Tr) },
            new Row { Label = "Stok bitmesi", Value = k => k.StockoutRate, Format = Pct },
            new Row { Label = "Split oranı", Value = k => k.SplitRate, Format = Pct },
            new Row { Label = "Gecikme oranı", Value = k => k.LateRate, Format = Pct },
            new Row { Label = "Servis seviyesi", Value = k => k.ServiceLevel, Format = Pct, HigherIsBetter = true },
            new Row { Label = "Ort. mesafe (km)", Value = k => k.AverageFulfilmentKm, Format = v => v.ToString("0.00", Tr) },
            new Row { Label = "Güvenlik stoğu ihlali", Value = k => k.DepletionEvents, Format = v => v.ToString("N0", Tr) }
        };

        private readonly SimulationRunner runner;
        private readonly VisualElement overlay;
        private readonly Label title;
        private readonly VisualElement grid;
        private readonly VisualElement buttons;

        public VisualElement Root => overlay;
        public bool IsOpen => overlay.style.display == DisplayStyle.Flex;

        public SummaryPanel(SimulationRunner runner)
        {
            this.runner = runner;
            overlay = new VisualElement();
            overlay.AddToClassList("summary-overlay");

            var card = new VisualElement();
            card.AddToClassList("panel");
            card.AddToClassList("summary-card");
            overlay.Add(card);

            title = new Label();
            title.AddToClassList("summary-title");
            card.Add(title);
            var note = new Label("Diğer politikalar aynı seed, aynı siparişler ve aynı ayarlarla ekransız koşturuldu. ✔ satırdaki en iyi değer.");
            note.AddToClassList("summary-note");
            card.Add(note);

            grid = new VisualElement();
            grid.AddToClassList("summary-grid");
            card.Add(grid);

            buttons = new VisualElement();
            buttons.AddToClassList("summary-buttons");
            card.Add(buttons);
            Hide();
        }

        public void Hide() => overlay.style.display = DisplayStyle.None;

        public void Show(SimulationEngine finished)
        {
            ConfigSet cfg = runner.Configs;
            var columns = new List<(string policy, KpiTracker kpis)>();
            foreach (string policy in cfg.AvailablePolicies)
            {
                KpiTracker k = policy == runner.Policy
                    ? finished.Kpis
                    : EpisodeRunner.Run(runner.CurrentSetup(), cfg.CreatePolicy(policy)).Kpis;
                columns.Add((policy, k));
            }

            title.text = $"Episode özeti · Senaryo {cfg.Scenario(runner.Scenario).displayName} · Rejim {cfg.Regime(runner.Regime).displayName} · Seed {runner.Seed}";
            grid.Clear();
            grid.Add(HeaderRow(columns.Select(c => c.policy).ToList()));
            foreach (Row row in Rows)
            {
                double[] values = columns.Select(c => row.Value(c.kpis)).ToArray();
                double best = row.HigherIsBetter ? values.Max() : values.Min();
                var line = new VisualElement();
                line.AddToClassList("summary-row");
                line.Add(Cell(row.Label, "summary-label"));
                for (int i = 0; i < values.Length; i++)
                {
                    bool isBest = Math.Abs(values[i] - best) < 1e-9 && values.Count(v => Math.Abs(v - best) < 1e-9) < values.Length;
                    Label cell = Cell((isBest ? "✔ " : "") + row.Format(values[i]), "summary-value");
                    cell.EnableInClassList("best", isBest);
                    cell.EnableInClassList("current", columns[i].policy == runner.Policy);
                    line.Add(cell);
                }
                grid.Add(line);
            }

            buttons.Clear();
            foreach (string policy in cfg.AvailablePolicies)
            {
                string p = policy;
                var b = new Button(() => Replay(p)) { text = $"{p} ile oynat" };
                b.AddToClassList("tool-button");
                if (p == runner.Policy) b.AddToClassList("primary");
                buttons.Add(b);
            }
            var close = new Button(Hide) { text = "Kapat" };
            close.AddToClassList("tool-button");
            buttons.Add(close);

            overlay.style.display = DisplayStyle.Flex;
        }

        private VisualElement HeaderRow(List<string> policies)
        {
            var line = new VisualElement();
            line.AddToClassList("summary-row");
            line.AddToClassList("summary-head");
            line.Add(Cell("", "summary-label"));
            foreach (string p in policies)
            {
                Label c = Cell(p == runner.Policy ? $"{p}\n(bu çalıştırma)" : p, "summary-value"); // one line would overflow the column
                c.EnableInClassList("current", p == runner.Policy);
                line.Add(c);
            }
            return line;
        }

        private void Replay(string policy)
        {
            Hide();
            runner.Configure(runner.Scenario, runner.Regime, policy, runner.Seed);
            runner.Play();
        }

        private static Label Cell(string text, string cls)
        {
            var l = new Label(text);
            l.AddToClassList(cls);
            return l;
        }

        private static string Pct(double v) => "%" + (v * 100.0).ToString("0.00", Tr);
    }
}
