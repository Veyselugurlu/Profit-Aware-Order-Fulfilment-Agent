using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fulfilment.Presentation
{
    /// <summary>
    /// Command-line control of a player build, for demos, screenshots and FPS checks:
    /// -scenario medium -regime stable -policy Nearest -seed 1 -speed 4 -autoplay
    /// -screenshot shot.png -screenshotAt 20 -fpsLog fps.txt -quitAfter 30 -select FC-D -diagLog diag.txt
    /// Does nothing in the Editor unless arguments are given.
    /// </summary>
    public sealed class DemoAutomation : MonoBehaviour
    {
        [SerializeField] private SimulationRunner runner;
        [SerializeField] private SelectionController selection;
        [SerializeField] private MapView map;

        private const float WarmUpSeconds = 5f;

        private string screenshotPath;
        private float screenshotAt = -1f;
        private string fpsLogPath;
        private float quitAfter = -1f;
        private float elapsed;
        private int frames;
        private float worstFrame;
        private bool shotTaken;
        private float worstFrameAt;
        private string diagPath;
        private string selectCode;

        private void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            string scenario = Arg(args, "-scenario"), regime = Arg(args, "-regime"), policy = Arg(args, "-policy"), seed = Arg(args, "-seed");
            if (scenario != null || regime != null || policy != null || seed != null)
                runner.Configure(scenario ?? runner.Scenario, regime ?? runner.Regime, policy ?? runner.Policy,
                    seed != null ? int.Parse(seed, CultureInfo.InvariantCulture) : runner.Seed);

            string speed = Arg(args, "-speed");
            if (speed != null) runner.SetSpeed(float.Parse(speed, CultureInfo.InvariantCulture));
            if (Array.IndexOf(args, "-autoplay") >= 0) runner.Play();

            screenshotPath = Arg(args, "-screenshot");
            screenshotAt = ParseFloat(Arg(args, "-screenshotAt"), screenshotPath != null ? 5f : -1f);
            fpsLogPath = Arg(args, "-fpsLog");
            quitAfter = ParseFloat(Arg(args, "-quitAfter"), -1f);
            diagPath = Arg(args, "-diagLog");
            selectCode = Arg(args, "-select");
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;

            // -select FC-D / R06: select a center or region once the map exists (to show the inspection panel).
            if (selectCode != null && selection != null && map != null && map.Centers.Count > 0)
            {
                ISelectable target = map.Centers.FirstOrDefault(c => c.Center.Code == selectCode) as ISelectable
                                     ?? map.Regions.FirstOrDefault(r => r.Region.Code == selectCode);
                if (target != null) selection.Select(target);
                selectCode = null;
            }
            if (elapsed > WarmUpSeconds) // skip start-up (assembly load, first-frame shader warm-up)
            {
                frames++;
                if (dt > worstFrame) { worstFrame = dt; worstFrameAt = elapsed; }
            }

            if (screenshotPath != null && !shotTaken && elapsed >= screenshotAt)
            {
                ScreenCapture.CaptureScreenshot(screenshotPath);
                shotTaken = true;
                WriteDiagnostics();
            }

            if (quitAfter > 0f && elapsed >= quitAfter)
            {
                WriteFpsLog();
                Application.Quit();
                quitAfter = -1f;
            }
        }

        private void WriteDiagnostics()
        {
            if (diagPath == null) return;
            var sb = new StringBuilder();
            foreach (UIDocument doc in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                VisualElement root = doc.rootVisualElement;
                sb.AppendLine($"UIDocument '{doc.name}' enabled={doc.enabled} panelSettings={(doc.panelSettings != null ? doc.panelSettings.name : "null")} " +
                              $"theme={(doc.panelSettings != null && doc.panelSettings.themeStyleSheet != null ? doc.panelSettings.themeStyleSheet.name : "null")}");
                sb.AppendLine($"  root={(root != null)} panel={(root?.panel != null)} children={root?.childCount ?? -1} rootRect={root?.worldBound}");
                if (root != null)
                    foreach (VisualElement child in root.Children())
                        sb.AppendLine($"  child name={child.name} classes={string.Join(" ", child.GetClasses())} rect={child.worldBound} display={child.resolvedStyle.display} opacity={child.resolvedStyle.opacity}");
            }
            if (runner.Engine != null)
            {
                int transit = runner.Engine.ActiveShipments.Count(s => runner.SmoothTick >= s.DispatchTick);
                sb.AppendLine($"tick={runner.Engine.Tick} smooth={runner.SmoothTick:0.00} active={runner.Engine.ActiveShipments.Count} inTransit={transit}");
            }
            sb.AppendLine($"screen={Screen.width}x{Screen.height} worstFrameSoFar={worstFrame * 1000f:0}ms at {worstFrameAt:0.0}s");
            File.WriteAllText(diagPath, sb.ToString());
        }

        private void WriteFpsLog()
        {
            if (fpsLogPath == null) return;
            float measured = Mathf.Max(0.001f, elapsed - WarmUpSeconds);
            string line = string.Format(CultureInfo.InvariantCulture,
                "scenario={0} regime={1} policy={2} speed={3} resolution={4}x{5} avg_fps={6:0.0} worst_frame_ms={7:0.0} worst_at_s={10:0.0} sim_tick={8} parcels={9}",
                runner.Scenario, runner.Regime, runner.Policy, runner.Speed, Screen.width, Screen.height,
                frames / measured, worstFrame * 1000f, runner.Engine?.Tick ?? 0, runner.Engine?.ActiveShipments.Count ?? 0, worstFrameAt);
            File.WriteAllText(fpsLogPath, line + Environment.NewLine);
        }

        private static string Arg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private static float ParseFloat(string s, float fallback) =>
            s != null ? float.Parse(s, CultureInfo.InvariantCulture) : fallback;
    }
}
