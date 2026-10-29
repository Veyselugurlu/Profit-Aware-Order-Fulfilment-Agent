using System.Collections.Generic;
using System.Globalization;
using Fulfilment.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Fulfilment.Presentation
{
    /// <summary>
    /// Runtime HUD built with UI Toolkit: control bar (play/pause, step, reset, speed), clock and episode info,
    /// live KPI strip, inspection panel of the selected entity and map labels. Layout is created in code and
    /// styled by Hud.uss.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HudController : MonoBehaviour
    {
        private const float RefreshInterval = 0.1f;
        private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

        [SerializeField] private SimulationRunner runner;
        [SerializeField] private SelectionController selection;
        [SerializeField] private CameraController cameraController;
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private MapView map;
        [SerializeField] private StyleSheet styleSheet;

        private VisualElement root;
        private VisualElement labelLayer;
        private Button playButton;
        private readonly List<Button> speedButtons = new List<Button>();
        private Label clockLabel;
        private Label episodeLabel;
        private Label statusLabel;
        private VisualElement progressFill;
        private readonly Dictionary<string, Label> kpis = new Dictionary<string, Label>();
        private DecisionPanel decisionPanel;
        private SidePanel sidePanel;
        private Sparkline trend;
        private int trendService;
        private int trendStockout;
        private bool cardsDirty;
        private readonly List<(Label label, Transform target, Vector3 offset)> mapLabels = new List<(Label, Transform, Vector3)>();
        private float refreshTimer;
        private bool labelsDirty;

        private void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            if (styleSheet != null) root.styleSheets.Add(styleSheet);
            root.AddToClassList("hud-root");
            root.pickingMode = PickingMode.Ignore;

            labelLayer = new VisualElement { name = "map-labels", pickingMode = PickingMode.Ignore };
            labelLayer.AddToClassList("label-layer");
            root.Add(labelLayer);

            BuildTopBar();
            BuildKpiStrip();
            decisionPanel = new DecisionPanel();
            root.Add(decisionPanel.Root);

            if (runner != null)
            {
                runner.EpisodeStarted += OnEpisodeStarted;
                runner.EpisodeFinished += OnEpisodeFinished;
                runner.Stepped += OnStepped;
                if (runner.Engine != null) OnEpisodeStarted(runner.Engine);
            }
        }

        private void OnDisable()
        {
            if (runner == null) return;
            runner.EpisodeStarted -= OnEpisodeStarted;
            runner.EpisodeFinished -= OnEpisodeFinished;
            runner.Stepped -= OnStepped;
        }

        /// <summary>The scenario panel reads the loaded configs, so it is built once the runner has them.</summary>
        private void EnsureSidePanel()
        {
            if (sidePanel != null || runner == null || runner.Configs == null) return;
            sidePanel = new SidePanel(runner);
            root.Add(sidePanel.Root);
        }

        // ---------------------------------------------------------------- layout

        private void BuildTopBar()
        {
            var bar = Panel("top-bar");
            root.Add(bar);

            var title = new Label("Profit-Aware Order Fulfilment");
            title.AddToClassList("title");
            bar.Add(title);

            playButton = ToolButton("▶ Başlat", () => runner.TogglePause());
            playButton.AddToClassList("primary");
            bar.Add(playButton);
            bar.Add(ToolButton("⏭ Adım", () => runner.StepOnce()));
            bar.Add(ToolButton("Sıfırla", () => runner.ResetEpisode()));

            var speeds = new VisualElement();
            speeds.AddToClassList("speed-group");
            foreach (float s in SimulationRunner.Speeds)
            {
                float speed = s;
                Button b = ToolButton($"{s:0}x", () => runner.SetSpeed(speed));
                b.userData = speed;
                speedButtons.Add(b);
                speeds.Add(b);
            }
            bar.Add(speeds);

            var clock = new VisualElement();
            clock.AddToClassList("clock");
            clockLabel = new Label("1g 00:00");
            clockLabel.AddToClassList("clock-time");
            var progress = new VisualElement();
            progress.AddToClassList("progress");
            progressFill = new VisualElement();
            progressFill.AddToClassList("progress-fill");
            progress.Add(progressFill);
            clock.Add(clockLabel);
            clock.Add(progress);
            bar.Add(clock);

            var info = new VisualElement();
            info.AddToClassList("episode-info");
            episodeLabel = new Label();
            statusLabel = new Label();
            statusLabel.AddToClassList("status");
            info.Add(episodeLabel);
            info.Add(statusLabel);
            bar.Add(info);
        }

        private void BuildKpiStrip()
        {
            var strip = Panel("kpi-strip");
            root.Add(strip);
            foreach ((string key, string caption) in new[]
                     {
                         ("orders", "Sipariş"), ("profit", "Kâr (TL)"), ("stockout", "Stok bitmesi"),
                         ("split", "Split"), ("ontime", "Zamanında"), ("km", "Ort. mesafe"),
                         ("service", "Servis seviyesi"), ("parcels", "Yoldaki paket")
                     })
            {
                var card = new VisualElement();
                card.AddToClassList("kpi");
                var value = new Label("–");
                value.AddToClassList("kpi-value");
                var name = new Label(caption);
                name.AddToClassList("kpi-caption");
                card.Add(value);
                card.Add(name);
                strip.Add(card);
                kpis[key] = value;
            }

            var trendCard = new VisualElement();
            trendCard.AddToClassList("kpi");
            trendCard.AddToClassList("trend-card");
            trend = new Sparkline();
            trendService = trend.AddSeries(Palette.Ok);
            trendStockout = trend.AddSeries(Palette.Critical);
            var trendCaption = new Label("Saatlik trend: servis (yeşil), stok bitmesi ×5 (kırmızı)");
            trendCaption.AddToClassList("kpi-caption");
            trendCard.Add(trend);
            trendCard.Add(trendCaption);
            strip.Add(trendCard);
        }

        private static VisualElement Panel(string cls)
        {
            var v = new VisualElement();
            v.AddToClassList("panel");
            v.AddToClassList(cls);
            return v;
        }

        private static Button ToolButton(string text, System.Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("tool-button");
            return b;
        }

        // ---------------------------------------------------------------- events

        private void OnEpisodeStarted(SimulationEngine engine)
        {
            episodeLabel.text = $"Senaryo {Cap(runner.Scenario)} · Rejim {Cap(runner.Regime)} · Politika {runner.Policy} · Seed {runner.Seed}";
            statusLabel.text = "";
            EnsureSidePanel();
            sidePanel?.SyncFromRunner();
            cardsDirty = true;
            trend.ClearData();
            trend.Capacity = engine.EpisodeTicks / SimTime.TicksPerHour + 1;
            labelsDirty = true; // the map may rebuild after us in the same event; place labels next frame
            Refresh();
        }

        private void OnEpisodeFinished(SimulationEngine engine)
        {
            statusLabel.text = "Episode tamamlandı (özet ekranı S5'te)";
            Refresh();
        }

        private void RebuildMapLabels()
        {
            labelLayer.Clear();
            mapLabels.Clear();
            if (map == null) return;
            foreach (CenterView c in map.Centers) AddMapLabel(c.Center.Code, c.transform, new Vector3(0f, 1.9f, 0f), "label-center");
            foreach (RegionView r in map.Regions) AddMapLabel(r.Region.Name, r.transform, new Vector3(0f, -1.4f, 0f), "label-region");
        }

        private void AddMapLabel(string text, Transform target, Vector3 offset, string cls)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("map-label");
            label.AddToClassList(cls);
            labelLayer.Add(label);
            mapLabels.Add((label, target, offset));
        }

        // ---------------------------------------------------------------- per frame

        private void Update()
        {
            HandleShortcuts();
            UpdatePointerBlocking();
            if (labelsDirty && map != null && map.Centers.Count > 0)
            {
                RebuildMapLabels();
                labelsDirty = false;
            }
            if (cardsDirty && sidePanel != null && map != null && map.Centers.Count > 0 && runner.Engine != null)
            {
                sidePanel.BuildCards(runner.Engine);
                cardsDirty = false;
            }
            PlaceMapLabels();

            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = RefreshInterval;
                Refresh();
            }
        }

        private void HandleShortcuts()
        {
            Keyboard k = Keyboard.current;
            if (k == null || runner == null) return;
            if (k.spaceKey.wasPressedThisFrame) runner.TogglePause();
            if (k.nKey.wasPressedThisFrame) runner.StepOnce();
            if (k.rKey.wasPressedThisFrame) runner.ResetEpisode();
            if (k.digit1Key.wasPressedThisFrame) runner.SetSpeed(SimulationRunner.Speeds[0]);
            if (k.digit2Key.wasPressedThisFrame) runner.SetSpeed(SimulationRunner.Speeds[1]);
            if (k.digit3Key.wasPressedThisFrame) runner.SetSpeed(SimulationRunner.Speeds[2]);
            if (k.digit4Key.wasPressedThisFrame) runner.SetSpeed(SimulationRunner.Speeds[3]);
        }

        private void UpdatePointerBlocking()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || root.panel == null) return;
            Vector2 screen = mouse.position.ReadValue();
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
            VisualElement picked = root.panel.Pick(panelPos);
            bool overUi = picked != null && picked != root && picked != labelLayer;
            if (cameraController != null) cameraController.PointerBlocked = overUi;
            if (selection != null) selection.PointerBlocked = overUi;
        }

        private void PlaceMapLabels()
        {
            if (root.panel == null || sceneCamera == null) return;
            foreach ((Label label, Transform target, Vector3 offset) in mapLabels)
            {
                if (target == null) continue;
                Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, target.position + offset, sceneCamera);
                label.style.left = p.x;
                label.style.top = p.y;
            }
        }

        private void Refresh()
        {
            SimulationEngine engine = runner != null ? runner.Engine : null;
            if (engine == null) return;

            playButton.text = runner.IsRunning ? "⏸ Duraklat" : engine.IsFinished ? "✓ Bitti" : "▶ Başlat";
            foreach (Button b in speedButtons)
                b.EnableInClassList("selected", Mathf.Approximately((float)b.userData, runner.Speed));

            int tick = engine.Tick;
            clockLabel.text = SimTime.Format(tick);
            float progress = Mathf.Clamp01(tick / (float)engine.EpisodeTicks);
            progressFill.style.width = Length.Percent(progress * 100f);
            if (!engine.IsFinished && tick >= engine.EpisodeTicks)
                statusLabel.text = "Yeni sipariş yok, yoldaki paketler teslim ediliyor";

            KpiTracker k = engine.Kpis;
            kpis["orders"].text = k.Orders.ToString("N0", Tr);
            kpis["profit"].text = k.Profit.ToString("N0", Tr);
            kpis["stockout"].text = Percent(k.StockoutRate);
            kpis["split"].text = Percent(k.SplitRate);
            kpis["ontime"].text = k.DeliveredOrders > 0 ? Percent(k.OnTimeRate) : "–";
            kpis["km"].text = k.Packages > 0 ? k.AverageFulfilmentKm.ToString("0.0", Tr) + " km" : "–";
            kpis["service"].text = k.CompletedOrders > 0 ? Percent(k.ServiceLevel) : "–";
            kpis["parcels"].text = engine.ActiveShipments.Count.ToString("N0", Tr);
            kpis["ontime"].EnableInClassList("bad", k.DeliveredOrders > 0 && k.OnTimeRate < 0.95);
            kpis["stockout"].EnableInClassList("bad", k.StockoutRate > 0.05);

            decisionPanel.Refresh(engine, selection != null ? selection.Current : null);
            sidePanel?.Refresh(engine, map);
        }

        /// <summary>One trend sample per simulated hour while orders are coming in.</summary>
        private void OnStepped(IReadOnlyList<SimEvent> events)
        {
            SimulationEngine engine = runner.Engine;
            if (engine.Tick % SimTime.TicksPerHour != 0 || engine.Tick > engine.EpisodeTicks) return;
            KpiTracker k = engine.Kpis;
            trend.Add(trendService, k.CompletedOrders > 0 ? (float)k.ServiceLevel : 1f);
            trend.Add(trendStockout, (float)k.StockoutRate * 5f);
        }

        private static string Percent(double v) => "%" + (v * 100.0).ToString("0.0", Tr);
        private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
