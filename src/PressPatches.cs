using HarmonyLib;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.UI.Stations;

namespace CashBricks;

internal static class PressPatches
{
    private static readonly Dictionary<IntPtr, (BrickPress Press, bool Input)> PressSlots = new();
    private static readonly Dictionary<IntPtr, string> LastStatus = new();
    private static readonly HashSet<IntPtr> ConvertingOutputs = new();

    internal static void Observe(BrickPress press)
    {
        if (press.ProductSlots != null)
            foreach (var slot in press.ProductSlots)
                if (slot != null) PressSlots[slot.Pointer] = (press, true);
        if (press.OutputSlot != null) PressSlots[press.OutputSlot.Pointer] = (press, false);
    }

    internal static bool IsInput(ItemSlot? slot)
    {
        if (slot == null) return false;
        if (PressSlots.TryGetValue(slot.Pointer, out var tracked) && tracked.Press != null) return tracked.Input;
        var press = slot.SlotOwner?.TryCast<BrickPress>();
        return press != null && press.ProductSlots != null &&
            press.ProductSlots.Any(input => input != null && input.Pointer == slot.Pointer);
    }

    internal static bool IsOutput(ItemSlot? slot)
    {
        if (slot == null) return false;
        if (PressSlots.TryGetValue(slot.Pointer, out var tracked) && tracked.Press != null) return !tracked.Input;
        var press = slot.SlotOwner?.TryCast<BrickPress>();
        return press != null && press.OutputSlot != null && press.OutputSlot.Pointer == slot.Pointer;
    }

    internal static bool HasCash(BrickPress press) => press.ProductSlots != null &&
        press.ProductSlots.Any(slot => Main.AsCash(slot?.ItemInstance) != null);

    internal static bool HasBricks(BrickPress press) => press.ProductSlots != null &&
        press.ProductSlots.Any(slot => Main.IsBrick(slot?.ItemInstance));

    private static bool IsCashMode(BrickPress press) => HasCash(press) || HasBricks(press);

    internal static bool Busy(BrickPress press) => CashPressTasks.IsActive(press) || CashEmployeePress.IsActive(press);

    internal static int CashCapacity(ItemSlot output) => Main.BrickDefinition == null ||
        (output.ItemInstance != null && !Main.IsBrick(output.ItemInstance)) ? 0 :
        Math.Max(0, Main.BrickDefinition.StackLimit - output.Quantity);

    internal static PackagingStation.EState CashState(IEnumerable<ItemSlot> inputs, ItemSlot output, out int count,
        int maximumBricks = int.MaxValue)
    {
        count = 0;
        if (Main.BrickDefinition == null) return PackagingStation.EState.MissingItems;
        var balances = new List<float>();
        var seen = new HashSet<IntPtr> { output.Pointer };
        foreach (var slot in inputs)
        {
            if (!seen.Add(slot.Pointer)) return PackagingStation.EState.Mismatch;
            if (slot.IsLocked || slot.IsRemovalLocked) return PackagingStation.EState.MissingItems;
            if (slot.ItemInstance == null) continue;
            var cash = Main.AsCash(slot.ItemInstance);
            if (cash == null) return PackagingStation.EState.Mismatch;
            if (!float.IsFinite(cash.Balance) || cash.Balance < 0) return PackagingStation.EState.Mismatch;
            balances.Add(cash.Balance);
        }
        double balance = balances.Sum(value => Math.Round((double)value * 100, MidpointRounding.AwayFromZero)) / 100;
        if (balance < CashRules.DollarsPerBrick) return PackagingStation.EState.InsufficentProduct;
        // BrickPress.Awake always sets IsAddLocked on its output to prevent
        // manual deposits. Native production still writes there; this flag is
        // not a production lock. Keep it set and respect the actual slot lock.
        if (output.IsLocked) return PackagingStation.EState.OutputSlotFull;
        if (output.ItemInstance != null && !Main.IsBrick(output.ItemInstance))
            return PackagingStation.EState.Mismatch;
        // The stock output is filtered for ProductItemInstance. Cash bricks are
        // native storable items, so validate their own ID/quantity and stack limit.
        int capacity = Math.Min(CashCapacity(output), maximumBricks);
        if (capacity <= 0) return PackagingStation.EState.OutputSlotFull;
        return CashRules.TryPlanPress(balances, capacity, out count, out _)
            ? PackagingStation.EState.CanBegin : PackagingStation.EState.Mismatch;
    }

    internal static bool Convert(IEnumerable<ItemSlot> source, ItemSlot output, out int pressed,
        int maximumBricks = int.MaxValue)
    {
        var inputs = source.ToArray();
        pressed = 0;
        if (!ConvertingOutputs.Add(output.Pointer)) return false;
        try
        {
            if (CashState(inputs, output, out _, maximumBricks) != PackagingStation.EState.CanBegin) return false;
            // Stage copies before any native writes. Cash dollar balances are
            // independent from Quantity, so replicate the whole instance.
            var before = inputs.Select(slot => CashSnapshots.Copy(slot.ItemInstance)).ToArray();
            var oldOutput = CashSnapshots.Copy(output.ItemInstance);
            var balances = before.Select(item => Main.AsCash(item)?.Balance ?? 0).ToArray();
            if (!CashRules.TryPlanPress(balances, Math.Min(CashCapacity(output), maximumBricks), out int count, out var remainders))
                throw new InvalidOperationException("Cash was pressable, but the native cash snapshots could not back the planned bricks.");
            var writes = new List<(ItemSlot Slot, ItemInstance? Before, ItemInstance? After)>();
            for (int i = 0; i < inputs.Length; i++)
            {
                if (balances[i] == remainders[i]) continue;
                CashInstance? copy = null;
                if (remainders[i] > 0)
                {
                    copy = Main.AsCash(CashSnapshots.Copy(before[i]))!;
                    copy.SetBalance(remainders[i], true);
                }
                writes.Add((inputs[i], before[i], copy));
            }
            var newOutput = Main.BrickDefinition!.GetDefaultInstance(checked((oldOutput?.Quantity ?? 0) + count));
            writes.Add((output, oldOutput, newOutput));
            CashTransaction.Run(writes.Count,
                i => CashSnapshots.Store(writes[i].Slot, writes[i].After),
                i => CashSnapshots.Store(writes[i].Slot, writes[i].Before));
            pressed = count;
            return true;
        }
        finally { ConvertingOutputs.Remove(output.Pointer); }
    }

    internal static void Tick()
    {
        CashPressTasks.Tick();
        CashEmployeePress.Tick();
        foreach (var key in PressSlots.Where(pair => pair.Value.Press == null).Select(pair => pair.Key).ToArray())
            PressSlots.Remove(key);
    }

    internal static void ClearSceneState()
    {
        CashPressTasks.Clear();
        CashEmployeePress.Clear();
        PressSlots.Clear();
        LastStatus.Clear();
    }

    [HarmonyPatch(typeof(ItemSlot), nameof(ItemSlot.DoesItemMatchHardFilters))]
    private static class CashFilters
    {
        private static bool Prefix(ItemSlot __instance, ItemInstance item, ref bool __result)
        {
            if (PackagingPatches.Filter(__instance, item, out bool accepted))
            {
                __result = accepted;
                return false;
            }
            if (Main.AsCash(item) != null && IsInput(__instance))
            {
                __result = __instance.ItemInstance == null || Main.AsCash(__instance.ItemInstance) != null;
                return false;
            }
            if (Main.IsBrick(item) && IsInput(__instance))
            {
                __result = false; // Unpack completed bricks at a packaging station.
                return false;
            }
            if (Main.IsBrick(item) && IsOutput(__instance)) { __result = true; return false; }
            return true;
        }
    }

    // Do not patch CanSlotAcceptCash. Its native implementation already returns
    // true for press slots and shares a three-byte stub with hundreds of other
    // methods, including static methods. Detouring it misinterprets their
    // arguments as an ItemSlot and can crash startup. Cash restrictions belong
    // in the unique DoesItemMatchHardFilters implementation above.

    [HarmonyPatch(typeof(BrickPress), nameof(BrickPress.GetState))]
    private static class State
    {
        private static bool Prefix(BrickPress __instance, ref PackagingStation.EState __result)
        {
            Observe(__instance);
            if (!IsCashMode(__instance)) return true;
            __result = CashState(__instance.ProductSlots, __instance.OutputSlot, out _, 1);
            if (CashPressTasks.BlocksBegin(__instance)) __result = PackagingStation.EState.OutputSlotFull;
            return false;
        }
    }

    [HarmonyPatch(typeof(BrickPress), "UpdateInputVisuals")]
    private static class Inputs
    {
        private static bool Prefix(BrickPress __instance)
        {
            bool cashMode = IsCashMode(__instance);
            Visuals.UpdatePress(__instance, cashMode);
            return !cashMode;
        }
    }

    private static bool UpdateCashUI(BrickPressCanvas canvas)
    {
        var press = canvas.Press;
        if (press == null) return false;
        Observe(press);
        if (!IsCashMode(press))
        {
            LastStatus.Remove(press.Pointer);
            return false;
        }
        bool hasBricks = HasBricks(press);
        var state = CashState(press.ProductSlots, press.OutputSlot, out _, 1);
        bool busy = Busy(press);
        canvas.BeginButton.interactable = !busy && state == PackagingStation.EState.CanBegin;
        canvas.InstructionLabel.gameObject.SetActive(true);
        canvas.InstructionLabel.text = busy ? "Pressing cash..." : hasBricks
            ? "Remove cash bricks and unpack them at a packaging station" : state switch
        {
            PackagingStation.EState.CanBegin => "Begin: pour $1,000 of cash into the mould, then pull the handle",
            PackagingStation.EState.InsufficentProduct => "Add cash totalling at least $1,000",
            PackagingStation.EState.Mismatch => "Use cash only; clear the output or split very large cash balances",
            PackagingStation.EState.OutputSlotFull => "Collect the cash bricks from the output",
            _ => "Unlock the input slots to press cash"
        };
        string status = $"mode=Pack, state={state}, busy={busy}, inputs=" + string.Join(";", press.ProductSlots.Select(slot =>
            $"{slot.ItemInstance?.ID ?? "empty"}:qty={slot.Quantity}:${Main.AsCash(slot.ItemInstance)?.Balance ?? 0:R} lock={slot.IsLocked}/{slot.IsRemovalLocked}")) +
            $", output={press.OutputSlot.ItemInstance?.ID ?? "empty"}:{press.OutputSlot.Quantity}:${Main.AsCash(press.OutputSlot.ItemInstance)?.Balance ?? 0:R}, brickCapacity={CashCapacity(press.OutputSlot)}, locked={press.OutputSlot.IsLocked}, manualAddLocked={press.OutputSlot.IsAddLocked}";
        if (!LastStatus.TryGetValue(press.Pointer, out var previous) || previous != status)
        {
            LastStatus[press.Pointer] = status;
            Main.Mod.LoggerInstance.Msg("Cash press UI: " + status);
        }
        return true;
    }

    [HarmonyPatch(typeof(BrickPressCanvas), "UpdateUI")]
    private static class Instructions
    {
        private static bool Prefix(BrickPressCanvas __instance) => !UpdateCashUI(__instance);
    }

    [HarmonyPatch(typeof(BrickPressCanvas), "Update")]
    private static class FrameUI
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(BrickPressCanvas __instance) => UpdateCashUI(__instance);
    }

    [HarmonyPatch(typeof(BrickPressCanvas), nameof(BrickPressCanvas.BeginButtonPressed))]
    private static class Begin
    {
        private static bool Prefix(BrickPressCanvas __instance)
        {
            var press = __instance.Press;
            if (press == null || !IsCashMode(press)) return true;
            if (Busy(press) || HasBricks(press)) return false;
            CashPressTasks.Begin(__instance, press);
            return false;
        }
    }
}
