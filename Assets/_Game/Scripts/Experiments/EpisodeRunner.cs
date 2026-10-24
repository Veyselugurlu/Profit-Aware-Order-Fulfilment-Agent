using System.Collections.Generic;
using System.Diagnostics;
using Fulfilment.Simulation;

namespace Fulfilment.Experiments
{
    public sealed class EpisodeResult
    {
        public string Policy;
        public string Scenario;
        public string Regime;
        public int Seed;
        public KpiTracker Kpis;
        public IReadOnlyList<OrderRecord> Records;
        public World World;
        public int TicksSimulated;
        public double WallSeconds;
    }

    /// <summary>Runs one episode headless with the same engine the live scene uses.</summary>
    public static class EpisodeRunner
    {
        public static EpisodeResult Run(EpisodeSetup setup, IFulfilmentPolicy policy)
        {
            var watch = Stopwatch.StartNew();
            var engine = new SimulationEngine(setup, policy);
            int ticks = engine.RunToEnd();
            watch.Stop();

            return new EpisodeResult
            {
                Policy = policy.Name,
                Scenario = setup.Scenario.code,
                Regime = setup.RegimeCode,
                Seed = setup.Seed,
                Kpis = engine.Kpis,
                Records = engine.Records,
                World = setup.World,
                TicksSimulated = ticks,
                WallSeconds = watch.Elapsed.TotalSeconds
            };
        }
    }
}
