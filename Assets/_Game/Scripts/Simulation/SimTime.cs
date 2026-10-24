namespace Fulfilment.Simulation
{
    /// <summary>
    /// Simulation clock units. One tick is one simulated minute; the clock is independent of the
    /// render frame rate (the same idea as FixedUpdate running on a fixed timestep).
    /// </summary>
    public static class SimTime
    {
        public const int TicksPerHour = 60;
        public const int TicksPerDay = 24 * TicksPerHour;

        public static int HourOfDay(int tick) => (tick / TicksPerHour) % 24;

        public static int Day(int tick) => tick / TicksPerDay;

        public static int FromHours(double hours) => (int)System.Math.Round(hours * TicksPerHour);

        /// <summary>"1g 07:42" style label used by the UI and logs.</summary>
        public static string Format(int tick)
        {
            int minuteOfDay = tick % TicksPerDay;
            return $"{Day(tick) + 1}g {minuteOfDay / TicksPerHour:00}:{minuteOfDay % TicksPerHour:00}";
        }
    }
}
