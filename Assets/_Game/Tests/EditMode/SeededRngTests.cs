using System.Linq;
using Fulfilment.Simulation;
using NUnit.Framework;

namespace Fulfilment.Tests
{
    public class SeededRngTests
    {
        [Test]
        public void SameSeedAndStream_GiveSameSequence()
        {
            var a = new SeededRng(1001, RngStream.Orders);
            var b = new SeededRng(1001, RngStream.Orders);
            for (int i = 0; i < 1000; i++)
                Assert.AreEqual(a.NextULong(), b.NextULong());
        }

        [Test]
        public void DifferentStreams_AreIndependentSequences()
        {
            var orders = new SeededRng(1001, RngStream.Orders);
            var policy = new SeededRng(1001, RngStream.Policy);
            int equal = Enumerable.Range(0, 100).Count(_ => orders.NextULong() == policy.NextULong());
            Assert.AreEqual(0, equal);
        }

        [Test]
        public void DifferentSeeds_GiveDifferentSequences()
        {
            var a = new SeededRng(1001, RngStream.Orders);
            var b = new SeededRng(1002, RngStream.Orders);
            Assert.AreNotEqual(a.NextULong(), b.NextULong());
        }

        [Test]
        public void NextDouble_StaysInUnitInterval()
        {
            var rng = new SeededRng(7, RngStream.Orders);
            for (int i = 0; i < 100000; i++)
            {
                double u = rng.NextDouble();
                Assert.That(u, Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));
            }
        }

        [Test]
        public void NextInt_CoversRangeWithoutLeavingIt()
        {
            var rng = new SeededRng(7, RngStream.Orders);
            var seen = new bool[5];
            for (int i = 0; i < 10000; i++)
            {
                int v = rng.NextInt(0, 5);
                Assert.That(v, Is.InRange(0, 4));
                seen[v] = true;
            }
            Assert.That(seen, Is.All.True);
        }

        [TestCase(0.05)]
        [TestCase(0.8)]
        [TestCase(4.0)]
        [TestCase(50.0)]
        public void Poisson_MeanAndVarianceMatch(double mean)
        {
            var rng = new SeededRng(42, RngStream.Orders);
            const int n = 200000;
            double sum = 0, sumSq = 0;
            for (int i = 0; i < n; i++)
            {
                int k = rng.NextPoisson(mean);
                sum += k;
                sumSq += (double)k * k;
            }
            double m = sum / n;
            double variance = sumSq / n - m * m;
            Assert.AreEqual(mean, m, mean * 0.03 + 0.002);
            Assert.AreEqual(mean, variance, mean * 0.06 + 0.004);
        }

        [Test]
        public void Normal_HasZeroMeanAndUnitVariance()
        {
            var rng = new SeededRng(3, RngStream.Orders);
            const int n = 200000;
            double sum = 0, sumSq = 0;
            for (int i = 0; i < n; i++)
            {
                double x = rng.NextNormal();
                sum += x;
                sumSq += x * x;
            }
            Assert.AreEqual(0.0, sum / n, 0.01);
            Assert.AreEqual(1.0, sumSq / n, 0.02);
        }

        [Test]
        public void DiscreteSampler_FollowsWeights()
        {
            var sampler = new DiscreteSampler(new[] { 1.0, 0.0, 3.0 });
            var rng = new SeededRng(11, RngStream.Orders);
            var counts = new int[3];
            const int n = 100000;
            for (int i = 0; i < n; i++)
                counts[sampler.Sample(rng)]++;

            Assert.AreEqual(0, counts[1]);
            Assert.AreEqual(0.25, counts[0] / (double)n, 0.01);
            Assert.AreEqual(0.75, counts[2] / (double)n, 0.01);
        }

        [Test]
        public void ZipfWeights_DecreaseWithRank()
        {
            double[] w = DiscreteSampler.ZipfWeights(30, 1.1);
            for (int i = 1; i < w.Length; i++)
                Assert.Less(w[i], w[i - 1]);
        }
    }
}
