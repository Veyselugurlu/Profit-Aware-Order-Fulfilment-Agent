using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Fulfilment.Simulation;

namespace Fulfilment.Experiments
{
    /// <summary>An experiment definition (configs/experiments/*.json).</summary>
    [Serializable]
    public sealed class ExperimentConfig
    {
        public string name;
        public string description;

        /// <summary>train, validation, test or rl_test (see <see cref="SeedSets"/>).</summary>
        public string seedSet;

        public string[] policies;
        public string[] scenarios;
        public string[] regimes;

        /// <summary>Order-up-to multiplier (1 = scenario default).</summary>
        public double stockMultiplier = 1.0;

        /// <summary>Split penalty override in currency; negative keeps cost.json.</summary>
        public double splitPenalty = -1.0;

        /// <summary>Also write one CSV per episode with every order decision (large).</summary>
        public bool writeOrderLogs;

        public int EpisodeCount => SeedSets.ByName(seedSet).Count * policies.Length * scenarios.Length * regimes.Length;

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(name)) errors.Add("experiment name is missing");
            try { SeedSets.ByName(seedSet); } catch (ArgumentException e) { errors.Add(e.Message); }
            if (policies == null || policies.Length == 0) errors.Add("no policies");
            if (scenarios == null || scenarios.Length == 0) errors.Add("no scenarios");
            if (regimes == null || regimes.Length == 0) errors.Add("no regimes");
            if (stockMultiplier <= 0) errors.Add("stockMultiplier must be positive");
            return errors;
        }
    }

    /// <summary>Provenance written next to the results so every table can be traced to code and configs.</summary>
    public sealed class ExperimentProvenance
    {
        public string GitCommit = "unknown";
        public bool GitDirty;
        public string ConfigHash = "unknown";
        public string UnityVersion = "unknown";
    }

    /// <summary>
    /// Runs every (regime, scenario, seed, policy) combination of an experiment headless and writes
    /// runs.csv (one KPI row per episode) and meta.json. For each seed all policies run back to back on the same
    /// order stream, so results can be compared pair-wise per seed (common random numbers).
    /// </summary>
    public static class ExperimentRunner
    {
        public delegate EpisodeSetup SetupFactory(string scenario, string regime, int seed);

        public static List<EpisodeResult> Run(ExperimentConfig config, SetupFactory setups, Func<string, IFulfilmentPolicy> policies,
            string outputDirectory, ExperimentProvenance provenance, Action<int, int, EpisodeResult> progress = null)
        {
            List<string> errors = config.Validate();
            if (errors.Count > 0)
                throw new ArgumentException($"Invalid experiment '{config.name}':\n- " + string.Join("\n- ", errors));

            Directory.CreateDirectory(outputDirectory);
            var watch = Stopwatch.StartNew();
            var results = new List<EpisodeResult>();
            IReadOnlyList<int> seeds = SeedSets.ByName(config.seedSet);
            int total = config.EpisodeCount, done = 0;

            foreach (string regime in config.regimes)
                foreach (string scenario in config.scenarios)
                    foreach (int seed in seeds)
                        foreach (string policyName in config.policies)
                        {
                            EpisodeResult result = EpisodeRunner.Run(setups(scenario, regime, seed), policies(policyName));
                            results.Add(result);
                            if (config.writeOrderLogs)
                                CsvExport.WriteOrders(Path.Combine(outputDirectory, "orders", $"{policyName}_{scenario}_{regime}_{seed}.csv"), result);
                            progress?.Invoke(++done, total, result);
                        }

            watch.Stop();
            CsvExport.WriteKpis(Path.Combine(outputDirectory, "runs.csv"), results);
            File.WriteAllText(Path.Combine(outputDirectory, "meta.json"), MetaJson(config, provenance, seeds, results.Count, watch.Elapsed.TotalSeconds), new UTF8Encoding(false));
            return results;
        }

        private static string MetaJson(ExperimentConfig c, ExperimentProvenance p, IReadOnlyList<int> seeds, int episodes, double seconds)
        {
            string Str(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            string Arr(IEnumerable<string> xs) => "[" + string.Join(", ", xs.Select(Str)) + "]";
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"experiment\": {Str(c.name)},");
            sb.AppendLine($"  \"description\": {Str(c.description)},");
            sb.AppendLine($"  \"created_utc\": {Str(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", inv))},");
            sb.AppendLine($"  \"git_commit\": {Str(p.GitCommit)},");
            sb.AppendLine($"  \"git_dirty\": {(p.GitDirty ? "true" : "false")},");
            sb.AppendLine($"  \"config_sha256\": {Str(p.ConfigHash)},");
            sb.AppendLine($"  \"unity_version\": {Str(p.UnityVersion)},");
            sb.AppendLine($"  \"seed_set\": {Str(c.seedSet)},");
            sb.AppendLine($"  \"seeds\": [{string.Join(", ", seeds.Select(s => s.ToString(inv)))}],");
            sb.AppendLine($"  \"policies\": {Arr(c.policies)},");
            sb.AppendLine($"  \"scenarios\": {Arr(c.scenarios)},");
            sb.AppendLine($"  \"regimes\": {Arr(c.regimes)},");
            sb.AppendLine($"  \"stock_multiplier\": {c.stockMultiplier.ToString(inv)},");
            sb.AppendLine($"  \"split_penalty_override\": {c.splitPenalty.ToString(inv)},");
            sb.AppendLine($"  \"episodes\": {episodes.ToString(inv)},");
            sb.AppendLine($"  \"wall_seconds\": {seconds.ToString("0.###", inv)}");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
