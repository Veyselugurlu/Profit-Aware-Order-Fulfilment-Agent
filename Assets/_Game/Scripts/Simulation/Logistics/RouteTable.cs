using System;
using System.Collections.Generic;

namespace Fulfilment.Simulation
{
    /// <summary>A* route of every (center, region) pair, computed once per world.</summary>
    public sealed class RouteTable
    {
        private readonly GridPath[,] paths;

        public RouteTable(World world)
        {
            var finder = new AStarPathfinder(world.Roads);
            paths = new GridPath[world.Centers.Count, world.Regions.Count];
            var missing = new List<string>();
            for (int c = 0; c < world.Centers.Count; c++)
                for (int r = 0; r < world.Regions.Count; r++)
                {
                    paths[c, r] = finder.FindPath(world.Centers[c].Cell, world.Regions[r].Cell);
                    if (paths[c, r] == null)
                        missing.Add($"{world.Centers[c].Code}→{world.Regions[r].Code}");
                }

            if (missing.Count > 0)
                throw new InvalidOperationException("No road route for: " + string.Join(", ", missing));
        }

        public GridPath Path(int centerId, int regionId) => paths[centerId, regionId];
    }

    /// <summary>Distance along the A* road route (replaces the straight Manhattan estimate of Sprint 2).</summary>
    public sealed class RoadDistanceModel : IDistanceModel
    {
        private readonly RouteTable routes;
        private readonly double kmPerCell;

        public RoadDistanceModel(World world, double kmPerCell)
        {
            routes = world.Routes;
            this.kmPerCell = kmPerCell;
        }

        public double DistanceKm(int centerId, int regionId) => routes.Path(centerId, regionId).Steps * kmPerCell;
    }
}
