using HarmonyLib;
using Il2CppScheduleOne.ItemFramework;

namespace CashBricks;

// Employee TakeItem calls the native virtual GetCopy before removing the source
// item, and delivery can copy it again. Fix that actual boundary, not just our
// own transaction snapshots. The native method already preserves ID/quantity.
[HarmonyPatch(typeof(CashInstance), nameof(CashInstance.GetCopy))]
internal static class CashCopyPatch
{
    private static void Postfix(CashInstance __instance, ItemInstance __result)
    {
        var copy = Main.AsCash(__result);
        if (copy == null || copy.Pointer == __instance.Pointer)
            throw new InvalidOperationException("The native cash copy did not create an independent cash instance.");
        copy.SetBalance(copy.Quantity > 0 ? __instance.Balance : 0, true);
    }
}
