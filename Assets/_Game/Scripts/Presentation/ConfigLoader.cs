using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fulfilment.Simulation;
using UnityEngine;

namespace Fulfilment.Presentation
{
    /// <summary>Every config needed to build an episode, loaded once from StreamingAssets.</summary>
    public sealed class ConfigSet
    {
        public WorldConfig World { get; }
        public DemandProfileConfig Demand { get; }

        /// <summary>Load levels ordered by daily order volume (Low, Medium, High).</summary>
        public IReadOnlyList<ScenarioConfig> Scenarios { get; }

        public ConfigSet(WorldConfig world, DemandProfileConfig demand, IReadOnlyList<ScenarioConfig> scenarios)
        {
            World = world;
            Demand = demand;
            Scenarios = scenarios;
        }

        public ScenarioConfig Scenario(string code) =>
            Scenarios.FirstOrDefault(s => string.Equals(s.code, code, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"No scenario with code '{code}'.");

        /// <summary>Every validation error across the configs; empty when the set meets the brief.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            errors.AddRange(ConfigValidator.Validate(World));
            errors.AddRange(ConfigValidator.Validate(Demand, World?.skus?.Length ?? 0));
            errors.AddRange(ConfigValidator.Validate(Scenarios));
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

            string scenarioDir = Path.Combine(root, "scenarios");
            if (!Directory.Exists(scenarioDir))
                throw new DirectoryNotFoundException($"Scenario folder not found: {scenarioDir}");

            List<ScenarioConfig> scenarios = Directory.GetFiles(scenarioDir, "*.json")
                .Select(Read<ScenarioConfig>)
                .OrderBy(s => s.ordersPerDay)
                .ToList();

            return new ConfigSet(world, demand, scenarios);
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
    }
}
