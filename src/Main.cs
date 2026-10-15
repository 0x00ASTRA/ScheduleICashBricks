using HarmonyLib;
using Il2CppScheduleOne;
using Il2CppScheduleOne.Core.Items.Framework;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Storage;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(CashBricks.Main), "Cash Bricks", "1.0.12", "Astra")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace CashBricks;

public sealed class Main : MelonMod
{
    public const string BrickId = "astra_cashbrick";
    internal static Main Mod = null!;
    internal static StorableItemDefinition? BrickDefinition;
    internal static CashDefinition? CashDefinition;
    private float nextCheck;
    private bool testsRan;
    private bool registering;
    private string? lastRegistrationError;

    public override void OnInitializeMelon()
    {
        Mod = this;
        LoggerInstance.Msg("$1,000 per cash brick; pack at the brick press and unpack in a packaging station's Unpack mode.");
        LoggerInstance.Msg("Employee cash transfers preserve dollar balances; native employee press cycles pack one $1,000 brick each.");
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName) => EnsureRegistered();

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        PressPatches.Tick();
        Visuals.PruneDestroyedPresses();
    }

    public override void OnUpdate()
    {
        if (Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + 1f;
            EnsureRegistered();
            Visuals.PruneDestroyedPresses();
        }

        PressPatches.Tick();
    }

    internal static bool IsBrick(ItemInstance? item) => item != null && item.ID == BrickId;
    internal static CashInstance? AsCash(ItemInstance? item) => item?.TryCast<CashInstance>();

    internal static void EnsureRegistered()
    {
        if (Mod == null || Mod.registering || Registry.Instance == null)
            return;
        Mod.registering = true;
        try
        {
            if (BrickDefinition != null && Registry.ItemExists(BrickId)) return;
            CashDefinition = Registry.Instance._GetItem("cash", false)?.TryCast<CashDefinition>();
            if (CashDefinition == null)
                return;

            int stackLimit = 0;
            foreach (var item in Registry.Instance.GetAllItems())
            {
                var product = item.TryCast<ProductDefinition>();
                if (product == null || product.ValidPackaging == null)
                    continue;
                foreach (var packaging in product.ValidPackaging)
                {
                    if (packaging == null || packaging.ID != "brick")
                        continue;
                    var sample = product.GetDefaultInstance(1).TryCast<ProductItemInstance>();
                    if (sample == null)
                        continue;
                    sample.SetPackaging(packaging);
                    stackLimit = sample.StackLimit;
                    break;
                }
                if (stackLimit > 0) break;
            }
            if (stackLimit <= 0) return; // Never guess the live product stack limit.

            if (BrickDefinition == null)
            {
                var definition = ScriptableObject.CreateInstance<StorableItemDefinition>();
                try
                {
                    definition.name = "CashBrick";
                    definition.ID = BrickId;
                    definition.Name = "Cash Brick ($1,000)";
                    definition.Description = "A pressed brick containing $1,000 cash. Use a packaging station's Unpack mode: put bricks in the output slot and collect loose cash from the product slot.";
                    definition.Category = EItemCategory.Cash;
                    definition.legalStatus = ELegalStatus.Legal;
                    definition.StackLimit = stackLimit;
                    definition.UsableInFilters = true;
                    definition.BasePurchasePrice = CashRules.DollarsPerBrick;
                    definition.ResellMultiplier = 0f;
                    definition.EquipMode = ItemDefinition.EEquipMode.Legacy;
                    Visuals.Attach(definition);
                    definition.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                    BrickDefinition = definition;
                }
                catch
                {
                    UnityEngine.Object.Destroy(definition);
                    throw;
                }
            }
            BrickDefinition.StackLimit = stackLimit;
            if (!Registry.ItemExists(BrickId))
            {
                Registry.Instance.AddToRegistry(BrickDefinition);
                Mod.LoggerInstance.Msg($"Registered cash brick: $1,000 each, {stackLimit} per stack (same as product bricks).");
            }
            Mod.lastRegistrationError = null;
            if (!Mod.testsRan && Environment.GetCommandLineArgs().Contains("--cashbricks-selftest"))
            {
                Mod.testsRan = true;
                try { RuntimeChecks.Run(); }
                catch (Exception e) { Mod.LoggerInstance.Error($"Cash Bricks native self-test failed: {e}"); }
            }
        }
        catch (Exception e)
        {
            if (Mod.lastRegistrationError != e.Message)
            {
                Mod.lastRegistrationError = e.Message;
                Mod.LoggerInstance.Error($"Cash brick initialization failed (will retry): {e}");
            }
        }
        finally { Mod.registering = false; }
    }
}

[HarmonyPatch(typeof(Registry), "Start")]
internal static class RegisterItemsPatch
{
    private static void Postfix() => Main.EnsureRegistered();
}

[HarmonyPatch(typeof(Registry), nameof(Registry.RemoveRuntimeItems))]
internal static class ReregisterItemsPatch
{
    private static void Postfix()
    {
        PressPatches.ClearSceneState();
        Visuals.ClearPressModels();
        Main.EnsureRegistered();
    }
}

[HarmonyPatch(typeof(StorableItemInstance), nameof(StorableItemInstance.GetMonetaryValue))]
internal static class BrickValuePatch
{
    private static void Postfix(StorableItemInstance __instance, ref float __result)
    {
        if (Main.IsBrick(__instance)) __result = (float)__instance.Quantity * CashRules.DollarsPerBrick;
    }
}
