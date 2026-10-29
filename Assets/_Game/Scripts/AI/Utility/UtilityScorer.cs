using System;
using System.Collections.Generic;
using Fulfilment.Simulation;

namespace Fulfilment.AI
{
    /// <summary>
    /// Utility AI scoring (Zorunlu AI 1). Each candidate plan gets a score in [0, 1] per criterion, and the total
    /// is the weighted sum (weights normalised to 1):
    ///   cost  – shipping cost, min–max normalised across the candidates (cheapest 1, dearest 0, all equal 1)
    ///   time  – slack to the promised time in minutes, logistic curve
    ///   stock – after the plan, the lowest stock / safety-stock ratio among the touched (center, SKU) pairs, capped at 1
    ///   split – 1 / number of parcels
    ///   fill  – shipped units / ordered units
    /// The same scorer is used by the fixed-weight Utility AI and, later, by the adaptive policy that picks weights.
    /// </summary>
    public sealed class UtilityScorer
    {
        private readonly CriterionConfig[] criteria;
        private readonly double[] baseWeights;

        public IReadOnlyList<string> Keys { get; }
        public double[] Weights => (double[])baseWeights.Clone();

        public UtilityScorer(UtilityConfig config)
        {
            List<string> errors = UtilityCriteria.Validate(config);
            if (errors.Count > 0)
                throw new ArgumentException("Invalid utility config:\n- " + string.Join("\n- ", errors));

            criteria = config.criteria;
            var keys = new string[criteria.Length];
            baseWeights = new double[criteria.Length];
            double sum = 0.0;
            for (int i = 0; i < criteria.Length; i++)
            {
                keys[i] = criteria[i].key;
                sum += criteria[i].weight;
            }
            for (int i = 0; i < criteria.Length; i++)
                baseWeights[i] = criteria[i].weight / sum;
            Keys = keys;
        }

        /// <summary>Scores every candidate with the configured weights, or with <paramref name="weights"/> if given.</summary>
        public DecisionTrace Score(DecisionContext context, double[] weights = null, string policyName = "Utility")
        {
            int n = context.Candidates.Count;
            weights ??= baseWeights;
            var trace = new DecisionTrace
            {
                Policy = policyName,
                Rule = "Her plan 5 kritere göre 0–1 arası puanlanır; ağırlıklı toplamı en yüksek olan seçilir.",
                Criteria = Keys,
                Weights = (double[])weights.Clone(),
                Raw = new double[n][],
                Scores = new double[n][],
                Totals = new double[n]
            };

            double minCost = double.MaxValue, maxCost = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                double c = context.Evaluations[i].ShippingCost;
                minCost = Math.Min(minCost, c);
                maxCost = Math.Max(maxCost, c);
            }

            for (int i = 0; i < n; i++)
            {
                trace.Raw[i] = new double[criteria.Length];
                trace.Scores[i] = new double[criteria.Length];
                for (int k = 0; k < criteria.Length; k++)
                {
                    double raw = RawValue(criteria[k].key, context, i);
                    double normalised = Normalise(criteria[k].key, raw, minCost, maxCost);
                    double score = Clamp01(Curve(criteria[k], normalised, raw));
                    trace.Raw[i][k] = raw;
                    trace.Scores[i][k] = score;
                    trace.Totals[i] += weights[k] * score;
                }
            }

            trace.Chosen = ArgMax(trace.Totals, context);
            return trace;
        }

        private static double RawValue(string key, DecisionContext context, int i)
        {
            PlanEvaluation e = context.Evaluations[i];
            switch (key)
            {
                case UtilityCriteria.Cost: return e.ShippingCost;
                case UtilityCriteria.Time: return e.SlackMinutes;
                case UtilityCriteria.Stock: return StockRatioAfter(context, context.Candidates[i]);
                case UtilityCriteria.Split: return e.PackageCount;
                case UtilityCriteria.Fill:
                    int units = e.ShippedUnits + e.LostUnits;
                    return units > 0 ? e.ShippedUnits / (double)units : 1.0;
                default: return 0.0;
            }
        }

        /// <summary>Lowest (stock after the plan) / (safety stock) among the touched pairs; 1 or more is healthy.</summary>
        public static double StockRatioAfter(DecisionContext context, FulfilmentPlan plan)
        {
            double worst = double.MaxValue;
            foreach (Allocation a in plan.Allocations)
            {
                double safety = context.Costs.SafetyStock(a.CenterId, a.SkuId);
                if (safety <= 0.0) continue;
                double after = context.Inventory.OnHand(a.CenterId, a.SkuId) - a.Quantity;
                worst = Math.Min(worst, after / safety);
            }
            return worst == double.MaxValue ? 1.0 : Math.Max(0.0, worst);
        }

        private static double Normalise(string key, double raw, double minCost, double maxCost)
        {
            switch (key)
            {
                case UtilityCriteria.Cost: return maxCost - minCost < 1e-9 ? 1.0 : 1.0 - (raw - minCost) / (maxCost - minCost);
                case UtilityCriteria.Stock: return Math.Min(1.0, raw);
                case UtilityCriteria.Split: return 1.0 / Math.Max(1.0, raw);
                case UtilityCriteria.Fill: return raw;
                default: return raw; // time: the logistic curve works on the raw slack
            }
        }

        private static double Curve(CriterionConfig c, double normalised, double raw)
        {
            switch (c.curve)
            {
                case "power": return Math.Pow(Clamp01(normalised), c.exponent);
                case "logistic": return 1.0 / (1.0 + Math.Exp(-(raw - c.midpoint) / c.scale));
                default: return normalised;
            }
        }

        // Highest total; ties go to the plan that loses fewer units, then to the earlier candidate.
        private static int ArgMax(double[] totals, DecisionContext context)
        {
            int best = 0;
            for (int i = 1; i < totals.Length; i++)
            {
                if (totals[i] > totals[best] + 1e-12) best = i;
                else if (Math.Abs(totals[i] - totals[best]) <= 1e-12 &&
                         context.Evaluations[i].LostUnits < context.Evaluations[best].LostUnits) best = i;
            }
            return best;
        }

        private static double Clamp01(double v) => v < 0.0 ? 0.0 : v > 1.0 ? 1.0 : v;
    }
}
