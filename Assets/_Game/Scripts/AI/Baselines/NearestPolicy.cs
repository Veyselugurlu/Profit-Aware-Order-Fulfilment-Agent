using Fulfilment.Simulation;

namespace Fulfilment.AI
{
    /// <summary>
    /// Baseline 1a (brief: "nearest fulfilment point"). Ship from the nearest center that holds the whole
    /// order; when no single center can, take the plan that loses the fewest units, then the shortest
    /// average distance. Stock levels, queues, cost and lateness are ignored.
    /// </summary>
    public sealed class NearestPolicy : IFulfilmentPolicy, IExplainablePolicy
    {
        public const string RuleText = "Önce en az ürün kaybettiren planlar; sonra tek depodan gönderim; sonra en kısa yol mesafesi.";

        public string Name => "Nearest";
        public DecisionTrace LastTrace { get; private set; }

        public void Reset(int seed) => LastTrace = null;

        public int Choose(DecisionContext context)
        {
            int best = 0;
            for (int i = 1; i < context.Candidates.Count; i++)
                if (IsBetter(context, i, best))
                    best = i;
            LastTrace = new DecisionTrace { Policy = Name, Rule = RuleText, Chosen = best };
            return best;
        }

        private static bool IsBetter(DecisionContext context, int a, int b)
        {
            PlanEvaluation ea = context.Evaluations[a], eb = context.Evaluations[b];
            if (ea.LostUnits != eb.LostUnits) return ea.LostUnits < eb.LostUnits;

            bool singleA = context.Candidates[a].Kind == PlanKind.SingleCenter;
            bool singleB = context.Candidates[b].Kind == PlanKind.SingleCenter;
            if (singleA != singleB) return singleA;

            return ea.AverageKm < eb.AverageKm;
        }

        public void Observe(DecisionFeedback feedback) { }
    }
}
