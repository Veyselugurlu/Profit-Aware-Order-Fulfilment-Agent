using System;
using System.Collections.Generic;

namespace Fulfilment.AI
{
    /// <summary>Named Utility weight sets (policies/weight_profiles.json) that adaptive policies choose between.</summary>
    [Serializable]
    public sealed class WeightProfilesConfig
    {
        public string note;
        public WeightProfile[] profiles;

        public List<string> Validate(int criteriaCount)
        {
            var errors = new List<string>();
            if (profiles == null || profiles.Length < 2)
            {
                errors.Add("at least two weight profiles are required");
                return errors;
            }
            foreach (WeightProfile p in profiles)
            {
                if (p.weights == null || p.weights.Length != criteriaCount)
                    errors.Add($"profile '{p.name}' needs {criteriaCount} weights");
                else
                {
                    double sum = 0;
                    foreach (double w in p.weights)
                    {
                        if (w < 0) errors.Add($"profile '{p.name}' has a negative weight");
                        sum += w;
                    }
                    if (Math.Abs(sum - 1.0) > 1e-6) errors.Add($"profile '{p.name}' weights sum to {sum:0.###}, expected 1");
                }
            }
            return errors;
        }
    }

    [Serializable]
    public sealed class WeightProfile
    {
        public string name;
        public string label;
        public double[] weights;
    }
}
