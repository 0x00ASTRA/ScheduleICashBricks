namespace CashBricks;

// One completion permit per native employee cycle. No money changes during
// work, and reentrant, repeated, cancelled or failed callbacks cannot retry it.
internal sealed class CashEmployeeCycle
{
    internal enum Phase { Working, Ready, Committing, Complete, Cancelled }
    internal Phase State { get; private set; } = Phase.Working;

    internal bool Offer()
    {
        if (State == Phase.Working) State = Phase.Ready;
        return State == Phase.Ready;
    }

    internal bool Commit(Action write)
    {
        if (State != Phase.Ready) return false;
        State = Phase.Committing;
        try
        {
            write();
            State = Phase.Complete;
            return true;
        }
        catch { State = Phase.Cancelled; throw; }
    }

    internal void Cancel()
    {
        if (State != Phase.Complete && State != Phase.Committing) State = Phase.Cancelled;
    }
}
