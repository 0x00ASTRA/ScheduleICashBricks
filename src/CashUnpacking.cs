using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;

namespace CashBricks;

// Like native PackagingStation.Unpack, one station action removes one package
// and returns its contents to a station slot. The native unpackage layout uses
// OutputSlot as the package source and ProductSlot as the loose destination.
// No equipped item or wallet writes.
internal static class CashUnpacking
{
    private static readonly HashSet<IntPtr> ActiveOutputs = new();

    internal static PackagingStation.EState State(PackagingStation station, PackagingStation.EMode mode)
    {
        if (mode != PackagingStation.EMode.Unpackage || station.OutputSlot == null || station.ProductSlot == null)
            return PackagingStation.EState.MissingItems;
        var state = State(new[] { station.OutputSlot }, station.ProductSlot, out _);
        // Match the native unpack UI's capacity warning for occupied, locked,
        // full, or imprecise loose-content destinations.
        return state is PackagingStation.EState.OutputSlotFull or PackagingStation.EState.Mismatch
            ? PackagingStation.EState.ProductSlotFull : state;
    }

    internal static bool Unpack(PackagingStation station) =>
        State(station, PackagingStation.EMode.Unpackage) == PackagingStation.EState.CanBegin &&
        Unpack(new[] { station.OutputSlot }, station.ProductSlot);

    internal static PackagingStation.EState State(IEnumerable<ItemSlot> inputs, ItemSlot output, out float cashAfter)
    {
        cashAfter = 0;
        if (Main.BrickDefinition == null || Main.CashDefinition == null) return PackagingStation.EState.MissingItems;
        bool foundBrick = false;
        var seen = new HashSet<IntPtr> { output.Pointer };
        foreach (var slot in inputs)
        {
            if (!seen.Add(slot.Pointer)) return PackagingStation.EState.Mismatch;
            if (slot.IsLocked || slot.IsRemovalLocked) return PackagingStation.EState.MissingItems;
            var item = slot.ItemInstance;
            if (item == null) continue;
            if (!Main.IsBrick(item) || item.Quantity <= 0 || item.Quantity > Main.BrickDefinition.StackLimit)
                return PackagingStation.EState.Mismatch;
            foundBrick = true;
        }
        if (!foundBrick) return PackagingStation.EState.MissingItems;
        if (output.IsLocked) return PackagingStation.EState.OutputSlotFull;
        var cash = Main.AsCash(output.ItemInstance);
        if (output.ItemInstance != null && cash == null) return PackagingStation.EState.Mismatch;
        if (cash != null && (!float.IsFinite(cash.Balance) || cash.Balance < 0 || cash.Quantity <= 0))
            return PackagingStation.EState.Mismatch;
        // IsAddLocked forbids manual output deposits, not machine production.
        return CashRules.TryPlanUnpack(cash?.Balance ?? 0, CashInstance.MAX_BALANCE, out cashAfter)
            ? PackagingStation.EState.CanBegin : PackagingStation.EState.OutputSlotFull;
    }

    internal static bool Unpack(IEnumerable<ItemSlot> source, ItemSlot output)
    {
        if (!ActiveOutputs.Add(output.Pointer)) return false;
        try
        {
            var inputs = source.ToArray();
            if (State(inputs, output, out float cashAfter) != PackagingStation.EState.CanBegin) return false;
            var input = inputs.First(slot => Main.IsBrick(slot.ItemInstance));
            var brickBefore = CashSnapshots.Copy(input.ItemInstance)!;
            var outputBefore = CashSnapshots.Copy(output.ItemInstance);
            var brickAfter = brickBefore.Quantity == 1 ? null : brickBefore.GetCopy(brickBefore.Quantity - 1);
            var cash = Main.AsCash(outputBefore == null ? Main.CashDefinition!.GetDefaultInstance(1) :
                CashSnapshots.Copy(outputBefore))
                ?? throw new InvalidOperationException("The native cash definition did not create unpacked cash.");
            cash.SetBalance(cashAfter, true);
            if (cash.Balance != cashAfter)
                throw new InvalidOperationException("The native cash instance did not retain the unpacked balance.");
            CashTransaction.Run(2,
                step => CashSnapshots.Store(step == 0 ? input : output, step == 0 ? brickAfter : cash),
                step => CashSnapshots.Store(step == 0 ? input : output, step == 0 ? brickBefore : outputBefore));
            Main.Mod.LoggerInstance.Msg("Cash brick unpacked at the packaging station: one brick removed from the output slot, $1,000 added to the product slot.");
            return true;
        }
        finally { ActiveOutputs.Remove(output.Pointer); }
    }
}
