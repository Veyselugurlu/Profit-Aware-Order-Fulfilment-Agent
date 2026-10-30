using System;
using UnityEngine;

// Two integer genes. Each gene encodes: 0 = forward, 1 = left, 2 = right.
[Serializable]
public sealed class DNA
{
    private readonly int[] genes;

    public DNA(int groundAction, int edgeAction)
    {
        genes = new[] { CheckAction(groundAction), CheckAction(edgeAction) };
    }

    public int GetGene(int index)
    {
        if (index < 0 || index >= genes.Length) throw new ArgumentOutOfRangeException(nameof(index));
        return genes[index];
    }

    public DNA Copy() => new DNA(genes[0], genes[1]);

    public static DNA RandomGenome() => new DNA(UnityEngine.Random.Range(0, 3), UnityEngine.Random.Range(0, 3));

    public static DNA Cross(DNA parentA, DNA parentB)
    {
        if (parentA == null || parentB == null) throw new ArgumentNullException("parent");
        // Two-gene one-point crossover: ground policy from A, edge policy from B.
        // TODO A: replace with a true one-point crossover from A and B.
        return parentA.Copy();
    }

    public void Mutate(float probabilityPerGene)
    {
        float p = Mathf.Clamp01(probabilityPerGene);
        for (int i = 0; i < genes.Length; i++)
        {
            if (UnityEngine.Random.value >= p) continue;
            // Guarantee a different value rather than occasionally 'mutating' to itself.
            // TODO B: choose a DIFFERENT action gene with probability p.
            // Deliberately left unchanged for the starter activity.
        }
    }

    private static int CheckAction(int action)
    {
        if (action < 0 || action > 2) throw new ArgumentOutOfRangeException(nameof(action));
        return action;
    }

    public override string ToString() => "[" + genes[0] + ", " + genes[1] + "]";
}
