namespace CashBricks;

// Keep monetary planning independent from Unity so conservation and edge cases
// can be verified without launching or modifying a saved game.
internal static class CashRules
{
    public const int DollarsPerBrick = 1000;

    public static int BricksToPress(double cash, int outputCapacity)
    {
        if (!double.IsFinite(cash) || cash < DollarsPerBrick || outputCapacity <= 0)
            return 0;
        return (int)Math.Min(Math.Floor(cash / DollarsPerBrick), outputCapacity);
    }

    public static double TakeFromSlot(double balance, double remaining)
    {
        if (!double.IsFinite(balance) || !double.IsFinite(remaining) || balance <= 0 || remaining <= 0)
            return 0;
        return Math.Min(balance, remaining);
    }

    public static bool TryPlanPress(IReadOnlyList<float> balances, int outputCapacity,
        out int bricks, out float[] balancesAfter)
    {
        bricks = 0;
        balancesAfter = Array.Empty<float>();
        var cents = new long[balances.Count];
        double totalCents = 0;
        for (int i = 0; i < balances.Count; i++)
        {
            if (!TryGetCents(balances[i], out cents[i])) return false;
            totalCents += cents[i];
        }
        int count = BricksToPress(totalCents / 100, outputCapacity);
        if (count == 0) return false;

        var staged = balances.ToArray();
        long remaining = (long)count * DollarsPerBrick * 100;
        // Larger slots first avoids rounding away small untouched cash stacks.
        foreach (int index in Enumerable.Range(0, balances.Count).OrderByDescending(i => balances[i]))
        {
            if (remaining == 0) break;
            long taken = Math.Min(cents[index], remaining);
            if (taken == 0) continue;
            long remainder = cents[index] - taken;
            staged[index] = (float)(remainder / 100d);
            // Plan in cents; normal float noise must not prevent a $600.10 +
            // $399.90 press. Reject any native remainder that changes a cent.
            if (!TryGetCents(staged[index], out long represented) || represented != remainder) return false;
            remaining -= taken;
        }
        if (remaining != 0) return false;
        bricks = count;
        balancesAfter = staged;
        return true;
    }

    public static bool TryPlanUnpack(float balance, float maximumBalance, out float balanceAfter)
    {
        balanceAfter = balance;
        if (!TryGetCents(balance, out long cents) || !float.IsFinite(maximumBalance) ||
            maximumBalance < 0 || cents > long.MaxValue - DollarsPerBrick * 100L)
            return false;
        long targetCents = cents + DollarsPerBrick * 100L;
        double target = (double)balance + DollarsPerBrick;
        if (target > maximumBalance) return false;
        float result = balance + DollarsPerBrick;
        if (!TryGetCents(result, out long represented) || represented != targetCents) return false;
        balanceAfter = result;
        return true;
    }

    private static bool TryGetCents(float balance, out long cents)
    {
        cents = 0;
        double scaled = (double)balance * 100;
        if (!float.IsFinite(balance) || balance < 0 || scaled >= long.MaxValue) return false;
        cents = (long)Math.Round(scaled, MidpointRounding.AwayFromZero);
        return true;
    }
}
