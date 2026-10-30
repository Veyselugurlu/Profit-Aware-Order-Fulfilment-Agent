using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Runs the lecture's class experiment on the unchanged Week 3 scripts. It does not modify PopulationManager:
/// it sets the public mutation rate and seed before PopulationManager.Awake, speeds time up, and turns the
/// "Generation N: avg=…, best=…, alive=…/…, bestDNA=[a, b]" log line PopulationManager writes every generation
/// into CSV rows.
/// Player arguments: -week3lab -w3mutation 0.1 -w3seed 2026 -w3generations 30 -w3timescale 10 -w3out runs.csv
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class Week3LabRunner : MonoBehaviour
{
    public PopulationManager population;

    private static readonly Regex GenerationLine = new Regex(
        @"^Generation (\d+): avg=(-?[0-9]+[.,][0-9]+), best=(-?[0-9]+[.,][0-9]+), alive=(\d+)/(\d+), bestDNA=\[(\d), (\d)\]");

    private readonly List<string> rows = new List<string>();
    private string outPath;
    private int targetGenerations;
    private float mutationRate;
    private int seed;

    private void Awake()
    {
        string[] args = Environment.GetCommandLineArgs();
        outPath = Arg(args, "-w3out");
        if (population == null) return;

        string m = Arg(args, "-w3mutation"), s = Arg(args, "-w3seed"), g = Arg(args, "-w3generations"), ts = Arg(args, "-w3timescale");
        if (m != null) population.mutationRate = float.Parse(m, CultureInfo.InvariantCulture);
        if (s != null)
        {
            population.fixedSeed = true;
            population.seed = int.Parse(s, CultureInfo.InvariantCulture);
        }
        if (ts != null) Time.timeScale = float.Parse(ts, CultureInfo.InvariantCulture);
        targetGenerations = g != null ? int.Parse(g, CultureInfo.InvariantCulture) : 0;
        mutationRate = population.mutationRate;
        seed = population.seed;

        if (outPath != null)
            Application.logMessageReceived += OnLog;
    }

    private void OnDestroy() => Application.logMessageReceived -= OnLog;

    private void OnLog(string message, string stackTrace, LogType type)
    {
        Match match = GenerationLine.Match(message);
        if (!match.Success) return;

        double Num(int group) => double.Parse(match.Groups[group].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
        int generation = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        rows.Add(string.Join(",",
            mutationRate.ToString(CultureInfo.InvariantCulture), seed.ToString(CultureInfo.InvariantCulture),
            generation.ToString(CultureInfo.InvariantCulture), Num(2).ToString(CultureInfo.InvariantCulture),
            Num(3).ToString(CultureInfo.InvariantCulture), match.Groups[4].Value, match.Groups[5].Value,
            $"\"[{match.Groups[6].Value}, {match.Groups[7].Value}]\""));

        if (targetGenerations > 0 && generation >= targetGenerations)
        {
            Write();
            Application.Quit();
        }
    }

    private void Write()
    {
        string dir = Path.GetDirectoryName(outPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var sb = new StringBuilder("mutation_rate,seed,generation,mean_fitness,best_fitness,alive,population,best_dna\n");
        foreach (string row in rows) sb.Append(row).Append('\n');
        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
    }

    private static string Arg(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
