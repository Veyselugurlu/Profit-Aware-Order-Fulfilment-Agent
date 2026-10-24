using System;
using System.Collections.Generic;

namespace Fulfilment.Simulation
{
    /// <summary>Smooth demand wave across days: 1 + amplitude · sin(2π (day + phase) / period).</summary>
    public sealed class SeasonalRegime : IDemandRegime
    {
        private readonly double amplitude;
        private readonly double periodTicks;
        private readonly double phaseTicks;

        public string Code { get; }

        public SeasonalRegime(string code, double amplitude, double periodDays, double phaseDays)
        {
            Code = code;
            this.amplitude = amplitude;
            periodTicks = periodDays * SimTime.TicksPerDay;
            phaseTicks = phaseDays * SimTime.TicksPerDay;
        }

        public double RateMultiplier(int tick) =>
            1.0 + amplitude * Math.Sin(2.0 * Math.PI * (tick + phaseTicks) / periodTicks);
    }

    /// <summary>
    /// Random demand shocks (campaigns, viral products): their count, start, length and height are drawn
    /// once per episode from the Regime stream, so the shock schedule depends only on the seed.
    /// </summary>
    public sealed class VolatileRegime : IDemandRegime
    {
        public readonly struct Shock
        {
            public readonly int StartTick;
            public readonly int EndTick;
            public readonly double Multiplier;

            public Shock(int startTick, int endTick, double multiplier)
            {
                StartTick = startTick;
                EndTick = endTick;
                Multiplier = multiplier;
            }
        }

        private readonly List<Shock> shocks = new List<Shock>();

        public string Code { get; }
        public IReadOnlyList<Shock> Shocks => shocks;

        public VolatileRegime(RegimeConfig config, int episodeTicks, int seed)
        {
            Code = config.code;
            var rng = new SeededRng(seed, RngStream.Regime);
            double days = episodeTicks / (double)SimTime.TicksPerDay;
            int count = rng.NextPoisson(config.shocksPerDay * days);
            for (int i = 0; i < count; i++)
            {
                int start = rng.NextInt(0, episodeTicks);
                int length = SimTime.FromHours(rng.Range(config.shockMinHours, config.shockMaxHours));
                double height = rng.Range(config.shockMinMultiplier, config.shockMaxMultiplier);
                shocks.Add(new Shock(start, start + length, height));
            }
        }

        /// <summary>Overlapping shocks do not stack; the highest one applies.</summary>
        public double RateMultiplier(int tick)
        {
            double multiplier = 1.0;
            for (int i = 0; i < shocks.Count; i++)
                if (tick >= shocks[i].StartTick && tick < shocks[i].EndTick)
                    multiplier = Math.Max(multiplier, shocks[i].Multiplier);
            return multiplier;
        }
    }

    public static class DemandRegimes
    {
        public static IDemandRegime Create(RegimeConfig config, int episodeTicks, int seed)
        {
            if (config == null)
                return new StableRegime();

            List<string> errors = ConfigValidator.Validate(config);
            if (errors.Count > 0)
                throw new ArgumentException("Invalid regime config:\n- " + string.Join("\n- ", errors));

            switch (config.type)
            {
                case "seasonal":
                    return new SeasonalRegime(config.code, config.amplitude, config.periodDays, config.phaseDays);
                case "volatile":
                    return new VolatileRegime(config, episodeTicks, seed);
                default:
                    return new StableRegime();
            }
        }
    }
}
