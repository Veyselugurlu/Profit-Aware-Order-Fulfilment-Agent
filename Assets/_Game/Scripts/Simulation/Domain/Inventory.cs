using System;
using System.Collections.Generic;

namespace Fulfilment.Simulation
{
    /// <summary>On-hand stock per (fulfilment center, SKU).</summary>
    public sealed class Inventory
    {
        private readonly int[,] onHand;

        public int CenterCount { get; }
        public int SkuCount { get; }

        public Inventory(int centerCount, int skuCount)
        {
            if (centerCount <= 0) throw new ArgumentOutOfRangeException(nameof(centerCount));
            if (skuCount <= 0) throw new ArgumentOutOfRangeException(nameof(skuCount));
            CenterCount = centerCount;
            SkuCount = skuCount;
            onHand = new int[centerCount, skuCount];
        }

        public int OnHand(int centerId, int skuId) => onHand[centerId, skuId];

        public int NetworkOnHand(int skuId)
        {
            int total = 0;
            for (int c = 0; c < CenterCount; c++)
                total += onHand[c, skuId];
            return total;
        }

        public void Set(int centerId, int skuId, int quantity)
        {
            if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            onHand[centerId, skuId] = quantity;
        }

        public void Add(int centerId, int skuId, int quantity)
        {
            if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            onHand[centerId, skuId] += quantity;
        }

        /// <summary>Removes stock; throws instead of going negative so a wrong plan is caught early.</summary>
        public void Remove(int centerId, int skuId, int quantity)
        {
            if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (onHand[centerId, skuId] < quantity)
                throw new InvalidOperationException(
                    $"Center {centerId} has {onHand[centerId, skuId]} of SKU {skuId}, cannot remove {quantity}.");
            onHand[centerId, skuId] -= quantity;
        }

        public bool Has(int centerId, OrderLine line) => onHand[centerId, line.SkuId] >= line.Quantity;

        /// <summary>True when the center alone can ship every line of the order.</summary>
        public bool CanFulfilAll(int centerId, IReadOnlyList<OrderLine> lines)
        {
            for (int i = 0; i < lines.Count; i++)
                if (!Has(centerId, lines[i]))
                    return false;
            return true;
        }

        public Inventory Clone()
        {
            var copy = new Inventory(CenterCount, SkuCount);
            Array.Copy(onHand, copy.onHand, onHand.Length);
            return copy;
        }
    }
}
