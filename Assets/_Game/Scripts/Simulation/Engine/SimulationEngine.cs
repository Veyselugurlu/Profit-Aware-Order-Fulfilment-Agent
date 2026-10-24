using System;
using System.Collections.Generic;
using System.Linq;

namespace Fulfilment.Simulation
{
    /// <summary>
    /// Discrete-time simulation of one episode (one tick = one simulated minute). Each <see cref="Step"/>:
    /// 1) receives and places replenishment orders, 2) generates the tick's orders and asks the policy
    /// to choose a plan for each, then applies it (stock, center queue, parcels), 3) delivers parcels that
    /// arrive and completes orders whose last parcel arrived. New orders stop at the end of the episode;
    /// the episode finishes when every parcel has been delivered.
    /// The presentation layer drives the same engine tick by tick; experiments call <see cref="RunToEnd"/>.
    /// </summary>
    public sealed class SimulationEngine
    {
        private readonly List<Order> newOrders = new List<Order>();
        private readonly List<OrderRecord> records = new List<OrderRecord>();
        private readonly Dictionary<int, OrderRecord> recordsById = new Dictionary<int, OrderRecord>();
        private readonly List<Shipment> activeShipments = new List<Shipment>();
        private readonly Dictionary<int, Shipment> shipmentsById = new Dictionary<int, Shipment>();
        private readonly List<SimEvent> events = new List<SimEvent>();
        private readonly CandidateGenerator candidates;
        private readonly OrderGenerator orderGenerator;
        private int nextShipmentId;

        public EpisodeSetup Setup { get; }
        public IFulfilmentPolicy Policy { get; }
        public World World => Setup.World;
        public IDemandRegime Regime { get; }
        public StockPlan StockPlan { get; }
        public Inventory Inventory { get; }
        public ReplenishmentSystem Replenishment { get; }
        public CenterQueues Queues { get; }
        public CostModel Costs { get; }
        public KpiTracker Kpis { get; } = new KpiTracker();

        /// <summary>The next tick to be simulated.</summary>
        public int Tick { get; private set; }

        public int EpisodeTicks => Setup.Scenario.EpisodeTicks;
        public bool IsAcceptingOrders => Tick < EpisodeTicks;
        public bool IsFinished => Tick >= EpisodeTicks && activeShipments.Count == 0;

        public IReadOnlyList<OrderRecord> Records => records;
        public IReadOnlyList<Shipment> ActiveShipments => activeShipments;

        /// <summary>Events of the last <see cref="Step"/>; the list is reused, copy it if you keep it.</summary>
        public IReadOnlyList<SimEvent> LastEvents => events;

        public SimulationEngine(EpisodeSetup setup, IFulfilmentPolicy policy, IDistanceModel distances = null)
        {
            Setup = setup ?? throw new ArgumentNullException(nameof(setup));
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));

            distances ??= new GridDistanceModel(setup.World, setup.Cost.kmPerCell);
            Regime = DemandRegimes.Create(setup.Regime, setup.Scenario.EpisodeTicks, setup.Seed);
            orderGenerator = new OrderGenerator(setup.World, setup.Scenario, setup.Demand, setup.Seed, Regime);
            StockPlan = new StockPlan(setup.World, setup.Scenario, setup.Demand, setup.Cost, setup.StockMultiplier);
            Inventory = StockPlan.CreateInitialInventory();
            Replenishment = new ReplenishmentSystem(StockPlan, setup.Scenario, setup.Seed);
            Queues = new CenterQueues(setup.World, setup.Cost);
            Costs = new CostModel(setup.World, distances, setup.Cost, StockPlan);
            candidates = new CandidateGenerator(setup.World, distances, setup.Cost);

            Policy.Reset(setup.Seed);
        }

        public OrderRecord RecordOf(int orderId) => recordsById.TryGetValue(orderId, out OrderRecord r) ? r : null;
        public Shipment ShipmentOf(int shipmentId) => shipmentsById.TryGetValue(shipmentId, out Shipment s) ? s : null;

        public void Step()
        {
            if (IsFinished)
                return;

            int tick = Tick;
            events.Clear();

            Replenishment.Step(tick, Inventory, events);

            if (tick < EpisodeTicks)
            {
                newOrders.Clear();
                orderGenerator.Generate(tick, newOrders);
                foreach (Order order in newOrders)
                    Decide(order, tick);
            }

            DeliverArrivals(tick);
            Tick++;
        }

        /// <summary>Runs until every parcel is delivered; returns the number of ticks simulated.</summary>
        public int RunToEnd()
        {
            int start = Tick;
            while (!IsFinished)
                Step();
            return Tick - start;
        }

        private void Decide(Order order, int tick)
        {
            List<FulfilmentPlan> plans = candidates.Generate(order, Inventory);
            var evaluations = new PlanEvaluation[plans.Count];
            for (int i = 0; i < plans.Count; i++)
                evaluations[i] = Costs.Evaluate(order, plans[i], Inventory, Queues, tick);

            var context = new DecisionContext(order, tick, plans, evaluations, World, Inventory, Queues, Costs);
            int chosen = Policy.Choose(context);
            if (chosen < 0 || chosen >= plans.Count)
                throw new InvalidOperationException($"{Policy.Name} chose plan {chosen} of {plans.Count} for order {order.Id}.");

            FulfilmentPlan plan = plans[chosen];
            PlanEvaluation evaluation = evaluations[chosen];
            var record = new OrderRecord(order, tick, plan, evaluation, plans.Count, chosen);
            records.Add(record);
            recordsById[order.Id] = record;
            events.Add(SimEvent.OrderCreated(tick, order.Id));
            events.Add(SimEvent.OrderDecided(tick, order.Id));

            foreach (Allocation a in plan.Allocations)
            {
                bool wasSafe = !Costs.IsBelowSafety(a.CenterId, a.SkuId, Inventory.OnHand(a.CenterId, a.SkuId));
                Inventory.Remove(a.CenterId, a.SkuId, a.Quantity);
                int after = Inventory.OnHand(a.CenterId, a.SkuId);
                if (wasSafe && Costs.IsBelowSafety(a.CenterId, a.SkuId, after))
                {
                    Kpis.RecordDepletion();
                    events.Add(SimEvent.LowStock(tick, a.CenterId, a.SkuId, after));
                }
            }

            Kpis.RecordDecision(record);

            if (evaluation.PackageCount == 0)
            {
                record.Status = OrderStatus.Lost;
                record.CompletionTick = tick;
                Kpis.RecordCompletion(record);
                events.Add(SimEvent.OrderLost(tick, order.Id, order.TotalUnits));
                Policy.Observe(Feedback(record));
                return;
            }

            record.Status = OrderStatus.Shipping;
            foreach (PackageEstimate package in evaluation.Packages)
            {
                double dispatch = Queues.Reserve(package.CenterId, tick);
                double arrival = dispatch + Costs.TravelMinutes(package.DistanceKm);
                OrderLine[] lines = plan.Allocations
                    .Where(a => a.CenterId == package.CenterId)
                    .Select(a => new OrderLine(a.SkuId, a.Quantity))
                    .ToArray();

                var shipment = new Shipment(nextShipmentId++, order.Id, package.CenterId, order.RegionId, lines,
                    (int)Math.Ceiling(dispatch), (int)Math.Ceiling(arrival));
                activeShipments.Add(shipment);
                shipmentsById[shipment.Id] = shipment;
                record.ShipmentIds.Add(shipment.Id);
                events.Add(SimEvent.ShipmentDispatched(tick, shipment));
            }
            record.OutstandingShipments = evaluation.PackageCount;
        }

        private void DeliverArrivals(int tick)
        {
            int kept = 0;
            for (int i = 0; i < activeShipments.Count; i++)
            {
                Shipment shipment = activeShipments[i];
                if (!shipment.Advance(tick))
                {
                    activeShipments[kept++] = shipment;
                    continue;
                }

                events.Add(SimEvent.ShipmentDelivered(tick, shipment));
                OrderRecord record = recordsById[shipment.OrderId];
                record.OutstandingShipments--;
                if (record.OutstandingShipments == 0)
                    Complete(record, tick);
            }
            activeShipments.RemoveRange(kept, activeShipments.Count - kept);
        }

        private void Complete(OrderRecord record, int tick)
        {
            record.Status = OrderStatus.Delivered;
            record.CompletionTick = tick;
            record.LateMinutes = Math.Max(0, tick - record.Order.PromisedTick);
            record.LatenessCost = Costs.LatenessCost(record.LateMinutes, record.Order.Priority);
            Kpis.RecordCompletion(record);
            events.Add(SimEvent.OrderCompleted(tick, record.Order.Id));
            Policy.Observe(Feedback(record));
        }

        private static DecisionFeedback Feedback(OrderRecord record) => new DecisionFeedback
        {
            Order = record.Order,
            DecisionTick = record.DecisionTick,
            CompletionTick = record.CompletionTick,
            Plan = record.Plan,
            Evaluation = record.Evaluation,
            LateMinutes = record.LateMinutes,
            LatenessCost = record.LatenessCost,
            RealisedProfit = record.RealisedProfit
        };
    }
}
