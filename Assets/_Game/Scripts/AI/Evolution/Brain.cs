using System;
using System.Collections.Generic;
using System.Linq;
using Fulfilment.Simulation;

namespace Fulfilment.AI
{
    /// <summary>
    /// Evolved decision-table policy, following the Week 3 Brain: sense the situation, read the gene for that
    /// situation, act. In the lecture one Raycast sensor ("ground ahead?") selects gene 0 or gene 1 and the gene
    /// is a movement action. Here three binary sensors on the arriving order select one of eight genes, and the
    /// gene picks the Utility weight profile the order is decided with; the Utility AI then scores the candidate
    /// plans as usual, so every decision stays explainable.
    ///   sensor 0  express    – the order is express
    ///   sensor 1  stock risk – the nearest center cannot ship the whole order, or shipping it would take a SKU below safety stock
    ///   sensor 2  busy       – the nearest center's picking queue is longer than <see cref="BusyMinutes"/>
    ///   state = 4·express + 2·stockRisk + busy
    /// </summary>
    public sealed class Brain : IFulfilmentPolicy, IExplainablePolicy
    {
        public const double BusyMinutes = 30.0;
        public static readonly string[] SensorNames = { "ekspres", "stok riski", "kuyruk yoğun" };

        private readonly UtilityScorer scorer;
        private readonly WeightProfilesConfig profiles;

        public DNA Dna { get; }
        public string Name { get; }
        public DecisionTrace LastTrace { get; private set; }

        /// <summary>How often each state was sensed in the current episode (for analysis).</summary>
        public int[] StateCounts { get; } = new int[DNA.StateCount];

        public Brain(DNA dna, UtilityConfig utility, WeightProfilesConfig profiles, string name = "Evolved")
        {
            Dna = dna ?? throw new ArgumentNullException(nameof(dna));
            scorer = new UtilityScorer(utility);
            this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            List<string> errors = profiles.Validate(scorer.Keys.Count);
            if (errors.Count > 0) throw new ArgumentException("Invalid weight profiles:\n- " + string.Join("\n- ", errors));
            if (profiles.profiles.Length < DNA.ActionCount)
                throw new ArgumentException($"The genome has {DNA.ActionCount} actions but only {profiles.profiles.Length} profiles are defined.");
            Name = name;
        }

        public void Reset(int seed)
        {
            LastTrace = null;
            Array.Clear(StateCounts, 0, StateCounts.Length);
        }

        public int Choose(DecisionContext context)
        {
            bool[] sensors = Sense(context);
            int state = State(sensors);
            int action = Dna.GetGene(state);
            WeightProfile profile = profiles.profiles[action];
            StateCounts[state]++;

            LastTrace = scorer.Score(context, profile.weights, Name);
            LastTrace.Rule = "Sensörler siparişin durumunu algılar, DNA o durumun genini okur, gen hangi ağırlık profiliyle karar verileceğini seçer; planları Utility AI puanlar.";
            LastTrace.Note = $"Sensörler: {string.Join(", ", SensorNames.Select((n, i) => $"{n}={(sensors[i] ? 1 : 0)}"))} → gen[{state}] = {action} ({profile.label}) · DNA {Dna}";
            return LastTrace.Chosen;
        }

        public void Observe(DecisionFeedback feedback) { }

        public static int State(bool[] sensors) => (sensors[0] ? 4 : 0) + (sensors[1] ? 2 : 0) + (sensors[2] ? 1 : 0);

        /// <summary>The three sensor readings for an arriving order (lecture TODO C: the sensor).</summary>
        public static bool[] Sense(DecisionContext context)
        {
            Order order = context.Order;
            int nearest = Enumerable.Range(0, context.World.Centers.Count)
                .OrderBy(c => context.Costs.DistanceKm(c, order.RegionId))
                .First();

            bool stockRisk = !context.Inventory.CanFulfilAll(nearest, order.Lines);
            if (!stockRisk)
                foreach (OrderLine line in order.Lines)
                {
                    double safety = context.Costs.SafetyStock(nearest, line.SkuId);
                    if (context.Inventory.OnHand(nearest, line.SkuId) - line.Quantity < safety)
                    {
                        stockRisk = true;
                        break;
                    }
                }

            bool busy = context.Queues.Backlog(nearest, context.Tick) > BusyMinutes;
            return new[] { order.Priority == OrderPriority.Express, stockRisk, busy };
        }
    }
}
