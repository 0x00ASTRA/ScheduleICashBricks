using HarmonyLib;
using Il2CppFishNet;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.NPCs.Behaviour;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Product;

namespace CashBricks;

internal static class CashEmployeePress
{
    private sealed class Session
    {
        internal readonly BrickPress Press;
        internal readonly BrickPressBehaviour Worker;
        internal readonly ProductItemInstance Adapter;
        internal readonly CashEmployeeCycle Cycle = new();
        internal Session(BrickPress press, BrickPressBehaviour worker)
        {
            Press = press;
            Worker = worker;
            Adapter = CashPressTasks.CreatePhysicsAdapter();
        }

        internal bool Current => Press != null && Worker != null && Worker.PackagingInProgress &&
            Worker.Press?.Pointer == Press.Pointer && Press.NPCUserObject != null &&
            Worker.Npc?.NetworkObject?.Pointer == Press.NPCUserObject.Pointer && Press.PlayerUserObject == null;
    }

    private static readonly Dictionary<IntPtr, Session> Sessions = new();
    // Keep adapter identity guards across completion and scene teardown, just
    // like manual press adapters. A late native callback must never mint drugs.
    private static readonly Dictionary<IntPtr, ProductItemInstance> Adapters = new();

    internal static bool IsActive(BrickPress press) => Sessions.TryGetValue(press.Pointer, out var session) && session.Current;

    internal static void Tick()
    {
        foreach (var pair in Sessions.ToArray())
        {
            if (pair.Value.Current) continue;
            if (pair.Value.Cycle.State != CashEmployeeCycle.Phase.Complete)
                Main.Mod.LoggerInstance.Msg("Cash employee press ended without consuming cash (cancelled or no longer eligible).");
            pair.Value.Cycle.Cancel();
            Sessions.Remove(pair.Key);
        }
    }

    internal static void Clear()
    {
        foreach (var session in Sessions.Values) session.Cycle.Cancel();
        Sessions.Clear();
    }

    internal static bool Commit(CashEmployeeCycle cycle, IEnumerable<ItemSlot> inputs, ItemSlot output) => cycle.Commit(() =>
    {
        if (!PressPatches.Convert(inputs, output, out int count, 1) || count != 1)
            throw new InvalidOperationException("The employee's cash inputs or output are no longer eligible; no cash brick was produced.");
    });

    // True means this is our adapter: always suppress the game's product write,
    // even when a stale or repeated callback no longer has an active session.
    internal static bool Complete(BrickPress press, ProductItemInstance product)
    {
        if (!Adapters.ContainsKey(product.Pointer)) return false;
        if (!InstanceFinder.IsServer || !Sessions.TryGetValue(press.Pointer, out var session) ||
            session.Adapter.Pointer != product.Pointer || !session.Current || CashPressTasks.IsActive(press)) return true;
        try
        {
            if (Commit(session.Cycle, press.ProductSlots, press.OutputSlot))
                Main.Mod.LoggerInstance.Msg("Cash employee press complete: exactly $1,000 consumed and one cash brick stored; native employee cleanup retained.");
        }
        catch (Exception e) { Main.Mod.LoggerInstance.Error($"Cash employee press transaction failed: {e}"); }
        return true;
    }

    [HarmonyPatch(typeof(BrickPressBehaviour), nameof(BrickPressBehaviour.RpcLogic___BeginPackaging_2166136261))]
    private static class Started
    {
        private static void Prefix(BrickPressBehaviour __instance, out bool __state) => __state = !__instance.PackagingInProgress;

        private static void Postfix(BrickPressBehaviour __instance, bool __state)
        {
            var press = __instance.Press;
            if (!__state || !InstanceFinder.IsServer || !__instance.PackagingInProgress || press == null ||
                !PressPatches.HasCash(press) || PressPatches.HasBricks(press) || CashPressTasks.IsActive(press)) return;
            try
            {
                if (Sessions.Remove(press.Pointer, out var old)) old.Cycle.Cancel();
                var session = new Session(press, __instance);
                Adapters.Add(session.Adapter.Pointer, session.Adapter);
                Sessions.Add(press.Pointer, session);
                Main.Mod.LoggerInstance.Msg("Cash employee press started: native work duration, animation and station ownership retained; cash held until completion.");
            }
            catch (Exception e) { Main.Mod.LoggerInstance.Error($"Could not prepare the employee's cash press completion: {e}"); }
        }
    }

    [HarmonyPatch(typeof(BrickPress), nameof(BrickPress.HasSufficientProduct))]
    private static class SufficientCash
    {
        private static bool Prefix(BrickPress __instance, ref ProductItemInstance product, ref bool __result)
        {
            bool tracked = Sessions.TryGetValue(__instance.Pointer, out var session);
            if (!tracked && !PressPatches.HasCash(__instance) && !PressPatches.HasBricks(__instance)) return true;
            product = null!;
            __result = false;
            if (!InstanceFinder.IsServer || !tracked || !session!.Current || CashPressTasks.IsActive(__instance) ||
                PressPatches.CashState(__instance.ProductSlots, __instance.OutputSlot, out _, 1) != PackagingStation.EState.CanBegin ||
                !session.Cycle.Offer()) return false;
            // Native employee coroutine calls HasSufficientProduct only after
            // its work and press animation finish. Return a typed token here;
            // CompletePress above converts real cash, never this dummy product.
            product = session.Adapter;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(BrickPressBehaviour), nameof(BrickPressBehaviour.IsStationReady))]
    private static class Readiness
    {
        private static bool Prefix(BrickPress press, ref bool __result)
        {
            // Bricks saved in press inputs by older versions remain removable.
            // They must not start an employee product-packing animation.
            if (press == null || !PressPatches.HasBricks(press)) return true;
            __result = false;
            return false;
        }
    }
}
