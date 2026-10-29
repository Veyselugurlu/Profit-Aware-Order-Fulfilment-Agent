using Fulfilment.Simulation;

namespace Fulfilment.AI
{
    /// <summary>Fixed-weight Utility AI: always uses the weights of its config.</summary>
    public sealed class UtilityPolicy : IFulfilmentPolicy, IExplainablePolicy
    {
        private readonly UtilityScorer scorer;

        public string Name { get; }
        public DecisionTrace LastTrace { get; private set; }

        public UtilityPolicy(UtilityConfig config, string name = "Utility")
        {
            scorer = new UtilityScorer(config);
            Name = name;
        }

        public void Reset(int seed) => LastTrace = null;

        public int Choose(DecisionContext context)
        {
            LastTrace = scorer.Score(context, policyName: Name);
            return LastTrace.Chosen;
        }

        public void Observe(DecisionFeedback feedback) { }
    }
}
