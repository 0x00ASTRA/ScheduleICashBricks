"""Rebuild with: blender --background --python assets/blender/build_assets.py"""
from pathlib import Path
import math
import json
import bpy
import numpy as np
from mathutils import Vector

OUT = Path(__file__).resolve().parents[1]
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.context.preferences.filepaths.save_version = 0
rng = np.random.default_rng(7319)
S = 1024
pixels = np.ones((S, S, 4), dtype=np.float32)
pixels[:, :, :3] = (0.66, 0.69, 0.51)

def rect(x0, y0, x1, y1, color):
    pixels[max(0,y0):min(S,y1), max(0,x0):min(S,x1), :3] = color

def line(x0,y0,x1,y1,color,width=1):
    count = int(max(abs(x1-x0),abs(y1-y0)))+1
    for x,y in zip(np.linspace(x0,x1,count), np.linspace(y0,y1,count)):
        rect(int(x)-width//2,int(y)-width//2,int(x)+1+width//2,int(y)+1+width//2,color)

def ellipse(cx,cy,rx,ry,color,width=1):
    a=np.linspace(0,2*math.pi,700)
    for x,y in zip(cx+rx*np.cos(a),cy+ry*np.sin(a)):
        rect(int(x),int(y),int(x)+width,int(y)+width,color)

FONT = {
 'A':['01110','10001','10001','11111','10001','10001','10001'],
 'B':['11110','10001','10001','11110','10001','10001','11110'],
 'C':['01111','10000','10000','10000','10000','10000','01111'],
 'D':['11110','10001','10001','10001','10001','10001','11110'],
 'E':['11111','10000','10000','11110','10000','10000','11111'],
 'H':['10001','10001','10001','11111','10001','10001','10001'],
 'I':['11111','00100','00100','00100','00100','00100','11111'],
 'K':['10001','10010','10100','11000','10100','10010','10001'],
 'L':['10000','10000','10000','10000','10000','10000','11111'],
 'N':['10001','11001','11001','10101','10011','10011','10001'],
 'O':['01110','10001','10001','10001','10001','10001','01110'],
 'R':['11110','10001','10001','11110','10100','10010','10001'],
 'S':['01111','10000','10000','01110','00001','00001','11110'],
 'T':['11111','00100','00100','00100','00100','00100','00100'],
 'U':['10001','10001','10001','10001','10001','10001','01110'],
 'V':['10001','10001','10001','10001','10001','01010','00100'],
 'Y':['10001','10001','01010','00100','00100','00100','00100'],
 '0':['01110','10001','10011','10101','11001','10001','01110'],
 '1':['00100','01100','00100','00100','00100','00100','01110'],
 '5':['11111','10000','10000','11110','00001','00001','11110'],
 '7':['11111','00001','00010','00100','01000','01000','01000'],
 '9':['01110','10001','10001','01111','00001','00001','01110'],
 ' ':['00000']*7,
}
def text(label,x,y,scale,color):
    for letter in label:
        for row,bits in enumerate(FONT.get(letter,FONT[' '])):
            for col,bit in enumerate(bits):
                if bit=='1': rect(x+col*scale,y+(6-row)*scale,x+(col+1)*scale,y+(7-row)*scale,color)
        x+=6*scale

# Original artwork informed by the installed game's broad cash visual language:
# ivory paper, charcoal frame, muted green stamps, and an exaggerated portrait.
# No native texture pixels or meshes are imported into this build.
def filled_ellipse(cx,cy,rx,ry,color):
    yy,xx=np.ogrid[:S,:S]
    mask=((xx-cx)/rx)**2+((yy-cy)/ry)**2<=1
    pixels[mask,:3]=color

def polygon(points,color):
    # Deterministic scanline fill, using only Blender's bundled NumPy.
    for y in range(max(0,int(min(p[1] for p in points))),min(S,int(max(p[1] for p in points))+1)):
        hits=[]
        for (x1,y1),(x2,y2) in zip(points,points[1:]+points[:1]):
            if (y1<=y<y2) or (y2<=y<y1): hits.append(x1+(y-y1)*(x2-x1)/(y2-y1))
        hits.sort()
        for x1,x2 in zip(hits[::2],hits[1::2]): rect(int(x1),y,int(x2)+1,y+1,color)

paper=(.84,.84,.70); ink=(.23,.25,.22); green=(.28,.37,.23)
rect(0,512,1024,1024,paper)
rect(24,535,1000,1001,ink)
rect(45,585,979,951,paper)
# Bold oval, intentionally simple enough to survive the inventory mip levels.
filled_ellipse(512,770,156,222,ink)
filled_ellipse(512,770,143,209,(.57,.60,.50))
# Original fictional teller, built from broad flat shaded shapes.
polygon([(395,590),(418,647),(468,670),(554,670),(610,642),(634,590)],(.30,.39,.32))
polygon([(463,665),(473,618),(511,588),(546,625),(553,666)],(.70,.65,.51))
polygon([(466,650),(450,638),(477,600),(503,619)],(.68,.72,.62))
polygon([(551,652),(575,637),(550,601),(523,620)],(.68,.72,.62))
polygon([(501,619),(519,619),(533,586),(489,586)],ink)
# Hair silhouette and ears: asymmetric, original character rather than native portrait.
filled_ellipse(513,795,100,151,(.29,.30,.27))
filled_ellipse(420,767,18,33,(.64,.58,.45))
filled_ellipse(602,768,17,32,(.64,.58,.45))
polygon([(433,843),(450,902),(506,921),(558,899),(590,850),(587,739),(566,696),(513,670),(464,696),(436,751)],(.76,.71,.57))
polygon([(556,897),(590,850),(587,739),(566,696),(513,670),(536,709),(557,775)],(.68,.62,.48))
polygon([(433,840),(438,891),(476,926),(533,928),(569,905),(586,866),(552,886),(500,886),(466,869)],(.30,.31,.27))
# Slightly different eye heights and an angular nose give the portrait game-like character.
for cx,cy in [(474,802),(550,808)]:
    filled_ellipse(cx,cy-4,29,36,(.58,.56,.46))
    filled_ellipse(cx,cy,27,33,(.91,.91,.81))
    filled_ellipse(cx+5,cy-1,9,13,(.17,.19,.17))
line(450,847,486,853,ink,8)
line(530,858,568,850,ink,8)
polygon([(515,805),(503,755),(527,754)],(.56,.51,.39))
line(486,727,535,730,ink,5)
line(500,714,523,714,(.59,.54,.43),3)
for x in (68,860):
    text('10',x,959,5,paper)
    text('10',x,546,5,paper)
text('HYLAND',709,898,4,green)
text('BANK',732,861,4,green)
text('H 71901',73,898,3,green)
text('TEN',752,632,3,green)
# Currency mark and an empty seal echo the sparse composition, without copying art.
for cx in (150,842):
    filled_ellipse(cx,766,58,65,green)
    filled_ellipse(cx,766,47,54,paper)
    filled_ellipse(cx,766,40,47,green)
text('S',824,743,7,paper)
line(840,729,840,805,paper,5)
line(104,644,131,664,ink,3);line(131,664,146,646,ink,3)
line(121,640,173,663,ink,3);line(173,663,194,650,ink,3)
# Cream cut paper, with sparse grouped lines instead of noisy olive moire.
rect(0,0,512,512,(.79,.80,.67))
for y in range(8,510,16):
    shade=float(rng.uniform(.61,.68))
    line(0,y,511,y,(shade,shade+.025,shade-.055),2)
    line(0,y+3,511,y+3,(.88,.87,.75),2)
# Unprinted warm ivory wrappers. The bold green center stripe reads at small scale.
rect(512,0,1024,512,(.91,.87,.73))
for x in (532,991): rect(x,0,x+5,512,(.60,.61,.48))
rect(554,220,982,286,(.36,.44,.32))
# Deliberately no denomination on straps: same mesh represents multiple cash values.
atlas=bpy.data.images.new('CashBrick_Albedo',width=S,height=S,alpha=True)
atlas.colorspace_settings.name='sRGB'
atlas.pixels.foreach_set(pixels.ravel())
atlas.filepath_raw=str(OUT/'cashbrick_albedo.png')
atlas.file_format='PNG'
atlas.save()
mat=bpy.data.materials.new('CashBrick_Paper')
mat.use_nodes=True
bsdf=mat.node_tree.nodes.get('Principled BSDF')
bsdf.inputs['Roughness'].default_value=.88
tex=mat.node_tree.nodes.new('ShaderNodeTexImage'); tex.image=atlas
mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
parts=[]

def box(name,location,scale,band=False,bevel=.00025):
    bpy.ops.mesh.primitive_cube_add(size=1,location=location)
    obj=bpy.context.object; obj.name=name
    obj.dimensions=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    mod=obj.modifiers.new('Soft paper corners','BEVEL');mod.width=bevel;mod.segments=1
    bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.append(mat)
    uv=obj.data.uv_layers.new(name='CashAtlas')
    obj.data.uv_layers.active_index=len(obj.data.uv_layers)-1
    uv.active_render=True
    for poly in obj.data.polygons:
        for li in poly.loop_indices:
            v=obj.data.vertices[obj.data.loops[li].vertex_index].co
            if band:
                u=(v.x/scale[0]+.5)*.46+.52
                vv=(v.y/scale[1]+.5)*.46+.02 if abs(poly.normal.z)>.5 else (v.z/scale[2]+.5)*.46+.02
            elif abs(poly.normal.z)>.5:
                u=(v.x/scale[0]+.5)*.98+.01
                vv=(v.y/scale[1]+.5)*.48+.51
            else:
                along=v.x/scale[0] if abs(poly.normal.y)>.5 else v.y/scale[1]
                u=(along+.5)*.48+.01
                vv=(v.z/scale[2]+.5)*.48+.01
            uv.data[li].uv=(u,vv)
    parts.append(obj)
    return obj

# Six shallow packets retain a brick silhouette but introduce readable paper lips.
# Deterministic offsets stay strictly inside the original runtime envelope.
for i in range(6):
    width=.160 if i in (0,5) else (.1592 if i%2 else .1596)
    depth=.075 if i%2 else .0746
    box('Layered paper packet %02d'%i,(0,0,.00365+i*.00725),
        (width,depth,.00730),bevel=.00035)

for i in range(3):
    box('Loose upper note %d'%i,(0,0,.0438+i*.0003),(.160-i*.0001,.075,.0003),bevel=.00008)
for x in (-.045,.045):
    box('Paper band crown',(x,0,.0450),(.017,.076,.00065),True,.00015)
    for y in (-.0378,.0378):
        box('Paper band side',(x,y,.0225),(.017,.0007,.045),True,.00015)
    box('Paper band base',(x,0,.00030),(.017,.076,.0006),True,.0001)
bpy.ops.object.select_all(action='DESELECT')
for obj in parts: obj.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.object.join()
obj=bpy.context.object;obj.name='CashBrick'
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
tri=obj.modifiers.new('Runtime triangles','TRIANGULATE')
bpy.ops.object.modifier_apply(modifier=tri.name)
# Collapse material slots created by join into one.
obj.data.materials.clear();obj.data.materials.append(mat)
for p in obj.data.polygons:p.material_index=0
mesh=obj.data

# Deterministic dependency-free OBJ with per-corner UV and flat normals.
# Rotation (x,y,z) -> (x,z,-y) has determinant +1; preserves winding.
def unity(v): return (v.x,v.z,-v.y)
lines=['# Blender cash brick; meters; Y up; base Y=0; one material supplied by runtime','o CashBrick']
for v in mesh.vertices: lines.append('v %.9f %.9f %.9f'%unity(v.co))
for loop in mesh.loops:
    uv=mesh.uv_layers.active.data[loop.index].uv
    lines.append('vt %.9f %.9f'%(uv.x,uv.y))
for p in mesh.polygons:
    for li in p.loop_indices: lines.append('vn %.9f %.9f %.9f'%unity(p.normal))
for p in mesh.polygons:
    lines.append('f '+' '.join('%d/%d/%d'%(mesh.loops[li].vertex_index+1,li+1,li+1) for li in p.loop_indices))
(OUT/'cashbrick.obj').write_text('\n'.join(lines)+'\n')

scene=bpy.context.scene
scene.render.engine='CYCLES';scene.cycles.samples=64
scene.render.resolution_x=256;scene.render.resolution_y=256;scene.render.resolution_percentage=100
scene.render.film_transparent=True
scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
scene.world.color=(.25,.25,.25)
scene.view_settings.view_transform='Standard'
scene.view_settings.look='None'
def aim(ob,target):ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(.22,-.27,.25))
cam=bpy.context.object;cam.name='Icon Camera';cam.data.type='ORTHO';cam.data.ortho_scale=.215
aim(cam,(0,0,.022));scene.camera=cam
for name,loc,power,size in [('Large warm key',(-.12,-.16,.32),.65,.24),('Soft fill',(.20,.08,.15),.22,.18)]:
    bpy.ops.object.light_add(type='AREA',location=loc)
    light=bpy.context.object;light.name=name;light.data.energy=power;light.data.shape='DISK';light.data.size=size
    aim(light,(0,0,.02))
# Keep just one authoritative UV layer in the editable source.
for layer in list(mesh.uv_layers):
    if layer != mesh.uv_layers.active: mesh.uv_layers.remove(layer)
atlas.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'blender'/'cashbrick.blend'))
scene.render.filepath=str(OUT/'cashbrick_icon.png')
bpy.ops.render.render(write_still=True)
scene.render.resolution_x=1024;scene.render.resolution_y=1024
scene.render.filepath=str(OUT/'blender'/'cashbrick_preview.png')
bpy.ops.render.render(write_still=True)
# Inspect the runtime's flattened pouring-stack scale as well as the full brick.
# Blender Z corresponds to Unity Y. Restore the object after the review render.
obj.scale=(.55,.55,.2)
cam.data.ortho_scale=.12
aim(cam,(0,0,.0045))
scene.render.resolution_x=512;scene.render.resolution_y=512
scene.render.filepath=str(OUT/'blender'/'cashstack_preview.png')
bpy.ops.render.render(write_still=True)
obj.scale=(1,1,1)
positions=np.array([unity(v.co) for v in mesh.vertices])
report={'art_revision':'1.0.7-stylized-cash','vertices':len(mesh.vertices),'triangles':len(mesh.polygons),'materials':len(mesh.materials),'bounds_min_m':positions.min(axis=0).tolist(),'bounds_max_m':positions.max(axis=0).tolist(),'dimensions_m':np.ptp(positions,axis=0).tolist(),'uv_in_unit_square':all(0<=x<=1 for uv in mesh.uv_layers.active.data for x in uv.uv),'all_triangles':all(len(p.vertices)==3 for p in mesh.polygons)}
assert report['uv_in_unit_square'] and report['all_triangles'] and report['materials']==1
assert np.allclose(report['bounds_min_m'],[-.08,0,-.03815],atol=1e-7)
assert np.allclose(report['bounds_max_m'],[.08,.045325,.03815],atol=1e-7)
assert np.all(pixels[:,:,3]==1)
assert all(p.area>1e-12 for p in mesh.polygons)
report['albedo_opaque']=True
report['nondegenerate_triangles']=True
report['native_art_included']=False
report['physical_stack_dimensions_m']=(np.ptp(positions,axis=0)*[.55,.2,.55]).tolist()
(OUT/'blender'/'validation.json').write_text(json.dumps(report,indent=2)+'\n')
print('CASHBRICK_ASSET_VALIDATION '+json.dumps(report))
