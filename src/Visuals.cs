using System.Globalization;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne;
using Il2CppScheduleOne.Equipping;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Storage;
using UnityEngine;

namespace CashBricks;

internal static class Visuals
{
    private static Mesh? mesh;
    private static Mesh? packedMesh;
    private static Material? material;
    private static Mesh? wrapMesh;
    private static Material? wrapMaterial;
    private sealed record CashModel(GameObject Root, MeshFilter Core, GameObject? Wrap);
    private sealed record PressModels(BrickPress Press, CashModel[] Models, bool[] OriginalVisibility);
    private sealed record PackagingCash(PackagingStation Station, CashModel Model, (Renderer Renderer, bool Enabled)[] Originals);
    private static readonly Dictionary<IntPtr, PressModels> InputModels = new();
    private static readonly Dictionary<IntPtr, PackagingCash> PackagingModels = new();
    internal static string AssetDirectory => Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "CashBricks");

    internal static void Attach(StorableItemDefinition definition)
    {
        var created = new List<UnityEngine.Object>();
        T Track<T>(T asset) where T : UnityEngine.Object
        {
            created.Add(asset);
            return asset;
        }
        // Check the whole install before allocating persistent Unity assets.
        foreach (string name in new[] { "cashbrick_wrapped_icon.png", "cashbrick.obj", "cashbrick_packed.obj", "cashbrick_albedo.png",
            "cashbrick_wrap.obj", "cashbrick_wrap_albedo.png" })
            if (!File.Exists(Path.Combine(AssetDirectory, name)))
                throw new FileNotFoundException($"Cash Bricks asset missing. Copy the package's UserData/CashBricks folder to {AssetDirectory}.", name);
        try
        {
            var icon = Track(LoadTexture("cashbrick_wrapped_icon.png"));
            definition.Icon = Sprite.Create(icon, new Rect(0, 0, icon.width, icon.height), new Vector2(0.5f, 0.5f), 100f);
            Track(definition.Icon);
            mesh = Track(LoadObj(Path.Combine(AssetDirectory, "cashbrick.obj")));
            packedMesh = Track(LoadObj(Path.Combine(AssetDirectory, "cashbrick_packed.obj")));
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Cannot find a lit material shader");
            material = Track(new Material(shader));
            material.mainTexture = Track(LoadTexture("cashbrick_albedo.png"));
            material.SetFloat("_Smoothness", 0.12f);
            material.SetFloat("_Glossiness", 0.12f);
            material.SetFloat("_Metallic", 0f);
            wrapMesh = Track(LoadObj(Path.Combine(AssetDirectory, "cashbrick_wrap.obj")));
            wrapMaterial = Track(CreateWrapMaterial(shader));
            wrapMaterial.mainTexture = Track(LoadTexture("cashbrick_wrap_albedo.png"));

            // Use an existing generic StoredItem prefab for its storage footprint.
            // Product/cash subclasses expect specific instance types and cannot be reused.
            foreach (var item in Registry.Instance.GetAllItems())
            {
                var candidate = item.TryCast<StorableItemDefinition>()?.StoredItem;
                if (candidate == null || candidate.GetIl2CppType().Name != "StoredItem") continue;
                var stored = UnityEngine.Object.Instantiate(candidate);
                Track(stored.gameObject);
                stored.name = "CashBrick_Stored";
                stored.gameObject.SetActive(false);
                foreach (var renderer in stored.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                Model(stored.transform, wrapped: true);
                definition.StoredItem = stored;
                break;
            }
            if (definition.StoredItem == null)
                throw new InvalidOperationException("No generic storage prefab is available for cash bricks");

            // HotbarSlot.Equip clones the prefab beneath EquipContainer, then
            // calls Equip without activating it. Keep the source hidden through
            // its parent so the cloned root retains activeSelf=true. The native
            // viewmodel supplies the held transform, layers, and unequip cleanup.
            var prefabContainer = Track(new GameObject("CashBrick_Prefabs"));
            prefabContainer.SetActive(false);
            var equipped = new GameObject("CashBrick_Equippable");
            equipped.transform.SetParent(prefabContainer.transform, false);
            var viewmodel = equipped.AddComponent<Equippable_Viewmodel>();
            viewmodel.CanInteractWhenEquipped = true;
            viewmodel.CanPickUpWhenEquipped = true;
            viewmodel.localPosition = new Vector3(0.15f, -0.18f, 0.35f);
            viewmodel.localEulerAngles = new Vector3(10f, -10f, 0f);
            viewmodel.localScale = Vector3.one;
            Model(equipped.transform, wrapped: true);
            definition.Equippable = viewmodel;
            foreach (var asset in created)
            {
                if (asset.TryCast<GameObject>() is { } gameObject) UnityEngine.Object.DontDestroyOnLoad(gameObject);
                else asset.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            }
            Main.Mod.LoggerInstance.Msg($"Loaded original loose cash ({mesh.vertexCount} vertices), compact packaged cash ({packedMesh.vertexCount} vertices), and separate plastic wrap ({wrapMesh.vertexCount} vertices), textures, and wrapped icon.");
        }
        catch
        {
            for (int i = created.Count - 1; i >= 0; i--) UnityEngine.Object.Destroy(created[i]);
            mesh = null;
            packedMesh = null;
            material = null;
            wrapMesh = null;
            wrapMaterial = null;
            definition.StoredItem = null;
            definition.Equippable = null;
            definition.Icon = null;
            throw;
        }
    }

    private static Material CreateWrapMaterial(Shader shader)
    {
        var result = new Material(shader) { name = "CashBrick_PlasticFilm", color = Color.white };
        // Match the native product brick's transparent, specular-preserving
        // film. The new atlas supplies opacity; the cash paper stays opaque.
        result.SetOverrideTag("RenderType", "Transparent");
        result.SetFloat("_Metallic", 0f);
        result.SetFloat("_Smoothness", 0.70f);
        result.SetFloat("_Glossiness", 0.70f);
        result.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        result.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        result.SetInt("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
        result.SetInt("_DstBlendAlpha", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        result.SetInt("_ZWrite", 0);
        result.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Back);
        if (result.HasProperty("_Surface"))
        {
            result.SetFloat("_Surface", 1f);
            result.SetFloat("_Blend", 0f);
            result.SetFloat("_BlendModePreserveSpecular", 1f);
            result.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        else result.SetFloat("_Mode", 3f); // Standard shader's transparent mode.
        result.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        result.DisableKeyword("_ALPHATEST_ON");
        result.DisableKeyword("_ALPHABLEND_ON");
        result.SetShaderPassEnabled("DepthOnly", false);
        result.SetShaderPassEnabled("ShadowCaster", false);
        result.SetShaderPassEnabled("SHADOWCASTER", false);
        result.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return result;
    }

    private static CashModel Model(Transform parent, bool wrapped = false)
    {
        var model = new GameObject("CashBrick_Model");
        model.transform.SetParent(parent, false);
        var core = model.AddComponent<MeshFilter>();
        core.sharedMesh = wrapped ? packedMesh : mesh;
        model.AddComponent<MeshRenderer>().sharedMaterial = material;
        GameObject? wrap = null;
        if (wrapped)
        {
            wrap = new GameObject("CashBrick_PlasticWrap");
            wrap.transform.SetParent(model.transform, false);
            wrap.AddComponent<MeshFilter>().sharedMesh = wrapMesh;
            var renderer = wrap.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = wrapMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        return new CashModel(model, core, wrap);
    }

    internal static void PhysicalCash(FunctionalProduct stack)
    {
        if (mesh == null || material == null || stack.Rb == null)
            throw new InvalidOperationException("Physical cash needs the cash mesh, material, and a native rigidbody.");
        int taskLayer = LayerMask.NameToLayer("Task");
        if (taskLayer < 0) throw new InvalidOperationException("The game's Task physics layer is unavailable.");
        foreach (var renderer in stack.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        // Keep the native FunctionalProduct/Draggable and rigidbody:
        // the task's click detection and mould detection require these components.
        foreach (var collider in stack.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        var scale = new Vector3(0.55f, 0.2f, 0.55f);
        var model = Model(stack.transform).Root;
        model.name = "CashStack_50Dollars";
        // GetProductInMould explicitly overlaps the Task layer, and resolves
        // FunctionalProduct in the collider's parents. The prefab's root layer
        // need not match its original child colliders, so put our shape on Task.
        model.layer = taskLayer;
        model.transform.localScale = scale;
        model.transform.localPosition = -Vector3.Scale(mesh.bounds.center, scale);
        var box = model.AddComponent<BoxCollider>();
        box.center = mesh.bounds.center;
        box.size = mesh.bounds.size;
        stack.Rb.ResetCenterOfMass();
        stack.Rb.ResetInertiaTensor();
        stack.Rb.collisionDetectionMode = stack.Rb.isKinematic
            ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.ContinuousDynamic;
    }

    internal static void UpdatePress(BrickPress press, bool cashMode)
    {
        if (mesh == null || packedMesh == null || material == null || wrapMesh == null || wrapMaterial == null) return;
        if (!InputModels.TryGetValue(press.Pointer, out var entry))
        {
            if (!cashMode) return;
            var models = new[] { Model(press.Container1.transform, wrapped: true), Model(press.Container2.transform, wrapped: true) };
            models[0].Root.transform.localPosition = press.Container1.transform.InverseTransformPoint(press.Container1.Contents_Min.position);
            models[1].Root.transform.localPosition = press.Container2.transform.InverseTransformPoint(press.Container2.Contents_Min.position);
            entry = new PressModels(press, models, new[]
            {
                press.Container1.ContentsContainer.gameObject.activeSelf,
                press.Container2.ContentsContainer.gameObject.activeSelf
            });
            InputModels[press.Pointer] = entry;
        }
        if (!cashMode)
        {
            ReleasePress(entry);
            InputModels.Remove(press.Pointer);
            return;
        }
        var inputModels = entry.Models;
        for (int i = 0; i < inputModels.Length; i++)
        {
            var model = inputModels[i];
            if (model.Root == null) continue;
            var item = press.ProductSlots[i].ItemInstance;
            var cash = Main.AsCash(item);
            bool brick = Main.IsBrick(item);
            model.Root.SetActive((cash != null && cash.Balance > 0) || (brick && item.Quantity > 0));
            model.Core.sharedMesh = brick ? packedMesh : mesh;
            model.Wrap!.SetActive(brick);
            var container = i == 0 ? press.Container1 : press.Container2;
            container.ContentsContainer.gameObject.SetActive(false);
            if (cash != null) model.Root.transform.localScale = new Vector3(1f, Mathf.Clamp(cash.Balance / 1000f, 0.1f, 1f), 1f);
            else if (brick) model.Root.transform.localScale = Vector3.one;
        }
    }

    private static void RestorePress(PressModels entry)
    {
        for (int i = 0; i < entry.Models.Length; i++)
        {
            if (entry.Models[i].Root != null) entry.Models[i].Root.SetActive(false);
            if (entry.Press == null) continue;
            var container = i == 0 ? entry.Press.Container1 : entry.Press.Container2;
            if (container != null && container.ContentsContainer != null)
                container.ContentsContainer.gameObject.SetActive(entry.OriginalVisibility[i]);
        }
    }

    internal static void PruneDestroyedPresses()
    {
        foreach (var key in InputModels.Where(pair => pair.Value.Press == null).Select(pair => pair.Key).ToArray())
            InputModels.Remove(key);
        foreach (var key in PackagingModels.Where(pair => pair.Value.Station == null).Select(pair => pair.Key).ToArray())
            PackagingModels.Remove(key);
    }

    internal static void UpdatePackagingCash(PackagingStation station, CashInstance? cash, int quantity)
    {
        if (cash == null)
        {
            if (PackagingModels.Remove(station.Pointer, out var previous)) ReleasePackagingCash(previous);
            return;
        }
        if (mesh == null || material == null || station.ProductAlignments == null ||
            station.ProductAlignments.Length == 0 || station.ProductAlignments[0] == null) return;
        if (!PackagingModels.TryGetValue(station.Pointer, out var entry))
        {
            var originals = station.ProductAlignments.Where(anchor => anchor != null)
                .SelectMany(anchor => anchor.GetComponentsInChildren<Renderer>(true))
                .Select(renderer => (renderer, renderer.enabled)).ToArray();
            // Stay outside the native alignment children: the game owns their
            // first child and caches its product ID for reuse/destruction.
            var model = Model(station.transform);
            model.Root.name = "CashBrick_UnpackedCash";
            var anchor = station.ProductAlignments[0];
            model.Root.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            entry = new PackagingCash(station, model, originals);
            PackagingModels[station.Pointer] = entry;
        }
        foreach (var original in entry.Originals)
            if (original.Renderer != null) original.Renderer.enabled = false;
        entry.Model.Root.SetActive(quantity > 0 && cash.Balance > 0);
        entry.Model.Root.transform.localScale = new Vector3(1, Mathf.Clamp(cash.Balance / 1000f, .1f, 1f), 1);
    }

    private static void ReleasePackagingCash(PackagingCash entry)
    {
        foreach (var original in entry.Originals)
            if (original.Renderer != null) original.Renderer.enabled = original.Enabled;
        if (entry.Model.Root != null) UnityEngine.Object.Destroy(entry.Model.Root);
    }

    internal static void ClearPressModels()
    {
        foreach (var entry in InputModels.Values) ReleasePress(entry);
        InputModels.Clear();
        foreach (var entry in PackagingModels.Values) ReleasePackagingCash(entry);
        PackagingModels.Clear();
    }

    private static void ReleasePress(PressModels entry)
    {
        RestorePress(entry);
        foreach (var model in entry.Models)
            if (model.Root != null) UnityEngine.Object.Destroy(model.Root);
    }

    private static Texture2D LoadTexture(string name)
    {
        var bytes = new Il2CppStructArray<byte>(File.ReadAllBytes(Path.Combine(AssetDirectory, name)));
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!ImageConversion.LoadImage(texture, bytes, false)) throw new InvalidDataException($"Cannot decode {name}");
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }
        catch
        {
            UnityEngine.Object.Destroy(texture);
            throw;
        }
    }

    private static Mesh LoadObj(string path)
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var uv = new List<Vector2>();
        var vertices = new List<Vector3>();
        var meshNormals = new List<Vector3>();
        var meshUv = new List<Vector2>();
        var triangles = new List<int>();
        var indexes = new Dictionary<string, int>();
        float F(string value) => float.Parse(value, CultureInfo.InvariantCulture);
        foreach (string line in File.ReadLines(path))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            switch (parts[0])
            {
                case "v": positions.Add(new Vector3(F(parts[1]), F(parts[2]), F(parts[3]))); break;
                case "vn": normals.Add(new Vector3(F(parts[1]), F(parts[2]), F(parts[3]))); break;
                case "vt": uv.Add(new Vector2(F(parts[1]), F(parts[2]))); break;
                case "f":
                    if (parts.Length != 4) throw new InvalidDataException("Cash brick OBJ must be triangulated");
                    var face = new int[3];
                    for (int i = 0; i < 3; i++)
                    {
                        string corner = parts[i + 1];
                        if (!indexes.TryGetValue(corner, out int index))
                        {
                            var p = corner.Split('/');
                            index = vertices.Count;
                            indexes.Add(corner, index);
                            vertices.Add(positions[int.Parse(p[0]) - 1]);
                            meshUv.Add(uv[int.Parse(p[1]) - 1]);
                            meshNormals.Add(normals[int.Parse(p[2]) - 1]);
                        }
                        face[i] = index;
                    }
                    // Export already rotates axes without reflection. Preserve
                    // the outward winding: native game meshes likewise have
                    // cross(edge1, edge2) aligned with their vertex normals.
                    triangles.Add(face[0]); triangles.Add(face[1]); triangles.Add(face[2]);
                    break;
            }
        }
        var result = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
        result.vertices = new Il2CppStructArray<Vector3>(vertices.ToArray());
        result.normals = new Il2CppStructArray<Vector3>(meshNormals.ToArray());
        result.uv = new Il2CppStructArray<Vector2>(meshUv.ToArray());
        result.triangles = new Il2CppStructArray<int>(triangles.ToArray());
        result.RecalculateBounds();
        return result;
    }
}
