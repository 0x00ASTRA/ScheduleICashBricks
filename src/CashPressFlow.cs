namespace CashBricks;

// Money changes only after the physical task has loaded its own twenty stacks
// and the player has pulled the handle. A native callback may re-enter commit.
internal sealed class CashPressFlow
{
    internal const int StackCount = 20;
    internal enum Phase { Pouring, Pressing, Ready, Committing, Committed, Cancelled }
    internal Phase State { get; private set; } = Phase.Pouring;

    internal bool LoadMould(int ownStacks)
    {
        if (State != Phase.Pouring || ownStacks != StackCount) return false;
        State = Phase.Pressing;
        return true;
    }

    internal bool PullHandle(bool atBottom)
    {
        if (State != Phase.Pressing || !atBottom) return false;
        State = Phase.Ready;
        return true;
    }

    internal bool Commit(Action write)
    {
        if (State != Phase.Ready) return false;
        State = Phase.Committing;
        try { write(); State = Phase.Committed; return true; }
        catch { State = Phase.Cancelled; throw; }
    }

    internal void Cancel()
    {
        if (State != Phase.Committed && State != Phase.Committing) State = Phase.Cancelled;
    }
}
