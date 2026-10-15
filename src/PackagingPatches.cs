using HarmonyLib;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;

namespace CashBricks;

internal static class PackagingPatches
{
    // Both packaging station versions inherit these slots and unpack methods.
    // Native SetMode keeps the output slot as the package source in Unpackage.
    internal static bool Filter(ItemSlot slot, ItemInstance item, out bool accepted)
    {
        accepted = false;
        var station = slot.SlotOwner?.TryCast<PackagingStation>();
        if (station == null) return false;
        bool source = station.OutputSlot != null && slot.Pointer == station.OutputSlot.Pointer;
        bool contents = station.ProductSlot != null && slot.Pointer == station.ProductSlot.Pointer;
        bool packaging = station.PackagingSlot != null && slot.Pointer == station.PackagingSlot.Pointer;
        if (!source && !contents && !packaging) return false;
        if (Main.IsBrick(item))
        {
            accepted = source && (slot.ItemInstance == null || Main.IsBrick(slot.ItemInstance));
            return true;
        }
        if (Main.AsCash(item) != null)
        {
            accepted = contents && (slot.ItemInstance == null || Main.AsCash(slot.ItemInstance) != null);
            return true;
        }
        return false;
    }

    internal static bool HasCashItems(PackagingStation station) =>
        Main.IsBrick(station.OutputSlot?.ItemInstance) || Main.IsBrick(station.ProductSlot?.ItemInstance) ||
        Main.AsCash(station.ProductSlot?.ItemInstance) != null;

    [HarmonyPatch(typeof(PackagingStation), nameof(PackagingStation.GetState))]
    private static class State
    {
        private static bool Prefix(PackagingStation __instance, PackagingStation.EMode mode,
            ref PackagingStation.EState __result)
        {
            if (!HasCashItems(__instance)) return true;
            __result = CashUnpacking.State(__instance, mode);
            return false;
        }
    }

    [HarmonyPatch(typeof(PackagingStation), nameof(PackagingStation.Unpack))]
    private static class Unpack
    {
        private static bool Prefix(PackagingStation __instance)
        {
            if (!HasCashItems(__instance)) return true;
            try { CashUnpacking.Unpack(__instance); }
            catch (Exception e) { Main.Mod.LoggerInstance.Error($"Cash brick unpacking at the packaging station failed: {e}"); }
            return false;
        }
    }

    // CashDefinition has no ProductDefinition/FunctionalProduct. Intercept the
    // actual visual implementation, including calls through its no-arg wrapper.
    [HarmonyPatch(typeof(PackagingStation), nameof(PackagingStation.UpdateProductVisuals), new[] { typeof(int) })]
    private static class ContentsVisuals
    {
        private static bool Prefix(PackagingStation __instance, int quantity)
        {
            var cash = Main.AsCash(__instance.ProductSlot?.ItemInstance);
            Visuals.UpdatePackagingCash(__instance, cash, quantity);
            return cash == null;
        }
    }
}
