using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Persistence.ItemLoaders;
using Il2CppScheduleOne.Product;
using UnityEngine;

namespace CashBricks;

internal static class RuntimeChecks
{
    internal static void Run(Product_Equippable heldTemplate)
    {
        int passed = 0;
        void Check(bool ok, string name)
        {
            if (!ok) throw new InvalidOperationException("SELFTEST FAILED: " + name);
            passed++;
            Main.Mod.LoggerInstance.Msg("SELFTEST PASS: " + name);
        }
        ItemSlot Cash(float balance)
        {
            var cash = Main.AsCash(Main.CashDefinition!.GetDefaultInstance(1))!;
            cash.SetBalance(balance, true);
            var slot = new ItemSlot();
            slot.SetStoredItem(cash);
            return slot;
        }
        ItemSlot Bricks(int quantity)
        {
            var slot = new ItemSlot();
            slot.SetStoredItem(Main.BrickDefinition!.GetDefaultInstance(quantity));
            return slot;
        }
        float Balance(ItemSlot slot) => Main.AsCash(slot.ItemInstance)?.Balance ?? 0;
        Check(!PressPatches.IsInput(null) && !PressPatches.IsOutput(null),
            "Uninitialized slots are never classified as press slots");
        Check(!PressPatches.IsInput(new ItemSlot()) && !PressPatches.IsOutput(new ItemSlot()),
            "Detached ordinary slots are never classified as press slots");
        Check(new ItemSlot().CanSlotAcceptCash(),
            "Native cash acceptance remains intact without detouring its shared stub");
        foreach (float amount in new[] { 0.01f, 650.25f, 1000f, CashInstance.MAX_BALANCE })
        {
            var original = Cash(amount).ItemInstance;
            var nativeCopy = Main.AsCash(original.GetCopy())
                ?? throw new InvalidOperationException("SELFTEST FAILED: Native cash GetCopy returned a non-cash item.");
            Check(nativeCopy.Pointer != original.Pointer && nativeCopy.Balance == amount &&
                nativeCopy.Quantity == original.Quantity && nativeCopy.ID == original.ID,
                $"Native virtual cash GetCopy preserves ${amount} for employee transfers");
            var movedCopy = Main.AsCash(original.GetCopy(1))!;
            Check(movedCopy != null && movedCopy.Balance == amount && movedCopy.Quantity == 1,
                "Employee quantity-overridden copies preserve the entire dollar balance without scaling it");
            nativeCopy.SetBalance(0, true);
            Check(Main.AsCash(original)!.Balance == amount, "Native copied cash is independent of its source");
            var snapshot = Main.AsCash(CashSnapshots.Copy(original))!;
            Check(snapshot.Balance == amount && snapshot.Quantity == original.Quantity && snapshot.ID == original.ID,
                $"Native cash snapshot preserves ${amount} separately from item quantity");
            snapshot.SetBalance(0, true);
            Check(Main.AsCash(original)!.Balance == amount,
                "Changing a cash snapshot never changes the source cash");
        }
        var savedCash = Cash(650.25f).ItemInstance.GetItemData();
        var loadedCash = Main.AsCash(new CashLoader().LoadItem(savedCash.GetJson()));
        Check(loadedCash != null && loadedCash.Balance == 650.25f && loadedCash.Quantity == 1,
            "Native CashData JSON round trip preserves the dollar balance");
        var transferInputs = new List<ItemSlot>();
        foreach (float amount in new[] { 650.25f, 500.50f })
        {
            var source = Cash(amount);
            var carried = source.ItemInstance.GetCopy(1);
            source.ChangeQuantity(-1);
            var employeeInventory = new ItemSlot();
            employeeInventory.InsertItem(carried);
            var delivered = employeeInventory.ItemInstance.GetCopy(1);
            employeeInventory.ChangeQuantity(-1);
            var destination = new ItemSlot();
            destination.InsertItem(delivered);
            Check(source.ItemInstance == null && employeeInventory.ItemInstance == null && Balance(destination) == amount,
                "Native source/carry/delivery copies conserve an employee's loose cash to the cent");
            transferInputs.Add(destination);
        }
        var employeeOutput = new ItemSlot();
        employeeOutput.SetIsAddLocked(true);
        var employeeCycle = new CashEmployeeCycle();
        Check(!CashEmployeePress.Commit(employeeCycle, transferInputs, employeeOutput) && employeeOutput.ItemInstance == null,
            "Employee cash cannot complete before the native completion callback offers its token");
        employeeCycle.Offer();
        Check(CashEmployeePress.Commit(employeeCycle, transferInputs, employeeOutput) && employeeOutput.Quantity == 1 &&
            Balance(transferInputs[0]) + Balance(transferInputs[1]) == 150.75f && employeeOutput.IsAddLocked,
            "Employee completion turns transferred $1,150.75 into one brick and $150.75 while retaining output deposit restrictions");
        Check(!CashEmployeePress.Commit(employeeCycle, transferInputs, employeeOutput) && employeeOutput.Quantity == 1 &&
            Balance(transferInputs[0]) + Balance(transferInputs[1]) == 150.75f,
            "A repeated employee completion cannot consume cash or create another brick");
        var cancelledEmployeeCycle = new CashEmployeeCycle();
        cancelledEmployeeCycle.Offer();
        cancelledEmployeeCycle.Cancel();
        Check(!CashEmployeePress.Commit(cancelledEmployeeCycle, new[] { Cash(1000) }, new ItemSlot()),
            "A cancelled employee completion cannot write any inventory");
        var perCycleInputs = new[] { Cash(3000), new ItemSlot() };
        var perCycleOutput = new ItemSlot();
        for (int cycleIndex = 1; cycleIndex <= 3; cycleIndex++)
        {
            var nextCycle = new CashEmployeeCycle();
            nextCycle.Offer();
            Check(CashEmployeePress.Commit(nextCycle, perCycleInputs, perCycleOutput) && perCycleOutput.Quantity == cycleIndex &&
                Balance(perCycleInputs[0]) == 3000 - cycleIndex * 1000,
                "Consecutive native employee cycles each convert exactly $1,000, never an entire source stack at once");
        }
        var inputs = new[] { Cash(1000), Cash(1000) };
        var output = new ItemSlot();
        Check(PressPatches.Convert(inputs, output, out int n) && n == 2 && output.Quantity == 2,
            "Two full cash stacks produce two bricks");
        Check(Balance(inputs[0]) + Balance(inputs[1]) == 0 && output.ItemInstance.GetMonetaryValue() == 2000,
            "Pressing preserves $2,000 exactly");
        inputs = new[] { Cash(650.25f), Cash(500.50f) };
        output = new ItemSlot();
        Check(PressPatches.Convert(inputs, output, out n) && n == 1 && Balance(inputs[1]) == 150.75f,
            "Combines partial cash and preserves the exact remainder");
        inputs = new[] { Cash(999.99f), new ItemSlot() };
        Check(!PressPatches.Convert(inputs, new ItemSlot(), out _) && Balance(inputs[0]) == 999.99f,
            "Insufficient cash remains untouched");
        inputs = new[] { Cash(600.10f), Cash(399.90f) };
        Check(PressPatches.Convert(inputs, new ItemSlot(), out n) && n == 1 && inputs.All(slot => slot.ItemInstance == null),
            "Native decimal cash stacks totalling $1,000 produce a brick");
        inputs = new[] { Cash(1000), Cash(1000) };
        output = new ItemSlot();
        output.SetIsAddLocked(true);
        Check(PressPatches.CashState(inputs, output, out n, 1) == PackagingStation.EState.CanBegin && n == 1 &&
            Balance(inputs[0]) + Balance(inputs[1]) == 2000 && output.ItemInstance == null && output.IsAddLocked,
            "Real press output policy permits Begin while manual deposits remain blocked and cash is untouched");
        output.ActiveLock = new ItemSlotLock(output, null, "Cash Bricks detached self-test");
        Check(output.IsLocked && !PressPatches.Convert(inputs, output, out _, 1) &&
            Balance(inputs[0]) + Balance(inputs[1]) == 2000 && output.ItemInstance == null,
            "An actual output slot lock still blocks production without consuming cash");
        output.ActiveLock = null;
        Check(PressPatches.Convert(inputs, output, out n, 1) && n == 1 && output.Quantity == 1 &&
            Balance(inputs[0]) + Balance(inputs[1]) == 1000 && output.IsAddLocked,
            "Native production stores one cash brick without unlocking manual output deposits");
        output.SetStoredItem(Main.BrickDefinition!.GetDefaultInstance(Main.BrickDefinition.StackLimit));
        Check(!PressPatches.Convert(inputs, output, out _, 1) && Balance(inputs[1]) == 1000 && output.IsAddLocked,
            "A genuinely full output still blocks production with the native manual deposit restriction set");
        inputs = new[] { Cash(1000), Cash(1000) };
        output = new ItemSlot();
        inputs[0].SetIsRemovalLocked(true);
        Check(!PressPatches.Convert(inputs, output, out _) && Balance(inputs[0]) == 1000,
            "Removal-locked input never consumes cash");
        inputs[0].SetIsRemovalLocked(false);
        Check(!PressPatches.Convert(new[] { inputs[0], inputs[0] }, output, out _) && Balance(inputs[0]) == 1000,
            "Duplicate slot references cannot count the same cash twice");
        Check(!PressPatches.Convert(inputs, inputs[0], out _) && Balance(inputs[0]) == 1000,
            "Output cannot alias a cash input");
        output.SetStoredItem(Main.CashDefinition!.GetDefaultInstance(1));
        Check(!PressPatches.Convert(inputs, output, out _) && Balance(inputs[0]) + Balance(inputs[1]) == 2000,
            "Non-brick output never consumes cash");
        output.ClearStoredInstance();
        inputs = new[] { Cash(1000), Cash(1000) };
        output.SetStoredItem(Main.BrickDefinition!.GetDefaultInstance(Main.BrickDefinition.StackLimit - 1));
        Check(PressPatches.Convert(inputs, output, out n) && n == 1 && output.Quantity == Main.BrickDefinition.StackLimit && Balance(inputs[1]) == 1000,
            "Only consumes money for the available brick stack capacity");
        Check(!PressPatches.Convert(inputs, output, out _) && Balance(inputs[1]) == 1000,
            "Full output never consumes cash");
        inputs = new[] { Cash(1000), Cash(1000) };
        output = new ItemSlot();
        Check(PressPatches.Convert(inputs, output, out n, 1) && n == 1 && output.Quantity == 1 && Balance(inputs[1]) == 1000,
            "One physical press cycle consumes only $1,000 even when $2,000 is loaded");
        Check(PressPatches.Convert(inputs, output, out n, 1) && n == 1 && output.Quantity == 2 && Balance(inputs[1]) == 0,
            "A second physical cycle consumes the remaining $1,000");
        inputs = new[] { Bricks(2), Bricks(1) };
        output = new ItemSlot();
        output.SetIsAddLocked(true);
        Check(CashUnpacking.State(inputs, output, out float unpackedBalance) == PackagingStation.EState.CanBegin &&
            unpackedBalance == 1000 && inputs[0].Quantity == 2 && inputs[1].Quantity == 1 && output.ItemInstance == null,
            "Loading cash bricks enables station unpacking without consuming them before clicking");
        Check(CashUnpacking.Unpack(inputs, output) && inputs[0].Quantity == 1 && inputs[1].Quantity == 1 &&
            Balance(output) == 1000 && output.Quantity == 1 && output.IsAddLocked,
            "One station click unpacks exactly one brick into native loose cash and retains the output deposit restriction");
        Check(CashUnpacking.Unpack(inputs, output) && CashUnpacking.Unpack(inputs, output) &&
            inputs.All(slot => slot.ItemInstance == null) && Balance(output) == 3000 && output.Quantity == 1,
            "Repeated station unpacking preserves all $3,000 across brick stacks using CashInstance.Balance");
        Check(!CashUnpacking.Unpack(inputs, output) && Balance(output) == 3000,
            "Clicking again with no bricks cannot produce more cash");
        inputs = new[] { Bricks(1), new ItemSlot() };
        output = Cash(123.45f);
        var existingCash = Main.AsCash(output.ItemInstance)!;
        Check(CashUnpacking.Unpack(inputs, output) && Balance(output) == 1123.45f && existingCash.Balance == 123.45f,
            "Unpacking adds exactly $1,000 to decimal output cash without mutating its previous instance");
        inputs = new[] { Bricks(1), Cash(1000) };
        Check(!CashUnpacking.Unpack(inputs, new ItemSlot()) && inputs[0].Quantity == 1 && Balance(inputs[1]) == 1000,
            "Mixed loose cash and brick inputs block unpacking without changing either input");
        inputs[1] = new ItemSlot();
        Check(!CashUnpacking.Unpack(inputs, Bricks(1)) && inputs[0].Quantity == 1,
            "An output containing a brick blocks unpacking");
        output = new ItemSlot();
        output.ActiveLock = new ItemSlotLock(output, null, "Cash Bricks detached unpack test");
        Check(!CashUnpacking.Unpack(inputs, output) && inputs[0].Quantity == 1 && output.ItemInstance == null,
            "An actual output lock blocks unpacking without consuming a brick");
        output.ActiveLock = null;
        inputs[0].SetIsRemovalLocked(true);
        Check(!CashUnpacking.Unpack(inputs, output) && inputs[0].Quantity == 1 && output.ItemInstance == null,
            "Removal-locked bricks cannot be unpacked");
        inputs[0].SetIsRemovalLocked(false);
        Check(!CashUnpacking.Unpack(new[] { inputs[0], inputs[0] }, output) && inputs[0].Quantity == 1,
            "Unpacking rejects duplicate input slot references");
        Check(!CashUnpacking.Unpack(inputs, inputs[0]) && inputs[0].Quantity == 1,
            "An unpacking output cannot alias its brick input");
        foreach (float blockedBalance in new[] { CashInstance.MAX_BALANCE, 134_217_728f })
        {
            output = Cash(blockedBalance);
            Check(!CashUnpacking.Unpack(inputs, output) && inputs[0].Quantity == 1 && Balance(output) == blockedBalance,
                "Full or imprecise cash output balances cannot consume a brick");
        }
        inputs = new[] { Cash(600.10f), Cash(399.90f) };
        var packed = new ItemSlot();
        output = new ItemSlot();
        Check(PressPatches.Convert(inputs, packed, out n, 1) && n == 1 &&
            CashUnpacking.Unpack(new[] { packed, new ItemSlot() }, output) &&
            packed.ItemInstance == null && Balance(output) == 1000 && inputs.All(slot => slot.ItemInstance == null),
            "A native station pack/unpack round trip conserves exactly $1,000");
        foreach (bool upgraded in new[] { false, true })
        {
            // Never activate this station: avoid Awake, networking, placement,
            // NPCs, player tasks, and saved stations. Exercise the native hooks
            // with independent slots, then destroy the inactive component.
            var probe = new GameObject("CashBrick_PackagingRoutingSelftest");
            probe.SetActive(false);
            try
            {
                PackagingStation station = upgraded
                    ? probe.AddComponent<Il2CppScheduleOne.Packaging.PackagingStationMk2>()
                    : probe.AddComponent<PackagingStation>();
                string kind = upgraded ? "Mk2" : "Mk1";
                station.OutputSlot = Bricks(2);
                station.ProductSlot = Cash(123.45f);
                station.PackagingSlot = new ItemSlot();
                station.OutputSlot.AddFilter(new ItemFilter_PackagedProduct());
                station.ProductSlot.AddFilter(new ItemFilter_UnpackagedProduct());
                var owner = station.Cast<IItemSlotOwner>();
                var slots = new[] { station.PackagingSlot, station.ProductSlot, station.OutputSlot };
                foreach (var slot in slots) slot._SlotOwner_k__BackingField = owner;
                try
                {
                    var brick = Main.BrickDefinition!.GetDefaultInstance(1);
                    Check(station.OutputSlot.DoesItemMatchHardFilters(brick),
                        $"{kind} native packaged-product slot accepts cash bricks through its station filter");
                    Check(!station.ProductSlot.DoesItemMatchHardFilters(brick) && !station.PackagingSlot.DoesItemMatchHardFilters(brick),
                        $"{kind} accepts cash bricks only in the native package-source slot");
                    var loose = Cash(1).ItemInstance;
                    Check(station.ProductSlot.DoesItemMatchHardFilters(loose) &&
                        !station.OutputSlot.DoesItemMatchHardFilters(loose) && !station.PackagingSlot.DoesItemMatchHardFilters(loose),
                        $"{kind} routes loose cash only to the native contents slot");
                }
                finally
                {
                    // Detached transactions must not send inventory RPCs.
                    foreach (var slot in slots) slot._SlotOwner_k__BackingField = null!;
                }
                Check(station.GetState(PackagingStation.EMode.Unpackage) == PackagingStation.EState.CanBegin &&
                    station.GetState(PackagingStation.EMode.Package) != PackagingStation.EState.CanBegin,
                    $"{kind} enables native Unpack mode and blocks packaging tasks for cash");
                station.Unpack();
                Check(station.OutputSlot.Quantity == 1 && Balance(station.ProductSlot) == 1123.45f &&
                    station.PackagingSlot.ItemInstance == null,
                    $"{kind} native Unpack removes one output brick and credits the product slot without returning plastic");
                station.Unpack();
                station.Unpack();
                Check(station.OutputSlot.ItemInstance == null && Balance(station.ProductSlot) == 2123.45f &&
                    station.GetState(PackagingStation.EMode.Unpackage) == PackagingStation.EState.MissingItems,
                    $"{kind} repeated native Unpack conserves cash and stops when its package source is empty");
                station.OutputSlot = Bricks(1);
                station.ProductSlot = Cash(CashInstance.MAX_BALANCE);
                station.Unpack();
                Check(station.OutputSlot.Quantity == 1 && Balance(station.ProductSlot) == CashInstance.MAX_BALANCE &&
                    station.GetState(PackagingStation.EMode.Unpackage) == PackagingStation.EState.ProductSlotFull,
                    $"{kind} full cash contents block unpacking with the native contents-capacity state");
            }
            finally { UnityEngine.Object.Destroy(probe); }
        }
        Check(PressPatches.CashState(new[] { Bricks(1), new ItemSlot() }, new ItemSlot(), out _) == PackagingStation.EState.Mismatch,
            "Legacy cash bricks in press inputs never enable cash packing or unpacking");
        Check(Main.BrickDefinition!.Equippable.CanInteractWhenEquipped && Main.BrickDefinition.Equippable.CanPickUpWhenEquipped,
            "The held cash brick allows normal interaction and pickup");
        var heldPrefab = Main.BrickDefinition.Equippable.gameObject;
        var cashViewmodel = Main.BrickDefinition.Equippable.TryCast<Il2CppScheduleOne.Equipping.Equippable_Viewmodel>()!;
        Check(Vector3.Distance(cashViewmodel.localPosition, heldTemplate.localPosition) < .000001f &&
            Quaternion.Angle(Quaternion.Euler(cashViewmodel.localEulerAngles), Quaternion.Euler(heldTemplate.localEulerAngles)) < .001f &&
            Vector3.Distance(cashViewmodel.localScale, heldTemplate.localScale) < .000001f,
            "The cash brick copies the native product brick's held pose and scale");
        Check(heldPrefab.activeSelf && !heldPrefab.activeInHierarchy,
            "The held template stays hidden while preserving an active clone root");
        var heldProbe = new GameObject("CashBrick_HeldCloneSelftest");
        heldProbe.SetActive(false);
        try
        {
            // Match HotbarSlot.Equip's native Instantiate(GameObject, parent),
            // without selecting a slot or calling Equip on player inventory.
            var heldClone = UnityEngine.Object.Instantiate(heldPrefab, heldProbe.transform);
            Check(heldClone.activeSelf && !heldClone.activeInHierarchy,
                "Native held-item cloning retains activeSelf beneath an inactive test parent");
            var heldRenderers = heldClone.GetComponentsInChildren<MeshRenderer>(true);
            Check(heldRenderers.Length == 2 && heldRenderers.All(renderer =>
                renderer.enabled && renderer.sharedMaterial != null &&
                renderer.GetComponent<MeshFilter>()?.sharedMesh != null),
                "The cloned held brick retains both the cash and plastic-wrap meshes and materials");
            var runtimeAtlas = heldRenderers.Single(renderer => renderer.GetComponent<MeshFilter>().sharedMesh.name == "cashbrick_packed")
                .sharedMaterial.mainTexture.TryCast<Texture2D>()!;
            var authoredAtlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                var bytes = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>(
                    File.ReadAllBytes(Path.Combine(Visuals.AssetDirectory, "cashbrick_packed_albedo.png")));
                if (!ImageConversion.LoadImage(authoredAtlas, bytes, false)) throw new InvalidDataException("Cannot decode authored cash atlas.");
                var original = authoredAtlas.GetPixels32();
                var composited = runtimeAtlas.GetPixels32();
                bool preserved = original.Length == composited.Length, replaced = false;
                for (int i = 0; i < original.Length && preserved; i++)
                {
                    var a = original[i]; var b = composited[i];
                    bool same = a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
                    if (i < 1024 * 512) preserved &= same;
                    else replaced |= !same;
                }
                Check(preserved && replaced,
                    "Native bill faces replace the authored face pixels while preserving every paper-edge and purple-band pixel");
            }
            finally { UnityEngine.Object.Destroy(authoredAtlas); }
            Check(Visuals.UsableIcon(Main.BrickDefinition.Icon.texture),
                "The cash brick inventory icon has a visible model and transparent background");
            Check(heldClone.GetComponentsInChildren<Transform>(true).All(child => child.gameObject.layer == LayerMask.NameToLayer("Viewmodel")) &&
                heldRenderers.All(renderer => renderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off),
                "Held cash and plastic use the native viewmodel layer without casting shadows on the world");
            // Evaluate actual imported mesh corners at the equipped pose without
            // equipping an item, activating a camera, or changing player state.
            heldClone.transform.localPosition = cashViewmodel.localPosition;
            heldClone.transform.localRotation = Quaternion.Euler(cashViewmodel.localEulerAngles);
            heldClone.transform.localScale = cashViewmodel.localScale;
            var nativeWrap = Visuals.NativeBrickWrapper(heldTemplate);
            var nativeMatrix = Matrix4x4.TRS(heldTemplate.localPosition, Quaternion.Euler(heldTemplate.localEulerAngles), heldTemplate.localScale) *
                heldTemplate.transform.worldToLocalMatrix * nativeWrap.transform.localToWorldMatrix;
            var cashWrap = heldRenderers.Single(renderer => renderer.GetComponent<MeshFilter>().sharedMesh.name == "cashbrick_wrap");
            var cashMatrix = heldProbe.transform.worldToLocalMatrix * cashWrap.transform.localToWorldMatrix;
            var nativeBounds = nativeWrap.sharedMesh.bounds;
            var cashBounds = cashWrap.GetComponent<MeshFilter>().sharedMesh.bounds;
            Check(Vector3.Distance(cashMatrix.MultiplyPoint3x4(cashBounds.center), nativeMatrix.MultiplyPoint3x4(nativeBounds.center)) < .00001f,
                "The held cash brick is centered on the native brick's grip instead of drifting left");
            float nativeForward = float.NegativeInfinity, cashForward = float.NegativeInfinity;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                var corner = new Vector3(x, y, z);
                nativeForward = Mathf.Max(nativeForward, nativeMatrix.MultiplyPoint3x4(nativeBounds.center + Vector3.Scale(nativeBounds.extents, corner)).z);
                cashForward = Mathf.Max(cashForward, cashMatrix.MultiplyPoint3x4(cashBounds.center + Vector3.Scale(cashBounds.extents, corner)).z);
            }
            Check(cashForward <= nativeForward + .005f,
                "Held cash stays within the native brick's forward reach instead of extending into nearby walls");
            foreach (var renderer in heldRenderers)
            {
                var heldMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                var vertices = heldMesh.vertices;
                var normals = heldMesh.normals;
                var triangles = heldMesh.triangles;
                bool outward = triangles.Length > 0;
                for (int i = 0; i < triangles.Length && outward; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    outward = Vector3.Dot(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]),
                        normals[a] + normals[b] + normals[c]) > 0;
                }
                Check(outward, $"Held {heldMesh.name} triangles face outward, matching native game mesh winding");
            }
            // Toggle synchronously, then hide again before any frame renders.
            heldProbe.SetActive(true);
            Check(heldClone.activeInHierarchy && heldRenderers.All(renderer => renderer.gameObject.activeInHierarchy),
                "Both held meshes become active under an active equip-style parent");
            heldProbe.SetActive(false);
            Check(heldPrefab.activeSelf && !heldPrefab.activeInHierarchy,
                "Activating a held clone leaves the persistent source hidden");
        }
        finally
        {
            heldProbe.SetActive(false);
            UnityEngine.Object.Destroy(heldProbe);
        }
        var adapter = CashPressTasks.CreatePhysicsAdapter();
        Check(adapter.Quantity == CashPressFlow.StackCount && adapter.Definition.TryCast<ProductDefinition>()?.FunctionalProduct != null,
            "The native pouring adapter has twenty units and a physical product prefab");
        var pouringProbe = new GameObject("CashBrick_NativePouringSelftest");
        pouringProbe.SetActive(false);
        try
        {
            var clone = Visuals.ClonePouringCash(pouringProbe.transform);
            var filter = clone.GetComponentsInChildren<MeshFilter>(true).Single();
            var renderer = filter.GetComponent<MeshRenderer>();
            var source = Visuals.NativePouringSource();
            var originalMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
            Check(filter.sharedMesh.Pointer == source.sharedMesh.Pointer &&
                renderer.sharedMaterials.Select(value => value.Pointer).SequenceEqual(originalMaterials.Select(value => value.Pointer)),
                "Pouring cash uses the base game's exact stack mesh, UVs, and original paper, bill-face and band materials");
            var collider = clone.GetComponentsInChildren<BoxCollider>(true).Single();
            Check(Vector3.Distance(collider.center, Vector3.zero) < .000001f &&
                Vector3.Distance(collider.size, new Vector3(.088f, .009065f, .041965f)) < .000001f &&
                collider.enabled && clone.GetComponentsInChildren<Transform>(true).All(child => child.gameObject.layer == LayerMask.NameToLayer("Task")),
                "Native cash stacks preserve the existing press collision footprint and Task-layer drag detection");
            var meshBounds = filter.sharedMesh.bounds;
            var transform = clone.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            bool inside = true;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                var point = transform.MultiplyPoint3x4(meshBounds.center + Vector3.Scale(meshBounds.extents, new Vector3(x, y, z)));
                var delta = point - collider.center;
                var half = collider.size * .5f + Vector3.one * .000001f;
                inside &= Mathf.Abs(delta.x) <= half.x && Mathf.Abs(delta.y) <= half.y && Mathf.Abs(delta.z) <= half.z;
            }
            Check(inside && clone.GetComponentsInChildren<Rigidbody>(true).Length == 0 && clone.GetComponentsInChildren<MonoBehaviour>(true).Length == 0,
                "The native stack visual fits its collider without adding rigidbodies or cash-inventory behaviours to the press task");
        }
        finally { UnityEngine.Object.Destroy(pouringProbe); }
        var item = Main.BrickDefinition.GetDefaultInstance(3);
        var copy = CashSnapshots.Copy(item)!;
        Check(Main.IsBrick(copy) && copy.Quantity == 3 && copy.StackLimit == Main.BrickDefinition.StackLimit,
            "Native item copies retain brick quantity and stack limit");
        var data = item.GetItemData();
        Check(data.ID == Main.BrickId && data.Quantity == 3,
            "Native save data retains cash brick ID and quantity");
        var loaded = new ItemLoader().LoadItem(data.GetJson());
        Check(Main.IsBrick(loaded) && loaded.Quantity == 3 && loaded.GetMonetaryValue() == 3000,
            "Native JSON save/load round-trip retains $3,000 in bricks");
        Main.Mod.LoggerInstance.Msg($"SELFTEST COMPLETE: {passed} checks passed; no player/save data touched.");
    }
}
