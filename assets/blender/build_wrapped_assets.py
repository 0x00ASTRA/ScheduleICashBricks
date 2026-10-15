"""Build compact packaged cash and render it inside the unchanged approved film."""
from pathlib import Path
import bpy, numpy as np, json, hashlib, runpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
OUT=Path(__file__).resolve().parents[1]
# Build packaged core independently; this also verifies all protected originals.
result=runpy.run_path(str(OUT/'blender/build_packed_assets.py'))
before=result['before'];hashes=result['hashes']
core=bpy.data.objects['CashBrickPacked']
# Read the approved wrapper; never regenerate or write its mesh or atlas.
verts=[];texcoords=[];faces=[];face_uv=[]
for line in (OUT/'cashbrick_wrap.obj').read_text().splitlines():
 p=line.split()
 if not p:continue
 if p[0]=='v':
  x,y,z=map(float,p[1:]);verts.append((x,-z,y))
 elif p[0]=='vt':texcoords.append(tuple(map(float,p[1:])))
 elif p[0]=='f':
  corners=[tuple(map(int,c.split('/'))) for c in p[1:]]
  faces.append([c[0]-1 for c in corners]);face_uv.append([texcoords[c[1]-1] for c in corners])
image=bpy.data.images.load(str(OUT/'cashbrick_wrap_albedo.png'));image.alpha_mode='STRAIGHT';image.pack()
mat=bpy.data.materials.new('Packaged cash transparent film');mat.use_nodes=True
nodes=mat.node_tree.nodes;nodes.clear()
out=nodes.new('ShaderNodeOutputMaterial');mix=nodes.new('ShaderNodeMixShader')
transparent=nodes.new('ShaderNodeBsdfTransparent');bsdf=nodes.new('ShaderNodeBsdfPrincipled');tex=nodes.new('ShaderNodeTexImage');tex.image=image
bsdf.inputs['Roughness'].default_value=.30;bsdf.inputs['Metallic'].default_value=0
mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
mat.node_tree.links.new(tex.outputs['Alpha'],mix.inputs[0]);mat.node_tree.links.new(transparent.outputs[0],mix.inputs[1]);mat.node_tree.links.new(bsdf.outputs[0],mix.inputs[2]);# Preserve surface reflections separately from coverage, as the game's URP film does.
add=nodes.new('ShaderNodeAddShader');gloss=nodes.new('ShaderNodeBsdfGlossy')
gloss.inputs['Color'].default_value=(.032,.032,.032,1);gloss.inputs['Roughness'].default_value=.30
mat.node_tree.links.new(mix.outputs[0],add.inputs[0]);mat.node_tree.links.new(gloss.outputs[0],add.inputs[1]);mat.node_tree.links.new(add.outputs[0],out.inputs['Surface'])
mesh=bpy.data.meshes.new('Approved folded film (unchanged)');mesh.from_pydata(verts,[],faces);mesh.update()
wrap=bpy.data.objects.new('Packaged cash film shell',mesh);bpy.context.collection.objects.link(wrap);mesh.materials.append(mat)
uv=mesh.uv_layers.new(name='FilmAtlas')
for p,coords in zip(mesh.polygons,face_uv):
 for li,coord in zip(p.loop_indices,coords):uv.data[li].uv=coord
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=96;scene.cycles.use_denoising=True;scene.cycles.transparent_max_bounces=12
scene.render.film_transparent=True;scene.render.image_settings.color_mode='RGBA'
scene.render.resolution_x=256;scene.render.resolution_y=256;scene.render.resolution_percentage=100
# A narrow reflection source makes the transparent packaging legible in the icon.
bpy.ops.object.light_add(type='AREA',location=(-.20,.24,.28));light=bpy.context.object;light.name='Film reflection strip';light.data.energy=.16;light.data.shape='RECTANGLE';light.data.size=.22;light.data.size_y=.035;light.rotation_euler=(Vector((0,0,.02))-light.location).to_track_quat('-Z','Y').to_euler()
bpy.context.view_layer.objects.active=wrap
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'blender/cashbrick_wrapped.blend'))
scene.render.filepath=str(OUT/'cashbrick_wrapped_icon.png');bpy.ops.render.render(write_still=True)
scene.render.resolution_x=1024;scene.render.resolution_y=1024
scene.render.filepath=str(OUT/'blender/cashbrick_wrapped_preview.png');bpy.ops.render.render(write_still=True)
# A low camera inspection render exposes the dense paper sides directly.
cam=scene.camera;cam.location=(.20,-.25,.105);cam.data.ortho_scale=.205
cam.rotation_euler=(Vector((0,0,.022))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(OUT/'blender/cashbrick_packed_sides_preview.png');bpy.ops.render.render(write_still=True)
def unity(v):return (v[0],v[2],-v[1])
pos=np.array([unity(v.co) for v in mesh.vertices]);counts={}
for f in faces:
 for a,b in zip(f,f[1:]+f[:1]):key=tuple(sorted((a,b)));counts[key]=counts.get(key,0)+1
bvh=BVHTree.FromPolygons(verts,faces,all_triangles=True)
core_inside=all((v.co-bvh.find_nearest(v.co)[0]).dot(bvh.find_nearest(v.co)[1])<1e-7 for v in core.data.vertices)
pixels=np.empty(len(image.pixels),dtype=np.float32);image.pixels.foreach_get(pixels)
report={'core_asset':'cashbrick_packed.obj','core_vertices_inside_shell':core_inside,'vertices':len(verts),'triangles':len(faces),'materials':len(mesh.materials),
 'bounds_min_m':pos.min(axis=0).tolist(),'bounds_max_m':pos.max(axis=0).tolist(),'dimensions_m':np.ptp(pos,axis=0).tolist(),
 'uv_in_unit_square':all(0<=x<=1 for item in uv.data for x in item.uv),'all_triangles':all(len(p.vertices)==3 for p in mesh.polygons),
 'nondegenerate_triangles':all(p.area>1e-12 for p in mesh.polygons),'closed_manifold':all(n==2 for n in counts.values()),
 'atlas_alpha_range':[float(pixels[3::4].min()),float(pixels[3::4].max())],
 'recommended_material':{'surface':'transparent','preserve_specular':True,'smoothness':.70,'metallic':0,'tint_rgba':[1,1,1,1],'cull':'back','zwrite':False},
 'protected_sha256':before,'protected_files_preserved':before==hashes(),'native_art_included':False}
assert core_inside and report['protected_files_preserved'] and report['closed_manifold'] and report['nondegenerate_triangles']
(OUT/'blender/wrapped_validation.json').write_text(json.dumps(report,indent=2)+'\n')
print('WRAPPED_CASH_VALIDATION '+json.dumps(report))

# Optional LOCAL preview of runtime-composited native bill faces. Native pixels
# stay in /tmp, are never packed into the authored .blend, and never overwrite
# the distributable fallback atlas, icon, or previews.
import os
reference=os.environ.get('CASHBRICKS_NATIVE_FACE_REFERENCE')
if reference:
 native=bpy.data.images.load(str(Path(reference).resolve()),check_existing=False)
 nw,nh=native.size
 native_pixels=np.empty(nw*nh*4,dtype=np.float32);native.pixels.foreach_get(native_pixels);native_pixels=native_pixels.reshape((nh,nw,4))
 authored=result['atlas'];sz=1024
 composed=np.empty(sz*sz*4,dtype=np.float32);authored.pixels.foreach_get(composed);composed=composed.reshape((sz,sz,4))
 u=np.clip(((np.arange(sz)+.5)/sz-.01)/.98,0,1)
 v=np.clip(((np.arange(512,sz)+.5)/sz-.51)/.48,0,1)
 contract=result['native_face_contract'];umin,vmin=contract['source_uv_min'];umax,vmax=contract['source_uv_max']
 xx=(umin+u*(umax-umin))*(nw-1);yy=(vmin+v*(vmax-vmin))*(nh-1)
 x0=np.floor(xx).astype(int);x1=np.minimum(x0+1,nw-1);tx=(xx-x0)[None,:,None]
 y0=np.floor(yy).astype(int);y1=np.minimum(y0+1,nh-1);ty=(yy-y0)[:,None,None]
 a=native_pixels[y0[:,None],x0[None,:]]*(1-tx)+native_pixels[y0[:,None],x1[None,:]]*tx
 b=native_pixels[y1[:,None],x0[None,:]]*(1-tx)+native_pixels[y1[:,None],x1[None,:]]*tx
 composed[512:]=a*(1-ty)+b*ty;composed[:,:,3]=1
 temp=Path('/tmp/cashbricks-native-packaged-preview');temp.mkdir(parents=True,exist_ok=True)
 preview_atlas=bpy.data.images.new('LOCAL native-face composite (not packed)',width=sz,height=sz,alpha=True)
 preview_atlas.colorspace_settings.name='sRGB';preview_atlas.pixels.foreach_set(composed.ravel());preview_atlas.file_format='PNG';preview_atlas.filepath_raw=str(temp/'cashbrick_runtime_atlas.png');preview_atlas.save()
 for node in core.data.materials[0].node_tree.nodes:
  if node.type=='TEX_IMAGE':node.image=preview_atlas
 cam.location=(.22,-.27,.25);cam.data.ortho_scale=.215
 cam.rotation_euler=(Vector((0,0,.022))-cam.location).to_track_quat('-Z','Y').to_euler()
 scene.render.resolution_x=256;scene.render.resolution_y=256
 scene.render.filepath=str(temp/'cashbrick_wrapped_icon.png');bpy.ops.render.render(write_still=True)
 scene.render.resolution_x=1024;scene.render.resolution_y=1024
 scene.render.filepath=str(temp/'cashbrick_wrapped_preview.png');bpy.ops.render.render(write_still=True)
 cam.location=(.20,-.25,.105);cam.data.ortho_scale=.205
 cam.rotation_euler=(Vector((0,0,.022))-cam.location).to_track_quat('-Z','Y').to_euler()
 scene.render.filepath=str(temp/'cashbrick_packed_sides_preview.png');bpy.ops.render.render(write_still=True)
 assert before==hashes()
 print('LOCAL_NATIVE_FACE_PREVIEWS '+str(temp))
