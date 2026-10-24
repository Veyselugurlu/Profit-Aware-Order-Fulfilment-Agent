using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Fulfilment.Simulation;

namespace Fulfilment.Experiments
{
    /// <summary>
    /// CSV output of episodes. Numbers use the invariant culture (dot decimal separator) so the analysis
    /// scripts read them the same way on every machine.
    /// </summary>
    public static class CsvExport
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static readonly string[] KpiColumns =
        {
            "policy", "scenario", "regime", "seed",
            "orders", "completed_orders", "lost_orders", "units_ordered", "units_lost", "packages",
            "revenue", "cogs", "shipping_cost", "split_cost", "stockout_penalty", "lateness_cost", "total_cost", "profit",
            "opportunity_cost", "depletion_events",
            "stockout_rate", "order_stockout_rate", "split_rate", "late_rate", "on_time_rate", "avg_late_hours",
            "avg_fulfilment_km", "service_level", "ticks", "wall_seconds"
        };

        public static string KpiHeader => string.Join(",", KpiColumns);

        public static string KpiRow(EpisodeResult r)
        {
            KpiTracker k = r.Kpis;
            return Join(
                Text(r.Policy), Text(r.Scenario), Text(r.Regime), Int(r.Seed),
                Int(k.Orders), Int(k.CompletedOrders), Int(k.LostOrders), Int(k.UnitsOrdered), Int(k.UnitsLost), Int(k.Packages),
                Num(k.Revenue), Num(k.Cogs), Num(k.ShippingCost), Num(k.SplitCost), Num(k.StockoutPenalty), Num(k.LatenessCost),
                Num(k.TotalCost), Num(k.Profit),
                Num(k.OpportunityCost), Int(k.DepletionEvents),
                Num(k.StockoutRate), Num(k.OrderStockoutRate), Num(k.SplitRate), Num(k.LateRate), Num(k.OnTimeRate),
                Num(k.AverageLateHours), Num(k.AverageFulfilmentKm), Num(k.ServiceLevel),
                Int(r.TicksSimulated), Num(r.WallSeconds));
        }

        public static void WriteKpis(string path, IEnumerable<EpisodeResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine(KpiHeader);
            foreach (EpisodeResult r in results)
                sb.AppendLine(KpiRow(r));
            Write(path, sb.ToString());
        }

        public static readonly string[] OrderColumns =
        {
            "order_id", "created_tick", "region", "priority", "lines", "units", "promised_tick",
            "plan_kind", "candidates", "chosen_index", "centers", "packages", "lost_units",
            "revenue", "cogs", "shipping_cost", "split_cost", "stockout_penalty", "opportunity_cost",
            "expected_slack_min", "status", "completion_tick", "late_minutes", "lateness_cost", "profit"
        };

        public static void WriteOrders(string path, EpisodeResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", OrderColumns));
            foreach (OrderRecord rec in result.Records)
            {
                Order o = rec.Order;
                PlanEvaluation e = rec.Evaluation;
                string centers = string.Join("|", rec.Plan.Centers().OrderBy(c => c).Select(c => result.World.Centers[c].Code));
                sb.AppendLine(Join(
                    Int(o.Id), Int(o.CreatedTick), Text(result.World.Regions[o.RegionId].Code), Text(o.Priority.ToString()),
                    Int(o.Lines.Count), Int(o.TotalUnits), Int(o.PromisedTick),
                    Text(rec.Plan.Kind.ToString()), Int(rec.CandidateCount), Int(rec.ChosenIndex), Text(centers),
                    Int(e.PackageCount), Int(e.LostUnits),
                    Num(e.Revenue), Num(e.Cogs), Num(e.ShippingCost), Num(e.SplitCost), Num(e.StockoutPenalty), Num(e.OpportunityCost),
                    Num(e.SlackMinutes), Text(rec.Status.ToString()), Int(rec.CompletionTick), Num(rec.LateMinutes),
                    Num(rec.LatenessCost), Num(rec.RealisedProfit)));
            }
            Write(path, sb.ToString());
        }

        private static void Write(string path, string content)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        private static string Join(params string[] cells) => string.Join(",", cells);
        private static string Int(int v) => v.ToString(Inv);
        private static string Num(double v) => v.ToString("0.######", Inv);

        private static string Text(string v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            return v.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        }
    }
}
