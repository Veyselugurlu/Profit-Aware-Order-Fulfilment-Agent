using System.Collections.Generic;
using System.Linq;
using Fulfilment.AI;
using Fulfilment.Experiments;
using Fulfilment.Simulation;
using NUnit.Framework;

namespace Fulfilment.Tests
{
    public class EngineTests : SimulationTestBase
    {
        private static IEnumerable<TestCaseData> Episodes()
        {
            foreach (string scenario in new[] { "low", "high" })
                foreach (string regime in new[] { "stable", "volatile" })
                    foreach (string policy in new[] { "Nearest", "LowestCost" })
                        yield return new TestCaseData(scenario, regime, policy);
        }

        [TestCaseSource(nameof(Episodes))]
        public void Episode_ConservesOrdersUnitsAndMoney(string scenario, string regime, string policy)
        {
            var engine = new SimulationEngine(Setup(scenario, regime, 1004), PolicyFactory.Create(policy));
            engine.RunToEnd();
            KpiTracker k = engine.Kpis;

            Assert.IsTrue(engine.IsFinished);
            Assert.AreEqual(0, engine.ActiveShipments.Count);
            Assert.GreaterOrEqual(k.Orders, BriefMinimums.OrdersPerEpisode);
            Assert.AreEqual(k.Orders, k.CompletedOrders, "every order is completed after the drain");
            Assert.That(engine.Records.All(r => r.IsComplete));
            Assert.AreEqual(k.UnitsOrdered, k.UnitsShipped + k.UnitsLost);
            Assert.AreEqual(k.Profit, engine.Records.Sum(r => r.RealisedProfit), 1e-6 * System.Math.Abs(k.Profit) + 1e-6);
            Assert.AreEqual(k.Packages, engine.Records.Sum(r => r.ShipmentIds.Count));
            Assert.That(k.ServiceLevel, Is.InRange(0.0, 1.0));
        }

        [Test]
        public void Inventory_NeverGoesNegative_AndReplenishmentArrives()
        {
            var engine = new SimulationEngine(Setup("high", "volatile", 1005), new NearestPolicy());
            int replenished = 0;
            while (!engine.IsFinished)
            {
                engine.Step();
                replenished += engine.LastEvents.Count(e => e.Type == SimEventType.Replenished);
                for (int c = 0; c < World.Centers.Count; c++)
                    for (int s = 0; s < World.Skus.Count; s++)
                        Assert.GreaterOrEqual(engine.Inventory.OnHand(c, s), 0);
            }
            Assert.Greater(replenished, 0);
        }

        [Test]
        public void SameSeed_GivesIdenticalEpisode()
        {
            EpisodeResult a = EpisodeRunner.Run(Setup("medium", "volatile", 1006), new LowestCostPolicy());
            EpisodeResult b = EpisodeRunner.Run(Setup("medium", "volatile", 1006), new LowestCostPolicy());
            Assert.AreEqual(CsvExport.KpiRow(a).Split(',').Take(28), CsvExport.KpiRow(b).Split(',').Take(28));
        }

        [Test]
        public void DifferentPolicies_SeeTheSameOrderStream()
        {
            EpisodeResult nearest = EpisodeRunner.Run(Setup("high", "volatile", 1007), new NearestPolicy());
            EpisodeResult cheapest = EpisodeRunner.Run(Setup("high", "volatile", 1007), new LowestCostPolicy());

            Assert.AreEqual(nearest.Records.Count, cheapest.Records.Count);
            for (int i = 0; i < nearest.Records.Count; i++)
            {
                Order x = nearest.Records[i].Order, y = cheapest.Records[i].Order;
                Assert.AreEqual(x.CreatedTick, y.CreatedTick);
                Assert.AreEqual(x.RegionId, y.RegionId);
                Assert.AreEqual(x.TotalUnits, y.TotalUnits);
            }
        }

        [Test]
        public void NearestPolicy_PicksTheClosestCenterThatHoldsTheOrder()
        {
            int region = RegionNamed("R06"); // Göl Kenarı, next to FC-D
            Inventory inv = EmptyInventory();
            inv.Add(0, 0, 5); // FC-K far away
            inv.Add(2, 0, 5); // FC-G
            inv.Add(3, 0, 5); // FC-B farthest; FC-D (closest) has nothing
            Order order = MakeOrder(region, (0, 1));

            var plans = Candidates().Generate(order, inv);
            var queues = new CenterQueues(World, Configs.Cost);
            CostModel costs = Costs();
            var evals = plans.Select(p => costs.Evaluate(order, p, inv, queues, 0)).ToArray();
            var context = new DecisionContext(order, 0, plans, evals, World, inv, queues, costs);

            int chosenCenter = plans[new NearestPolicy().Choose(context)].Allocations[0].CenterId;
            int closestHolding = new[] { 0, 2, 3 }.OrderBy(c => costs.DistanceKm(c, region)).First();
            Assert.AreEqual(closestHolding, chosenCenter);
        }

        [Test]
        public void Policies_PreferPlansThatLoseFewerUnits()
        {
            var context = TwoPlanContext(lostA: 2, costA: 10, kmA: 1, lostB: 0, costB: 500, kmB: 30);
            Assert.AreEqual(1, new NearestPolicy().Choose(context));
            Assert.AreEqual(1, new LowestCostPolicy().Choose(context));
        }

        [Test]
        public void LowestCost_PicksCheaperPlan_NearestPicksCloserPlan()
        {
            var context = TwoPlanContext(lostA: 0, costA: 300, kmA: 2, lostB: 0, costB: 100, kmB: 9);
            Assert.AreEqual(0, new NearestPolicy().Choose(context));
            Assert.AreEqual(1, new LowestCostPolicy().Choose(context));
        }

        private DecisionContext TwoPlanContext(int lostA, double costA, double kmA, int lostB, double costB, double kmB)
        {
            var plans = new[]
            {
                new FulfilmentPlan(PlanKind.SingleCenter, new[] { new Allocation(0, 0, 1) }, new OrderLine[0]),
                new FulfilmentPlan(PlanKind.SingleCenter, new[] { new Allocation(1, 0, 1) }, new OrderLine[0])
            };
            var evals = new[]
            {
                new PlanEvaluation { LostUnits = lostA, ShippingCost = costA, AverageKm = kmA },
                new PlanEvaluation { LostUnits = lostB, ShippingCost = costB, AverageKm = kmB }
            };
            return new DecisionContext(MakeOrder(0, (0, 1)), 0, plans, evals, World, EmptyInventory(), null, null);
        }

        [Test]
        public void SeasonalRegime_AveragesToOneOverAPeriod()
        {
            var regime = (SeasonalRegime)DemandRegimes.Create(Configs.Regime("seasonal"), 3 * SimTime.TicksPerDay, 1);
            int period = (int)(Configs.Regime("seasonal").periodDays * SimTime.TicksPerDay);
            double mean = Enumerable.Range(0, period).Average(t => regime.RateMultiplier(t));
            Assert.AreEqual(1.0, mean, 1e-3);
        }

        [Test]
        public void VolatileRegime_ShocksDependOnlyOnTheSeed()
        {
            RegimeConfig config = Configs.Regime("volatile");
            var a = (VolatileRegime)DemandRegimes.Create(config, 3 * SimTime.TicksPerDay, 1001);
            var b = (VolatileRegime)DemandRegimes.Create(config, 3 * SimTime.TicksPerDay, 1001);
            Assert.AreEqual(a.Shocks.Count, b.Shocks.Count);
            for (int t = 0; t < 3 * SimTime.TicksPerDay; t += 7)
            {
                Assert.AreEqual(a.RateMultiplier(t), b.RateMultiplier(t));
                Assert.That(a.RateMultiplier(t), Is.InRange(1.0, config.shockMaxMultiplier));
            }
        }

        [Test]
        public void KpiCsv_HasOneCellPerColumn()
        {
            EpisodeResult r = EpisodeRunner.Run(Setup("low", "stable", 1001), new NearestPolicy());
            Assert.AreEqual(CsvExport.KpiColumns.Length, CsvExport.KpiRow(r).Split(',').Length);
            StringAssert.DoesNotContain(";", CsvExport.KpiRow(r));
        }
    }
}
