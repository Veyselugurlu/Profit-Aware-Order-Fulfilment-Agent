using System.Collections.Generic;
using Fulfilment.Simulation;
using UnityEngine;

namespace Fulfilment.Presentation
{
    /// <summary>
    /// Short-lived visual feedback for simulation events: a pulse where an order appears, a green or orange
    /// ring when it is delivered on time or late, a red cross when it is lost and an amber ring when a
    /// center drops below safety stock. Effects age in real time so they stay readable at high speed.
    /// </summary>
    public sealed class EffectsLayer : MonoBehaviour
    {
        private const int MaxEffects = 400;

        [SerializeField] private SimulationRunner runner;
        [SerializeField] private MapView map;

        private sealed class Effect
        {
            public SpriteRenderer Renderer;
            public float Age;
            public float Duration;
            public float StartScale;
            public float EndScale;
            public Color Colour;
        }

        private readonly List<Effect> live = new List<Effect>();
        private readonly Stack<SpriteRenderer> freeRings = new Stack<SpriteRenderer>();
        private readonly Stack<SpriteRenderer> freeCrosses = new Stack<SpriteRenderer>();

        private void OnEnable()
        {
            if (runner == null) return;
            runner.Stepped += OnStepped;
            runner.EpisodeStarted += OnEpisodeStarted;
        }

        private void OnDisable()
        {
            if (runner == null) return;
            runner.Stepped -= OnStepped;
            runner.EpisodeStarted -= OnEpisodeStarted;
        }

        private void OnEpisodeStarted(SimulationEngine engine)
        {
            for (int i = live.Count - 1; i >= 0; i--) Release(i);
        }

        private void OnStepped(IReadOnlyList<SimEvent> events)
        {
            SimulationEngine engine = runner.Engine;
            foreach (SimEvent e in events)
            {
                switch (e.Type)
                {
                    case SimEventType.OrderCreated:
                        Spawn(RegionPosition(engine, e.OrderId), IconShape.Ring, Palette.WithAlpha(Palette.Neutral, 0.55f), 0.6f, 0.8f, 2.0f);
                        break;
                    case SimEventType.OrderCompleted:
                        OrderRecord done = engine.RecordOf(e.OrderId);
                        Spawn(RegionPosition(engine, e.OrderId), IconShape.Ring, done != null && done.IsLate ? Palette.Warning : Palette.Ok, 0.7f, 0.8f, 1.8f);
                        break;
                    case SimEventType.OrderLost:
                        Spawn(RegionPosition(engine, e.OrderId), IconShape.Cross, Palette.Critical, 1.2f, 1.6f, 1.2f);
                        break;
                    case SimEventType.LowStock:
                        Spawn(MapGeometry.CellCenter(engine.World.Centers[e.CenterId].Cell), IconShape.Ring, Palette.Warning, 0.9f, 2.0f, 4.0f);
                        break;
                }
            }
        }

        private static Vector3 RegionPosition(SimulationEngine engine, int orderId)
        {
            OrderRecord record = engine.RecordOf(orderId);
            return record == null ? Vector3.zero : MapGeometry.CellCenter(engine.World.Regions[record.Order.RegionId].Cell);
        }

        private void Spawn(Vector3 position, IconShape shape, Color colour, float duration, float startScale, float endScale)
        {
            if (live.Count >= MaxEffects) return;
            Stack<SpriteRenderer> free = shape == IconShape.Cross ? freeCrosses : freeRings;
            SpriteRenderer sr = free.Count > 0 ? free.Pop() : Create(shape);
            sr.gameObject.SetActive(true);
            sr.transform.localPosition = position;
            live.Add(new Effect { Renderer = sr, Duration = duration, StartScale = startScale, EndScale = endScale, Colour = colour });
        }

        private SpriteRenderer Create(IconShape shape)
        {
            var go = new GameObject(shape.ToString());
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Get(shape);
            sr.sortingOrder = shape == IconShape.Cross ? 16 : 15;
            if (map != null && map.SpriteMaterial != null) sr.sharedMaterial = map.SpriteMaterial;
            return sr;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                Effect e = live[i];
                e.Age += dt;
                float t = e.Age / e.Duration;
                if (t >= 1f)
                {
                    Release(i);
                    continue;
                }
                e.Renderer.transform.localScale = Vector3.one * Mathf.Lerp(e.StartScale, e.EndScale, t);
                e.Renderer.color = Palette.WithAlpha(e.Colour, (1f - t) * 0.9f * e.Colour.a);
            }
        }

        private void Release(int index)
        {
            SpriteRenderer sr = live[index].Renderer;
            sr.gameObject.SetActive(false);
            (sr.sprite == SpriteFactory.Get(IconShape.Cross) ? freeCrosses : freeRings).Push(sr);
            live.RemoveAt(index);
        }
    }
}
