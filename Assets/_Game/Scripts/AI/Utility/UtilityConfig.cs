using System;
using System.Collections.Generic;

namespace Fulfilment.AI
{
    /// <summary>Utility AI settings (policies/utility_*.json): one entry per criterion.</summary>
    [Serializable]
    public sealed class UtilityConfig
    {
        public string name;
        public CriterionConfig[] criteria;

        public double Weight(string key)
        {
            foreach (CriterionConfig c in criteria)
                if (c.key == key) return c.weight;
            return 0.0;
        }
    }

    /// <summary>
    /// A criterion and its response curve. Curves: "linear" (x), "power" (x^exponent) for inputs already in [0,1];
    /// "logistic" 1 / (1 + e^-(raw - midpoint) / scale) for the raw slack in minutes.
    /// </summary>
    [Serializable]
    public sealed class CriterionConfig
    {
        public string key;
        public double weight;
        public string curve;
        public double exponent = 1.0;
        public double midpoint;
        public double scale = 1.0;
    }

    public static class UtilityCriteria
    {
        public const string Cost = "cost";
        public const string Time = "time";
        public const string Stock = "stock";
        public const string Split = "split";
        public const string Fill = "fill";

        public static readonly IReadOnlyList<string> All = new[] { Cost, Time, Stock, Split, Fill };

        public static List<string> Validate(UtilityConfig config)
        {
            var errors = new List<string>();
            if (config?.criteria == null || config.criteria.Length == 0)
            {
                errors.Add("utility config has no criteria");
                return errors;
            }
            if (config.criteria.Length < 4)
                errors.Add($"utility config has {config.criteria.Length} criteria, the brief requires at least 4");

            double sum = 0.0;
            var seen = new HashSet<string>();
            foreach (CriterionConfig c in config.criteria)
            {
                if (!seen.Add(c.key)) errors.Add($"criterion '{c.key}' appears twice");
                if (Array.IndexOf(new[] { Cost, Time, Stock, Split, Fill }, c.key) < 0) errors.Add($"unknown criterion '{c.key}'");
                if (c.weight < 0.0) errors.Add($"criterion '{c.key}' weight must not be negative");
                if (c.curve != "linear" && c.curve != "power" && c.curve != "logistic") errors.Add($"criterion '{c.key}' has unknown curve '{c.curve}'");
                if (c.curve == "logistic" && c.scale <= 0.0) errors.Add($"criterion '{c.key}' logistic scale must be positive");
                sum += c.weight;
            }
            if (sum <= 0.0) errors.Add("criterion weights must not all be zero");
            return errors;
        }
    }
}
