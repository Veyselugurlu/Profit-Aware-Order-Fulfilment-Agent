namespace Fulfilment.Simulation
{
    /// <summary>Road distance between a center and a region (A* routes by default, see <see cref="RoadDistanceModel"/>).</summary>
    public interface IDistanceModel
    {
        double DistanceKm(int centerId, int regionId);
    }

    /// <summary>Straight Manhattan distance, ignoring the river (Sprint 2 estimate; kept for ablation and tests).</summary>
    public sealed class GridDistanceModel : IDistanceModel
    {
        private readonly double[,] km;

        public GridDistanceModel(World world, double kmPerCell)
        {
            km = new double[world.Centers.Count, world.Regions.Count];
            for (int c = 0; c < world.Centers.Count; c++)
                for (int r = 0; r < world.Regions.Count; r++)
                    km[c, r] = world.Centers[c].Cell.ManhattanDistance(world.Regions[r].Cell) * kmPerCell;
        }

        public double DistanceKm(int centerId, int regionId) => km[centerId, regionId];
    }
}
