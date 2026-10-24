using System;
using System.Collections.Generic;

namespace Fulfilment.Simulation
{
    /// <summary>The static part of the simulation: grid size, centers, regions and SKU catalogue.</summary>
    public sealed class World
    {
        public string Name { get; }
        public int GridWidth { get; }
        public int GridHeight { get; }
        public IReadOnlyList<FulfilmentCenter> Centers { get; }
        public IReadOnlyList<CustomerRegion> Regions { get; }
        public IReadOnlyList<Sku> Skus { get; }

        private World(string name, int gridWidth, int gridHeight,
            IReadOnlyList<FulfilmentCenter> centers, IReadOnlyList<CustomerRegion> regions, IReadOnlyList<Sku> skus)
        {
            Name = name;
            GridWidth = gridWidth;
            GridHeight = gridHeight;
            Centers = centers;
            Regions = regions;
            Skus = skus;
        }

        /// <summary>Builds the world; throws with every validation error if the config breaks the brief.</summary>
        public static World FromConfig(WorldConfig config)
        {
            List<string> errors = ConfigValidator.Validate(config);
            if (errors.Count > 0)
                throw new ArgumentException("Invalid world config:\n- " + string.Join("\n- ", errors));

            var centers = new FulfilmentCenter[config.centers.Length];
            for (int i = 0; i < centers.Length; i++)
            {
                CenterConfig c = config.centers[i];
                centers[i] = new FulfilmentCenter(i, c.code, c.name, new GridCell(c.x, c.y), c.capacityPerHour, c.stockShare);
            }

            var regions = new CustomerRegion[config.regions.Length];
            for (int i = 0; i < regions.Length; i++)
            {
                RegionConfig r = config.regions[i];
                regions[i] = new CustomerRegion(i, r.code, r.name, new GridCell(r.x, r.y), r.demandWeight);
            }

            var skus = new Sku[config.skus.Length];
            for (int i = 0; i < skus.Length; i++)
            {
                SkuConfig s = config.skus[i];
                skus[i] = new Sku(i, s.code, s.name, s.category, s.price, s.unitCost, s.weightKg);
            }

            return new World(config.name, config.gridWidth, config.gridHeight, centers, regions, skus);
        }
    }
}
