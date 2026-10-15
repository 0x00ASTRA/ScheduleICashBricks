# Cash models and textures

[README](../README.md) · [Building](../docs/building.md) · [Testing](../docs/testing.md)

The current packaged brick has dense paper sides, three thin top sheets, one
centered white-and-purple `$1000` band, and a separate plastic shell. Native bill
faces are read from the installed game at runtime. Pouring stacks use the game's
`10NoteStack` mesh and original paper, bill-face, and blue-and-white band materials.
No extracted native mesh, texture atlas, or asset bundle is distributed.

## Runtime files

| File | Purpose |
| --- | --- |
| `cashbrick.obj` | Original loose-cash previews at the press and packaging station |
| `cashbrick_albedo.png` | Original loose atlas; bill-face region replaced at runtime |
| `cashbrick_packed.obj` | Packaged core: 192 positions, 352 triangles |
| `cashbrick_packed_albedo.png` | Packaged atlas with the centered purple/white band |
| `cashbrick_wrap.obj` | Approved folded-film shell, unchanged |
| `cashbrick_wrap_albedo.png` | Film opacity and crease atlas, unchanged |
| `cashbrick_wrapped_icon.png` | Packaged toolbar-icon fallback |
| `cashbrick_icon.png` | Retained original loose icon |

The packaged core keeps its 0.160000 × 0.045325 × 0.076300 m envelope, centered in
XZ with its base at Y=0. The 41mm-wide band crosses the portrait at X=0 and wraps
its entire height. There are no gaps in the main paper body. The plastic shell
extends approximately 1–1.6mm beyond the paper and slightly below its base.

Native pouring geometry is fitted inside the existing 0.088000 × 0.009065 ×
0.041965 m press collider. Only the mesh and renderer materials are reused;
the press task keeps its native draggable object, rigidbody, and twenty-piece
completion flow. Visual band labels do not determine cash values.

## Texture and importer contract

The two paper atlases are 1024 × 1024 sRGB RGBA. Their upper halves contain
original fictional bill faces as authored placeholders; lower-left quadrants
contain paper edges, and lower-right quadrants contain band art. At startup,
`Visuals.ApplyNativeCashFaces` reads the installed `BankNoteModelTex` through a
GPU blit and replaces only the face region. The native crop and destination UVs
are recorded in `blender/packed_validation.json`. Paper-edge and band pixels
remain unchanged. No game textures are required to rebuild the authored assets.

The OBJ files use meters, Unity Y up, per-corner UVs/normals, and outward triangle
winding. Export maps Blender coordinates `(x,y,z)` to `(x,z,-y)`. The runtime
preserves triangle order and reflects printed UVs inside their atlas regions
to correct the face and band lettering. Native pouring geometry uses its own
unaltered UVs and materials.

Paper uses URP/Lit, white tint, metallic 0, and smoothness 0.12. Plastic uses
URP/Lit transparent with preserved specular, white RGBA tint, metallic 0,
smoothness 0.70, backface culling, queue 3000, and depth writes off. Its atlas
provides opacity; multiplying by another translucent tint would hide the film.
The Standard shader fallback uses equivalent transparent settings. Held meshes
use the native Viewmodel layer and product-brick grip without casting shadows
on the world.

The toolbar icon uses the native icon studio when ready and accepts only a
visible render with a transparent background. The supplied fallback is an
original-art Blender render, so it can differ from the live native bill faces.

## Editable Blender sources

Authored with Blender 5.2.0 LTS; only Blender's bundled Python and NumPy are needed.
Run from the repository root:

```sh
ALSOFT_DRIVERS=null blender --background --python assets/blender/build_wrapped_assets.py
```

This rebuilds the packaged core, band atlas, editable scenes, fallback icon, and
authored inspection renders. It reads the approved wrapper without rewriting
it and verifies ten protected original assets byte for byte. Run
`build_packed_assets.py` with the same command to rebuild only the packaged core.
`build_assets.py` is the historical loose-asset builder and is not part of the
packaged rebuild.

The `.blend` files keep packed original artwork. `packed_validation.json`
records dimensions, retained sheets, band placement, UVs, triangle checks, and
protected hashes. `wrapped_validation.json` records film enclosure, manifold
checks, bounds, and material settings. Inspection PNGs under `blender/` show
the authored fallback artwork.

## README renders

The current model renders live in `docs/images/` and are included in the release
ZIP so the extracted README displays them. They show native faces and the
actual pouring mesh; lighting is a Blender approximation of the game. They do
not use the player's older screenshots.

To regenerate them from locally inspected native references:

```sh
ALSOFT_DRIVERS=null blender --background --python assets/blender/render_readme_previews.py -- \
  --native-face /tmp/native-BankNoteModelTex.png \
  --native-band /tmp/native-cash-band-100.png \
  --native-stack /tmp/native-10NoteStack-mesh.json
```

The JSON contains `vertices`, `uv`, and `indices` (triangles grouped by submesh).
The renderer mirrors the runtime fit, face crop, printed-UV correction, and
material order. It writes only the two finished PNGs; native reference files
and the composited atlas stay local and are not saved into a Blender scene.
