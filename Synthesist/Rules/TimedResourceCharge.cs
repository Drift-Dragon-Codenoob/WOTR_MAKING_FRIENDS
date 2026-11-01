using System;

namespace WOTR_MAKING_FRIENDS.Synthesist.Rules
{
    // Calculate against native availability; this record owns no resource pool.
    internal readonly struct TimedResourceCharge
    {
        internal int Units { get; }
        internal long PaidUntil { get; }
        internal bool Expired { get; }
        internal TimedResourceCharge(long now, long paidUntil, long interval, int available)
        {
            if (now < 0 || paidUntil <= 0 || interval <= 0 || available < 0) throw new ArgumentOutOfRangeException();
            if (now < paidUntil) { Units = 0; PaidUntil = paidUntil; Expired = false; return; }
            long due = (now - paidUntil) / interval + 1;
            Units = (int)Math.Min(due, available);
            Expired = due > Units;
            PaidUntil = Expired ? 0 : checked(paidUntil + Units * interval);
        }
    }
}
