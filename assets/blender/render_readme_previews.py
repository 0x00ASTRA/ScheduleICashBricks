"""Render current runtime visuals using local native references; export PNGs only.

Run with Blender --background --python this_file -- --help for the input format.
Native reference images/mesh data and the composited atlas stay outside the repo.
"""
import argparse
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector
import numpy as np


ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--native-face', required=True, type=Path, help='Local BankNoteModelTex PNG')
parser.add_argument('--native-band', required=True, type=Path, help='Local Band_100 PNG')
parser.add_argument('--native-stack', required=True, type=Path,
                    help='Local 10NoteStack JSON: vertices, uv, indices (triangles per submesh)')
parser.add_argument('--output', type=Path, default=ROOT / 'docs/images')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
args.output.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'assets/blender/cashbrick_wrapped.blend'))
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = 64
scene.cycles.use_denoising = True
scene.render.film_transparent = True
scene.render.image_settings.color_mode = 'RGBA'
scene.render.resolution_x = scene.render.resolution_y = 900
scene.render.resolution_percentage = 100


def pixels(image):
    width, height = image.size
    values = np.empty(width * height * 4, dtype=np.float32)
    image.pixels.foreach_get(values)
    return values.reshape((height, width, 4))


def render(name, location, target, scale):
    camera = scene.camera
    camera.location = location
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = scale
    scene.render.filepath = str(args.output / name)
    bpy.ops.render.render(write_still=True)


native = bpy.data.images.load(str(args.native_face.resolve()), check_existing=False)
native_pixels = pixels(native)
authored = bpy.data.images.load(str(ROOT / 'assets/cashbrick_packed_albedo.png'), check_existing=False)
atlas = pixels(authored)
contract = json.loads((ROOT / 'assets/blender/packed_validation.json').read_text())['native_face_contract']
umin, vmin = contract['source_uv_min']
umax, vmax = contract['source_uv_max']
nw, nh = native.size
u = np.clip(((np.arange(1024) + .5) / 1024 - .01) / .98, 0, 1)
v = np.clip(((np.arange(512, 1024) + .5) / 1024 - .51) / .48, 0, 1)
xx = (umin + u * (umax - umin)) * (nw - 1)
yy = (vmin + v * (vmax - vmin)) * (nh - 1)
x0, y0 = np.floor(xx).astype(int), np.floor(yy).astype(int)
x1, y1 = np.minimum(x0 + 1, nw - 1), np.minimum(y0 + 1, nh - 1)
tx, ty = (xx - x0)[None, :, None], (yy - y0)[:, None, None]
a = native_pixels[y0[:, None], x0[None, :]] * (1 - tx) + native_pixels[y0[:, None], x1[None, :]] * tx
b = native_pixels[y1[:, None], x0[None, :]] * (1 - tx) + native_pixels[y1[:, None], x1[None, :]] * tx
atlas[512:] = a * (1 - ty) + b * ty
atlas[:, :, 3] = 1
composite = bpy.data.images.new('Preview-only native cash composite', width=1024, height=1024, alpha=True)
composite.colorspace_settings.name = 'sRGB'
composite.pixels.foreach_set(atlas.ravel())
core = bpy.data.objects['CashBrickPacked']
for node in core.data.materials[0].node_tree.nodes:
    if node.type == 'TEX_IMAGE':
        node.image = composite
for entry in core.data.uv_layers.active.data:
    # Match Visuals.LoadObj(mirrorPrint: true), including the corrected band.
    if entry.uv.y > .5:
        entry.uv.x = 1 - entry.uv.x
    elif entry.uv.x > .5:
        entry.uv.x = 1.5 - entry.uv.x
render('cash-brick.png', (.22, -.27, .25), (0, 0, .022), .215)

# Reuse lighting but remove the packaged model. No native gameplay components
# are imported: this is the exact mesh/material layout used by the pouring clone.
for obj in list(scene.objects):
    if obj.type == 'MESH':
        bpy.data.objects.remove(obj, do_unlink=True)
data = json.loads(args.native_stack.read_text())
source = np.asarray(data['vertices'])
minimum, maximum = source.min(axis=0), source.max(axis=0)
size, center = maximum - minimum, (maximum + minimum) * .5
physics = np.array([.088, .009065, .041965])
fit = min(physics[0] / size[1], physics[2] / size[2])
scaled = (source - center) * [physics[1] / size[0], fit, fit]
# Native X thickness -> Unity Y up, then Unity -> Blender Z up.
vertices = [(-y, -z, x) for x, y, z in scaled]
faces, material_indices = [], []
for material_index, triangles in enumerate(data['indices']):
    faces.extend(triangles)
    material_indices.extend([material_index] * len(triangles))
mesh = bpy.data.meshes.new('Native 10NoteStack preview only')
mesh.from_pydata(vertices, [], faces)
mesh.update()
stack = bpy.data.objects.new('Native pouring stack preview only', mesh)
scene.collection.objects.link(stack)


def material(name, tint, image=None):
    result = bpy.data.materials.new(name)
    result.use_nodes = True
    bsdf = result.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*tint, 1)
    bsdf.inputs['Roughness'].default_value = .85
    if image:
        tex = result.node_tree.nodes.new('ShaderNodeTexImage')
        tex.image = image
        multiply = result.node_tree.nodes.new('ShaderNodeMixRGB')
        multiply.blend_type = 'MULTIPLY'
        multiply.inputs[0].default_value = 1
        multiply.inputs[2].default_value = (*tint, 1)
        result.node_tree.links.new(tex.outputs['Color'], multiply.inputs[1])
        result.node_tree.links.new(multiply.outputs[0], bsdf.inputs['Base Color'])
    return result


band = bpy.data.images.load(str(args.native_band.resolve()), check_existing=False)
mesh.materials.append(material('Native paper tint', (.745098, .760784, .603922)))
mesh.materials.append(material('Native bill tint', (.862949, .886792, .807316), native))
mesh.materials.append(material('Native band', (1, 1, 1), band))
uv = mesh.uv_layers.new(name='Native UVs')
for polygon, material_index in zip(mesh.polygons, material_indices):
    polygon.material_index = material_index
    for loop_index in polygon.loop_indices:
        uv.data[loop_index].uv = data['uv'][mesh.loops[loop_index].vertex_index]
render('pouring-stack.png', (.13, -.17, .14), (0, 0, 0), .12)
print('README previews rendered; no native reference assets or scene saved.')
