using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fulfilment.Simulation;

namespace Fulfilment.AI
{
    /// <summary>Statistics of one generation (one row of generations.csv).</summary>
    public sealed class GenerationStats
    {
        public int Generation;
        public double MeanFitness;
        public double BestFitness;
        public DNA BestDna;
        public int DistinctGenomes;
    }

    /// <summary>
    /// Generation loop of the Week 3 PopulationManager without the scene: evaluate every genome, sort by fitness,
    /// copy the elite unchanged, then fill the population back to its fixed size with children of parents drawn
    /// from the better half (crossover + mutation). In the lecture a generation is an 8-second physics trial; here a
    /// genome's fitness comes from headless episodes on training seeds (see the fitness delegate).
    /// Fitness is deterministic for a genome, so repeated genomes (elites, duplicates) are evaluated once.
    /// </summary>
    public sealed class PopulationManager
    {
        private readonly Func<DNA, double> fitness;
        private readonly Dictionary<DNA, double> cache = new Dictionary<DNA, double>();
        private readonly SeededRng rng;

        public int PopulationSize { get; }
        public int EliteCount { get; }
        public double MutationRate { get; }
        public bool Parallel { get; set; } = true;
        public int Evaluations => cache.Count;

        public PopulationManager(Func<DNA, double> fitness, int seed, int populationSize = 24, int eliteCount = 2, double mutationRate = 0.10)
        {
            this.fitness = fitness ?? throw new ArgumentNullException(nameof(fitness));
            if (populationSize < 2) throw new ArgumentOutOfRangeException(nameof(populationSize));
            PopulationSize = populationSize;
            EliteCount = Math.Max(0, Math.Min(eliteCount, populationSize));
            MutationRate = mutationRate;
            rng = new SeededRng(seed, RngStream.Evolution);
        }

        /// <summary>Runs the evolution and returns one statistics row per generation (generation 1 = random start).</summary>
        public List<GenerationStats> Run(int generations, Action<GenerationStats> onGeneration = null)
        {
            var history = new List<GenerationStats>();
            List<DNA> population = Enumerable.Range(0, PopulationSize).Select(_ => DNA.RandomGenome(rng)).ToList();

            for (int generation = 1; generation <= generations; generation++)
            {
                Evaluate(population);
                // Stable sort with the genome text as tie-break keeps runs reproducible.
                List<DNA> sorted = population
                    .OrderByDescending(d => cache[d])
                    .ThenBy(d => d.ToString(), StringComparer.Ordinal)
                    .ToList();

                var stats = new GenerationStats
                {
                    Generation = generation,
                    MeanFitness = population.Average(d => cache[d]),
                    BestFitness = cache[sorted[0]],
                    BestDna = sorted[0].Copy(),
                    DistinctGenomes = population.Distinct().Count()
                };
                history.Add(stats);
                onGeneration?.Invoke(stats);

                if (generation < generations)
                    population = BuildNextGenomes(sorted);
            }
            return history;
        }

        /// <summary>Lecture TODO D: elites, then children of parents from the better half, until the size is reached.</summary>
        public List<DNA> BuildNextGenomes(List<DNA> sorted)
        {
            var children = new List<DNA>(PopulationSize);
            for (int i = 0; i < EliteCount; i++) children.Add(sorted[i].Copy());

            int pool = Math.Max(2, sorted.Count / 2);
            while (children.Count < PopulationSize)
            {
                DNA a = sorted[rng.NextInt(0, pool)];
                DNA b = sorted[rng.NextInt(0, pool)];
                DNA child = DNA.Cross(a, b, rng);
                child.Mutate(MutationRate, rng);
                children.Add(child);
            }
            return children;
        }

        private void Evaluate(List<DNA> population)
        {
            List<DNA> missing = population.Where(d => !cache.ContainsKey(d)).Distinct().ToList();
            var values = new double[missing.Count];
            if (Parallel)
                System.Threading.Tasks.Parallel.For(0, missing.Count, i => values[i] = fitness(missing[i]));
            else
                for (int i = 0; i < missing.Count; i++) values[i] = fitness(missing[i]);
            for (int i = 0; i < missing.Count; i++) cache[missing[i]] = values[i];
        }

        public double FitnessOf(DNA dna) => cache.TryGetValue(dna, out double f) ? f : fitness(dna);
    }
}
