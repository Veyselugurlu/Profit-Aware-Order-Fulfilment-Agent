using System.Collections.Generic;

namespace Fulfilment.Simulation
{
    public enum OrderStatus
    {
        /// <summary>Parcels are being packed or are on the road.</summary>
        Shipping,
        /// <summary>Every parcel arrived.</summary>
        Delivered,
        /// <summary>No unit could be shipped.</summary>
        Lost
    }

    /// <summary>Everything that happened to one order; one row of the episode log.</summary>
    public sealed class OrderRecord
    {
        public Order Order { get; }
        public int DecisionTick { get; }
        public FulfilmentPlan Plan { get; }
        public PlanEvaluation Evaluation { get; }
        public int CandidateCount { get; }
        public int ChosenIndex { get; }
        public List<int> ShipmentIds { get; } = new List<int>();

        public OrderStatus Status { get; internal set; }
        public int OutstandingShipments { get; internal set; }
        public int CompletionTick { get; internal set; } = -1;
        public double LateMinutes { get; internal set; }
        public double LatenessCost { get; internal set; }

        /// <summary>Revenue − cost of goods − shipping − split − stock-out penalty − lateness (opportunity cost excluded).</summary>
        public double RealisedProfit =>
            Evaluation.Revenue - Evaluation.Cogs - Evaluation.ShippingCost - Evaluation.SplitCost
            - Evaluation.StockoutPenalty - LatenessCost;

        public bool IsComplete => Status != OrderStatus.Shipping;
        public bool IsLate => LateMinutes > 0.0;

        public OrderRecord(Order order, int decisionTick, FulfilmentPlan plan, PlanEvaluation evaluation, int candidateCount, int chosenIndex)
        {
            Order = order;
            DecisionTick = decisionTick;
            Plan = plan;
            Evaluation = evaluation;
            CandidateCount = candidateCount;
            ChosenIndex = chosenIndex;
        }
    }
}
