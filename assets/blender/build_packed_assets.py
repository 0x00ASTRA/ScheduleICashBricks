"""Build one centered full-height band around packaged cash; approved loose art stays intact."""
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
# Authored purple/white band. The upper atlas half remains an original-art
# fallback; runtime replaces it from installed BankNoteModelTex using the contract below.
original=bpy.data.images.load(str(OUT/'cashbrick_albedo.png'),check_existing=False)
S=1024;pixels=np.empty(S*S*4,dtype=np.float32);original.pixels.foreach_get(pixels);pixels=pixels.reshape((S,S,4))
unchanged_pixels=pixels.copy();purple=np.array([105/255,65/255,160/255],dtype=np.float32)
pixels[:512,512:,:3]=purple;pixels[:512,512:,3]=1
# White center strip with purple outer borders across the width of each paper wrap.
pixels[:512,595:941,:3]=1
# Original clean monoline $1000 lettering; no external/native font pixels.
# Draw in bottom-origin coordinates, then orient from the near edge up the band.
label=np.zeros((72,252),dtype=bool)
gy,gx=np.mgrid[:72,:252]
def stroke(points,offset,width=2.7):
 for a,b in zip(points,points[1:]):
  ax=offset+4+a[0]*40;ay=4+a[1]*64;bx=offset+4+b[0]*40;by=4+b[1]*64
  t=np.clip(((gx-ax)*(bx-ax)+(gy-ay)*(by-ay))/max((bx-ax)**2+(by-ay)**2,1e-8),0,1)
  label[:] |= (gx-ax-t*(bx-ax))**2+(gy-ay-t*(by-ay))**2 <= width**2
for i,ch in enumerate('$1000'):
 offset=i*52
 if ch=='0':
  angles=np.linspace(0,2*np.pi,100);stroke(list(zip(.5+.32*np.cos(angles),.5+.40*np.sin(angles))),offset)
 elif ch=='1':
  stroke([(.23,.71),(.5,.90),(.5,.10)],offset);stroke([(.22,.10),(.78,.10)],offset)
 else:
  # Smooth S curves plus the recognizable vertical currency stroke.
  curves=[[(.82,.78),(.49,1.02),(.13,.83),(.21,.65)],[(.21,.65),(.27,.48),(.76,.52),(.80,.34)],[(.80,.34),(.88,.12),(.44,.02),(.17,.22)]]
  for p0,p1,p2,p3 in curves:
   t=np.linspace(0,1,28)[:,None];q=(1-t)**3*np.array(p0)+3*(1-t)**2*t*np.array(p1)+3*(1-t)*t*t*np.array(p2)+t**3*np.array(p3)
   stroke(q,offset)
  stroke([(.50,.02),(.50,.98)],offset,1.8)
label=np.repeat(np.rot90(label,-1),2,axis=1)
y0=256-label.shape[0]//2;x0=768-label.shape[1]//2
region=pixels[y0:y0+label.shape[0],x0:x0+label.shape[1],:3];region[label]=purple
assert np.array_equal(pixels[512:],unchanged_pixels[512:]) and np.array_equal(pixels[:512,:512],unchanged_pixels[:512,:512])
atlas=bpy.data.images.new('Packaged cash purple-white band',width=S,height=S,alpha=True)
atlas.colorspace_settings.name='sRGB';atlas.pixels.foreach_set(pixels.ravel());atlas.filepath_raw=str(OUT/'cashbrick_packed_albedo.png');atlas.file_format='PNG';atlas.save();atlas.pack()
for node in mat.node_tree.nodes:
 if node.type=='TEX_IMAGE':node.image=atlas

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
# Retain only the approved three loose top sheets. Whole-brick straps are removed.
keep=set()
for comp in components:
 points=np.array([source.vertices[i].co[:] for i in comp]);size=np.ptp(points,axis=0)
 if size[0]>.1 and size[2]<.001:keep.update(comp)
verts=[];faces=[];uvs=[];mapping={}
for poly in source.polygons:
 if not all(i in keep for i in poly.vertices):continue
 indices=[];coords=[]
 for li in poly.loop_indices:
  vi=source.loops[li].vertex_index
  if vi not in mapping:mapping[vi]=len(verts);verts.append(tuple(source.vertices[vi].co))
  indices.append(mapping[vi]);coords.append(tuple(source.uv_layers.active.data[li].uv))
 faces.append(indices);uvs.append(coords)
preserved_vertices=len(verts);preserved_triangles=len(faces)
assert preserved_triangles==132

def box(name,location,size,band=False,bevel=.00008):
 bpy.ops.mesh.primitive_cube_add(size=1,location=location)
 obj=bpy.context.object;obj.name=name;obj.dimensions=size
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 mod=obj.modifiers.new('Fine compressed corners','BEVEL');mod.width=bevel;mod.segments=1
 bpy.ops.object.modifier_apply(modifier=mod.name)
 tri=obj.modifiers.new('Runtime triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name)
 start=len(verts)
 verts.extend(tuple(v.co+obj.location) for v in obj.data.vertices)
 for poly in obj.data.polygons:
  indices=[];coords=[]
  for vi in poly.vertices:
   v=obj.data.vertices[vi].co;indices.append(start+vi)
   if band:
    u=(v.x/size[0]+.5)*.46+.52
    vv=(v.y/size[1]+.5)*.46+.02 if abs(poly.normal.z)>.5 else (v.z/size[2]+.5)*.06+.02
    coord=(u,vv)
   elif abs(poly.normal.z)>.999:
    coord=((v.x/.16+.5)*.98+.01,(v.y/.075+.5)*.48+.51)
   else:
    along=v.x/.16 if abs(poly.normal.y)>=abs(poly.normal.x) else v.y/.075
    # A shallow contact bevel samples an existing dark paper line, so
    # each packet seam reads through film without any open air gap.
    vv=8.5/1024 if abs(poly.normal.z)>.05 else ((v.z+location[2])/.04365)*.48+.01
    coord=((along+.5)*.48+.01,vv)
   coords.append(coord)
  faces.append(indices);uvs.append(coords)
 bpy.data.objects.remove(obj,do_unlink=True)

# One dense continuous paper body, approved top sheets, and ONE centered wrap.
# The single band covers the entire height and crosses the portrait at x=0.
band_width=.041
box('Dense continuous paper body',(0,0,.04365/2),(.16,.075,.04365),bevel=.00012)
box('Full-height band crown',(0,0,.045325-.0001625),(band_width,.076,.000325),True)
box('Full-height band base',(0,0,.00010),(band_width,.076,.00020),True,.00005)
for y in (-.0378,.0378):
 box('Full-height band side',(0,y,.045325/2),(band_width,.0007,.045325),True)
bpy.data.objects.remove(old,do_unlink=True)
mesh=bpy.data.meshes.new('Compact packaged cash core');mesh.from_pydata(verts,[],faces);mesh.update();mesh.materials.append(mat)
core=bpy.data.objects.new('CashBrickPacked',mesh);bpy.context.collection.objects.link(core)
uv=mesh.uv_layers.new(name='CashAtlas')
for p,coords in zip(mesh.polygons,uvs):
 for li,coord in zip(p.loop_indices,coords):uv.data[li].uv=coord

def unity(v):return (v[0],v[2],-v[1])
lines=['# Packaged cash with single centered full-height $1000 band; cashbrick_packed_albedo.png; meters; Unity Y up','o CashBrickPacked']
for v in mesh.vertices:lines.append('v %.9f %.9f %.9f'%unity(v.co))
for loop in mesh.loops:lines.append('vt %.9f %.9f'%tuple(uv.data[loop.index].uv))
for p in mesh.polygons:
 for li in p.loop_indices:lines.append('vn %.9f %.9f %.9f'%unity(p.normal))
for p in mesh.polygons:lines.append('f '+' '.join('%d/%d/%d'%(mesh.loops[li].vertex_index+1,li+1,li+1) for li in p.loop_indices))
(OUT/'cashbrick_packed.obj').write_text('\n'.join(lines)+'\n')
bpy.context.view_layer.objects.active=core
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'blender/cashbrick_packed.blend'))
positions=np.array([unity(v.co) for v in mesh.vertices])
native_face_contract={'texture_name':'BankNoteModelTex','texture_path_id':1760,
 'reduced_texture_name':'BankNoteModelTex reducedres','reduced_texture_path_id':1669,
 'material_name':'banknote mat','material_path_id':221,
 'source_uv_min':[.010205254,.576686919],'source_uv_max':[.991570175,.991879761],
 'target_uv_min':[.01,.51],'target_uv_max':[.99,.99],
 'target_pixel_region':[0,512,1024,512],'sampling':'bilinear; clamp normalized target UV to [0,1]',
 'native_pixels_in_repository':False,'authored_face_is_fallback':True}
report={'vertices':len(verts),'triangles':len(faces),'materials':len(mesh.materials),
 'albedo_asset':'cashbrick_packed_albedo.png','albedo_modified_region':'UV lower-right quadrant only',
 'band_purple_srgb':[105,65,160],'band_white_srgb':[255,255,255],'band_label':'$1000',
 'whole_height_band_count':1,'band_center_x_m':0,'band_width_m':band_width,
 'native_band_note_length_ratio':.257,'band_side_height_m':.045325,'paper_body_count':1,'paper_body_air_gap_m':0,
 'paper_line_pitch_m':.04365*16/(.48*1024),'native_face_contract':native_face_contract,
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
