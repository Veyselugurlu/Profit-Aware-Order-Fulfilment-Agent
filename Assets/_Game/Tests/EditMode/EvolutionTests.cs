using System.Collections.Generic;
using System.Linq;
using Fulfilment.AI;
using Fulfilment.Experiments;
using Fulfilment.Presentation;
using Fulfilment.Simulation;
using NUnit.Framework;

namespace Fulfilment.Tests
{
    /// <summary>The Week 3 GA activities as tests: genotype → behaviour, crossover, mutation, fixed population size.</summary>
    public class EvolutionTests : SimulationTestBase
    {
        private static SeededRng Rng(int seed = 1) => new SeededRng(seed, RngStream.Evolution);

        [Test]
        public void RandomGenome_HasValidActionCodes()
        {
            var rng = Rng();
            for (int i = 0; i < 200; i++)
            {
                DNA d = DNA.RandomGenome(rng);
                Assert.AreEqual(DNA.StateCount, d.Length);
                Assert.That(d.ToArray(), Is.All.InRange(0, DNA.ActionCount - 1));
            }
        }

        [Test]
        public void Cross_TakesThePrefixFromAAndTheRestFromB()
        {
            var a = new DNA(0, 0, 0, 0, 0, 0, 0, 0);
            var b = new DNA(3, 3, 3, 3, 3, 3, 3, 3);
            var rng = Rng(5);
            var cuts = new HashSet<int>();
            for (int i = 0; i < 300; i++)
            {
                int[] child = DNA.Cross(a, b, rng).ToArray();
                int cut = System.Array.IndexOf(child, 3);
                Assert.That(cut, Is.InRange(1, DNA.StateCount - 1), "at least one gene from each parent");
                Assert.That(child.Take(cut), Is.All.EqualTo(0));
                Assert.That(child.Skip(cut), Is.All.EqualTo(3));
                cuts.Add(cut);
            }
            Assert.AreEqual(DNA.StateCount - 1, cuts.Count, "every cut point is used");
        }

        [Test]
        public void Cross_DoesNotCreateNewAlleles()
        {
            var a = new DNA(0, 1, 0, 1, 0, 1, 0, 1);
            var b = new DNA(1, 0, 1, 0, 1, 0, 1, 0);
            var rng = Rng(9);
            for (int i = 0; i < 100; i++)
                Assert.That(DNA.Cross(a, b, rng).ToArray(), Is.All.InRange(0, 1));
        }

        [Test]
        public void Mutate_ZeroKeepsTheGenome_OneChangesEveryGeneToADifferentAction()
        {
            var rng = Rng(3);
            var original = new DNA(0, 1, 2, 3, 0, 1, 2, 3);

            DNA none = original.Copy();
            none.Mutate(0.0, rng);
            Assert.AreEqual(original, none);

            for (int trial = 0; trial < 50; trial++)
            {
                DNA all = original.Copy();
                all.Mutate(1.0, rng);
                for (int g = 0; g < DNA.StateCount; g++)
                {
                    Assert.AreNotEqual(original.GetGene(g), all.GetGene(g), "a mutation never keeps the old value");
                    Assert.That(all.GetGene(g), Is.InRange(0, DNA.ActionCount - 1));
                }
            }
        }

        [Test]
        public void Copy_IsIndependent()
        {
            var original = new DNA(0, 0, 0, 0, 0, 0, 0, 0);
            DNA copy = original.Copy();
            copy.Mutate(1.0, Rng());
            Assert.AreEqual(new DNA(0, 0, 0, 0, 0, 0, 0, 0), original);
        }

        [Test]
        public void NextGeneration_KeepsTheSizeAndTheElite()
        {
            var manager = new PopulationManager(d => d.ToArray().Sum(), seed: 2026, populationSize: 24, eliteCount: 2, mutationRate: 0.1) { Parallel = false };
            var rng = Rng(11);
            List<DNA> sorted = Enumerable.Range(0, 24).Select(_ => DNA.RandomGenome(rng))
                .OrderByDescending(d => d.ToArray().Sum()).ToList();
            List<DNA> next = manager.BuildNextGenomes(sorted);
            Assert.AreEqual(24, next.Count);
            Assert.AreEqual(sorted[0], next[0]);
            Assert.AreEqual(sorted[1], next[1]);
        }

        [Test]
        public void Evolution_ImprovesASimpleFitnessAndNeverLosesTheBest()
        {
            // Fitness = number of genes equal to action 2; the optimum is [2,2,2,2,2,2,2,2] with fitness 8.
            var manager = new PopulationManager(d => d.ToArray().Count(g => g == 2), seed: 2026, populationSize: 24, eliteCount: 2, mutationRate: 0.1) { Parallel = false };
            List<GenerationStats> history = manager.Run(25);
            Assert.Greater(history.Last().BestFitness, history.First().BestFitness);
            Assert.Greater(history.Last().MeanFitness, history.First().MeanFitness);
            for (int i = 1; i < history.Count; i++)
                Assert.GreaterOrEqual(history[i].BestFitness, history[i - 1].BestFitness, "elitism keeps the best");
        }

        [Test]
        public void Evolution_IsReproducibleForASeed()
        {
            double Fit(DNA d) => d.ToArray().Select((g, i) => g == i % DNA.ActionCount ? 1 : 0).Sum();
            var a = new PopulationManager(Fit, 7) { Parallel = false }.Run(10);
            var b = new PopulationManager(Fit, 7) { Parallel = false }.Run(10);
            CollectionAssert.AreEqual(a.Select(s => s.BestDna.ToString()), b.Select(s => s.BestDna.ToString()));
        }

        [Test]
        public void Brain_ReadsTheGeneOfTheSensedState()
        {
            // Every state maps to a different profile when the genome is [0,1,2,3,0,1,2,3].
            var dna = new DNA(0, 1, 2, 3, 0, 1, 2, 3);
            var brain = new Brain(dna, Configs.Utility, Configs.Policies.Profiles);
            var engine = new SimulationEngine(Setup("high", "volatile", 5), brain);
            for (int i = 0; i < 1500; i++) engine.Step();

            foreach (OrderRecord r in engine.Records.Take(200))
            {
                int state = int.Parse(r.Trace.Note.Split(new[] { "gen[" }, System.StringSplitOptions.None)[1].Split(']')[0]);
                double[] expected = Configs.Policies.Profiles.profiles[dna.GetGene(state)].weights;
                CollectionAssert.AreEqual(expected, r.Trace.Weights);
            }
            Assert.Greater(brain.StateCounts.Count(c => c > 0), 1, "more than one situation is sensed");
        }

        [Test]
        public void SensorState_EncodesTheThreeSensors()
        {
            Assert.AreEqual(0, Brain.State(new[] { false, false, false }));
            Assert.AreEqual(1, Brain.State(new[] { false, false, true }));
            Assert.AreEqual(2, Brain.State(new[] { false, true, false }));
            Assert.AreEqual(4, Brain.State(new[] { true, false, false }));
            Assert.AreEqual(7, Brain.State(new[] { true, true, true }));
        }

        [Test]
        public void WeightProfiles_AreValidAndCoverEveryAction()
        {
            Assert.IsEmpty(Configs.Policies.Profiles.Validate(Configs.Utility.criteria.Length));
            Assert.GreaterOrEqual(Configs.Policies.Profiles.profiles.Length, DNA.ActionCount);
        }

        [Test]
        public void EvolutionExperiment_EvaluatesOnTrainingSeedsOnly()
        {
            var config = ConfigLoader.Read<EvolutionConfig>(ExperimentLauncher.ExperimentPath("evolve_profiles"));
            Assert.IsEmpty(config.Validate());
            Assert.That(config.evaluation.Select(e => e.seed), Is.All.Matches<int>(s => SeedSets.Train.Contains(s)));
            CollectionAssert.AreEquivalent(new[] { 0.0, 0.1, 0.3 }, config.mutationRates, "the lecture's three mutation rates");

            config.evaluation[0].seed = SeedSets.Test[0];
            Assert.That(config.Validate(), Has.Some.Contains("not a training seed"));
        }
    }
}
