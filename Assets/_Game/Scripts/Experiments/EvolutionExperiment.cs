using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Fulfilment.AI;
using Fulfilment.Simulation;

namespace Fulfilment.Experiments
{
    /// <summary>An evolution experiment (configs/experiments/evolve_*.json).</summary>
    [Serializable]
    public sealed class EvolutionConfig
    {
        public string name;
        public string description;
        public int populationSize = 24;
        public int generations = 20;
        public int eliteCount = 2;
        public double[] mutationRates;
        public int[] gaSeeds;

        /// <summary>Episodes that make up one fitness evaluation; training seeds only.</summary>
        public EvaluationEpisode[] evaluation;

        public string[] validationScenarios;
        public string[] validationRegimes;

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(name)) errors.Add("name is missing");
            if (populationSize < 2) errors.Add("populationSize must be at least 2");
            if (generations < 1) errors.Add("generations must be at least 1");
            if (mutationRates == null || mutationRates.Length == 0) errors.Add("no mutation rates");
            if (gaSeeds == null || gaSeeds.Length == 0) errors.Add("no GA seeds");
            if (evaluation == null || evaluation.Length == 0) errors.Add("no evaluation episodes");
            else
                foreach (EvaluationEpisode e in evaluation)
                    if (!SeedSets.Train.Contains(e.seed))
                        errors.Add($"evaluation seed {e.seed} is not a training seed; fitness must never see validation or test seeds");
            if (validationScenarios == null || validationScenarios.Length == 0) errors.Add("no validation scenarios");
            if (validationRegimes == null || validationRegimes.Length == 0) errors.Add("no validation regimes");
            return errors;
        }
    }

    [Serializable]
    public sealed class EvaluationEpisode
    {
        public string scenario;
        public string regime;
        public int seed;
    }

    /// <summary>
    /// The Week 3 class experiment applied to the fulfilment agent: evolve the decision table (DNA → Brain) for every
    /// mutation rate × GA seed, record mean and best fitness per generation, then pick the genome that is best on the
    /// validation seeds. Fitness is the mean profit per order over the training episodes.
    /// Writes generations.csv, runs.csv and meta.json; returns the selected genome.
    /// </summary>
    public static class EvolutionExperiment
    {
        public sealed class RunResult
        {
            public double MutationRate;
            public int GaSeed;
            public List<GenerationStats> History;
            public DNA Best;
            public double TrainFitness;
            public double ValidationFitness;
            public int Evaluations;
            public double Seconds;
        }

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static double ProfitPerOrder(IEnumerable<(string scenario, string regime, int seed)> episodes, DNA dna,
            ExperimentRunner.SetupFactory setups, PolicySettings settings)
        {
            var values = new List<double>();
            foreach ((string scenario, string regime, int seed) in episodes)
            {
                var engine = new SimulationEngine(setups(scenario, regime, seed), new Brain(dna, settings.Utility, settings.Profiles));
                engine.RunToEnd();
                values.Add(engine.Kpis.Orders > 0 ? engine.Kpis.Profit / engine.Kpis.Orders : 0.0);
            }
            return values.Average();
        }

        public static List<RunResult> Run(EvolutionConfig config, ExperimentRunner.SetupFactory setups, PolicySettings settings,
            string outputDirectory, ExperimentProvenance provenance, Action<string> log)
        {
            List<string> errors = config.Validate();
            if (errors.Count > 0)
                throw new ArgumentException($"Invalid evolution experiment '{config.name}':\n- " + string.Join("\n- ", errors));

            Directory.CreateDirectory(outputDirectory);
            var total = Stopwatch.StartNew();
            var trainEpisodes = config.evaluation.Select(e => (e.scenario, e.regime, e.seed)).ToList();
            var validationEpisodes = (from s in config.validationScenarios
                                      from r in config.validationRegimes
                                      from seed in SeedSets.Validation
                                      select (s, r, seed)).ToList();
            var runs = new List<RunResult>();

            foreach (double rate in config.mutationRates)
                foreach (int gaSeed in config.gaSeeds)
                {
                    var watch = Stopwatch.StartNew();
                    var manager = new PopulationManager(dna => ProfitPerOrder(trainEpisodes, dna, setups, settings),
                        gaSeed, config.populationSize, config.eliteCount, rate);
                    List<GenerationStats> history = manager.Run(config.generations);
                    GenerationStats last = history[history.Count - 1];
                    DNA best = last.BestDna;
                    var run = new RunResult
                    {
                        MutationRate = rate,
                        GaSeed = gaSeed,
                        History = history,
                        Best = best,
                        TrainFitness = last.BestFitness,
                        ValidationFitness = ValidationFitness(validationEpisodes, best, setups, settings),
                        Evaluations = manager.Evaluations,
                        Seconds = watch.Elapsed.TotalSeconds
                    };
                    runs.Add(run);
                    log?.Invoke($"mutation {rate:0.00} seed {gaSeed}: best {best} train {run.TrainFitness:0.00} TL/order, validation {run.ValidationFitness:0.00}, {run.Evaluations} genomes, {run.Seconds:0}s");
                }

            WriteGenerations(Path.Combine(outputDirectory, "generations.csv"), runs);
            WriteRuns(Path.Combine(outputDirectory, "runs.csv"), runs);
            RunResult selected = runs.OrderByDescending(r => r.ValidationFitness).First();
            File.WriteAllText(Path.Combine(outputDirectory, "meta.json"), Meta(config, provenance, selected, total.Elapsed.TotalSeconds), new UTF8Encoding(false));
            return runs;
        }

        private static double ValidationFitness(List<(string, string, int)> episodes, DNA dna, ExperimentRunner.SetupFactory setups, PolicySettings settings)
        {
            var parts = new double[episodes.Count];
            System.Threading.Tasks.Parallel.For(0, episodes.Count, i => parts[i] = ProfitPerOrder(new[] { episodes[i] }, dna, setups, settings));
            return parts.Average();
        }

        private static void WriteGenerations(string path, List<RunResult> runs)
        {
            var sb = new StringBuilder("mutation_rate,ga_seed,generation,mean_fitness,best_fitness,best_dna,distinct_genomes\n");
            foreach (RunResult r in runs)
                foreach (GenerationStats g in r.History)
                    sb.Append(r.MutationRate.ToString(Inv)).Append(',').Append(r.GaSeed.ToString(Inv)).Append(',')
                      .Append(g.Generation.ToString(Inv)).Append(',').Append(g.MeanFitness.ToString("0.####", Inv)).Append(',')
                      .Append(g.BestFitness.ToString("0.####", Inv)).Append(",\"").Append(g.BestDna).Append("\",")
                      .Append(g.DistinctGenomes.ToString(Inv)).Append('\n');
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private static void WriteRuns(string path, List<RunResult> runs)
        {
            var sb = new StringBuilder("mutation_rate,ga_seed,best_dna,train_fitness,validation_fitness,evaluated_genomes,seconds\n");
            foreach (RunResult r in runs)
                sb.Append(r.MutationRate.ToString(Inv)).Append(',').Append(r.GaSeed.ToString(Inv)).Append(",\"").Append(r.Best).Append("\",")
                  .Append(r.TrainFitness.ToString("0.####", Inv)).Append(',').Append(r.ValidationFitness.ToString("0.####", Inv)).Append(',')
                  .Append(r.Evaluations.ToString(Inv)).Append(',').Append(r.Seconds.ToString("0.#", Inv)).Append('\n');
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private static string Meta(EvolutionConfig c, ExperimentProvenance p, RunResult selected, double seconds)
        {
            string Str(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"experiment\": {Str(c.name)},");
            sb.AppendLine($"  \"description\": {Str(c.description)},");
            sb.AppendLine($"  \"created_utc\": {Str(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", Inv))},");
            sb.AppendLine($"  \"git_commit\": {Str(p.GitCommit)},");
            sb.AppendLine($"  \"git_dirty\": {(p.GitDirty ? "true" : "false")},");
            sb.AppendLine($"  \"config_sha256\": {Str(p.ConfigHash)},");
            sb.AppendLine($"  \"unity_version\": {Str(p.UnityVersion)},");
            sb.AppendLine($"  \"population_size\": {c.populationSize},");
            sb.AppendLine($"  \"generations\": {c.generations},");
            sb.AppendLine($"  \"elite_count\": {c.eliteCount},");
            sb.AppendLine($"  \"mutation_rates\": [{string.Join(", ", c.mutationRates.Select(m => m.ToString(Inv)))}],");
            sb.AppendLine($"  \"ga_seeds\": [{string.Join(", ", c.gaSeeds)}],");
            sb.AppendLine($"  \"fitness\": \"mean profit per order over the training episodes\",");
            sb.AppendLine($"  \"training_episodes\": [{string.Join(", ", c.evaluation.Select(e => Str($"{e.scenario}/{e.regime}/{e.seed}")))}],");
            sb.AppendLine($"  \"validation\": \"{c.validationScenarios.Length * c.validationRegimes.Length * SeedSets.Validation.Count} episodes on seeds {SeedSets.Validation[0]}-{SeedSets.Validation[SeedSets.Validation.Count - 1]}\",");
            sb.AppendLine($"  \"selected_dna\": {Str(selected.Best.ToString())},");
            sb.AppendLine($"  \"selected_mutation_rate\": {selected.MutationRate.ToString(Inv)},");
            sb.AppendLine($"  \"selected_ga_seed\": {selected.GaSeed},");
            sb.AppendLine($"  \"selected_validation_fitness\": {selected.ValidationFitness.ToString("0.####", Inv)},");
            sb.AppendLine($"  \"wall_seconds\": {seconds.ToString("0.#", Inv)}");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
