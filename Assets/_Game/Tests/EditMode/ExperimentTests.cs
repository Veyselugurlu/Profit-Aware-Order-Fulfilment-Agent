using System.IO;
using System.Linq;
using Fulfilment.AI;
using Fulfilment.Experiments;
using Fulfilment.Presentation;
using NUnit.Framework;

namespace Fulfilment.Tests
{
    public class ExperimentTests : SimulationTestBase
    {
        private string tempDir;

        [SetUp]
        public void MakeTempDir() => tempDir = Path.Combine(Path.GetTempPath(), "pfa_exp_" + System.Guid.NewGuid().ToString("N"));

        [TearDown]
        public void RemoveTempDir()
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }

        [Test]
        public void SeedSets_AreDisjointAndLargeEnough()
        {
            Assert.IsEmpty(SeedSets.Train.Intersect(SeedSets.Validation));
            Assert.IsEmpty(SeedSets.Train.Intersect(SeedSets.Test));
            Assert.IsEmpty(SeedSets.Validation.Intersect(SeedSets.Test));
            Assert.GreaterOrEqual(SeedSets.Test.Count, 10, "the brief asks for at least 10 seeds");
            Assert.GreaterOrEqual(SeedSets.RlTest.Count, 30, "the brief asks for at least 30 RL test episodes");
        }

        [Test]
        public void ShippedExperiments_AreValid()
        {
            foreach (string name in new[] { "vize_prelim", "smoke_train" })
            {
                var config = ConfigLoader.Read<ExperimentConfig>(ExperimentLauncher.ExperimentPath(name));
                Assert.IsEmpty(config.Validate(), name);
                foreach (string p in config.policies) Assert.Contains(p, PolicyFactory.Names.ToList());
                foreach (string s in config.scenarios) Assert.DoesNotThrow(() => Configs.Scenario(s));
                foreach (string r in config.regimes) Assert.DoesNotThrow(() => Configs.Regime(r));
            }
            var vize = ConfigLoader.Read<ExperimentConfig>(ExperimentLauncher.ExperimentPath("vize_prelim"));
            Assert.AreEqual("test", vize.seedSet, "reported results use test seeds only");
        }

        private ExperimentConfig Small() => new ExperimentConfig
        {
            name = "unit",
            seedSet = "validation",
            policies = new[] { PolicyFactory.LowestCost, PolicyFactory.Utility },
            scenarios = new[] { "low" },
            regimes = new[] { "stable" }
        };

        private string[] RunSmall(string dir)
        {
            ExperimentRunner.Run(Small(), (s, r, seed) => Configs.Setup(s, r, seed), Configs.CreatePolicy, dir, new ExperimentProvenance());
            return File.ReadAllLines(Path.Combine(dir, "runs.csv"));
        }

        [Test]
        public void Runner_WritesOneRowPerEpisodeAndMetadata()
        {
            string[] lines = RunSmall(tempDir);
            Assert.AreEqual(1 + 2 * SeedSets.Validation.Count, lines.Length);
            Assert.AreEqual(CsvExport.KpiHeader, lines[0]);

            string meta = File.ReadAllText(Path.Combine(tempDir, "meta.json"));
            StringAssert.Contains("\"seed_set\": \"validation\"", meta);
            StringAssert.Contains("\"episodes\": 20", meta);

            // Policies run back to back for every seed: rows alternate LowestCost, Utility with the same seed.
            for (int i = 1; i < lines.Length; i += 2)
                Assert.AreEqual(lines[i].Split(',')[3], lines[i + 1].Split(',')[3]);
        }

        [Test]
        public void Runner_IsReproducibleExceptForWallTime()
        {
            string[] a = RunSmall(Path.Combine(tempDir, "a"));
            string[] b = RunSmall(Path.Combine(tempDir, "b"));
            Assert.AreEqual(a.Length, b.Length);
            int wall = CsvExport.KpiColumns.ToList().IndexOf("wall_seconds");
            for (int i = 0; i < a.Length; i++)
            {
                var x = a[i].Split(',').ToList();
                var y = b[i].Split(',').ToList();
                x.RemoveAt(wall);
                y.RemoveAt(wall);
                CollectionAssert.AreEqual(x, y, $"row {i}");
            }
        }

        [Test]
        public void ConfigHash_ChangesWhenAConfigChanges()
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "a.json"), "{\"x\": 1}");
            string before = ExperimentLauncher.ConfigHash(tempDir);
            Assert.AreEqual(before, ExperimentLauncher.ConfigHash(tempDir));
            File.WriteAllText(Path.Combine(tempDir, "a.json"), "{\"x\": 2}");
            Assert.AreNotEqual(before, ExperimentLauncher.ConfigHash(tempDir));
        }
    }
}
