using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class PopulationManager : MonoBehaviour
{
    [Header("Scene references")]
    public GameObject botPrefab;
    public Collider safePlatform;

    [Header("Evolution")]
    [Range(2, 100)] public int populationSize = 24;
    [Min(1f)] public float trialTime = 8f;
    [Range(0f, 1f)] public float mutationRate = 0.10f;
    [Range(0, 10)] public int eliteCount = 2;
    [Min(0.1f)] public float spawnHalfExtent = 1.7f;
    public bool fixedSeed = true;
    public int seed = 2026;

    private readonly List<Brain> population = new List<Brain>();
    private float elapsed;
    private int generation = 1;
    private bool transitioning;
    private float lastAverageFitness;
    private float lastBestFitness;
    private int lastSurvivors;
    private GUIStyle labelStyle;

    private void Awake()
    {
        if (fixedSeed) UnityEngine.Random.InitState(seed);
    }

    private void Start()
    {
        if (botPrefab == null || safePlatform == null || botPrefab.GetComponent<Brain>() == null)
        {
            Debug.LogError("Assign Bot Prefab (with Brain) and Safe Platform in PopulationManager.");
            enabled = false;
            return;
        }
        var genomes = new List<DNA>();
        for (int i = 0; i < populationSize; i++) genomes.Add(DNA.RandomGenome());
        SpawnGeneration(genomes);
    }

    private void SpawnGeneration(List<DNA> genomes)
    {
        foreach (DNA genome in genomes)
        {
            Vector3 position = transform.position + new Vector3(
                UnityEngine.Random.Range(-spawnHalfExtent, spawnHalfExtent),
                0f,
                UnityEngine.Random.Range(-spawnHalfExtent, spawnHalfExtent));
            Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            GameObject obj = Instantiate(botPrefab, position, rotation);
            obj.name = "Bot_G" + generation + "_" + population.Count;
            Brain bot = obj.GetComponent<Brain>();
            bot.Init(genome, safePlatform);

            // Avoid bot-bot collisions influencing the genotype experiment.
            Collider newCollider = obj.GetComponent<Collider>();
            foreach (Brain previous in population)
            {
                if (newCollider != null && previous != null)
                    Physics.IgnoreCollision(newCollider, previous.GetComponent<Collider>(), true);
            }
            population.Add(bot);
        }
    }

    private void Update()
    {
        if (transitioning) return;
        elapsed += Time.deltaTime;
        if (elapsed >= trialTime) StartCoroutine(NextGeneration());
    }

    private IEnumerator NextGeneration()
    {
        transitioning = true;
        foreach (Brain bot in population) bot.EndTrial(trialTime);

        List<Brain> sorted = population.OrderByDescending(bot => bot.Fitness).ToList();
        lastBestFitness = sorted[0].Fitness;
        lastAverageFitness = sorted.Average(bot => bot.Fitness);
        lastSurvivors = sorted.Count(bot => bot.isAlive);
        Debug.Log($"Generation {generation}: avg={lastAverageFitness:F2}, " +
                  $"best={lastBestFitness:F2}, alive={lastSurvivors}/{population.Count}, " +
                  $"bestDNA={sorted[0].dna}");

        List<DNA> nextGenomes = BuildNextGenomes(sorted);
        foreach (Brain bot in population) Destroy(bot.gameObject);
        population.Clear();

        // Destroy() is deferred until the end of the frame.
        yield return null;
        generation++;
        elapsed = 0f;
        SpawnGeneration(nextGenomes);
        transitioning = false;
    }

    private List<DNA> BuildNextGenomes(List<Brain> sorted)
    {
        var children = new List<DNA>(populationSize);
        int elite = Mathf.Clamp(eliteCount, 0, populationSize);
        for (int i = 0; i < elite; i++) children.Add(sorted[i].dna.Copy());

        // TODO D: select parents from the best half, crossover and mutate.
        // Starter baseline: generate independent random genomes (NO evolution).
        while (children.Count < populationSize)
        {
            children.Add(DNA.RandomGenome());
        }
        return children;
    }

    private void OnGUI()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 17 };
            labelStyle.normal.textColor = Color.white;
        }
        GUI.Box(new Rect(12, 12, 310, 125), "Evolution / Stay on Platform");
        GUI.Label(new Rect(23, 36, 290, 24), $"Generation: {generation} | Time: {elapsed:F1}/{trialTime:F0}", labelStyle);
        GUI.Label(new Rect(23, 62, 290, 24), $"Alive: {population.Count(b => b != null && b.isAlive)}/{population.Count}", labelStyle);
        if (generation > 1)
            GUI.Label(new Rect(23, 88, 290, 25), $"Last avg: {lastAverageFitness:F2} | Best: {lastBestFitness:F2}", labelStyle);
    }
}
