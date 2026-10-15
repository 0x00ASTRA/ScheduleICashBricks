using System.Globalization;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Equipping;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Storage;
using UnityEngine;
using PreviewGenerator = Il2Cpp.RuntimePreviewGenerator;

namespace CashBricks;

internal static class Visuals
{
    private static Mesh? mesh;
    private static Mesh? packedMesh;
    private static Material? material;
    private static Material? packedMaterial;
    private static Mesh? wrapMesh;
    private static Material? wrapMaterial;
    private static GameObject? pouringPrefab;
    private sealed record CashModel(GameObject Root, MeshFilter Core, MeshRenderer Renderer, GameObject? Wrap);
    private sealed record PressModels(BrickPress Press, CashModel[] Models, bool[] OriginalVisibility);
    private sealed record PackagingCash(PackagingStation Station, CashModel Model, (Renderer Renderer, bool Enabled)[] Originals);
    private static readonly Dictionary<IntPtr, PressModels> InputModels = new();
    private static readonly Dictionary<IntPtr, PackagingCash> PackagingModels = new();
    internal static string AssetDirectory => Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "CashBricks");

    internal static void Attach(StorableItemDefinition definition, Product_Equippable heldTemplate)
    {
        var created = new List<UnityEngine.Object>();
        T Track<T>(T asset) where T : UnityEngine.Object
        {
            created.Add(asset);
            return asset;
        }
        // Check the whole install before allocating persistent Unity assets.
        foreach (string name in new[] { "cashbrick_wrapped_icon.png", "cashbrick.obj", "cashbrick_packed.obj", "cashbrick_albedo.png", "cashbrick_packed_albedo.png",
            "cashbrick_wrap.obj", "cashbrick_wrap_albedo.png" })
            if (!File.Exists(Path.Combine(AssetDirectory, name)))
                throw new FileNotFoundException($"Cash Bricks asset missing. Copy the package's UserData/CashBricks folder to {AssetDirectory}.", name);
        try
        {
            mesh = Track(LoadObj(Path.Combine(AssetDirectory, "cashbrick.obj"), mirrorPrint: true));
            packedMesh = Track(LoadObj(Path.Combine(AssetDirectory, "cashbrick_packed.obj"), mirrorPrint: true));
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Cannot find a lit material shader");
            var looseTexture = Track(LoadTexture("cashbrick_albedo.png"));
            var packedTexture = Track(LoadTexture("cashbrick_packed_albedo.png"));
            ApplyNativeCashFaces(looseTexture, packedTexture);
            material = Track(new Material(shader));
            material.mainTexture = looseTexture;
            material.SetFloat("_Smoothness", 0.12f);
            material.SetFloat("_Glossiness", 0.12f);
            material.SetFloat("_Metallic", 0f);
            // Bill faces come from the live game. Keep each model's authored
            // paper edges and band art in the atlas's lower half.
            packedMaterial = Track(new Material(material) { name = "CashBrick_PackagedPaper" });
            packedMaterial.mainTexture = packedTexture;
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
            pouringPrefab = CreatePouringPrefab(prefabContainer.transform);
            var equipped = new GameObject("CashBrick_Equippable");
            equipped.transform.SetParent(prefabContainer.transform, false);
            var viewmodel = equipped.AddComponent<Equippable_Viewmodel>();
            viewmodel.CanInteractWhenEquipped = true;
            viewmodel.CanPickUpWhenEquipped = true;
            viewmodel.localPosition = heldTemplate.localPosition;
            viewmodel.localEulerAngles = heldTemplate.localEulerAngles;
            viewmodel.localScale = heldTemplate.localScale;
            // Match the native package's model pivot as well as its root pose.
            // The old 35cm-forward placement put the cash inside nearby walls.
            var nativeWrap = NativeBrickWrapper(heldTemplate);
            var grip = new GameObject("CashBrick_HeldGrip");
            grip.transform.SetParent(equipped.transform, false);
            var relative = heldTemplate.transform.worldToLocalMatrix * nativeWrap.transform.localToWorldMatrix;
            grip.transform.localPosition = relative.MultiplyPoint3x4(Vector3.zero);
            grip.transform.localRotation = Quaternion.Inverse(heldTemplate.transform.rotation) * nativeWrap.transform.rotation;
            grip.transform.localScale = relative.lossyScale;
            var heldModel = Model(grip.transform, wrapped: true);
            // The cash OBJ is Y-up; native wrapping meshes use Z for thickness.
            var rotation = Quaternion.Euler(90f, 0f, 0f);
            float fit = nativeWrap.sharedMesh.bounds.size.x / wrapMesh.bounds.size.x;
            heldModel.Root.transform.localRotation = rotation;
            heldModel.Root.transform.localScale = Vector3.one * fit;
            heldModel.Root.transform.localPosition = nativeWrap.sharedMesh.bounds.center - rotation * (wrapMesh.bounds.center * fit);
            int heldLayer = LayerMask.NameToLayer("Viewmodel");
            if (heldLayer < 0) throw new InvalidOperationException("The game's Viewmodel layer is unavailable.");
            foreach (var child in equipped.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = heldLayer;
            foreach (var renderer in equipped.GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Main.Mod.LoggerInstance.Msg($"Held cash uses native brick grip {viewmodel.localPosition}, rotation {viewmodel.localEulerAngles}, scale {viewmodel.localScale}; model fit {fit:F3}, Viewmodel layer {heldLayer}.");
            definition.Equippable = viewmodel;
            var icon = Track(CreateIcon());
            definition.Icon = Track(Sprite.Create(icon, new Rect(0, 0, icon.width, icon.height), new Vector2(0.5f, 0.5f), 100f));
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
            packedMaterial = null;
            wrapMesh = null;
            wrapMaterial = null;
            pouringPrefab = null;
            definition.StoredItem = null;
            definition.Equippable = null;
            definition.Icon = null;
            throw;
        }
    }

    internal static MeshFilter NativeBrickWrapper(Product_Equippable template) =>
        template.ModelContainer.GetComponentsInChildren<MeshFilter>(true)
            .Where(filter => filter.sharedMesh != null && filter.gameObject.name == "Wrapping")
            .OrderByDescending(filter => filter.sharedMesh.bounds.size.x)
            .FirstOrDefault() ?? throw new InvalidOperationException("The native filled brick has no wrapping mesh for its held grip.");

    internal static MeshFilter NativePouringSource() =>
        Main.CashDefinition!.Equippable.GetComponentsInChildren<MeshFilter>(true)
            .FirstOrDefault(filter => filter.sharedMesh != null && filter.sharedMesh.name == "10NoteStack" &&
                filter.GetComponent<MeshRenderer>() != null)
            ?? throw new InvalidOperationException("The native cash prefab has no 10NoteStack model.");

    private static GameObject CreatePouringPrefab(Transform parent)
    {
        var source = NativePouringSource();
        var sourceRenderer = source.GetComponent<MeshRenderer>();
        var bounds = source.sharedMesh.bounds;
        // Preserve the proven press collision footprint. Fit the native note's
        // long and short edges uniformly; adjust only its thickness to fit.
        var physicsSize = Vector3.Scale(mesh!.bounds.size, new Vector3(.55f, .2f, .55f));
        float fit = Mathf.Min(physicsSize.x / bounds.size.y, physicsSize.z / bounds.size.z);
        var scale = new Vector3(physicsSize.y / bounds.size.x, fit, fit);
        var rotation = Quaternion.Euler(0f, 0f, 90f); // native X thickness -> Y-up
        int taskLayer = LayerMask.NameToLayer("Task");
        if (taskLayer < 0) throw new InvalidOperationException("The game's Task physics layer is unavailable.");
        var root = new GameObject("CashStack_50Dollars");
        root.transform.SetParent(parent, false);
        root.layer = taskLayer;
        var visual = new GameObject("NativeCashStack");
        visual.transform.SetParent(root.transform, false);
        visual.layer = taskLayer;
        visual.transform.localRotation = rotation;
        visual.transform.localScale = scale;
        visual.transform.localPosition = -(rotation * Vector3.Scale(bounds.center, scale));
        visual.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
        var renderer = visual.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = sourceRenderer.sharedMaterials;
        renderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
        renderer.receiveShadows = sourceRenderer.receiveShadows;
        var box = root.AddComponent<BoxCollider>();
        box.center = Vector3.zero;
        box.size = physicsSize;
        Main.Mod.LoggerInstance.Msg($"Pouring cash uses native '{source.sharedMesh.name}', its {renderer.sharedMaterials.Length} original materials, and the existing press collider {physicsSize}.");
        return root;
    }

    internal static GameObject ClonePouringCash(Transform parent)
    {
        if (pouringPrefab == null) throw new InvalidOperationException("The native pouring-cash template is unavailable.");
        return UnityEngine.Object.Instantiate(pouringPrefab, parent, false);
    }

    private static void ApplyNativeCashFaces(params Texture2D[] atlases)
    {
        var native = Main.CashDefinition!.Equippable.GetComponentsInChildren<Renderer>(true)
            .SelectMany(renderer => renderer.sharedMaterials)
            .Where(candidate => candidate != null)
            .Select(candidate => candidate.mainTexture?.TryCast<Texture2D>())
            .Where(texture => texture != null && texture.name.StartsWith("BankNoteModelTex", StringComparison.Ordinal))
            .OrderByDescending(texture => texture!.width)
            .FirstOrDefault() ?? throw new InvalidOperationException("The native cash prefab has no BankNoteModelTex bill texture.");
        if (atlases.Any(atlas => atlas.width != 1024 || atlas.height != 1024))
            throw new InvalidDataException("Cash atlases must be 1024 by 1024.");

        // Native 10NoteStack front-face UV rectangle, read from the game mesh.
        // Map the authored face UVs (.01..99, .51..99) onto that rectangle.
        const float u0 = .010205254f, u1 = .991570175f, v0 = .576686919f, v1 = .991879761f;
        var scale = new Vector2((u1 - u0) / .98f, (v1 - v0) * .5f / .48f);
        var offset = new Vector2(u0 - .01f * scale.x, v0 - .01f * (v1 - v0) / .48f);
        var previousTarget = RenderTexture.active;
        bool previousSrgb = GL.sRGBWrite;
        var target = RenderTexture.GetTemporary(1024, 512, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        try
        {
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Graphics.Blit(native, target, scale, offset);
            RenderTexture.active = target;
            foreach (var atlas in atlases)
            {
                atlas.ReadPixels(new Rect(0, 0, 1024, 512), 0, 512, false);
                atlas.Apply(false, false);
            }
            Main.Mod.LoggerInstance.Msg($"Cash bill faces use native texture '{native.name}' ({native.width}x{native.height}); authored paper edges and bands retained.");
        }
        finally
        {
            RenderTexture.active = previousTarget;
            GL.sRGBWrite = previousSrgb;
            RenderTexture.ReleaseTemporary(target);
        }
    }

    internal static bool UsableIcon(Texture2D texture)
    {
        var pixels = texture.GetPixels32();
        int transparent = 0, visible = 0;
        foreach (var pixel in pixels)
        {
            if (pixel.a <= 5) transparent++;
            if (pixel.a > 128 && pixel.r + pixel.g + pixel.b > 60) visible++;
        }
        return transparent > pixels.Length / 100 && visible > pixels.Length / 100;
    }

    private static Texture2D CreateIcon()
    {
        var generator = IconGenerator.Instance;
        if (generator == null || generator.CameraPosition == null || generator.MainContainer == null ||
            generator.ItemContainer == null || generator.rendererData == null)
        {
            Main.Mod.LoggerInstance.Msg("Native icon studio is not ready; using the supplied cash-brick icon.");
            return LoadTexture("cashbrick_wrapped_icon.png");
        }
        var preview = Model(generator.ItemContainer, wrapped: true);
        var camera = PreviewGenerator.PreviewRenderCamera;
        var cameraPosition = PreviewGenerator.CamPos;
        var cameraRotation = PreviewGenerator.CamRot;
        var direction = PreviewGenerator.PreviewDirection;
        float padding = PreviewGenerator.Padding, supersampling = PreviewGenerator.RenderSupersampling;
        var background = PreviewGenerator.BackgroundColor;
        bool orthographic = PreviewGenerator.OrthographicMode, localBounds = PreviewGenerator.UseLocalBounds;
        bool nonReadable = PreviewGenerator.MarkTextureNonReadable;
        bool mainActive = generator.MainContainer.gameObject.activeSelf;
        bool itemActive = generator.ItemContainer.gameObject.activeSelf;
        var ambientMode = RenderSettings.ambientMode;
        var ambientLight = RenderSettings.ambientLight;
        var features = new List<(UnityEngine.Rendering.Universal.ScriptableRendererFeature Feature, bool Active)>();
        var nativeFeatures = generator.rendererData.rendererFeatures;
        for (int i = 0; i < nativeFeatures.Count; i++)
        {
            var feature = nativeFeatures[i];
            if (feature != null) features.Add((feature, feature.isActive));
        }
        try
        {
            PreviewGenerator.PreviewRenderCamera = null!;
            PreviewGenerator.PreviewDirection = new Vector3(-1f, -.9f, -1.2f);
            PreviewGenerator.Padding = .05f;
            PreviewGenerator.BackgroundColor = Color.clear;
            PreviewGenerator.OrthographicMode = true;
            PreviewGenerator.UseLocalBounds = false;
            PreviewGenerator.RenderSupersampling = 2f;
            PreviewGenerator.MarkTextureNonReadable = false;
            // The game customizes RuntimePreviewGenerator to require CamPos/
            // CamRot and its URP feature setup. GetTexture supplies both.
            var icon = generator.GetTexture(preview.Root.transform, 256)
                ?? throw new InvalidOperationException("The native icon renderer returned no texture.");
            if (!UsableIcon(icon))
            {
                UnityEngine.Object.Destroy(icon);
                throw new InvalidOperationException("The native icon renderer returned a blank or opaque image.");
            }
            Main.Mod.LoggerInstance.Msg("Generated a visible cash-brick inventory icon with native bill faces.");
            return icon;
        }
        catch (Exception error)
        {
            Main.Mod.LoggerInstance.Warning($"Native cash icon rendering failed; using the supplied icon: {error.Message}");
            return LoadTexture("cashbrick_wrapped_icon.png");
        }
        finally
        {
            PreviewGenerator.PreviewRenderCamera = camera;
            PreviewGenerator.CamPos = cameraPosition;
            PreviewGenerator.CamRot = cameraRotation;
            PreviewGenerator.PreviewDirection = direction;
            PreviewGenerator.Padding = padding;
            PreviewGenerator.BackgroundColor = background;
            PreviewGenerator.OrthographicMode = orthographic;
            PreviewGenerator.UseLocalBounds = localBounds;
            PreviewGenerator.RenderSupersampling = supersampling;
            PreviewGenerator.MarkTextureNonReadable = nonReadable;
            foreach (var feature in features) feature.Feature.SetActive(feature.Active);
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientLight = ambientLight;
            generator.MainContainer.gameObject.SetActive(mainActive);
            generator.ItemContainer.gameObject.SetActive(itemActive);
            UnityEngine.Object.Destroy(preview.Root);
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
        var coreRenderer = model.AddComponent<MeshRenderer>();
        coreRenderer.sharedMaterial = wrapped ? packedMaterial : material;
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
        return new CashModel(model, core, coreRenderer, wrap);
    }

    internal static void PhysicalCash(FunctionalProduct stack)
    {
        if (pouringPrefab == null || stack.Rb == null)
            throw new InvalidOperationException("Physical cash needs the native cash template and a native rigidbody.");
        foreach (var renderer in stack.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        // Keep the native FunctionalProduct/Draggable and rigidbody:
        // the task's click detection and mould detection require these components.
        foreach (var collider in stack.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        // Clone only a mesh/renderer/collider template: the original native
        // FunctionalProduct/Draggable and rigidbody continue owning the task.
        ClonePouringCash(stack.transform);
        stack.Rb.ResetCenterOfMass();
        stack.Rb.ResetInertiaTensor();
        stack.Rb.collisionDetectionMode = stack.Rb.isKinematic
            ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.ContinuousDynamic;
    }

    internal static void UpdatePress(BrickPress press, bool cashMode)
    {
        if (mesh == null || packedMesh == null || material == null || packedMaterial == null || wrapMesh == null || wrapMaterial == null) return;
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
            model.Renderer.sharedMaterial = brick ? packedMaterial : material;
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

    private static Mesh LoadObj(string path, bool mirrorPrint = false)
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
                            var textureUv = uv[int.Parse(p[1]) - 1];
                            // Reflect printed art inside its own atlas region.
                            // A whole-atlas flip would swap paper edges and band.
                            if (mirrorPrint && textureUv.y > .5f) textureUv.x = 1f - textureUv.x;
                            else if (mirrorPrint && textureUv.x > .5f) textureUv.x = 1.5f - textureUv.x;
                            meshUv.Add(textureUv);
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
