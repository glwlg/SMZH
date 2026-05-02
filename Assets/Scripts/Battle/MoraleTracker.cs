namespace XTD.Battle
{
    public sealed class MoraleTracker
    {
        private int pendingSoldiers;

        public MoraleTracker(int soldiersPerCharge = 5)
        {
            SoldiersPerCharge = soldiersPerCharge;
        }

        public int Charges { get; private set; }
        public int PendingSoldiers => pendingSoldiers;
        public int SoldiersPerCharge { get; set; } = 5;

        public void RegisterSummonedSoldiers(int count)
        {
            if (count <= 0)
            {
                return;
            }

            pendingSoldiers += count;
            var threshold = SoldiersPerCharge < 1 ? 1 : SoldiersPerCharge;
            while (pendingSoldiers >= threshold)
            {
                pendingSoldiers -= threshold;
                Charges++;
            }
        }

        public bool TryConsume()
        {
            if (Charges <= 0)
            {
                return false;
            }

            Charges--;
            return true;
        }

        public void RefundCharge()
        {
            Charges++;
        }

        public void AddCharges(int count)
        {
            if (count <= 0)
            {
                return;
            }

            Charges += count;
        }

        public void Reset()
        {
            pendingSoldiers = 0;
            Charges = 0;
        }
    }
}
