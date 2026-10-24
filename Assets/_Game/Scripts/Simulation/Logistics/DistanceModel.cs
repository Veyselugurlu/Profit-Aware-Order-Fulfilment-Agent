namespace Fulfilment.Simulation
{
    /// <summary>Road distance between a center and a region. S3 replaces the grid estimate with A* routes.</summary>
    public interface IDistanceModel
    {
        double DistanceKm(int centerId, int regionId);
    }

    /// <summary>Manhattan distance on the city grid (streets run along the grid axes).</summary>
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
