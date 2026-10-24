namespace Fulfilment.Simulation
{
    /// <summary>Running totals of an episode and the KPIs required by the brief.</summary>
    public sealed class KpiTracker
    {
        // Counts
        public int Orders { get; private set; }
        public int CompletedOrders { get; private set; }
        public int LostOrders { get; private set; }
        public int OrdersWithLostUnits { get; private set; }
        public int ShippedOrders { get; private set; }
        public int SplitOrders { get; private set; }
        public int LateOrders { get; private set; }
        public int OnTimeCompleteOrders { get; private set; }
        public int Packages { get; private set; }
        public int UnitsOrdered { get; private set; }
        public int UnitsShipped { get; private set; }
        public int UnitsLost { get; private set; }
        public int DepletionEvents { get; private set; }

        // Money
        public double Revenue { get; private set; }
        public double Cogs { get; private set; }
        public double ShippingCost { get; private set; }
        public double SplitCost { get; private set; }
        public double StockoutPenalty { get; private set; }
        public double LatenessCost { get; private set; }
        public double OpportunityCost { get; private set; }

        // Distance and time
        public double TotalKm { get; private set; }
        public double TotalLateMinutes { get; private set; }

        public double TotalCost => ShippingCost + SplitCost + StockoutPenalty + LatenessCost;
        public double Profit => Revenue - Cogs - TotalCost;

        public double StockoutRate => UnitsOrdered > 0 ? UnitsLost / (double)UnitsOrdered : 0.0;
        public double OrderStockoutRate => Orders > 0 ? OrdersWithLostUnits / (double)Orders : 0.0;
        public double SplitRate => ShippedOrders > 0 ? SplitOrders / (double)ShippedOrders : 0.0;
        public double LateRate => DeliveredOrders > 0 ? LateOrders / (double)DeliveredOrders : 0.0;
        public double OnTimeRate => 1.0 - LateRate;
        public double AverageLateHours => LateOrders > 0 ? TotalLateMinutes / LateOrders / 60.0 : 0.0;
        public double AverageFulfilmentKm => Packages > 0 ? TotalKm / Packages : 0.0;

        /// <summary>Share of orders delivered complete (no lost units) and on time.</summary>
        public double ServiceLevel => CompletedOrders > 0 ? OnTimeCompleteOrders / (double)CompletedOrders : 0.0;

        public int DeliveredOrders => CompletedOrders - LostOrders;

        internal void RecordDecision(OrderRecord record)
        {
            PlanEvaluation e = record.Evaluation;
            Orders++;
            UnitsOrdered += record.Order.TotalUnits;
            UnitsShipped += e.ShippedUnits;
            UnitsLost += e.LostUnits;
            if (e.LostUnits > 0) OrdersWithLostUnits++;
            if (e.PackageCount > 0) ShippedOrders++;
            if (e.PackageCount > 1) SplitOrders++;
            Packages += e.PackageCount;
            TotalKm += e.TotalKm;

            Revenue += e.Revenue;
            Cogs += e.Cogs;
            ShippingCost += e.ShippingCost;
            SplitCost += e.SplitCost;
            StockoutPenalty += e.StockoutPenalty;
            OpportunityCost += e.OpportunityCost;
        }

        internal void RecordDepletion() => DepletionEvents++;

        internal void RecordCompletion(OrderRecord record)
        {
            CompletedOrders++;
            if (record.Status == OrderStatus.Lost)
            {
                LostOrders++;
                return;
            }

            LatenessCost += record.LatenessCost;
            if (record.IsLate)
            {
                LateOrders++;
                TotalLateMinutes += record.LateMinutes;
            }
            else if (record.Evaluation.LostUnits == 0)
            {
                OnTimeCompleteOrders++;
            }
        }
    }
}
