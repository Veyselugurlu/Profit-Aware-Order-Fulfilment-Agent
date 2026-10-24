using System.Linq;
using Fulfilment.Presentation;
using Fulfilment.Simulation;
using NUnit.Framework;

namespace Fulfilment.Tests
{
    /// <summary>The shipped configs must load and meet the minimums of the project brief.</summary>
    public class ConfigTests
    {
        private ConfigSet configs;

        [OneTimeSetUp]
        public void LoadConfigs() => configs = ConfigLoader.LoadDefault();

        [Test]
        public void ShippedConfigs_HaveNoValidationErrors()
        {
            var errors = configs.Validate();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void World_MeetsBriefMinimums()
        {
            World world = World.FromConfig(configs.World);
            Assert.GreaterOrEqual(world.Centers.Count, BriefMinimums.Centers);
            Assert.GreaterOrEqual(world.Skus.Count, BriefMinimums.Skus);
            Assert.IsTrue(world.Skus.All(s => s.Margin > 0));
        }

        [Test]
        public void Scenarios_AreLowMediumHighInOrder()
        {
            CollectionAssert.AreEqual(new[] { "low", "medium", "high" }, configs.Scenarios.Select(s => s.code).ToArray());
            Assert.That(configs.Scenarios.All(s => s.ExpectedOrders >= BriefMinimums.OrdersPerEpisode));
        }

        [Test]
        public void World_IdsAreDenseIndices()
        {
            World world = World.FromConfig(configs.World);
            for (int i = 0; i < world.Centers.Count; i++) Assert.AreEqual(i, world.Centers[i].Id);
            for (int i = 0; i < world.Regions.Count; i++) Assert.AreEqual(i, world.Regions[i].Id);
            for (int i = 0; i < world.Skus.Count; i++) Assert.AreEqual(i, world.Skus[i].Id);
        }

        [Test]
        public void Validator_ReportsConfigBelowBrief()
        {
            var tooSmall = new WorldConfig
            {
                name = "tiny",
                gridWidth = 10,
                gridHeight = 10,
                centers = new[]
                {
                    new CenterConfig { code = "A", x = 1, y = 1, capacityPerHour = 10, stockShare = 0.5 },
                    new CenterConfig { code = "B", x = 1, y = 1, capacityPerHour = 10, stockShare = 0.5 }
                },
                regions = new[] { new RegionConfig { code = "R", x = 20, y = 2, demandWeight = 1 } },
                skus = new[] { new SkuConfig { code = "S", price = 10, unitCost = 12, weightKg = 1 } }
            };

            var errors = ConfigValidator.Validate(tooSmall);
            Assert.That(errors, Has.Some.Contains("fulfilment centers"));
            Assert.That(errors, Has.Some.Contains("SKUs"));
            Assert.That(errors, Has.Some.Contains("shares a cell"));
            Assert.That(errors, Has.Some.Contains("outside"));
            Assert.That(errors, Has.Some.Contains("unit cost must be below"));
            Assert.Throws<System.ArgumentException>(() => World.FromConfig(tooSmall));
        }
    }
}
