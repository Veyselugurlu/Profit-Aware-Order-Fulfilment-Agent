using System;
using Fulfilment.Presentation;
using Fulfilment.Simulation;
using NUnit.Framework;

namespace Fulfilment.Tests
{
    /// <summary>Loads the shipped configs once per fixture and offers small helpers for hand-made cases.</summary>
    public abstract class SimulationTestBase
    {
        protected ConfigSet Configs;
        protected World World;

        [OneTimeSetUp]
        public void LoadConfigs()
        {
            Configs = ConfigLoader.LoadDefault();
            World = Configs.BuildWorld();
        }

        protected EpisodeSetup Setup(string scenario = "medium", string regime = "stable", int seed = 1001) =>
            Configs.Setup(scenario, regime, seed);

        protected Inventory EmptyInventory() => new Inventory(World.Centers.Count, World.Skus.Count);

        protected static Order MakeOrder(int regionId, params (int sku, int qty)[] lines) =>
            MakeOrder(regionId, OrderPriority.Standard, 0, 480, lines);

        protected static Order MakeOrder(int regionId, OrderPriority priority, int tick, int promisedTick, params (int sku, int qty)[] lines) =>
            new Order(0, tick, regionId, priority, promisedTick, Array.ConvertAll(lines, l => new OrderLine(l.sku, l.qty)));

        protected CostModel Costs(EpisodeSetup setup = null)
        {
            setup ??= Setup();
            var plan = new StockPlan(World, setup.Scenario, setup.Demand, setup.Cost);
            return new CostModel(World, new GridDistanceModel(World, setup.Cost.kmPerCell), setup.Cost, plan);
        }

        protected CandidateGenerator Candidates() =>
            new CandidateGenerator(World, new GridDistanceModel(World, Configs.Cost.kmPerCell), Configs.Cost);

        protected int RegionNamed(string code) => Array.FindIndex(ToArray(World.Regions), r => r.Code == code);

        private static T[] ToArray<T>(System.Collections.Generic.IReadOnlyList<T> list)
        {
            var array = new T[list.Count];
            for (int i = 0; i < array.Length; i++) array[i] = list[i];
            return array;
        }
    }
}
