using System.Linq;
using Fulfilment.AI;
using Fulfilment.Simulation;
using NUnit.Framework;

namespace Fulfilment.Tests
{
    public class UtilityTests : SimulationTestBase
    {
        private DecisionContext Context(Order order, Inventory inv)
        {
            var plans = Candidates().Generate(order, inv);
            var queues = new CenterQueues(World, Configs.Cost);
            CostModel costs = Costs();
            var evals = plans.Select(p => costs.Evaluate(order, p, inv, queues, 0)).ToArray();
            return new DecisionContext(order, 0, plans, evals, World, inv, queues, costs);
        }

        private Inventory Stocked(int quantity)
        {
            Inventory inv = EmptyInventory();
            for (int c = 0; c < World.Centers.Count; c++)
                for (int s = 0; s < World.Skus.Count; s++)
                    inv.Add(c, s, quantity);
            return inv;
        }

        [Test]
        public void ShippedConfig_HasAtLeastFourCriteriaAndValidWeights()
        {
            Assert.IsEmpty(UtilityCriteria.Validate(Configs.Utility));
            Assert.GreaterOrEqual(Configs.Utility.criteria.Length, 4);
            var scorer = new UtilityScorer(Configs.Utility);
            Assert.AreEqual(1.0, scorer.Weights.Sum(), 1e-9);
        }

        [Test]
        public void Validation_RejectsTooFewCriteriaAndUnknownCurves()
        {
            var config = new UtilityConfig
            {
                name = "bad",
                criteria = new[]
                {
                    new CriterionConfig { key = "cost", weight = 1, curve = "linear" },
                    new CriterionConfig { key = "time", weight = 1, curve = "wiggly" }
                }
            };
            var errors = UtilityCriteria.Validate(config);
            Assert.That(errors, Has.Some.Contains("at least 4"));
            Assert.That(errors, Has.Some.Contains("unknown curve"));
        }

        [Test]
        public void TraceTotals_AreTheWeightedSumOfScores_AndChosenIsTheBest()
        {
            DecisionContext ctx = Context(MakeOrder(RegionNamed("R06"), (0, 1), (5, 2), (9, 1)), Stocked(30));
            DecisionTrace t = new UtilityScorer(Configs.Utility).Score(ctx);

            Assert.AreEqual(ctx.Candidates.Count, t.Totals.Length);
            for (int i = 0; i < t.Totals.Length; i++)
            {
                double sum = 0;
                for (int k = 0; k < t.Criteria.Count; k++)
                {
                    Assert.That(t.Scores[i][k], Is.InRange(0.0, 1.0));
                    sum += t.Contribution(i, k);
                }
                Assert.AreEqual(t.Totals[i], sum, 1e-12);
            }
            Assert.AreEqual(t.Totals.Max(), t.Totals[t.Chosen], 1e-12);
        }

        [Test]
        public void EqualCosts_GiveEveryCandidateTheFullCostScore()
        {
            // One candidate only: min == max, the cost criterion must not divide by zero (edge case EC-4).
            Inventory inv = EmptyInventory();
            inv.Add(0, 0, 5);
            DecisionContext ctx = Context(MakeOrder(0, (0, 1)), inv);
            DecisionTrace t = new UtilityScorer(Configs.Utility).Score(ctx);
            int cost = t.Criteria.ToList().IndexOf(UtilityCriteria.Cost);
            Assert.That(t.Scores.All(s => s[cost] == 1.0));
        }

        [Test]
        public void FillAndSplitScores_FollowTheirDefinitions()
        {
            Inventory inv = EmptyInventory();
            inv.Add(0, 0, 1);   // FC-K holds 1 of the 2 units of SKU 0
            inv.Add(2, 1, 5);   // FC-G holds SKU 1
            DecisionContext ctx = Context(MakeOrder(0, (0, 2), (1, 1)), inv);
            DecisionTrace t = new UtilityScorer(Configs.Utility).Score(ctx);
            int fill = t.Criteria.ToList().IndexOf(UtilityCriteria.Fill);
            int split = t.Criteria.ToList().IndexOf(UtilityCriteria.Split);

            for (int i = 0; i < ctx.Candidates.Count; i++)
            {
                PlanEvaluation e = ctx.Evaluations[i];
                Assert.AreEqual(e.ShippedUnits / 3.0, t.Raw[i][fill], 1e-12);
                Assert.AreEqual(1.0 / System.Math.Max(1, e.PackageCount), t.Scores[i][split], 1e-12);
            }
        }

        [Test]
        public void StockCriterion_PrefersTheCenterThatStaysAboveSafety()
        {
            int region = RegionNamed("R02");
            Inventory inv = Stocked(1000);
            // Make the nearest center for R02 hold exactly one unit: shipping from it empties the SKU.
            int nearest = Enumerable.Range(0, World.Centers.Count).OrderBy(c => Costs().DistanceKm(c, region)).First();
            inv.Set(nearest, 3, 1);
            DecisionContext ctx = Context(MakeOrder(region, (3, 1)), inv);

            double nearestRatio = UtilityScorer.StockRatioAfter(ctx, ctx.Candidates.First(p => p.Allocations[0].CenterId == nearest));
            Assert.AreEqual(0.0, nearestRatio, 1e-12);

            var stockOnly = new UtilityConfig
            {
                name = "stock-only",
                criteria = new[]
                {
                    new CriterionConfig { key = "cost", weight = 0, curve = "linear" },
                    new CriterionConfig { key = "time", weight = 0, curve = "logistic", scale = 30 },
                    new CriterionConfig { key = "stock", weight = 1, curve = "power", exponent = 0.5 },
                    new CriterionConfig { key = "split", weight = 0, curve = "linear" },
                    new CriterionConfig { key = "fill", weight = 0, curve = "linear" }
                }
            };
            int chosen = new UtilityPolicy(stockOnly).Choose(ctx);
            Assert.AreNotEqual(nearest, ctx.Candidates[chosen].Allocations[0].CenterId);
        }

        [Test]
        public void EngineRecords_KeepTheTraceThatMatchesTheDecision()
        {
            var engine = new SimulationEngine(Setup("high", "stable", 7), Configs.CreatePolicy(PolicyFactory.Utility));
            engine.RunToEnd();
            foreach (OrderRecord r in engine.Records.Take(300))
            {
                Assert.IsNotNull(r.Trace);
                Assert.AreEqual(r.ChosenIndex, r.Trace.Chosen);
                Assert.AreEqual(r.CandidateCount, r.Trace.Totals.Length);
                Assert.AreSame(r.Plan, r.Candidates[r.ChosenIndex]);
                Assert.AreSame(r.Evaluation, r.CandidateEvaluations[r.ChosenIndex]);
            }
        }

        [Test]
        public void Baselines_ExplainTheirRule()
        {
            var engine = new SimulationEngine(Setup("low", "stable", 7), Configs.CreatePolicy(PolicyFactory.Nearest));
            for (int i = 0; i < 900; i++) engine.Step();
            OrderRecord r = engine.Records.First();
            Assert.AreEqual(NearestPolicy.RuleText, r.Trace.Rule);
            Assert.IsFalse(r.Trace.HasScores);
            Assert.AreEqual(r.ChosenIndex, r.Trace.Chosen);
        }

        [Test]
        public void ScenarioOverrides_ChangeOnlyTheEpisodeCopy()
        {
            double original = Configs.Cost.splitPenaltyPerExtraPackage;
            EpisodeSetup setup = Configs.Setup("medium", "stable", 1, stockMultiplier: 0.7, splitPenalty: 99);
            Assert.AreEqual(99, setup.Cost.splitPenaltyPerExtraPackage);
            Assert.AreEqual(original, Configs.Cost.splitPenaltyPerExtraPackage, "loaded config untouched");
            Assert.AreEqual(0.7, setup.StockMultiplier);
        }
    }
}
