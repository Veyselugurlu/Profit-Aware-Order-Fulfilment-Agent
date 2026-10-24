using System;
using System.Collections.Generic;

namespace Fulfilment.Simulation
{
    /// <summary>
    /// Produces the order stream of an episode. Arrivals are Poisson per tick with rate
    /// λ(t) = ordersPerDay / ticksPerDay × hourProfile(t) × regime(t). Region, basket size,
    /// SKUs and quantities are drawn from the demand profile. All draws use the Orders stream only,
    /// so the same seed gives the same orders whatever policy is running.
    /// </summary>
    public sealed class OrderGenerator
    {
        private const int MaxSkuDrawAttempts = 64;

        private readonly World world;
        private readonly ScenarioConfig scenario;
        private readonly IDemandRegime regime;
        private readonly SeededRng rng;

        private readonly double[] hourFactor = new double[24];
        private readonly double baseRatePerTick;
        private readonly DiscreteSampler regionSampler;
        private readonly DiscreteSampler skuSampler;
        private readonly DiscreteSampler basketSampler;
        private readonly DiscreteSampler quantitySampler;
        private readonly int promisedStandardTicks;
        private readonly int promisedExpressTicks;
        private readonly HashSet<int> basketSkus = new HashSet<int>();

        private int nextOrderId;

        public int Seed => rng.Seed;
        public int OrdersCreated => nextOrderId;

        public OrderGenerator(World world, ScenarioConfig scenario, DemandProfileConfig demand, int seed, IDemandRegime regime = null)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            if (demand == null) throw new ArgumentNullException(nameof(demand));

            List<string> errors = ConfigValidator.Validate(demand, world.Skus.Count);
            if (errors.Count > 0)
                throw new ArgumentException("Invalid demand profile:\n- " + string.Join("\n- ", errors));

            this.regime = regime ?? new StableRegime();
            rng = new SeededRng(seed, RngStream.Orders);

            double hourMean = 0.0;
            for (int h = 0; h < 24; h++) hourMean += demand.hourWeights[h];
            hourMean /= 24.0;
            for (int h = 0; h < 24; h++) hourFactor[h] = demand.hourWeights[h] / hourMean;

            baseRatePerTick = scenario.ordersPerDay / SimTime.TicksPerDay;

            var regionWeights = new double[world.Regions.Count];
            for (int i = 0; i < regionWeights.Length; i++) regionWeights[i] = world.Regions[i].DemandWeight;
            regionSampler = new DiscreteSampler(regionWeights);

            bool hasSkuWeights = demand.skuWeights != null && demand.skuWeights.Length > 0;
            skuSampler = new DiscreteSampler(hasSkuWeights
                ? demand.skuWeights
                : DiscreteSampler.ZipfWeights(world.Skus.Count, demand.zipfExponent));

            basketSampler = new DiscreteSampler(demand.basketSizeWeights);
            quantitySampler = new DiscreteSampler(demand.quantityWeights);

            promisedStandardTicks = SimTime.FromHours(scenario.promisedHoursStandard);
            promisedExpressTicks = SimTime.FromHours(scenario.promisedHoursExpress);
        }

        /// <summary>Expected arrivals in one tick, before Poisson noise.</summary>
        public double ArrivalRate(int tick) =>
            baseRatePerTick * hourFactor[SimTime.HourOfDay(tick)] * regime.RateMultiplier(tick);

        /// <summary>Appends the orders that arrive during <paramref name="tick"/>.</summary>
        public void Generate(int tick, List<Order> output)
        {
            int arrivals = rng.NextPoisson(ArrivalRate(tick));
            for (int i = 0; i < arrivals; i++)
                output.Add(CreateOrder(tick));
        }

        /// <summary>The whole episode at once (tests, experiments and replays).</summary>
        public List<Order> GenerateEpisode()
        {
            var orders = new List<Order>((int)(scenario.ExpectedOrders * 1.2));
            for (int tick = 0; tick < scenario.EpisodeTicks; tick++)
                Generate(tick, orders);
            return orders;
        }

        private Order CreateOrder(int tick)
        {
            int regionId = regionSampler.Sample(rng);
            bool express = rng.Chance(scenario.expressShare);
            int basketSize = Math.Min(basketSampler.Sample(rng) + 1, world.Skus.Count);

            basketSkus.Clear();
            var lines = new OrderLine[basketSize];
            for (int i = 0; i < basketSize; i++)
            {
                int skuId = DrawDistinctSku();
                int quantity = quantitySampler.Sample(rng) + 1;
                lines[i] = new OrderLine(skuId, quantity);
            }

            OrderPriority priority = express ? OrderPriority.Express : OrderPriority.Standard;
            int promised = tick + (express ? promisedExpressTicks : promisedStandardTicks);
            return new Order(nextOrderId++, tick, regionId, priority, promised, lines);
        }

        private int DrawDistinctSku()
        {
            for (int attempt = 0; attempt < MaxSkuDrawAttempts; attempt++)
            {
                int skuId = skuSampler.Sample(rng);
                if (basketSkus.Add(skuId))
                    return skuId;
            }

            // Very unlikely with 30 SKUs and baskets of 5; keeps the draw count bounded.
            for (int skuId = 0; skuId < world.Skus.Count; skuId++)
                if (basketSkus.Add(skuId))
                    return skuId;

            throw new InvalidOperationException("Basket is larger than the SKU catalogue.");
        }
    }
}
