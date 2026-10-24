namespace Fulfilment.Simulation
{
    /// <summary>
    /// Time-varying multiplier on the arrival rate (stable, seasonal, volatile ... regimes).
    /// Must be deterministic for a given tick so that the order stream of a seed never changes.
    /// </summary>
    public interface IDemandRegime
    {
        string Code { get; }
        double RateMultiplier(int tick);
    }

    public sealed class StableRegime : IDemandRegime
    {
        public string Code => "stable";
        public double RateMultiplier(int tick) => 1.0;
    }
}
