using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Fulfilment.AI;
using Fulfilment.Experiments;
using Fulfilment.Presentation;
using UnityEditor;
using UnityEngine;

namespace Fulfilment.Editor
{
    /// <summary>
    /// Quick headless check of the simulation: both baselines × every load level × every regime for one seed.
    /// Writes a KPI table and the order log of each run to results/_scratch/ (not committed).
    /// Menu: Fulfilment → Run Headless Check. Batch: -executeMethod Fulfilment.Editor.HeadlessRun.RunBatch [-seed N]
    /// </summary>
    public static class HeadlessRun
    {
        private const string Marker = "[HeadlessRun]";

        [MenuItem("Fulfilment/Run Headless Check")]
        public static void RunFromMenu() => Run(1001);

        public static void RunBatch()
        {
            int exitCode = 0;
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                int index = Array.IndexOf(args, "-seed");
                int seed = index >= 0 && index + 1 < args.Length ? int.Parse(args[index + 1]) : 1001;
                Run(seed);
            }
            catch (Exception e)
            {
                Debug.LogError($"{Marker} FAILED {e}");
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        private static void Run(int seed)
        {
            ConfigSet configs = ConfigLoader.LoadDefault();
            List<string> errors = configs.Validate();
            if (errors.Count > 0)
                throw new InvalidOperationException("Config errors:\n" + string.Join("\n", errors));

            string outDir = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "results", "_scratch", "s02_headless");
            var results = new List<EpisodeResult>();

            foreach (var regime in configs.Regimes)
                foreach (var scenario in configs.Scenarios)
                    foreach (string policyName in PolicyFactory.Names)
                    {
                        EpisodeResult result = EpisodeRunner.Run(configs.Setup(scenario.code, regime.code, seed), PolicyFactory.Create(policyName));
                        results.Add(result);
                        CsvExport.WriteOrders(Path.Combine(outDir, $"orders_{policyName}_{scenario.code}_{regime.code}_{seed}.csv"), result);
                    }

            string kpiPath = Path.Combine(outDir, $"kpis_{seed}.csv");
            CsvExport.WriteKpis(kpiPath, results);

            var sb = new StringBuilder();
            sb.AppendLine($"{Marker} seed {seed}, {results.Count} episodes, {results.Sum(r => r.WallSeconds):0.00}s total");
            sb.AppendLine($"{Marker} {"policy",-10} {"scenario",-7} {"regime",-9} {"orders",6} {"profit",11} {"stockout%",9} {"split%",7} {"late%",6} {"km",5} {"service",7}");
            foreach (EpisodeResult r in results)
            {
                var k = r.Kpis;
                sb.AppendLine($"{Marker} {r.Policy,-10} {r.Scenario,-7} {r.Regime,-9} {k.Orders,6} {k.Profit,11:0} {k.StockoutRate * 100,9:0.00} {k.SplitRate * 100,7:0.0} {k.LateRate * 100,6:0.0} {k.AverageFulfilmentKm,5:0.0} {k.ServiceLevel,7:0.000}");
            }
            sb.AppendLine($"{Marker} written {kpiPath}");
            Debug.Log(sb.ToString());
        }
    }
}
