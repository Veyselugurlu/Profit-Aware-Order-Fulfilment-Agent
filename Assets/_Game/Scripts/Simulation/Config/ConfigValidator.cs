using System;
using System.Collections.Generic;

namespace Fulfilment.Simulation
{
    /// <summary>
    /// Minimums from the project brief (Proje 2, "Minimum Functional Requirements" and the common rules).
    /// Configs below these numbers would fail the brief, so validation reports them as errors.
    /// </summary>
    public static class BriefMinimums
    {
        public const int Centers = 3;
        public const int Skus = 30;
        public const int OrdersPerEpisode = 500;
        public const int LoadLevels = 3;
    }

    public static class ConfigValidator
    {
        public static List<string> Validate(WorldConfig world)
        {
            var errors = new List<string>();
            if (world == null)
            {
                errors.Add("world config is missing");
                return errors;
            }

            if (world.gridWidth <= 0 || world.gridHeight <= 0)
                errors.Add("grid size must be positive");

            int centers = world.centers?.Length ?? 0;
            int skus = world.skus?.Length ?? 0;
            int regions = world.regions?.Length ?? 0;
            if (centers < BriefMinimums.Centers)
                errors.Add($"{centers} fulfilment centers, the brief requires at least {BriefMinimums.Centers}");
            if (skus < BriefMinimums.Skus)
                errors.Add($"{skus} SKUs, the brief requires at least {BriefMinimums.Skus}");
            if (regions == 0)
                errors.Add("at least one customer region is required");

            var cells = new HashSet<(int, int)>();
            double shareSum = 0.0;
            for (int i = 0; i < centers; i++)
            {
                CenterConfig c = world.centers[i];
                CheckCell(errors, world, $"center {c.code}", c.x, c.y);
                if (!cells.Add((c.x, c.y)))
                    errors.Add($"center {c.code} shares a cell with another site");
                if (c.capacityPerHour <= 0)
                    errors.Add($"center {c.code} capacity must be positive");
                if (c.stockShare <= 0.0)
                    errors.Add($"center {c.code} stock share must be positive");
                shareSum += c.stockShare;
            }
            if (centers > 0 && Math.Abs(shareSum - 1.0) > 1e-6)
                errors.Add($"center stock shares sum to {shareSum:0.###}, expected 1");

            for (int i = 0; i < regions; i++)
            {
                RegionConfig r = world.regions[i];
                CheckCell(errors, world, $"region {r.code}", r.x, r.y);
                if (!cells.Add((r.x, r.y)))
                    errors.Add($"region {r.code} shares a cell with another site");
                if (r.demandWeight <= 0.0)
                    errors.Add($"region {r.code} demand weight must be positive");
            }

            var codes = new HashSet<string>();
            for (int i = 0; i < skus; i++)
            {
                SkuConfig s = world.skus[i];
                if (string.IsNullOrEmpty(s.code) || !codes.Add(s.code))
                    errors.Add($"SKU {i} has an empty or duplicate code");
                if (s.price <= 0.0 || s.unitCost <= 0.0)
                    errors.Add($"SKU {s.code} price and unit cost must be positive");
                if (s.unitCost >= s.price)
                    errors.Add($"SKU {s.code} unit cost must be below its price");
                if (s.weightKg <= 0.0)
                    errors.Add($"SKU {s.code} weight must be positive");
            }

            return errors;
        }

        public static List<string> Validate(DemandProfileConfig demand, int skuCount)
        {
            var errors = new List<string>();
            if (demand == null)
            {
                errors.Add("demand profile is missing");
                return errors;
            }

            if (demand.hourWeights == null || demand.hourWeights.Length != 24)
                errors.Add("hourWeights must have 24 values");
            else if (!HasPositive(demand.hourWeights))
                errors.Add("hourWeights must contain a positive value");

            bool hasSkuWeights = demand.skuWeights != null && demand.skuWeights.Length > 0;
            if (hasSkuWeights && demand.skuWeights.Length != skuCount)
                errors.Add($"skuWeights has {demand.skuWeights.Length} values for {skuCount} SKUs");
            if (!hasSkuWeights && demand.zipfExponent <= 0.0)
                errors.Add("without skuWeights a positive zipfExponent is required");

            if (demand.basketSizeWeights == null || !HasPositive(demand.basketSizeWeights))
                errors.Add("basketSizeWeights must contain a positive value");
            if (demand.quantityWeights == null || !HasPositive(demand.quantityWeights))
                errors.Add("quantityWeights must contain a positive value");
            return errors;
        }

        public static List<string> Validate(IReadOnlyList<ScenarioConfig> scenarios)
        {
            var errors = new List<string>();
            int count = scenarios?.Count ?? 0;
            if (count < BriefMinimums.LoadLevels)
                errors.Add($"{count} load scenarios, the brief requires at least {BriefMinimums.LoadLevels}");

            for (int i = 0; i < count; i++)
            {
                ScenarioConfig s = scenarios[i];
                if (s.episodeDays <= 0)
                    errors.Add($"scenario {s.code} episodeDays must be positive");
                if (s.ExpectedOrders < BriefMinimums.OrdersPerEpisode)
                    errors.Add($"scenario {s.code} expects {s.ExpectedOrders:0} orders per episode, the brief requires {BriefMinimums.OrdersPerEpisode}+");
                if (s.expressShare < 0.0 || s.expressShare > 1.0)
                    errors.Add($"scenario {s.code} expressShare must be in [0,1]");
                if (s.promisedHoursStandard <= 0.0 || s.promisedHoursExpress <= 0.0)
                    errors.Add($"scenario {s.code} promised hours must be positive");
                if (s.stockDays <= 0.0)
                    errors.Add($"scenario {s.code} stockDays must be positive");
                if (s.leadTimeHoursMin < 0.0 || s.leadTimeHoursMax < s.leadTimeHoursMin)
                    errors.Add($"scenario {s.code} lead time range is invalid");
            }
            return errors;
        }

        private static void CheckCell(List<string> errors, WorldConfig world, string label, int x, int y)
        {
            if (x < 0 || y < 0 || x >= world.gridWidth || y >= world.gridHeight)
                errors.Add($"{label} at ({x},{y}) is outside the {world.gridWidth}x{world.gridHeight} grid");
        }

        private static bool HasPositive(double[] values)
        {
            bool positive = false;
            foreach (double v in values)
            {
                if (v < 0.0 || double.IsNaN(v)) return false;
                if (v > 0.0) positive = true;
            }
            return positive;
        }
    }
}
