using System.Collections.Generic;
using System.Linq;
using Fulfilment.Simulation;
using NUnit.Framework;

namespace Fulfilment.Tests
{
    public class RoadNetworkTests : SimulationTestBase
    {
        [Test]
        public void EverySite_IsOnTheRoadNetwork()
        {
            foreach (var c in World.Centers) Assert.AreEqual(CellType.Site, World.Roads[c.Cell], c.Code);
            foreach (var r in World.Regions) Assert.AreEqual(CellType.Site, World.Roads[r.Cell], r.Code);
        }

        [Test]
        public void River_IsOnlyCrossableOnBridges()
        {
            RoadLayoutConfig layout = Configs.World.roads;
            for (int y = 0; y < World.GridHeight; y++)
            {
                CellType expected = layout.bridgeYs.Contains(y) ? CellType.Bridge : CellType.River;
                Assert.AreEqual(expected, World.Roads[layout.riverX, y], $"row {y}");
            }
        }

        [Test]
        public void EveryCenterRegionPair_HasAValidRoute()
        {
            for (int c = 0; c < World.Centers.Count; c++)
                for (int r = 0; r < World.Regions.Count; r++)
                {
                    GridPath path = World.Routes.Path(c, r);
                    Assert.IsNotNull(path);
                    Assert.AreEqual(World.Centers[c].Cell, path.Cells[0]);
                    Assert.AreEqual(World.Regions[r].Cell, path.Cells[path.Cells.Count - 1]);
                    for (int i = 1; i < path.Cells.Count; i++)
                    {
                        Assert.AreEqual(1, path.Cells[i - 1].ManhattanDistance(path.Cells[i]), "consecutive cells are neighbours");
                        Assert.IsTrue(World.Roads.IsDrivable(path.Cells[i].X, path.Cells[i].Y), "route stays on roads");
                    }
                    Assert.GreaterOrEqual(path.Steps, World.Centers[c].Cell.ManhattanDistance(World.Regions[r].Cell));
                }
        }

        [Test]
        public void CrossRiverRoutes_UseABridge()
        {
            int riverX = Configs.World.roads.riverX;
            int crossings = 0;
            for (int c = 0; c < World.Centers.Count; c++)
                for (int r = 0; r < World.Regions.Count; r++)
                {
                    bool westCenter = World.Centers[c].Cell.X < riverX;
                    bool westRegion = World.Regions[r].Cell.X < riverX;
                    if (westCenter == westRegion) continue;
                    crossings++;
                    Assert.That(World.Routes.Path(c, r).Cells.Any(cell => World.Roads[cell] == CellType.Bridge));
                }
            Assert.Greater(crossings, 0);
        }

        [Test]
        public void AStar_FindsShortestRoutes_SameLengthAsBreadthFirstSearch()
        {
            var drivable = new List<GridCell>();
            for (int x = 0; x < World.Roads.Width; x++)
                for (int y = 0; y < World.Roads.Height; y++)
                    if (World.Roads.IsDrivable(x, y)) drivable.Add(new GridCell(x, y));

            var rng = new SeededRng(7, RngStream.Layout);
            var finder = new AStarPathfinder(World.Roads);
            for (int i = 0; i < 200; i++)
            {
                GridCell a = drivable[rng.NextInt(0, drivable.Count)];
                GridCell b = drivable[rng.NextInt(0, drivable.Count)];
                int bfs = BreadthFirstSteps(World.Roads, a, b);
                GridPath path = finder.FindPath(a, b);
                if (bfs < 0) Assert.IsNull(path);
                else Assert.AreEqual(bfs, path.Steps, $"{a} → {b}");
            }
        }

        [Test]
        public void AStar_ExpandsFewerNodesThanUninformedSearch()
        {
            var informed = new AStarPathfinder(World.Roads);
            var uninformed = new AStarPathfinder(World.Roads) { Heuristic = (x, y) => 0.0 }; // Dijkstra
            GridCell from = World.Centers[3].Cell, to = World.Regions[5].Cell;
            GridPath a = informed.FindPath(from, to), d = uninformed.FindPath(from, to);
            Assert.AreEqual(d.Steps, a.Steps);
            Assert.Less(a.Expanded, d.Expanded);
        }

        [Test]
        public void Blocks_AreNotDrivable_AndGiveNoRoute()
        {
            var finder = new AStarPathfinder(World.Roads);
            GridCell block = new GridCell(1, 1); // inside the first building block
            Assert.AreEqual(CellType.Block, World.Roads[block]);
            Assert.IsNull(finder.FindPath(block, World.Regions[0].Cell));
        }

        [Test]
        public void RoadDistance_IsNeverShorterThanStraightManhattan()
        {
            var road = new RoadDistanceModel(World, Configs.Cost.kmPerCell);
            var grid = new GridDistanceModel(World, Configs.Cost.kmPerCell);
            for (int c = 0; c < World.Centers.Count; c++)
                for (int r = 0; r < World.Regions.Count; r++)
                    Assert.GreaterOrEqual(road.DistanceKm(c, r), grid.DistanceKm(c, r) - 1e-9);
        }

        private static int BreadthFirstSteps(RoadNetwork roads, GridCell from, GridCell to)
        {
            var dist = new Dictionary<GridCell, int> { [from] = 0 };
            var queue = new Queue<GridCell>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                GridCell c = queue.Dequeue();
                if (c == to) return dist[c];
                foreach (var n in new[] { new GridCell(c.X + 1, c.Y), new GridCell(c.X - 1, c.Y), new GridCell(c.X, c.Y + 1), new GridCell(c.X, c.Y - 1) })
                {
                    if (!roads.IsDrivable(n.X, n.Y) || dist.ContainsKey(n)) continue;
                    dist[n] = dist[c] + 1;
                    queue.Enqueue(n);
                }
            }
            return -1;
        }
    }
}
