"""Inspect the project's textured character source before choosing a production base."""
import bpy, os, math, json
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__), '../..'))
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
reports=[]
for slot,num in [('Body',2),('Face',2),('Hair',2),('Cosmetic',2)]:
    stem=f'{slot}_{num:05d}'
    path=os.path.join(ROOT,'client/Assets/Arts/Models',slot,stem,stem+'_Fbx.fbx')
    before=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    objects=set(bpy.data.objects)-before
    for o in objects:
        if o.type=='MESH':
            for modifier in list(o.modifiers):
                if modifier.type=='ARMATURE': o.modifiers.remove(modifier)
            reports.append({'name':o.name,'vertices':len(o.data.vertices),'polygons':len(o.data.polygons),'location':list(o.location),'bounds':[list(o.matrix_world@Vector(v)) for v in o.bound_box]})
            texpath=os.path.join(os.path.dirname(path),stem+'_001_T.png')
            if not os.path.exists(texpath): texpath=os.path.join(os.path.dirname(path),stem+'_001_T.tga')
            if os.path.exists(texpath):
                mat=bpy.data.materials.new(stem+'_Review'); mat.use_nodes=True
                bsdf=mat.node_tree.nodes.get('Principled BSDF')
                tex=mat.node_tree.nodes.new('ShaderNodeTexImage'); tex.image=bpy.data.images.load(texpath)
                mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
                bsdf.inputs['Roughness'].default_value=.55
                o.data.materials.clear();o.data.materials.append(mat)
            for polygon in o.data.polygons: polygon.use_smooth=True
    for o in objects:
        if o.type=='ARMATURE': o.hide_render=True
meshes=[o for o in bpy.data.objects if o.type=='MESH']
points=[o.matrix_world@Vector(v) for o in meshes for v in o.bound_box]
lo=Vector(tuple(min(v[i] for v in points) for i in range(3)));hi=Vector(tuple(max(v[i] for v in points) for i in range(3)))
center=(lo+hi)/2; size=max(hi-lo)
print('BASE_BOUNDS',list(lo),list(hi))
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24
scene.world.color=(.3,.3,.3)
def aim(o,p):o.rotation_euler=(p-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=center+Vector((size*.8,-size*1.5,size*.45)))
camera=bpy.context.object;aim(camera,center);camera.data.type='ORTHO';camera.data.ortho_scale=size*1.18;scene.camera=camera
for pos,power,area in [((-.8,-1.3,1.5),650,2),((1,-.5,.8),350,2),((0,1,1.5),700,1.5)]:
    bpy.ops.object.light_add(type='AREA',location=center+Vector(pos)*size);light=bpy.context.object;light.data.energy=power*size*size;light.data.shape='DISK';light.data.size=area*size;aim(light,center)
scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.render.film_transparent=True;scene.view_settings.view_transform='AgX'
out=os.path.join(ROOT,'build/model-quality');os.makedirs(out,exist_ok=True)
with open(os.path.join(out,'source-mesh-audit.json'),'w') as f:json.dump(reports,f,indent=2)
scene.render.filepath=os.path.join(out,'source-guanyu.png');bpy.ops.render.render(write_still=True)
