using System;
using System.Diagnostics;
using System.IO;
using Fulfilment.Experiments;
using Fulfilment.Presentation;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Fulfilment.Editor
{
    /// <summary>
    /// Runs an experiment from configs/experiments/&lt;name&gt;.json and writes results/&lt;name&gt;/runs.csv and meta.json.
    /// Batch: -executeMethod Fulfilment.Editor.ExperimentCli.Run -experiment vize_prelim [-out dir]
    /// </summary>
    public static class ExperimentCli
    {
        private const string Marker = "[ExperimentCli]";

        [MenuItem("Fulfilment/Run Experiment/smoke_train (training seeds)")]
        public static void RunSmoke() => RunNamed("smoke_train", null);

        [MenuItem("Fulfilment/Run Experiment/vize_prelim (test seeds)")]
        public static void RunVize() => RunNamed("vize_prelim", null);

        public static void Run()
        {
            int exitCode = 0;
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                string name = Arg(args, "-experiment") ?? throw new ArgumentException("-experiment <name> is required");
                RunNamed(name, Arg(args, "-out"));
            }
            catch (Exception e)
            {
                Debug.LogError($"{Marker} FAILED {e}");
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        private static void RunNamed(string name, string outDir)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath) ?? ".";
            outDir ??= Path.Combine(projectRoot, "results", name);
            ExperimentProvenance provenance = GitProvenance(projectRoot);
            var results = ExperimentLauncher.Run(name, outDir, provenance, line => Debug.Log($"{Marker} {line}"));
            Debug.Log($"{Marker} done: {results.Count} episodes, commit {provenance.GitCommit}{(provenance.GitDirty ? " (uncommitted changes)" : "")} → {outDir}");
        }

        public static ExperimentProvenance GitProvenance(string projectRoot)
        {
            var provenance = new ExperimentProvenance();
            string commit = Git(projectRoot, "rev-parse HEAD");
            if (!string.IsNullOrWhiteSpace(commit))
            {
                provenance.GitCommit = commit.Trim();
                provenance.GitDirty = !string.IsNullOrWhiteSpace(Git(projectRoot, "status --porcelain --untracked-files=no"));
            }
            return provenance;
        }

        private static string Git(string workingDirectory, string arguments)
        {
            try
            {
                var info = new ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using Process p = Process.Start(info);
                string output = p?.StandardOutput.ReadToEnd() ?? "";
                p?.WaitForExit(5000);
                return p != null && p.ExitCode == 0 ? output : "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        private static string Arg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
