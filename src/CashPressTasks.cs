using HarmonyLib;
using Il2CppScheduleOne;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.PlayerTasks;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.UI.Stations;
using UnityEngine;

namespace CashBricks;

internal static class CashPressTasks
{
    private sealed class Reservation
    {
        internal readonly ItemSlot Slot;
        internal readonly bool Add, Remove;
        internal bool Applied;
        internal Reservation(ItemSlot slot) { Slot = slot; Add = slot.IsAddLocked; Remove = slot.IsRemovalLocked; }
        internal void Apply()
        {
            Applied = true; // Restore even if a native callback throws halfway through.
            Slot.SetIsAddLocked(true);
            Slot.SetIsRemovalLocked(true);
        }
        internal void Release()
        {
            if (!Applied) return;
            Slot.SetIsAddLocked(Add);
            Slot.SetIsRemovalLocked(Remove);
            Applied = false;
        }
    }

    private sealed class Session
    {
        internal readonly BrickPress Press;
        internal readonly IntPtr PressPointer;
        internal readonly ProductItemInstance Proxy;
        internal readonly ItemSlot[] Inputs;
        internal readonly ItemInstance?[] Before;
        internal readonly ItemInstance? OutputBefore;
        internal readonly Reservation[] Reservations;
        internal readonly PlayerCamera? Camera;
        internal readonly Dictionary<string, int> UiBefore;
        internal string? NativeTaskName;
        internal readonly CashPressFlow Flow = new();
        internal UseBrickPress? Task;
        internal HashSet<IntPtr> Stacks = new();
        internal bool Constructing = true;
        internal bool CommitFailed;

        internal Session(BrickPress press, ProductItemInstance proxy)
        {
            Press = press;
            PressPointer = press.Pointer;
            Proxy = proxy;
            Inputs = press.ProductSlots.ToArray();
            Before = Inputs.Select(slot => CashSnapshots.Copy(slot.ItemInstance)).ToArray();
            OutputBefore = CashSnapshots.Copy(press.OutputSlot.ItemInstance);
            Reservations = Inputs.Append(press.OutputSlot).Select(slot => new Reservation(slot)).ToArray();
            Camera = PlayerCamera.Instance;
            UiBefore = CaptureUi(Camera);
        }

        internal bool Unchanged() => Inputs.Select((slot, i) => Same(slot.ItemInstance, Before[i])).All(value => value) &&
            Same(Press.OutputSlot.ItemInstance, OutputBefore);

        private static bool Same(ItemInstance? item, ItemInstance? snapshot)
        {
            if (item == null || snapshot == null) return item == null && snapshot == null;
            return item.ID == snapshot.ID && item.Quantity == snapshot.Quantity &&
                Main.AsCash(item)?.Balance == Main.AsCash(snapshot)?.Balance;
        }

        internal void Release()
        {
            foreach (var reservation in Reservations)
            {
                try { reservation.Release(); }
                catch (Exception e) { Main.Mod.LoggerInstance.Error($"Could not release a cash press slot: {e}"); }
            }
        }

        internal void CheckUiCleanup()
        {
            if (Camera == null || NativeTaskName == null) return;
            try
            {
                var after = CaptureUi(Camera);
                int expected = UiBefore.GetValueOrDefault(NativeTaskName);
                int remaining = after.GetValueOrDefault(NativeTaskName);
                bool stillOwned = Task != null && TaskManager.Instance?.CurrentTask?.Pointer == Task.Pointer;
                string state = $"nativeName='{NativeTaskName}', taskActive={Task?.TaskActive}, taskInManager={stillOwned}, " +
                    $"taskUiLocks={remaining}, before={expected}, ui=[{string.Join(", ", after.Select(pair => $"{pair.Key}:{pair.Value}"))}]";
                if (remaining > expected || stillOwned || Task?.TaskActive == true)
                    Main.Mod.LoggerInstance.Warning("Cash press task cleanup left an active task or UI lock: " + state);
                else Main.Mod.LoggerInstance.Msg("Cash press task cleanup verified: " + state);
            }
            catch (Exception e) { Main.Mod.LoggerInstance.Warning($"Could not inspect cash press UI cleanup: {e.Message}"); }
        }
    }

    private static Dictionary<string, int> CaptureUi(PlayerCamera? camera)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        if (camera == null) return counts;
        foreach (string name in camera.ActiveUIElements)
            counts[name] = counts.GetValueOrDefault(name) + 1;
        return counts;
    }

    private static readonly Dictionary<IntPtr, Session> Sessions = new();
    // Keep each small adapter instance alive for the process. Native tasks can
    // retain it across scene teardown or a repeated StopTask. This prevents
    // pointer reuse and stops late callbacks from minting the adapter's product.
    private static readonly Dictionary<IntPtr, ProductItemInstance> Proxies = new();

    internal static bool IsActive(BrickPress press) => Sessions.ContainsKey(press.Pointer);
    internal static bool BlocksBegin(BrickPress press) => Sessions.TryGetValue(press.Pointer, out var session) &&
        !session.Constructing;

    internal static ProductItemInstance CreatePhysicsAdapter()
    {
        foreach (var item in Registry.Instance.GetAllItems())
        {
            var definition = item.TryCast<ProductDefinition>();
            if (definition == null || definition.FunctionalProduct == null) continue;
            var adapter = definition.GetDefaultInstance(CashPressFlow.StackCount).TryCast<ProductItemInstance>();
            if (adapter != null) return adapter;
        }
        throw new InvalidOperationException("No native physical product prefab is available for the pouring task.");
    }

    internal static void Begin(BrickPressCanvas canvas, BrickPress press)
    {
        if (IsActive(press) || press.NPCUserObject != null || TaskManager.Instance == null || TaskManager.Instance.IsTaskActive ||
            PressPatches.CashState(press.ProductSlots, press.OutputSlot, out _, 1) != PackagingStation.EState.CanBegin)
            return;
        Session? session = null;
        try
        {
            // The adapter is used only to construct the game's draggable bucket
            // and FunctionalProducts. Real CashInstances stay in their slots.
            var adapter = CreatePhysicsAdapter();
            session = new Session(press, adapter);
            Sessions.Add(press.Pointer, session);
            Proxies.Add(adapter.Pointer, adapter);
            canvas.BeginTask(adapter);
            session.Task = TaskManager.Instance.CurrentTask?.TryCast<UseBrickPress>();
            var task = session.Task;
            if (task == null || !task.TaskActive || task.press?.Pointer != press.Pointer ||
                task.product?.Pointer != adapter.Pointer || task.container == null || task.products == null ||
                task.products.Count != CashPressFlow.StackCount)
                throw new InvalidOperationException("The native brick press did not create the expected pouring task and twenty physical stacks.");
            // Task's constructor acquires a PlayerCamera UI lock using this
            // name. StopTask removes it using TaskName again. Changing it after
            // construction leaves the original lock behind, blocking E and
            // inventory access even after the task and station UI have closed.
            session.NativeTaskName = task.TaskName;
            foreach (var stack in task.products)
            {
                if (stack == null) throw new InvalidOperationException("The native pouring task contains a missing physical stack.");
                Visuals.PhysicalCash(stack);
                session.Stacks.Add(stack.Pointer);
            }
            if (session.Stacks.Count != CashPressFlow.StackCount || !session.Unchanged())
                throw new InvalidOperationException("The cash inputs changed while the pouring task was starting.");
            foreach (var reservation in session.Reservations) reservation.Apply();
            session.Constructing = false;
            // Native CheckMould and the press stages own CurrentInstruction.
            // Writing it here or in Tick makes the two messages alternate.
            Main.Mod.LoggerInstance.Msg($"Cash press task started: twenty physical $50 stacks; native task identity '{session.NativeTaskName}' retained for UI cleanup.");
        }
        catch (Exception e)
        {
            if (session != null)
            {
                session.Flow.Cancel();
                // BeginTask can throw after its task constructor registered itself.
                var task = session.Task ?? TaskManager.Instance?.CurrentTask?.TryCast<UseBrickPress>();
                try
                {
                    if (task != null && task.press?.Pointer == press.Pointer && task.TaskActive && task.products != null)
                        task.CancelTask();
                }
                catch (Exception stopError) { Main.Mod.LoggerInstance.Error($"Cash task cleanup failed: {stopError}"); }
                session.Release();
                Sessions.Remove(press.Pointer);
                try { if (canvas.Press == null && press != null) canvas.Open(press); }
                catch (Exception openError) { Main.Mod.LoggerInstance.Error($"Could not reopen the cash press: {openError}"); }
            }
            Main.Mod.LoggerInstance.Error($"Could not start physical cash pressing: {e}");
        }
    }

    private static Session? ForTask(UseBrickPress task) => task.press != null &&
        Sessions.TryGetValue(task.press.Pointer, out var session) && task.product?.Pointer == session.Proxy.Pointer
            ? session : null;

    private static int MouldCount(Session session)
    {
        var found = new HashSet<IntPtr>();
        foreach (var stack in session.Press.GetProductInMould())
            if (stack != null && session.Stacks.Contains(stack.Pointer)) found.Add(stack.Pointer);
        return found.Count;
    }

    internal static void Tick()
    {
        foreach (var session in Sessions.Values.ToArray())
        {
            if (session.Constructing) continue;
            if (session.Press == null || session.Task == null || !session.Task.TaskActive)
            {
                session.Flow.Cancel();
                session.Release();
                Sessions.Remove(session.PressPointer);
            }
        }
    }

    internal static void Clear()
    {
        foreach (var session in Sessions.Values) { session.Flow.Cancel(); session.Release(); }
        Sessions.Clear();
        // Preserve adapter guards for any late native task callbacks.
    }

    [HarmonyPatch(typeof(BrickPress), nameof(BrickPress.GetProductInMould))]
    private static class OwnMouldContents
    {
        private static void Postfix(BrickPress __instance,
            Il2CppSystem.Collections.Generic.List<FunctionalProduct> __result)
        {
            if (!Sessions.TryGetValue(__instance.Pointer, out var session) || __result == null) return;
            // Native CheckMould disables dragging before calling BeginPress.
            // Filter before that check so another task's objects cannot advance
            // or disable this task. During construction the owned set is empty.
            for (int i = __result.Count - 1; i >= 0; i--)
                if (__result[i] == null || !session.Stacks.Contains(__result[i].Pointer)) __result.RemoveAt(i);
        }
    }

    [HarmonyPatch(typeof(UseBrickPress), "BeginPress")]
    private static class LoadedMould
    {
        private static bool Prefix(UseBrickPress __instance)
        {
            var session = ForTask(__instance);
            if (session == null) return true;
            try
            {
                if (!session.Flow.LoadMould(MouldCount(session))) return false;
                Main.Mod.LoggerInstance.Msg("Cash press mould loaded: all twenty cash stacks detected; native handle enabled.");
                return true;
            }
            catch (Exception e)
            {
                Main.Mod.LoggerInstance.Error($"Could not validate cash in the mould: {e}");
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(UseBrickPress), "FinishPress")]
    private static class PulledHandle
    {
        private static bool Prefix(UseBrickPress __instance)
        {
            var session = ForTask(__instance);
            if (session == null) return true;
            if (!session.Flow.PullHandle(session.Press.Handle.CurrentPosition >= 1f)) return false;
            Main.Mod.LoggerInstance.Msg("Cash press handle pulled to the bottom; waiting for native press completion.");
            return true;
        }
    }

    [HarmonyPatch(typeof(BrickPress), nameof(BrickPress.CompletePress))]
    private static class Complete
    {
        private static bool Prefix(BrickPress __instance, ProductItemInstance product)
        {
            if (product != null && CashEmployeePress.Complete(__instance, product)) return false;
            if (product == null || !Proxies.ContainsKey(product.Pointer)) return true;
            // Always suppress product completion for an adapter, including a
            // cancelled or already completed task. Never put it in inventory.
            if (!Sessions.TryGetValue(__instance.Pointer, out var session) || session.Proxy.Pointer != product.Pointer)
                return false;
            try
            {
                session.Flow.Commit(() =>
                {
                    if (!session.Unchanged())
                        throw new InvalidOperationException("Cash inputs or output changed during the physical press task; no cash was consumed.");
                    session.Release();
                    if (!PressPatches.Convert(session.Inputs, __instance.OutputSlot, out int count, 1) || count != 1)
                        throw new InvalidOperationException("The completed press could not store exactly one cash brick.");
                    Main.Mod.LoggerInstance.Msg("Physical cash press complete: exactly $1,000 consumed and one cash brick stored.");
                });
            }
            catch (Exception e)
            {
                session.CommitFailed = true;
                Main.Mod.LoggerInstance.Error($"Physical cash pressing failed: {e}");
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(UseBrickPress), nameof(UseBrickPress.StopTask))]
    private static class Stopped
    {
        private static void Postfix(UseBrickPress __instance)
        {
            var session = ForTask(__instance);
            if (session == null) return;
            // Native StopTask calls CompletePress AFTER its base method fires
            // onTaskStop and reopens the canvas. Cleanup must be a postfix.
            bool completed = session.Flow.State == CashPressFlow.Phase.Committed;
            session.Flow.Cancel();
            session.Release();
            Sessions.Remove(session.Press.Pointer);
            session.CheckUiCleanup();
            if (!completed)
                Main.Mod.LoggerInstance.Msg(session.CommitFailed ? "Cash press completion failed; see the transaction error above." :
                    "Cash press task cancelled; the mod did not consume input cash.");
        }
    }
}
