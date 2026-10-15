namespace CashBricks;

// Native callbacks can throw after changing a slot. Restore every attempted
// write in reverse order, including that failing write, and keep trying even
// when a restoration itself fails.
internal static class CashTransaction
{
    internal static void Run(int steps, Action<int> apply, Action<int> restore)
    {
        int attempted = -1;
        try
        {
            for (int i = 0; i < steps; i++)
            {
                attempted = i;
                apply(i);
            }
        }
        catch (Exception failure)
        {
            var failures = new List<Exception> { failure };
            for (int i = attempted; i >= 0; i--)
            {
                try { restore(i); }
                catch (Exception rollbackFailure) { failures.Add(rollbackFailure); }
            }
            if (failures.Count > 1)
                throw new AggregateException("Cash operation failed and could not fully restore its snapshots.", failures);
            throw;
        }
    }
}
