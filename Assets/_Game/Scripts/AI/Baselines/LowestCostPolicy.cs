using Fulfilment.Simulation;

namespace Fulfilment.AI
{
    /// <summary>
    /// Baseline 1b (brief: "lowest shipping cost"). Among the plans that lose the fewest units, take the one
    /// with the lowest shipping cost including the split penalty. Delivery time, stock levels and the
    /// future value of stock are ignored, which is exactly the myopia the adaptive agent should overcome.
    /// </summary>
    public sealed class LowestCostPolicy : IFulfilmentPolicy, IExplainablePolicy
    {
        public const string RuleText = "Önce en az ürün kaybettiren planlar; aralarından kargo + split maliyeti en düşük olan.";

        public string Name => "LowestCost";
        public DecisionTrace LastTrace { get; private set; }

        public void Reset(int seed) => LastTrace = null;

        public int Choose(DecisionContext context)
        {
            int best = 0;
            for (int i = 1; i < context.Candidates.Count; i++)
            {
                PlanEvaluation e = context.Evaluations[i], b = context.Evaluations[best];
                if (e.LostUnits != b.LostUnits)
                {
                    if (e.LostUnits < b.LostUnits) best = i;
                    continue;
                }
                if (e.ShippingCost + e.SplitCost < b.ShippingCost + b.SplitCost)
                    best = i;
            }
            LastTrace = new DecisionTrace { Policy = Name, Rule = RuleText, Chosen = best };
            return best;
        }

        public void Observe(DecisionFeedback feedback) { }
    }
}
