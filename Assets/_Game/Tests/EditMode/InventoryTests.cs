using System;
using Fulfilment.Simulation;
using NUnit.Framework;

namespace Fulfilment.Tests
{
    public class InventoryTests
    {
        [Test]
        public void AddRemove_TracksStockPerCenter()
        {
            var inv = new Inventory(3, 5);
            inv.Add(0, 2, 10);
            inv.Add(1, 2, 4);
            inv.Remove(0, 2, 3);

            Assert.AreEqual(7, inv.OnHand(0, 2));
            Assert.AreEqual(4, inv.OnHand(1, 2));
            Assert.AreEqual(11, inv.NetworkOnHand(2));
        }

        [Test]
        public void Remove_MoreThanOnHand_Throws()
        {
            var inv = new Inventory(1, 1);
            inv.Add(0, 0, 2);
            Assert.Throws<InvalidOperationException>(() => inv.Remove(0, 0, 3));
            Assert.AreEqual(2, inv.OnHand(0, 0), "failed removal must not change stock");
        }

        [Test]
        public void CanFulfilAll_RequiresEveryLine()
        {
            var inv = new Inventory(2, 3);
            inv.Add(0, 0, 1);
            inv.Add(0, 1, 2);
            inv.Add(1, 0, 5);

            var lines = new[] { new OrderLine(0, 1), new OrderLine(1, 2) };
            Assert.IsTrue(inv.CanFulfilAll(0, lines));
            Assert.IsFalse(inv.CanFulfilAll(1, lines));
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var inv = new Inventory(1, 1);
            inv.Add(0, 0, 5);
            Inventory copy = inv.Clone();
            copy.Remove(0, 0, 5);

            Assert.AreEqual(5, inv.OnHand(0, 0));
            Assert.AreEqual(0, copy.OnHand(0, 0));
        }

        [Test]
        public void Plan_CountsPackagesByDistinctCenter()
        {
            var plan = new FulfilmentPlan(PlanKind.MinCostSplit,
                new[] { new Allocation(0, 1, 1), new Allocation(2, 3, 1), new Allocation(0, 4, 2) },
                Array.Empty<OrderLine>());

            Assert.AreEqual(2, plan.PackageCount);
            Assert.IsTrue(plan.IsSplit);
            Assert.IsFalse(plan.HasLostUnits);
        }

        [Test]
        public void Shipment_AdvancesThroughStates()
        {
            var shipment = new Shipment(0, 0, 0, 0, new[] { new OrderLine(0, 1) }, dispatchTick: 10, arrivalTick: 70);
            Assert.IsFalse(shipment.Advance(5));
            Assert.AreEqual(ShipmentState.Packing, shipment.State);
            Assert.IsFalse(shipment.Advance(40));
            Assert.AreEqual(ShipmentState.InTransit, shipment.State);
            Assert.AreEqual(0.5, shipment.Progress(40), 1e-9);
            Assert.IsTrue(shipment.Advance(70));
            Assert.IsFalse(shipment.Advance(71), "delivery is reported once");
        }
    }
}
