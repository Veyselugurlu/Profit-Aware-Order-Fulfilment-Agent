using System;

namespace Fulfilment.Simulation
{
    /// <summary>Integer cell of the city grid (x to the right, y upwards).</summary>
    public readonly struct GridCell : IEquatable<GridCell>
    {
        public readonly int X;
        public readonly int Y;

        public GridCell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int ManhattanDistance(GridCell other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

        public double EuclideanDistance(GridCell other)
        {
            int dx = X - other.X, dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public bool Equals(GridCell other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridCell other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public static bool operator ==(GridCell a, GridCell b) => a.Equals(b);
        public static bool operator !=(GridCell a, GridCell b) => !a.Equals(b);
        public override string ToString() => $"({X},{Y})";
    }
}
