using System.Collections.Generic;
using Fulfilment.Simulation;
using UnityEngine;

namespace Fulfilment.Presentation
{
    /// <summary>Grid cell (x, y) is drawn as the unit square [x, x+1] × [y, y+1] in world space.</summary>
    public static class MapGeometry
    {
        public static Vector3 CellCenter(GridCell cell, float z = 0f) => new Vector3(cell.X + 0.5f, cell.Y + 0.5f, z);

        public static Vector3[] PathPoints(GridPath path)
        {
            var points = new Vector3[path.Cells.Count];
            for (int i = 0; i < points.Length; i++)
                points[i] = CellCenter(path.Cells[i]);
            return points;
        }

        /// <summary>Point at fraction <paramref name="t"/> (0..1) along a polyline of equal-length segments.</summary>
        public static Vector3 Along(IReadOnlyList<Vector3> points, float t)
        {
            if (points.Count == 1) return points[0];
            float position = Mathf.Clamp01(t) * (points.Count - 1);
            int i = Mathf.Min((int)position, points.Count - 2);
            return Vector3.Lerp(points[i], points[i + 1], position - i);
        }
    }

    /// <summary>Something the user can click on the map; the HUD shows its description.</summary>
    public interface ISelectable
    {
        string Title { get; }
        Transform Anchor { get; }
        bool IsAlive { get; }
        string Describe(SimulationEngine engine);
    }
}
