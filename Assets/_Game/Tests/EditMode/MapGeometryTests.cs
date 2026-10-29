using Fulfilment.Presentation;
using Fulfilment.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Fulfilment.Tests
{
    public class MapGeometryTests : SimulationTestBase
    {
        [Test]
        public void CellCenter_IsTheMiddleOfTheUnitSquare()
        {
            Assert.AreEqual(new Vector3(3.5f, 7.5f, 0f), MapGeometry.CellCenter(new GridCell(3, 7)));
        }

        [Test]
        public void Along_StartsAtTheCenterAndEndsAtTheRegion()
        {
            GridPath path = World.Routes.Path(0, 5);
            Vector3[] points = MapGeometry.PathPoints(path);
            Assert.AreEqual(MapGeometry.CellCenter(World.Centers[0].Cell), MapGeometry.Along(points, 0f));
            Assert.AreEqual(MapGeometry.CellCenter(World.Regions[5].Cell), MapGeometry.Along(points, 1f));
            Assert.AreEqual(MapGeometry.CellCenter(World.Regions[5].Cell), MapGeometry.Along(points, 1.7f), "clamped past the end");
        }

        [Test]
        public void Along_MovesAtConstantSpeedOnTheRoad()
        {
            Vector3[] points = { Vector3.zero, Vector3.right, new Vector3(1f, 1f, 0f) };
            Assert.AreEqual(new Vector3(0.5f, 0f, 0f), MapGeometry.Along(points, 0.25f));
            Assert.AreEqual(new Vector3(1f, 0f, 0f), MapGeometry.Along(points, 0.5f));
            Assert.AreEqual(new Vector3(1f, 0.5f, 0f), MapGeometry.Along(points, 0.75f));
        }

        [Test]
        public void EveryRoutePoint_LiesOnARoadCell()
        {
            for (int c = 0; c < World.Centers.Count; c++)
                for (int r = 0; r < World.Regions.Count; r++)
                    foreach (Vector3 p in MapGeometry.PathPoints(World.Routes.Path(c, r)))
                        Assert.IsTrue(World.Roads.IsDrivable(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y)));
        }
    }
}
