using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fulfilment.AI;
using Fulfilment.Simulation;
using UnityEngine;

namespace Fulfilment.Presentation
{
    /// <summary>Every config needed to build an episode, loaded once from StreamingAssets.</summary>
    public sealed class ConfigSet
    {
        private static readonly string[] RegimeOrder = { "stable", "seasonal", "volatile" };

        private World world;

        public WorldConfig World { get; }
        public DemandProfileConfig Demand { get; }
        public CostConfig Cost { get; }

        /// <summary>Fixed-weight Utility AI settings (policies/utility_default.json).</summary>
        public UtilityConfig Utility { get; }

        /// <summary>Load levels ordered by daily order volume (Low, Medium, High).</summary>
        public IReadOnlyList<ScenarioConfig> Scenarios { get; }

        /// <summary>Demand regimes: stable, seasonal, volatile, then any others by code.</summary>
        public IReadOnlyList<RegimeConfig> Regimes { get; }

        public ConfigSet(WorldConfig world, DemandProfileConfig demand, CostConfig cost, UtilityConfig utility,
            IReadOnlyList<ScenarioConfig> scenarios, IReadOnlyList<RegimeConfig> regimes)
        {
            World = world;
            Demand = demand;
            Cost = cost;
            Utility = utility;
            Scenarios = scenarios;
            Regimes = regimes
                .OrderBy(r => Array.IndexOf(RegimeOrder, r.code) is int i && i >= 0 ? i : RegimeOrder.Length)
                .ThenBy(r => r.code, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>The world built from <see cref="World"/>, created once.</summary>
        public World BuildWorld() => world ??= Simulation.World.FromConfig(World);

        public ScenarioConfig Scenario(string code) =>
            Scenarios.FirstOrDefault(s => string.Equals(s.code, code, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"No scenario with code '{code}'.");

        public RegimeConfig Regime(string code) =>
            Regimes.FirstOrDefault(r => string.Equals(r.code, code, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"No regime with code '{code}'.");

        /// <summary>
        /// Episode with optional overrides from the scenario panel: stock level (order-up-to multiplier) and the
        /// split penalty per extra parcel. Overrides never change the loaded configs.
        /// </summary>
        public EpisodeSetup Setup(string scenario, string regime, int seed, double stockMultiplier = 1.0, double? splitPenalty = null)
        {
            CostConfig cost = Cost;
            if (splitPenalty.HasValue)
            {
                cost = Cost.Clone();
                cost.splitPenaltyPerExtraPackage = splitPenalty.Value;
            }
            return new EpisodeSetup(BuildWorld(), Scenario(scenario), Demand, cost, Regime(regime), seed, stockMultiplier);
        }

        public IFulfilmentPolicy CreatePolicy(string name) => PolicyFactory.Create(name, Utility);

        /// <summary>Every validation error across the configs; empty when the set meets the brief.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            errors.AddRange(ConfigValidator.Validate(World));
            errors.AddRange(ConfigValidator.Validate(Demand, World?.skus?.Length ?? 0));
            errors.AddRange(ConfigValidator.Validate(Scenarios));
            errors.AddRange(ConfigValidator.Validate(Cost));
            errors.AddRange(UtilityCriteria.Validate(Utility));
            if (Regimes.Count == 0)
                errors.Add("at least one demand regime is required");
            foreach (RegimeConfig regime in Regimes)
                errors.AddRange(ConfigValidator.Validate(regime));
            return errors;
        }
    }

    /// <summary>
    /// Reads the JSON configs with JsonUtility. The simulation assemblies stay free of UnityEngine,
    /// so parsing lives here and only plain config objects are passed down.
    /// </summary>
    public static class ConfigLoader
    {
        public static string DefaultRoot => Path.Combine(Application.streamingAssetsPath, "configs");

        public static ConfigSet LoadDefault() => Load(DefaultRoot);

        public static ConfigSet Load(string root)
        {
            var world = Read<WorldConfig>(Path.Combine(root, "world.json"));
            var demand = Read<DemandProfileConfig>(Path.Combine(root, "demand_profile.json"));
            var cost = Read<CostConfig>(Path.Combine(root, "cost.json"));
            var utility = Read<UtilityConfig>(Path.Combine(root, "policies", "utility_default.json"));

            List<ScenarioConfig> scenarios = ReadAll<ScenarioConfig>(Path.Combine(root, "scenarios"))
                .OrderBy(s => s.ordersPerDay)
                .ToList();
            List<RegimeConfig> regimes = ReadAll<RegimeConfig>(Path.Combine(root, "regimes"));

            return new ConfigSet(world, demand, cost, utility, scenarios, regimes);
        }

        public static T Read<T>(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Config file not found: {path}", path);
            T value = JsonUtility.FromJson<T>(File.ReadAllText(path));
            if (value == null)
                throw new InvalidDataException($"Config file is empty or not valid JSON: {path}");
            return value;
        }

        private static List<T> ReadAll<T>(string directory)
        {
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException($"Config folder not found: {directory}");
            return Directory.GetFiles(directory, "*.json")
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(Read<T>)
                .ToList();
        }
    }
}
