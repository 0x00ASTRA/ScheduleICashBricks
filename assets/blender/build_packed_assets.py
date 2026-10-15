"""Build only the contiguous packaged-cash core; approved loose art stays intact."""
from pathlib import Path
import bpy, json, hashlib
import numpy as np

OUT=Path(__file__).resolve().parents[1]
PROTECTED=['cashbrick.obj','cashbrick_albedo.png','cashbrick_icon.png',
 'blender/build_assets.py','blender/cashbrick.blend','blender/cashbrick_preview.png',
 'blender/cashstack_preview.png','blender/validation.json',
 'cashbrick_wrap.obj','cashbrick_wrap_albedo.png']
def hashes():return {p:hashlib.sha256((OUT/p).read_bytes()).hexdigest() for p in PROTECTED}
before=hashes()
bpy.ops.wm.open_mainfile(filepath=str(OUT/'blender/cashbrick.blend'))
bpy.context.preferences.filepaths.save_version=0
old=bpy.data.objects['CashBrick'];source=old.data;mat=source.materials[0]
# Identify disconnected paper/band components by topology, not vertex ordering.
adj={v.index:set() for v in source.vertices}
for edge in source.edges:
 a,b=edge.vertices;adj[a].add(b);adj[b].add(a)
remaining=set(adj);components=[]
while remaining:
 seed=remaining.pop();component={seed};pending=[seed]
 while pending:
  for other in adj[pending.pop()]:
   if other in remaining:remaining.remove(other);component.add(other);pending.append(other)
 components.append(component)
remove=set();removed=0
for comp in components:
 points=np.array([source.vertices[i].co[:] for i in comp]);size=np.ptp(points,axis=0)
 if size[0]>.1 and size[2]>.005:
  remove.update(comp);removed+=1
assert removed==6, 'Expected exactly six broad paper packets in approved source'
# Copy approved top sheets and straps, including all original per-corner UVs.
verts=[];faces=[];uvs=[];mapping={}
for poly in source.polygons:
 if any(i in remove for i in poly.vertices):continue
 indices=[];coords=[]
 for li in poly.loop_indices:
  vi=source.loops[li].vertex_index
  if vi not in mapping:mapping[vi]=len(verts);verts.append(tuple(source.vertices[vi].co))
  indices.append(mapping[vi]);coords.append(tuple(source.uv_layers.active.data[li].uv))
 faces.append(indices);uvs.append(coords)
preserved_vertices=len(verts);preserved_triangles=len(faces)
# Single solid body fills the entire space below the three approved top sheets.
# 0.12mm bevel is enough to catch light without creating shelf-like paper gaps.
bpy.ops.mesh.primitive_cube_add(size=1,location=(0,0,.04365/2))
body=bpy.context.object;body.name='Contiguous packaged paper body';body.dimensions=(.16,.075,.04365)
bpy.ops.object.transform_apply(location=True,rotation=False,scale=True)
bevel=body.modifiers.new('Fine compressed paper corners','BEVEL');bevel.width=.00012;bevel.segments=1
bpy.ops.object.modifier_apply(modifier=bevel.name)
tri=body.modifiers.new('Runtime triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name)
start=len(verts);verts.extend(tuple(v.co) for v in body.data.vertices)
for poly in body.data.polygons:
 indices=[];coords=[]
 for vi in poly.vertices:
  v=body.data.vertices[vi].co;indices.append(start+vi)
  if abs(poly.normal.z)>.999:
   coord=((v.x/.16+.5)*.98+.01,(v.y/.075+.5)*.48+.51)
  else:
   along=v.x/.16 if abs(poly.normal.y)>=abs(poly.normal.x) else v.y/.075
   coord=((along+.5)*.48+.01,(v.z/.04365)*.48+.01)
  coords.append(coord)
 faces.append(indices);uvs.append(coords)
bpy.data.objects.remove(body,do_unlink=True)
bpy.data.objects.remove(old,do_unlink=True)
mesh=bpy.data.meshes.new('Compact packaged cash core');mesh.from_pydata(verts,[],faces);mesh.update();mesh.materials.append(mat)
core=bpy.data.objects.new('CashBrickPacked',mesh);bpy.context.collection.objects.link(core)
uv=mesh.uv_layers.new(name='CashAtlas')
for p,coords in zip(mesh.polygons,uvs):
 for li,coord in zip(p.loop_indices,coords):uv.data[li].uv=coord

def unity(v):return (v[0],v[2],-v[1])
lines=['# Packaged-only contiguous cash core; existing cashbrick_albedo.png; meters; Unity Y up','o CashBrickPacked']
for v in mesh.vertices:lines.append('v %.9f %.9f %.9f'%unity(v.co))
for loop in mesh.loops:lines.append('vt %.9f %.9f'%tuple(uv.data[loop.index].uv))
for p in mesh.polygons:
 for li in p.loop_indices:lines.append('vn %.9f %.9f %.9f'%unity(p.normal))
for p in mesh.polygons:lines.append('f '+' '.join('%d/%d/%d'%(mesh.loops[li].vertex_index+1,li+1,li+1) for li in p.loop_indices))
(OUT/'cashbrick_packed.obj').write_text('\n'.join(lines)+'\n')
bpy.context.view_layer.objects.active=core
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'blender/cashbrick_packed.blend'))
positions=np.array([unity(v.co) for v in mesh.vertices])
report={'vertices':len(verts),'triangles':len(faces),'materials':len(mesh.materials),
 'removed_separate_packet_count':removed,'contiguous_body_count':1,'body_height_m':.04365,
 'body_internal_horizontal_faces':0,'body_air_gap_m':0,'paper_line_pitch_m':.04365*16/(.48*1024),
 'retained_top_sheet_count':3,'top_sheet_thickness_m':.0003,'body_corner_bevel_m':.00012,
 'retained_approved_vertices':preserved_vertices,'retained_approved_triangles':preserved_triangles,
 'bounds_min_m':positions.min(axis=0).tolist(),'bounds_max_m':positions.max(axis=0).tolist(),
 'dimensions_m':np.ptp(positions,axis=0).tolist(),
 'uv_in_unit_square':all(0<=x<=1 for item in uv.data for x in item.uv),
 'all_triangles':all(len(p.vertices)==3 for p in mesh.polygons),
 'nondegenerate_triangles':all(p.area>1e-12 for p in mesh.polygons),
 'protected_sha256':before,'protected_files_preserved':before==hashes()}
assert np.allclose(positions.min(axis=0),[-.08,0,-.03815],atol=1e-7)
assert np.allclose(positions.max(axis=0),[.08,.045325,.03815],atol=1e-7)
assert report['uv_in_unit_square'] and report['all_triangles'] and report['nondegenerate_triangles'] and report['protected_files_preserved']
(OUT/'blender/packed_validation.json').write_text(json.dumps(report,indent=2)+'\n')
print('PACKED_CASH_VALIDATION '+json.dumps(report))
