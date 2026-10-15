using Il2CppScheduleOne.ItemFramework;

namespace CashBricks;

internal static class CashSnapshots
{
    internal static void Store(ItemSlot slot, ItemInstance? item)
    {
        if (item == null) slot.ClearStoredInstance();
        else slot.SetStoredItem(item);
        var stored = slot.ItemInstance;
        if (item == null ? stored != null : stored == null || stored.ID != item.ID || stored.Quantity != item.Quantity)
            throw new InvalidOperationException("A native slot did not store the staged cash item.");
        var expectedCash = Main.AsCash(item);
        if (expectedCash != null && Main.AsCash(stored)?.Balance != expectedCash.Balance)
            throw new InvalidOperationException("A native slot did not store the staged cash balance.");
    }

    internal static ItemInstance? Copy(ItemInstance? item)
    {
        var cash = Main.AsCash(item);
        if (cash == null) return item?.GetCopy();

        // In Schedule I 0.4.7f12, CashInstance.GetCopy creates a cash instance
        // with the same definition and quantity but never assigns Balance.
        // Use the native cash factory and explicitly preserve its dollar field,
        // as MoneyManager.GetCashInstance and ItemUIManager.EndCashDrag do.
        var snapshot = Main.AsCash(cash.Definition.GetDefaultInstance(cash.Quantity))
            ?? throw new InvalidOperationException("The cash definition did not create a CashInstance.");
        snapshot.SetBalance(cash.Balance, true);
        if (snapshot.ID != cash.ID || snapshot.Quantity != cash.Quantity || snapshot.Balance != cash.Balance)
            throw new InvalidOperationException("The native cash snapshot did not preserve its definition, quantity, and dollar balance.");
        return snapshot;
    }
}
