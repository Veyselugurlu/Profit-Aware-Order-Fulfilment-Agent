using System;
using System.Collections.Generic;

namespace Fulfilment.Simulation
{
    /// <summary>A shortest route on the road grid, start and goal included.</summary>
    public sealed class GridPath
    {
        public IReadOnlyList<GridCell> Cells { get; }

        /// <summary>Number of moves between neighbouring cells (route length in cells).</summary>
        public int Steps => Cells.Count - 1;

        /// <summary>Nodes taken off the open list while searching (for comparing heuristics).</summary>
        public int Expanded { get; }

        public GridPath(IReadOnlyList<GridCell> cells, int expanded)
        {
            Cells = cells;
            Expanded = expanded;
        }
    }

    /// <summary>
    /// A* on the 4-connected road grid, following the structure of the Week 4 lab (FindPathAStar): every node
    /// has G (cost from start), H (heuristic to goal) and F = G + H, the open list is expanded in order of
    /// lowest F then lowest H, and closed nodes are not revisited. Differences from the lab code:
    /// neighbours are bounds-checked before the map is read, the path is rebuilt from the parent links,
    /// and the open list is a binary heap instead of a list sorted after every step.
    /// The Manhattan heuristic never overestimates on a 4-connected grid with unit steps, so routes are shortest.
    /// </summary>
    public sealed class AStarPathfinder
    {
        private static readonly (int dx, int dy)[] Directions = { (0, 1), (1, 0), (0, -1), (-1, 0) };

        private readonly RoadNetwork roads;
        private readonly double[] g;
        private readonly double[] h;
        private readonly int[] parent;
        private readonly bool[] closed;
        private readonly int[] heapIndex;
        private readonly int[] stamp;
        private readonly List<int> heap = new List<int>();
        private int search;

        public Func<GridCell, GridCell, double> Heuristic { get; set; } = (a, b) => a.ManhattanDistance(b);

        public AStarPathfinder(RoadNetwork roads)
        {
            this.roads = roads;
            int n = roads.Width * roads.Height;
            g = new double[n];
            h = new double[n];
            parent = new int[n];
            closed = new bool[n];
            heapIndex = new int[n];
            stamp = new int[n];
        }

        private int Index(int x, int y) => y * roads.Width + x;
        private GridCell Cell(int index) => new GridCell(index % roads.Width, index / roads.Width);

        /// <summary>Shortest drivable route, or null when the goal cannot be reached.</summary>
        public GridPath FindPath(GridCell start, GridCell goal)
        {
            if (!roads.IsDrivable(start.X, start.Y) || !roads.IsDrivable(goal.X, goal.Y))
                return null;

            search++;
            heap.Clear();
            int startIndex = Index(start.X, start.Y);
            int goalIndex = Index(goal.X, goal.Y);
            Open(startIndex, 0.0, Heuristic(start, goal), -1);
            int expanded = 0;

            while (heap.Count > 0)
            {
                int current = PopLowestF();
                closed[current] = true;
                expanded++;
                if (current == goalIndex)
                    return new GridPath(Reconstruct(goalIndex), expanded);

                GridCell cell = Cell(current);
                foreach ((int dx, int dy) in Directions)
                {
                    int nx = cell.X + dx, ny = cell.Y + dy;
                    if (!roads.InBounds(nx, ny) || !roads.IsDrivable(nx, ny))
                        continue;

                    int next = Index(nx, ny);
                    if (stamp[next] == search && closed[next])
                        continue;

                    double tentative = g[current] + 1.0;
                    if (stamp[next] != search)
                        Open(next, tentative, Heuristic(new GridCell(nx, ny), goal), current);
                    else if (tentative < g[next])
                    {
                        g[next] = tentative;
                        parent[next] = current;
                        SiftUp(heapIndex[next]);
                    }
                }
            }
            return null;
        }

        private List<GridCell> Reconstruct(int goalIndex)
        {
            var cells = new List<GridCell>();
            for (int i = goalIndex; i != -1; i = parent[i])
                cells.Add(Cell(i));
            cells.Reverse();
            return cells;
        }

        private void Open(int index, double gScore, double hScore, int from)
        {
            stamp[index] = search;
            closed[index] = false;
            g[index] = gScore;
            h[index] = hScore;
            parent[index] = from;
            heap.Add(index);
            heapIndex[index] = heap.Count - 1;
            SiftUp(heap.Count - 1);
        }

        // Lower F first; on equal F the node closer to the goal (lower H), as in the lab's OrderBy(F).ThenBy(H).
        private bool Less(int a, int b)
        {
            double fa = g[a] + h[a], fb = g[b] + h[b];
            if (fa != fb) return fa < fb;
            if (h[a] != h[b]) return h[a] < h[b];
            return a < b;
        }

        private int PopLowestF()
        {
            int top = heap[0];
            int last = heap[heap.Count - 1];
            heap.RemoveAt(heap.Count - 1);
            if (heap.Count > 0)
            {
                heap[0] = last;
                heapIndex[last] = 0;
                SiftDown(0);
            }
            return top;
        }

        private void SiftUp(int i)
        {
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (!Less(heap[i], heap[p])) break;
                Swap(i, p);
                i = p;
            }
        }

        private void SiftDown(int i)
        {
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, smallest = i;
                if (l < heap.Count && Less(heap[l], heap[smallest])) smallest = l;
                if (r < heap.Count && Less(heap[r], heap[smallest])) smallest = r;
                if (smallest == i) return;
                Swap(i, smallest);
                i = smallest;
            }
        }

        private void Swap(int i, int j)
        {
            (heap[i], heap[j]) = (heap[j], heap[i]);
            heapIndex[heap[i]] = i;
            heapIndex[heap[j]] = j;
        }
    }
}
