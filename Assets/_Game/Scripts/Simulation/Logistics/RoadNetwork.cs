using System;
using System.Collections.Generic;

namespace Fulfilment.Simulation
{
    public enum CellType : byte
    {
        Block = 0,
        Road = 1,
        River = 2,
        Bridge = 3,
        Site = 4
    }

    /// <summary>
    /// The drivable city grid. Streets lie on every <c>streetSpacing</c>-th row and column, a river cuts the
    /// city and can only be crossed on bridges, and every center and region gets a short driveway to the
    /// nearest street. Built deterministically from the world config.
    /// </summary>
    public sealed class RoadNetwork
    {
        private readonly CellType[,] cells;

        public int Width { get; }
        public int Height { get; }

        public CellType this[int x, int y] => cells[x, y];
        public CellType this[GridCell c] => cells[c.X, c.Y];

        private RoadNetwork(int width, int height)
        {
            Width = width;
            Height = height;
            cells = new CellType[width, height];
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public bool IsDrivable(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            CellType t = cells[x, y];
            return t == CellType.Road || t == CellType.Bridge || t == CellType.Site;
        }

        public static RoadNetwork Build(WorldConfig config)
        {
            var net = new RoadNetwork(config.gridWidth, config.gridHeight);
            RoadLayoutConfig layout = config.roads;

            if (layout == null || layout.streetSpacing <= 0)
            {
                for (int x = 0; x < net.Width; x++)
                    for (int y = 0; y < net.Height; y++)
                        net.cells[x, y] = CellType.Road;
            }
            else
            {
                var bridges = new HashSet<int>(layout.bridgeYs ?? Array.Empty<int>());
                for (int x = 0; x < net.Width; x++)
                {
                    bool inRiver = x >= layout.riverX && x < layout.riverX + layout.riverWidth;
                    for (int y = 0; y < net.Height; y++)
                    {
                        bool street = x % layout.streetSpacing == 0 || y % layout.streetSpacing == 0;
                        if (inRiver)
                            net.cells[x, y] = bridges.Contains(y) ? CellType.Bridge : CellType.River;
                        else
                            net.cells[x, y] = street ? CellType.Road : CellType.Block;
                    }
                }
            }

            foreach (CenterConfig c in config.centers ?? Array.Empty<CenterConfig>())
                net.ConnectSite(c.x, c.y);
            foreach (RegionConfig r in config.regions ?? Array.Empty<RegionConfig>())
                net.ConnectSite(r.x, r.y);
            return net;
        }

        /// <summary>Marks the site cell and carves a straight driveway to the nearest road on its row or column.</summary>
        private void ConnectSite(int sx, int sy)
        {
            if (!InBounds(sx, sy))
                return;

            bool alreadyOnRoad = IsDrivable(sx, sy);
            cells[sx, sy] = CellType.Site;
            if (alreadyOnRoad)
                return;

            int bestDx = 0, bestDy = 0, bestLength = int.MaxValue;
            foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                for (int step = 1; step < Math.Max(Width, Height); step++)
                {
                    int x = sx + dx * step, y = sy + dy * step;
                    if (!InBounds(x, y) || cells[x, y] == CellType.River) break;
                    if (cells[x, y] == CellType.Road || cells[x, y] == CellType.Bridge)
                    {
                        if (step < bestLength) { bestLength = step; bestDx = dx; bestDy = dy; }
                        break;
                    }
                }
            }

            if (bestLength == int.MaxValue)
                return; // unreachable site; route validation reports it

            for (int step = 1; step < bestLength; step++)
            {
                int x = sx + bestDx * step, y = sy + bestDy * step;
                if (cells[x, y] == CellType.Block)
                    cells[x, y] = CellType.Road;
            }
        }
    }
}
