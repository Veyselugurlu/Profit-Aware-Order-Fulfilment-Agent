using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Fulfilment.AI;
using Fulfilment.Experiments;
using UnityEngine;

namespace Fulfilment.Presentation
{
    /// <summary>Build information written by the build script (StreamingAssets/build_info.json).</summary>
    [Serializable]
    public sealed class BuildInfo
    {
        public string commit;
        public bool dirty;
        public string builtUtc;
    }

    /// <summary>
    /// Loads an experiment definition and the configs, fingerprints the configs and runs the experiment.
    /// Shared by the Editor command line (ExperimentCli) and the player (-experiment name).
    /// </summary>
    public static class ExperimentLauncher
    {
        public static string ExperimentPath(string name) => Path.Combine(ConfigLoader.DefaultRoot, "experiments", name + ".json");

        public static List<EpisodeResult> Run(string experimentName, string outputDirectory, ExperimentProvenance provenance, Action<string> log)
        {
            ConfigSet configs = ConfigLoader.LoadDefault();
            List<string> configErrors = configs.Validate();
            if (configErrors.Count > 0)
                throw new InvalidOperationException("Config errors:\n" + string.Join("\n", configErrors));

            ExperimentConfig experiment = ConfigLoader.Read<ExperimentConfig>(ExperimentPath(experimentName));
            provenance.ConfigHash = ConfigHash(ConfigLoader.DefaultRoot);
            provenance.UnityVersion = Application.unityVersion;

            double? split = experiment.splitPenalty >= 0 ? experiment.splitPenalty : (double?)null;
            log?.Invoke($"experiment {experiment.name}: {experiment.EpisodeCount} episodes → {outputDirectory}");

            return ExperimentRunner.Run(
                experiment,
                (scenario, regime, seed) => configs.Setup(scenario, regime, seed, experiment.stockMultiplier, split),
                configs.CreatePolicy,
                outputDirectory,
                provenance,
                (done, total, r) =>
                {
                    if (done % 30 == 0 || done == total)
                        log?.Invoke($"  {done}/{total} {r.Policy} {r.Scenario} {r.Regime} seed {r.Seed}: profit {r.Kpis.Profit:0}");
                });
        }

        /// <summary>
        /// Runs an evolution experiment and writes the genome that wins on the validation seeds to
        /// policies/evolved_dna.json, where the Evolved policy reads it.
        /// </summary>
        public static List<EvolutionExperiment.RunResult> RunEvolution(string experimentName, string outputDirectory,
            ExperimentProvenance provenance, Action<string> log)
        {
            ConfigSet configs = ConfigLoader.LoadDefault();
            List<string> configErrors = configs.Validate();
            if (configErrors.Count > 0)
                throw new InvalidOperationException("Config errors:\n" + string.Join("\n", configErrors));

            var experiment = ConfigLoader.Read<EvolutionConfig>(ExperimentPath(experimentName));
            provenance.ConfigHash = ConfigHash(ConfigLoader.DefaultRoot);
            provenance.UnityVersion = Application.unityVersion;
            _ = configs.BuildWorld().Routes; // build the shared world and routes before parallel evaluation

            log?.Invoke($"evolution {experiment.name}: {experiment.mutationRates.Length} mutation rates × {experiment.gaSeeds.Length} GA seeds, " +
                        $"population {experiment.populationSize}, {experiment.generations} generations → {outputDirectory}");
            List<EvolutionExperiment.RunResult> runs = EvolutionExperiment.Run(experiment,
                (scenario, regime, seed) => configs.Setup(scenario, regime, seed), configs.Policies, outputDirectory, provenance, log);

            EvolutionExperiment.RunResult best = runs.OrderByDescending(r => r.ValidationFitness).First();
            var dna = new EvolvedDnaConfig
            {
                genes = best.Best.ToArray(),
                source = $"{experiment.name}: mutation {best.MutationRate.ToString(System.Globalization.CultureInfo.InvariantCulture)}, GA seed {best.GaSeed}, commit {provenance.GitCommit}",
                validationFitness = Math.Round(best.ValidationFitness, 4)
            };
            File.WriteAllText(Path.Combine(ConfigLoader.DefaultRoot, "policies", "evolved_dna.json"), JsonUtility.ToJson(dna, true));
            log?.Invoke($"selected {best.Best} (mutation {best.MutationRate}, seed {best.GaSeed}), validation {best.ValidationFitness:0.00} TL/order → policies/evolved_dna.json");
            return runs;
        }

        /// <summary>SHA-256 over every config file (path and content), so results can be matched to the exact settings.</summary>
        public static string ConfigHash(string root)
        {
            using var sha = SHA256.Create();
            var sb = new StringBuilder();
            foreach (string file in Directory.GetFiles(root, "*.json", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
            {
                string relative = file.Substring(root.Length).Replace('\\', '/');
                sb.Append(relative).Append('\n').Append(File.ReadAllText(file).Replace("\r\n", "\n")).Append('\n');
            }
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            return string.Concat(hash.Select(b => b.ToString("x2")));
        }

        /// <summary>Provenance inside a player build: the commit recorded by the build script, if any.</summary>
        public static ExperimentProvenance FromBuildInfo()
        {
            var provenance = new ExperimentProvenance();
            string path = Path.Combine(Application.streamingAssetsPath, "build_info.json");
            if (!File.Exists(path)) return provenance;
            var info = JsonUtility.FromJson<BuildInfo>(File.ReadAllText(path));
            if (info != null && !string.IsNullOrEmpty(info.commit))
            {
                provenance.GitCommit = info.commit;
                provenance.GitDirty = info.dirty;
            }
            return provenance;
        }
    }
}
