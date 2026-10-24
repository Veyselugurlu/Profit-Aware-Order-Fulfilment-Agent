using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fulfilment.Presentation;
using Fulfilment.Simulation;
using NUnit.Framework;

namespace Fulfilment.Tests
{
    public class OrderGeneratorTests
    {
        private static readonly int[] TestSeeds = Enumerable.Range(1001, 10).ToArray();

        private ConfigSet configs;
        private World world;

        [OneTimeSetUp]
        public void LoadConfigs()
        {
            configs = ConfigLoader.LoadDefault();
            world = World.FromConfig(configs.World);
        }

        private List<Order> Episode(string scenario, int seed) =>
            new OrderGenerator(world, configs.Scenario(scenario), configs.Demand, seed).GenerateEpisode();

        private static string Fingerprint(List<Order> orders)
        {
            var sb = new StringBuilder();
            foreach (Order o in orders)
            {
                sb.Append(o.Id).Append(':').Append(o.CreatedTick).Append(':').Append(o.RegionId)
                  .Append(':').Append((int)o.Priority).Append(':').Append(o.PromisedTick);
                foreach (OrderLine l in o.Lines) sb.Append('|').Append(l.SkuId).Append('x').Append(l.Quantity);
                sb.Append(';');
            }
            return sb.ToString();
        }

        [Test]
        public void SameSeed_GivesIdenticalOrderStream()
        {
            Assert.AreEqual(Fingerprint(Episode("medium", 1001)), Fingerprint(Episode("medium", 1001)));
        }

        [Test]
        public void DifferentSeeds_GiveDifferentOrderStreams()
        {
            Assert.AreNotEqual(Fingerprint(Episode("medium", 1001)), Fingerprint(Episode("medium", 1002)));
        }

        [Test]
        public void PolicyStreamDraws_DoNotChangeOrders()
        {
            // Same seed, but another stream is consumed in between: the order stream must not move.
            var policyRng = new SeededRng(1001, RngStream.Policy);
            var generator = new OrderGenerator(world, configs.Scenario("medium"), configs.Demand, 1001);
            var orders = new List<Order>();
            for (int tick = 0; tick < configs.Scenario("medium").EpisodeTicks; tick++)
            {
                policyRng.NextDouble();
                generator.Generate(tick, orders);
            }
            Assert.AreEqual(Fingerprint(Episode("medium", 1001)), Fingerprint(orders));
        }

        [TestCase("low")]
        [TestCase("medium")]
        [TestCase("high")]
        public void EveryTestSeed_HasAtLeast500Orders(string scenario)
        {
            foreach (int seed in TestSeeds)
                Assert.GreaterOrEqual(Episode(scenario, seed).Count, BriefMinimums.OrdersPerEpisode, $"seed {seed}");
        }

        [TestCase("low")]
        [TestCase("medium")]
        [TestCase("high")]
        public void MeanOrderCount_MatchesScenario(string scenario)
        {
            double expected = configs.Scenario(scenario).ExpectedOrders;
            double mean = TestSeeds.Average(seed => Episode(scenario, seed).Count);
            Assert.AreEqual(expected, mean, expected * 0.04);
        }

        [Test]
        public void Orders_AreWellFormed()
        {
            ScenarioConfig s = configs.Scenario("high");
            List<Order> orders = Episode("high", 1001);
            int previousTick = 0;
            for (int i = 0; i < orders.Count; i++)
            {
                Order o = orders[i];
                Assert.AreEqual(i, o.Id);
                Assert.GreaterOrEqual(o.CreatedTick, previousTick, "orders are in time order");
                Assert.Less(o.CreatedTick, s.EpisodeTicks);
                Assert.That(o.RegionId, Is.InRange(0, world.Regions.Count - 1));
                Assert.That(o.Lines.Count, Is.InRange(1, configs.Demand.basketSizeWeights.Length));
                Assert.AreEqual(o.Lines.Count, o.Lines.Select(l => l.SkuId).Distinct().Count(), "SKUs in a basket are distinct");
                Assert.That(o.Lines.All(l => l.Quantity >= 1 && l.Quantity <= configs.Demand.quantityWeights.Length));
                double hours = o.Priority == OrderPriority.Express ? s.promisedHoursExpress : s.promisedHoursStandard;
                Assert.AreEqual(o.CreatedTick + SimTime.FromHours(hours), o.PromisedTick);
                previousTick = o.CreatedTick;
            }
        }

        [Test]
        public void ExpressShare_MatchesScenario()
        {
            ScenarioConfig s = configs.Scenario("high");
            var all = TestSeeds.SelectMany(seed => Episode("high", seed)).ToList();
            double share = all.Count(o => o.Priority == OrderPriority.Express) / (double)all.Count;
            Assert.AreEqual(s.expressShare, share, 0.015);
        }

        [Test]
        public void PopularSkus_AreOrderedMoreOften()
        {
            var counts = new int[world.Skus.Count];
            foreach (int seed in TestSeeds)
                foreach (Order o in Episode("high", seed))
                    foreach (OrderLine l in o.Lines)
                        counts[l.SkuId]++;

            Assert.Greater(counts[0], counts[9]);
            Assert.Greater(counts[9], counts[29]);
            Assert.That(counts.All(c => c > 0), "every SKU is ordered at least once");
        }

        [Test]
        public void HourlyArrivals_FollowTheHourProfile()
        {
            var byHour = new int[24];
            int total = 0;
            foreach (int seed in TestSeeds)
                foreach (Order o in Episode("high", seed))
                {
                    byHour[SimTime.HourOfDay(o.CreatedTick)]++;
                    total++;
                }

            double[] weights = configs.Demand.hourWeights;
            double weightSum = weights.Sum();
            for (int h = 0; h < 24; h++)
                Assert.AreEqual(weights[h] / weightSum, byHour[h] / (double)total, 0.012, $"hour {h}");

            int quietest = System.Array.IndexOf(weights, weights.Min());
            int busiest = System.Array.IndexOf(weights, weights.Max());
            Assert.Less(byHour[quietest] * 10, byHour[busiest]);
        }
    }
}
