using System.Collections.Generic;

namespace Fulfilment.Simulation
{
    /// <summary>What a policy sees when an order arrives.</summary>
    public sealed class DecisionContext
    {
        public Order Order { get; }
        public int Tick { get; }
        public IReadOnlyList<FulfilmentPlan> Candidates { get; }

        /// <summary>Evaluation of each candidate, same index as <see cref="Candidates"/>.</summary>
        public IReadOnlyList<PlanEvaluation> Evaluations { get; }

        public World World { get; }

        /// <summary>Current stock; policies must treat it as read-only.</summary>
        public Inventory Inventory { get; }

        public CenterQueues Queues { get; }
        public CostModel Costs { get; }

        public DecisionContext(Order order, int tick, IReadOnlyList<FulfilmentPlan> candidates, IReadOnlyList<PlanEvaluation> evaluations,
            World world, Inventory inventory, CenterQueues queues, CostModel costs)
        {
            Order = order;
            Tick = tick;
            Candidates = candidates;
            Evaluations = evaluations;
            World = world;
            Inventory = inventory;
            Queues = queues;
            Costs = costs;
        }
    }

    /// <summary>Outcome of a decision, reported when the last parcel of the order is delivered (or at once if nothing ships).</summary>
    public sealed class DecisionFeedback
    {
        public Order Order;
        public int DecisionTick;
        public int CompletionTick;
        public FulfilmentPlan Plan;
        public PlanEvaluation Evaluation;
        public double LateMinutes;
        public double LatenessCost;
        public double RealisedProfit;
    }

    /// <summary>
    /// A fulfilment policy: observes the decision context and picks one candidate plan. Implementations live
    /// in the AI assembly; the interface is here because the simulation engine calls it.
    /// </summary>
    public interface IFulfilmentPolicy
    {
        string Name { get; }

        /// <summary>Called at the start of every episode.</summary>
        void Reset(int seed);

        /// <summary>Index of the chosen plan in <see cref="DecisionContext.Candidates"/>.</summary>
        int Choose(DecisionContext context);

        /// <summary>Delayed outcome of an earlier decision; non-learning policies ignore it.</summary>
        void Observe(DecisionFeedback feedback);
    }
}
