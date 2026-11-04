using System;
using System.Collections.Generic;
using System.Linq;
using Fulfilment.Simulation;

namespace Fulfilment.AI
{
    /// <summary>Best genome found by the evolution experiment (policies/evolved_dna.json).</summary>
    [Serializable]
    public sealed class EvolvedDnaConfig
    {
        public int[] genes;
        public string source;
        public double validationFitness;
    }

    /// <summary>Everything the policies can be built from; loaded once with the other configs.</summary>
    public sealed class PolicySettings
    {
        public UtilityConfig Utility;
        public WeightProfilesConfig Profiles;
        public EvolvedDnaConfig Evolved;
    }

    /// <summary>Creates policies by name for the UI policy selector and the experiment runner.</summary>
    public static class PolicyFactory
    {
        public const string Nearest = "Nearest";
        public const string LowestCost = "LowestCost";
        public const string Utility = "Utility";
        public const string Evolved = "Evolved";

        public static readonly IReadOnlyList<string> Names = new[] { Nearest, LowestCost, Utility, Evolved };

        /// <summary>Policies whose settings are present (Evolved needs the genome written by the evolution experiment).</summary>
        public static IReadOnlyList<string> Available(PolicySettings settings) =>
            Names.Where(n => n != Evolved || settings?.Evolved?.genes?.Length == DNA.StateCount).ToList();

        public static IFulfilmentPolicy Create(string name, PolicySettings settings = null)
        {
            switch (name)
            {
                case Nearest: return new NearestPolicy();
                case LowestCost: return new LowestCostPolicy();
                case Utility:
                    if (settings?.Utility == null) throw new ArgumentException("The Utility policy needs a utility config.");
                    return new UtilityPolicy(settings.Utility);
                case Evolved:
                    if (settings?.Evolved?.genes == null)
                        throw new ArgumentException("The Evolved policy needs policies/evolved_dna.json (run the evolve_profiles experiment first).");
                    return new Brain(new DNA(settings.Evolved.genes), settings.Utility, settings.Profiles);
                default: throw new ArgumentException($"Unknown policy '{name}'. Known: {string.Join(", ", Names)}");
            }
        }
    }
}
