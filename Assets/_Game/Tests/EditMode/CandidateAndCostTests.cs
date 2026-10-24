using System.Linq;
using Fulfilment.Simulation;
using NUnit.Framework;

namespace Fulfilment.Tests
{
    public class CandidateAndCostTests : SimulationTestBase
    {
        private const int FcK = 0, FcD = 1, FcG = 2, FcB = 3;

        [Test]
        public void EveryCenterHoldingTheOrder_GetsASingleCenterPlan()
        {
            Inventory inv = EmptyInventory();
            foreach (int c in new[] { FcK, FcD, FcG })
            {
                inv.Add(c, 0, 5);
                inv.Add(c, 1, 5);
            }
            inv.Add(FcB, 0, 5); // B lacks SKU 1

            var plans = Candidates().Generate(MakeOrder(0, (0, 1), (1, 2)), inv);
            var singles = plans.Where(p => p.Kind == PlanKind.SingleCenter).Select(p => p.Allocations[0].CenterId).ToArray();
            CollectionAssert.AreEquivalent(new[] { FcK, FcD, FcG }, singles);
            Assert.That(plans.All(p => !p.HasLostUnits));
        }

        [Test]
        public void NoSingleCenter_ProducesSplitWithoutLoss()
        {
            Inventory inv = EmptyInventory();
            inv.Add(FcK, 0, 3);
            inv.Add(FcG, 1, 3);

            var plans = Candidates().Generate(MakeOrder(0, (0, 2), (1, 1)), inv);
            Assert.That(plans.All(p => p.Kind != PlanKind.SingleCenter));
            Assert.That(plans.Any(p => p.IsSplit && !p.HasLostUnits));
        }

        [Test]
        public void NothingInStock_GivesOneLostPlanWithoutParcels()
        {
            var plans = Candidates().Generate(MakeOrder(0, (0, 1), (1, 1)), EmptyInventory());
            Assert.AreEqual(1, plans.Count, "min-cost and max-coverage plans are identical and deduplicated");
            Assert.AreEqual(0, plans[0].PackageCount);
            Assert.AreEqual(2, plans[0].LostLines.Sum(l => l.Quantity));
        }

        [Test]
        public void PartialStock_LosesOnlyTheMissingUnits()
        {
            Inventory inv = EmptyInventory();
            inv.Add(FcK, 0, 1);
            inv.Add(FcD, 0, 1);

            var plans = Candidates().Generate(MakeOrder(0, (0, 4)), inv);
            FulfilmentPlan best = plans.OrderBy(p => p.LostLines.Sum(l => l.Quantity)).First();
            Assert.AreEqual(2, best.Allocations.Sum(a => a.Quantity));
            Assert.AreEqual(2, best.LostLines.Sum(l => l.Quantity));
        }

        [Test]
        public void Plans_NeverExceedStock_AndAccountForEveryUnit()
        {
            EpisodeSetup setup = Setup("high", "volatile", 1003);
            var generator = new OrderGenerator(World, setup.Scenario, setup.Demand, setup.Seed);
            var stock = new StockPlan(World, setup.Scenario, setup.Demand, setup.Cost, 0.3).CreateInitialInventory();
            CandidateGenerator candidates = Candidates();

            foreach (Order order in generator.GenerateEpisode().Take(400))
            {
                var plans = candidates.Generate(order, stock);
                Assert.That(plans.Count, Is.InRange(1, World.Centers.Count + 2));
                Assert.AreEqual(plans.Count, plans.Select(CandidateGenerator.Signature).Distinct().Count(), "no duplicate plans");
                foreach (FulfilmentPlan plan in plans)
                {
                    Assert.AreEqual(order.TotalUnits, plan.Allocations.Sum(a => a.Quantity) + plan.LostLines.Sum(l => l.Quantity));
                    foreach (var group in plan.Allocations.GroupBy(a => (a.CenterId, a.SkuId)))
                        Assert.LessOrEqual(group.Sum(a => a.Quantity), stock.OnHand(group.Key.CenterId, group.Key.SkuId));
                }
                // consume the first plan so stock runs low and splits/losses appear
                foreach (Allocation a in plans[0].Allocations) stock.Remove(a.CenterId, a.SkuId, a.Quantity);
            }
        }

        [Test]
        public void ShippingCost_FollowsTheFormula()
        {
            CostModel costs = Costs();
            CostConfig c = Configs.Cost;
            Assert.AreEqual(c.shippingBasePerPackage + c.shippingPerKm * 10 + c.shippingPerKgKm * 2 * 10, costs.ShippingCost(10, 2), 1e-9);
            Assert.AreEqual(0.0, costs.SplitCost(1));
            Assert.AreEqual(2 * c.splitPenaltyPerExtraPackage, costs.SplitCost(3), 1e-9);
        }

        [Test]
        public void LatenessCost_IsZeroOnTime_AndHigherForExpress()
        {
            CostModel costs = Costs();
            Assert.AreEqual(0.0, costs.LatenessCost(0, OrderPriority.Express));
            double standard = costs.LatenessCost(90, OrderPriority.Standard);
            Assert.AreEqual(1.5 * Configs.Cost.latenessPerHour, standard, 1e-9);
            Assert.AreEqual(standard * Configs.Cost.expressLatenessMultiplier, costs.LatenessCost(90, OrderPriority.Express), 1e-9);
        }

        [Test]
        public void OpportunityCost_OnlyCountsUnitsBelowSafetyStock()
        {
            EpisodeSetup setup = Setup();
            var plan = new StockPlan(World, setup.Scenario, setup.Demand, setup.Cost);
            CostModel costs = Costs(setup);
            int sku = 0; // most popular SKU, its safety stock at FC-K is well above 2 units
            double safety = plan.SafetyStock(FcK, sku);
            Assume.That(safety, Is.GreaterThan(4.0));
            double unitCharge = World.Skus[sku].Margin * Configs.Cost.opportunityCostFactor;

            int above = (int)System.Math.Ceiling(safety) + 10;
            Assert.AreEqual(0.0, costs.OpportunityCost(FcK, sku, 3, above), "ends above safety: no charge");

            int justAbove = (int)System.Math.Ceiling(safety) + 1;
            double unitsBelow = safety - (justAbove - 4);
            Assert.AreEqual(unitsBelow * unitCharge, costs.OpportunityCost(FcK, sku, 4, justAbove), 1e-9, "only the part below safety");

            int alreadyBelow = (int)System.Math.Floor(safety) - 2;
            Assert.AreEqual(2 * unitCharge, costs.OpportunityCost(FcK, sku, 2, alreadyBelow), 1e-9, "every unit counts once below");
        }

        [Test]
        public void Evaluate_AddsUpRevenueCostsAndArrival()
        {
            Inventory inv = EmptyInventory();
            inv.Add(FcK, 0, 10);
            inv.Add(FcG, 1, 10);
            var order = MakeOrder(0, OrderPriority.Express, 100, 100 + 150, (0, 2), (1, 1));
            FulfilmentPlan split = Candidates().Generate(order, inv).Single();
            var queues = new CenterQueues(World, Configs.Cost);
            CostModel costs = Costs();

            PlanEvaluation e = costs.Evaluate(order, split, inv, queues, 100);
            Sku a = World.Skus[0], b = World.Skus[1];
            Assert.AreEqual(2 * a.Price + b.Price, e.Revenue, 1e-9);
            Assert.AreEqual(2 * a.UnitCost + b.UnitCost, e.Cogs, 1e-9);
            Assert.AreEqual(2, e.PackageCount);
            Assert.AreEqual(Configs.Cost.splitPenaltyPerExtraPackage, e.SplitCost, 1e-9);
            Assert.AreEqual(e.Packages.Sum(p => p.ShippingCost), e.ShippingCost, 1e-9);
            Assert.AreEqual(e.Packages.Max(p => p.ArrivalTick), e.LatestArrivalTick, 1e-9);
            Assert.AreEqual(order.PromisedTick - e.LatestArrivalTick, e.SlackMinutes, 1e-9);
            Assert.AreEqual(e.Revenue - e.Cogs - e.CashCost, e.ExpectedProfit, 1e-9);
        }

        [Test]
        public void CenterQueue_DelaysParcelsWhenBusy()
        {
            var queues = new CenterQueues(World, Configs.Cost);
            double pick = 60.0 / World.Centers[FcK].CapacityPerHour;
            double first = queues.Reserve(FcK, 0);
            double second = queues.Reserve(FcK, 0);
            Assert.AreEqual(pick, second - first, 1e-9);
            Assert.AreEqual(2 * pick, queues.Backlog(FcK, 0), 1e-9);
            Assert.AreEqual(0.0, queues.Backlog(FcD, 0));
        }
    }
}
