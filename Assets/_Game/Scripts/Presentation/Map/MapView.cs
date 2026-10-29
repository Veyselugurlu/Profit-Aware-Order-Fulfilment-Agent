using System.Collections.Generic;
using Fulfilment.Simulation;
using UnityEngine;

namespace Fulfilment.Presentation
{
    /// <summary>Builds the ground, centers and regions for the current world and keeps center states up to date.</summary>
    public sealed class MapView : MonoBehaviour
    {
        [SerializeField] private SimulationRunner runner;
        [SerializeField] private Material spriteMaterial;

        private readonly List<CenterView> centers = new List<CenterView>();
        private readonly List<RegionView> regions = new List<RegionView>();
        private readonly Dictionary<(int, int), Vector3[]> routePoints = new Dictionary<(int, int), Vector3[]>();
        private World builtFor;
        private SpriteRenderer ground;

        public IReadOnlyList<CenterView> Centers => centers;
        public IReadOnlyList<RegionView> Regions => regions;
        public Material SpriteMaterial => spriteMaterial;
        public Vector2 Size { get; private set; }

        private void OnEnable()
        {
            if (runner == null) return;
            runner.EpisodeStarted += Rebuild;
            runner.Stepped += OnStepped;
            if (runner.Engine != null) Rebuild(runner.Engine);
        }

        private void OnDisable()
        {
            if (runner == null) return;
            runner.EpisodeStarted -= Rebuild;
            runner.Stepped -= OnStepped;
        }

        public Vector3[] Route(int centerId, int regionId)
        {
            if (!routePoints.TryGetValue((centerId, regionId), out Vector3[] points))
            {
                points = MapGeometry.PathPoints(runner.Engine.World.Routes.Path(centerId, regionId));
                routePoints[(centerId, regionId)] = points;
            }
            return points;
        }

        private void Rebuild(SimulationEngine engine)
        {
            if (builtFor != engine.World)
            {
                Clear();
                Build(engine.World);
                builtFor = engine.World;
            }
            foreach (CenterView c in centers) c.Refresh(engine);
        }

        private void Build(World world)
        {
            Size = new Vector2(world.GridWidth, world.GridHeight);

            var groundGo = new GameObject("Ground");
            groundGo.transform.SetParent(transform, false);
            ground = groundGo.AddComponent<SpriteRenderer>();
            ground.sprite = SpriteFactory.Ground(world.Roads);
            ground.sortingOrder = 0;
            if (spriteMaterial != null) ground.sharedMaterial = spriteMaterial;

            foreach (FulfilmentCenter center in world.Centers)
            {
                var go = new GameObject(center.Code);
                go.transform.SetParent(transform, false);
                var view = go.AddComponent<CenterView>();
                view.Initialize(center, spriteMaterial);
                centers.Add(view);
            }

            foreach (CustomerRegion region in world.Regions)
            {
                var go = new GameObject(region.Code);
                go.transform.SetParent(transform, false);
                var view = go.AddComponent<RegionView>();
                view.Initialize(region, spriteMaterial);
                regions.Add(view);
            }
        }

        private void Clear()
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);
            centers.Clear();
            regions.Clear();
            routePoints.Clear();
        }

        private void OnStepped(IReadOnlyList<SimEvent> events)
        {
            // Stock and queue change with almost every order; the views are cheap to refresh.
            foreach (CenterView c in centers) c.Refresh(runner.Engine);
        }
    }
}
