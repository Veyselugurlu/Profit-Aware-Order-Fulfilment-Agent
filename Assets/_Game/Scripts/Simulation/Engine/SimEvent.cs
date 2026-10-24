namespace Fulfilment.Simulation
{
    public enum SimEventType
    {
        OrderCreated,
        OrderDecided,
        OrderLost,
        ShipmentDispatched,
        ShipmentDelivered,
        OrderCompleted,
        ReplenishmentOrdered,
        Replenished,
        LowStock
    }

    /// <summary>
    /// Something that happened during a tick. The presentation layer animates these; the simulation
    /// itself never reads them back.
    /// </summary>
    public readonly struct SimEvent
    {
        public readonly SimEventType Type;
        public readonly int Tick;
        public readonly int OrderId;
        public readonly int CenterId;
        public readonly int SkuId;
        public readonly int ShipmentId;
        public readonly int Quantity;

        private SimEvent(SimEventType type, int tick, int orderId = -1, int centerId = -1, int skuId = -1, int shipmentId = -1, int quantity = 0)
        {
            Type = type;
            Tick = tick;
            OrderId = orderId;
            CenterId = centerId;
            SkuId = skuId;
            ShipmentId = shipmentId;
            Quantity = quantity;
        }

        public static SimEvent OrderCreated(int tick, int orderId) => new SimEvent(SimEventType.OrderCreated, tick, orderId);
        public static SimEvent OrderDecided(int tick, int orderId) => new SimEvent(SimEventType.OrderDecided, tick, orderId);
        public static SimEvent OrderLost(int tick, int orderId, int units) => new SimEvent(SimEventType.OrderLost, tick, orderId, quantity: units);
        public static SimEvent ShipmentDispatched(int tick, Shipment s) => new SimEvent(SimEventType.ShipmentDispatched, tick, s.OrderId, s.CenterId, shipmentId: s.Id);
        public static SimEvent ShipmentDelivered(int tick, Shipment s) => new SimEvent(SimEventType.ShipmentDelivered, tick, s.OrderId, s.CenterId, shipmentId: s.Id);
        public static SimEvent OrderCompleted(int tick, int orderId) => new SimEvent(SimEventType.OrderCompleted, tick, orderId);
        public static SimEvent ReplenishmentOrdered(int tick, int centerId, int skuId, int quantity) => new SimEvent(SimEventType.ReplenishmentOrdered, tick, centerId: centerId, skuId: skuId, quantity: quantity);
        public static SimEvent Replenished(int tick, int centerId, int skuId, int quantity) => new SimEvent(SimEventType.Replenished, tick, centerId: centerId, skuId: skuId, quantity: quantity);
        public static SimEvent LowStock(int tick, int centerId, int skuId, int onHand) => new SimEvent(SimEventType.LowStock, tick, centerId: centerId, skuId: skuId, quantity: onHand);

        public override string ToString() => $"{Tick} {Type} order={OrderId} fc={CenterId} sku={SkuId} ship={ShipmentId} q={Quantity}";
    }
}
