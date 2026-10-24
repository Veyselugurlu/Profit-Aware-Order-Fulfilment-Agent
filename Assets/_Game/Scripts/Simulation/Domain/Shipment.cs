using System.Collections.Generic;

namespace Fulfilment.Simulation
{
    public enum ShipmentState
    {
        Packing = 0,
        InTransit = 1,
        Delivered = 2
    }

    /// <summary>One parcel from one center to the order's region. A split order has several shipments.</summary>
    public sealed class Shipment
    {
        public int Id { get; }
        public int OrderId { get; }
        public int CenterId { get; }
        public int RegionId { get; }
        public IReadOnlyList<OrderLine> Lines { get; }
        public int DispatchTick { get; }
        public int ArrivalTick { get; }

        public ShipmentState State { get; private set; }

        public Shipment(int id, int orderId, int centerId, int regionId, IReadOnlyList<OrderLine> lines, int dispatchTick, int arrivalTick)
        {
            Id = id;
            OrderId = orderId;
            CenterId = centerId;
            RegionId = regionId;
            Lines = lines;
            DispatchTick = dispatchTick;
            ArrivalTick = arrivalTick;
            State = ShipmentState.Packing;
        }

        /// <summary>Moves the state forward from the clock; returns true when it just got delivered.</summary>
        public bool Advance(int tick)
        {
            if (State == ShipmentState.Delivered)
                return false;
            if (tick >= ArrivalTick)
            {
                State = ShipmentState.Delivered;
                return true;
            }
            if (tick >= DispatchTick)
                State = ShipmentState.InTransit;
            return false;
        }

        /// <summary>0..1 progress along the route, used by the parcel animation.</summary>
        public double Progress(double tick)
        {
            if (tick <= DispatchTick) return 0.0;
            if (tick >= ArrivalTick || ArrivalTick == DispatchTick) return 1.0;
            return (tick - DispatchTick) / (ArrivalTick - DispatchTick);
        }
    }
}
