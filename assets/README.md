# Blender cash assets

Original procedural fictional currency, authored and rendered in Blender 5.2.0 LTS.
The 1.0.7 art revision follows the installed game's cash visual language: pale ivory
paper, a bold charcoal frame, sparse green seals, and an exaggerated cartoon portrait.
The fictional Hyland teller, lettering, seals, geometry, and atlas are original.
Native `BankNoteModelTex` was inspected locally as a visual reference only; no game
textures, mesh data, or extracted reference files are included or required to rebuild.

Six lightly beveled paper packets and three loose top sheets give the brick readable
edges. Two ivory wrappers have simple green marks and no monetary value: the runtime
uses this asset both for a $1,000 brick and scaled physical $50 pouring stacks.
The cosmetic note faces say 10; the runtime determines the actual item value.

Runtime files:

- `cashbrick.obj`: one combined triangulated mesh, 408 positions / 748 triangles,
  explicit per-corner UVs and flat normals. Ordinary `v`, `vt`, `vn`, `f` records;
  no MTL dependency. Assign the supplied atlas to one opaque material.
- `cashbrick_albedo.png`: 1024 × 1024 sRGB RGBA atlas; every pixel opaque.
- `cashbrick_icon.png`: 256 × 256 RGBA transparent three-quarter Cycles render.

The OBJ uses meters and Unity Y up, is centered in XZ, and rests at Y=0.
The preexisting runtime envelope is preserved: X ±0.080000, Y 0–0.045325,
Z ±0.038150. Overall size is 0.160000 × 0.045325 × 0.076300 m (X/Y/Z).
At runtime scale (0.55, 0.20, 0.55), pouring stacks measure
0.088000 × 0.009065 × 0.041965 m.
The Blender mesh uses Z up; export applies `(x,y,z) -> (x,z,-y)`, a proper
rotation preserving standard OBJ counterclockwise exterior winding. An importer
must account for Unity's triangle/front-face convention as needed.

UVs use the ordinary OBJ bottom-left origin and stay in [0,1]. The upper atlas half
holds the note face; lower left holds cut paper edges; lower right holds the wrapper.
There is one material with roughness 0.88, metallic 0, and no normal map. The flat
palette deliberately avoids engraving noise so it remains readable at inventory size.

Editable and reproducible source:

- `blender/cashbrick.blend`: mesh with one UV layer, packed atlas, camera, and lights.
- `blender/build_assets.py`: deterministic geometry, procedural raster artwork,
  custom OBJ export, Cycles renders, and assertions for the asset contract.
- `blender/cashbrick_preview.png`: 1024 × 1024 transparent full-brick inspection render.
- `blender/cashstack_preview.png`: 512 × 512 transparent inspection render at pouring scale.
- `blender/validation.json`: mesh counts, exact-envelope checks, UV/material checks,
  nondegenerate triangles, opaque atlas, and scaled dimensions.

Rebuild from any directory (replace the absolute path if the project moves):

```sh
ALSOFT_DRIVERS=null blender --background --python /home/astra/Projects/Games/ScheduleICashBricks/assets/blender/build_assets.py
```

Only Blender's bundled Python and NumPy are required. No downloaded art, external
font, image-generation service, asset bundle, or game installation is needed.
Blender may report an optional extension-manager warning or a cache-thumbnail write
warning under restricted permissions; neither prevents the asset files or renders.
These previews validate the asset appearance; final lighting depends on the game scene.

## Packaged cash: separate plastic film (1.0.8)

Packaged bricks combine the compact `cashbrick_packed.obj` core with the unchanged
`cashbrick_albedo.png` artwork and a separate original film shell. Loose notes and $50 pouring stacks keep
all of the approved 1.0.7 assets and use no film. The packaged icon is separate too.

- `cashbrick_wrap.obj`: closed, triangulated folded-film shell, Unity Y up,
  one UV atlas and one transparent material; no MTL dependency.
- `cashbrick_wrap_albedo.png`: 1024 × 1024 straight-alpha sRGB RGBA film atlas.
  Neutral translucent gray with sparse pale creases, tucked short-end folds,
  and a narrow underside heat-seal. The central portrait remains unobstructed.
- `cashbrick_wrapped_icon.png`: 256 × 256 transparent packaged-brick icon.
- `blender/cashbrick_wrapped.blend`: compact packaged core plus approved film shell,
  packed images, camera, and lights.
- `blender/cashbrick_wrapped_preview.png`: 1024 × 1024 inspection render.
- `blender/wrapped_validation.json`: shell bounds/counts, manifold/UV/triangle
  checks, core enclosure, material settings, and original-art SHA-256 hashes.
- `blender/build_wrapped_assets.py`: independent deterministic packaging build.
  Calls the packaged-core builder, then reads the approved wrapper OBJ/atlas without
  regenerating them. Verifies that all eight original loose-cash assets plus both
  wrapper files remain byte-for-byte unchanged. It never runs `build_assets.py`.

The installed game's product `Wrapping` mesh and `brick plastic wrapping mat`
were inspected read-only. Their broad faceted film shape and transparent material
informed the original shell. No extracted native geometry, pixels, or normal map
is shipped or needed to rebuild. The game's material uses gray film at alpha
approximately 0.204, smoothness 0.70, and preserved specular reflections. Our atlas
provides its own opacity (mostly 0.20, higher at creases), so use **white RGBA
material tint (1,1,1,1)**; multiplying by the native alpha again would hide the film.

Runtime film material: URP/Lit transparent, metallic 0, smoothness 0.70,
backface culling, depth writes off, queue 3000. Preserve specular with
`_ALPHAPREMULTIPLY_ON` / `_SURFACE_TYPE_TRANSPARENT`, source blend One,
destination OneMinusSrcAlpha (same factors for alpha), `_Blend=0`,
`_BlendModePreserveSpecular=1`, `_Surface=1`, `_Cull=2`, `_ZWrite=0`.
Disable shadow/depth-only passes on the shell. Do not enable alpha clipping.
The Standard fallback should use its transparent/premultiplied mode, smoothness
0.70, metallic 0, and the same atlas/tint. No custom shader or normal texture is needed.

The shell extends approximately 1–1.6 millimeters beyond the original cash envelope
and slightly below its base. Keep the original core at the same local origin;
use the core's existing collider and placement footprint. See the validation JSON
for exact bounds. Blender previews approximate the runtime film with preserved
surface reflection; exact highlights depend on in-game lighting and shader.

Rebuild packaging only:

```sh
ALSOFT_DRIVERS=null blender --background --python /home/astra/Projects/Games/ScheduleICashBricks/assets/blender/build_wrapped_assets.py
```

## Compact packaged paper sides

`cashbrick_packed.obj` replaces the six thick internal packets with one continuous
43.65mm paper body. Its sides map continuously to the existing paper-edge atlas:
approximately 31 subtle printed lines at 1.421mm pitch, with no physical air gaps
or internal horizontal note faces. A 0.12mm outer bevel catches light without
creating large shelves. The three approved 0.3mm top sheets and all wrapper-band
geometry/UVs are copied exactly, preserving the existing portrait and bands.

The core has 288 positions and 528 triangles, one opaque material, and the same
X ±0.080000, Y 0–0.045325, Z ±0.038150 meter envelope. Only completed packaged
bricks use this mesh; loose cash continues to use `cashbrick.obj`. The approved
plastic shell mesh and atlas are reused without any byte changes.

- `blender/build_packed_assets.py`: independently rebuilds the compact core.
- `blender/cashbrick_packed.blend`: editable compact-core source scene.
- `blender/packed_validation.json`: bounds, UVs, triangles, dimensions, spacing,
  retained geometry counts, and protected asset SHA-256 hashes.
- `blender/cashbrick_packed_sides_preview.png`: low-angle packaged inspection
  render for comparison with the supplied brick and native wallet-stack screenshots.

Run `build_wrapped_assets.py` (command above) to rebuild the packed core and all
packaged previews/icons together, or run `build_packed_assets.py` with the same
Blender command to rebuild just the packaged core. Export winding stays standard
OBJ counterclockwise with outward normals; the runtime must preserve the native
mesh winding rather than invert every triangle.
