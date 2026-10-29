using System;
using System.Collections.Generic;
using Fulfilment.Simulation;
using UnityEngine;

namespace Fulfilment.Presentation
{
    /// <summary>
    /// Drives the simulation engine from the Unity frame loop. Rendering and simulation are decoupled the same
    /// way FixedUpdate is decoupled from Update (Week 3): every frame adds Time.deltaTime × speed × rate to an
    /// accumulator, and the engine steps once per whole simulated minute in the accumulator. The fractional rest
    /// gives <see cref="SmoothTick"/> so parcels move smoothly between ticks.
    /// </summary>
    public sealed class SimulationRunner : MonoBehaviour
    {
        public static readonly float[] Speeds = { 1f, 2f, 4f, 8f };

        [SerializeField] private float simMinutesPerSecond = 12f;
        [SerializeField] private int maxStepsPerFrame = 240;
        [SerializeField] private string scenario = "medium";
        [SerializeField] private string regime = "stable";
        [SerializeField] private string policy = "Nearest";
        [SerializeField] private int seed = 1;
        [SerializeField] private float stockMultiplier = 1f;
        [SerializeField] private float splitPenalty = -1f; // < 0: use cost.json

        private double accumulator;

        public ConfigSet Configs { get; private set; }
        public SimulationEngine Engine { get; private set; }
        public bool IsRunning { get; private set; }
        public float Speed { get; private set; } = 1f;

        public string Scenario => scenario;
        public string Regime => regime;
        public string Policy => policy;
        public int Seed => seed;
        public float StockMultiplier => stockMultiplier;

        /// <summary>Split penalty in use (the cost.json value unless overridden).</summary>
        public float SplitPenalty => splitPenalty >= 0f ? splitPenalty : (float)(Configs?.Cost.splitPenaltyPerExtraPackage ?? 0);

        /// <summary>Simulated time including the fraction since the last tick, for smooth animation.</summary>
        public double SmoothTick => Engine == null ? 0.0 : Engine.Tick + (IsRunning ? Math.Min(accumulator, 1.0) : 0.0);

        /// <summary>Raised after an episode is (re)built: subscribers rebuild their views.</summary>
        public event Action<SimulationEngine> EpisodeStarted;

        /// <summary>Raised after each engine step with the events of that tick.</summary>
        public event Action<IReadOnlyList<SimEvent>> Stepped;

        /// <summary>Raised when the last parcel of the episode has been delivered.</summary>
        public event Action<SimulationEngine> EpisodeFinished;

        private void Awake()
        {
            Configs = ConfigLoader.LoadDefault();
            List<string> errors = Configs.Validate();
            if (errors.Count > 0)
                Debug.LogError("Config errors:\n" + string.Join("\n", errors));
        }

        private void Start() => ResetEpisode();

        public void Configure(string scenarioCode, string regimeCode, string policyName, int seedValue)
        {
            scenario = scenarioCode;
            regime = regimeCode;
            policy = policyName;
            seed = seedValue;
            ResetEpisode();
        }

        /// <summary>Scenario-panel parameters; applied on the next reset.</summary>
        public void SetParameters(float stockLevel, float splitPenaltyValue)
        {
            stockMultiplier = Mathf.Clamp(stockLevel, 0.2f, 3f);
            splitPenalty = Mathf.Max(0f, splitPenaltyValue);
        }

        public void ResetEpisode()
        {
            IsRunning = false;
            accumulator = 0.0;
            EpisodeSetup setup = Configs.Setup(scenario, regime, seed, stockMultiplier, splitPenalty >= 0f ? splitPenalty : (double?)null);
            Engine = new SimulationEngine(setup, Configs.CreatePolicy(policy));
            EpisodeStarted?.Invoke(Engine);
        }

        public void Play()
        {
            if (Engine == null || Engine.IsFinished) return;
            IsRunning = true;
        }

        public void Pause() => IsRunning = false;

        public void TogglePause()
        {
            if (IsRunning) Pause();
            else Play();
        }

        /// <summary>Advances exactly one simulated minute; pauses first so the step is visible.</summary>
        public void StepOnce()
        {
            Pause();
            accumulator = 0.0;
            Advance();
        }

        public void SetSpeed(float speed) => Speed = Mathf.Max(0.1f, speed);

        private void Update()
        {
            if (!IsRunning || Engine == null)
                return;

            accumulator += Time.deltaTime * Speed * simMinutesPerSecond;
            int steps = 0;
            while (accumulator >= 1.0 && steps < maxStepsPerFrame && IsRunning)
            {
                accumulator -= 1.0;
                Advance();
                steps++;
            }

            // Never let a slow frame build an endless backlog of ticks.
            if (accumulator > maxStepsPerFrame)
                accumulator = maxStepsPerFrame;
        }

        private void Advance()
        {
            if (Engine.IsFinished)
                return;

            Engine.Step();
            Stepped?.Invoke(Engine.LastEvents);

            if (Engine.IsFinished)
            {
                IsRunning = false;
                accumulator = 0.0;
                EpisodeFinished?.Invoke(Engine);
            }
        }
    }
}
