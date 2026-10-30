using NUnit.Framework;

namespace Week3Lab.Tests
{
    /// <summary>The lecture's activities on the unchanged Week 3 DNA class (TODO A and B completed).</summary>
    public class Week3LabTests
    {
        [Test]
        public void Activity05_ChildTakesFirstGeneFromAAndSecondFromB()
        {
            var child = DNA.Cross(new DNA(0, 1), new DNA(2, 2));
            Assert.AreEqual("[0, 2]", child.ToString());
        }

        [Test]
        public void Activity06_MutationAlwaysPicksADifferentAction()
        {
            UnityEngine.Random.InitState(2026);
            for (int i = 0; i < 200; i++)
            {
                var dna = new DNA(0, 2);
                dna.Mutate(1f);
                Assert.AreNotEqual(0, dna.GetGene(0));
                Assert.AreNotEqual(2, dna.GetGene(1));
                Assert.That(dna.GetGene(0), Is.InRange(0, 2));
                Assert.That(dna.GetGene(1), Is.InRange(0, 2));
            }
        }

        [Test]
        public void ZeroMutationKeepsTheGenome()
        {
            var dna = new DNA(1, 2);
            dna.Mutate(0f);
            Assert.AreEqual("[1, 2]", dna.ToString());
        }

        [Test]
        public void GenesAreActionCodes()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new DNA(3, 0));
            var random = DNA.RandomGenome();
            Assert.That(random.GetGene(0), Is.InRange(0, 2));
            Assert.That(random.GetGene(1), Is.InRange(0, 2));
        }
    }
}
