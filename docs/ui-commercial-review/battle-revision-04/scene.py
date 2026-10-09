import bpy,re,struct,json,math,random
from pathlib import Path
from mathutils import Vector,Matrix,Quaternion
from bpy_extras.object_utils import world_to_camera_view
ROOT=Path.cwd()
OUT=ROOT/'docs/ui-commercial-review/battle-revision-04/assets'
bpy.ops.wm.read_factory_settings(use_empty=True)
paths={}
for p in (ROOT/'client/Assets').rglob('*.meta'):
    if 'Library' in str(p):continue
    m=re.search(r'^guid: (\w+)',p.read_text(errors='ignore'),re.M)
    if m:paths[m[1]]=Path(str(p)[:-5])
def scalar(t,k,default=0):
    m=re.search(r'^\s*'+k+r': ([^\n]+)',t,re.M)
    return float(m[1]) if m else default
def vec(t,k,n):
    m=re.search(k+r': \{([^}]+)\}',t)
    return [float(v) for v in re.findall(r': ([^,}]+)',m[1])] if m else [0]*n
def fid(t,k):
    m=re.search(k+r': \{fileID: (-?\d+)',t)
    return int(m[1]) if m else 0
def load_character(name,pos,angle):
    text=(ROOT/f'client/Assets/Resources/ProductionCharacters/{name}.prefab').read_text()
    blocks={int(m[2]):(int(m[1]),m[3]) for m in re.finditer(r'--- !u!(\d+) &(-?\d+)\n(.*?)(?=--- !u!|\Z)',text,re.S)}
    transforms={k:t for k,(kind,t) in blocks.items() if kind==4}
    matrices={}
    def world(k):
        if not k:return Matrix.Identity(4)
        if k in matrices:return matrices[k]
        t=transforms[k];p=vec(t,'m_LocalPosition',3);q=vec(t,'m_LocalRotation',4);s=vec(t,'m_LocalScale',3)
        local=Matrix.LocRotScale(Vector(p),Quaternion((q[3],q[0],q[1],q[2])),Vector(s))
        matrices[k]=world(fid(t,'m_Father'))@local
        return matrices[k]
    convert=Matrix(((1,0,0,0),(0,0,-1,0),(0,1,0,0),(0,0,0,1)))
    group=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(group)
    all_points=[]
    for blockid,(kind,t) in blocks.items():
        if kind not in [137,23]:continue
        meshmatch=re.search(r'm_Mesh: \{fileID: \d+, guid: (\w+)',t)
        if not meshmatch and kind==23:
            game=fid(t,'m_GameObject')
            for _,(other,mt) in blocks.items():
                if other==33 and fid(mt,'m_GameObject')==game:meshmatch=re.search(r'm_Mesh: \{fileID: \d+, guid: (\w+)',mt);break
        if not meshmatch:continue
        path=paths[meshmatch[1]]
        if path.suffix!='.asset':continue
        mt=path.read_text();count=int(scalar(mt,'m_VertexCount'))
        channels=[tuple(map(int,m)) for m in re.findall(r'- stream: (\d+)\n\s+offset: (\d+)\n\s+format: (\d+)\n\s+dimension: (\d+)',mt)]
        raw=bytes.fromhex(re.search(r'_typelessdata: (\w+)',mt)[1]);strides={}
        for st,offset,fmt,dim in channels:
            if dim:strides[st]=max(strides.get(st,0),offset+dim*4)
        starts={};cursor=0
        for st in sorted(strides):starts[st]=cursor;cursor=((cursor+strides[st]*count+15)//16)*16
        def channel(i,c):
            st,offset,fmt,dim=channels[c]
            return struct.unpack_from('<'+('I' if fmt==10 else 'f')*dim,raw,starts[st]+i*strides[st]+offset)
        bindtext=mt.split('m_BindPose:')[1].split('m_BoneNameHashes:')[0] if 'm_BindPose:' in mt else ''
        bindvals=[float(v) for v in re.findall(r'e\d\d: ([^\n]+)',bindtext)]
        binds=[Matrix([bindvals[i+j:i+j+4] for j in (0,4,8,12)]) for i in range(0,len(bindvals),16)]
        bonetext=t.split('m_Bones:')[1].split('m_BlendShapeWeights:')[0] if 'm_Bones:' in t else ''
        bones=[int(v) for v in re.findall(r'fileID: (-?\d+)',bonetext)]
        game=fid(t,'m_GameObject');tk=next(k for k,v in transforms.items() if fid(v,'m_GameObject')==game)
        points=[];uvs=[]
        for i in range(count):
            v=Vector((*channel(i,0),1))
            if bones and len(channels)>13 and channels[12][3]:
                weights=channel(i,12);indices=channel(i,13);p=Vector((0,0,0,0))
                for w,b in zip(weights,indices):
                    if w>0 and b<len(bones):p+=w*(world(bones[b])@binds[b]@v)
            else:p=world(tk)@v
            p=(convert@p).to_3d();points.append(p);all_points.append(p);uvs.append(channel(i,4) if channels[4][3] else (0,0))
        indices=bytes.fromhex(re.search(r'm_IndexBuffer: (\w+)',mt)[1]);fmt='I' if scalar(mt,'m_IndexFormat')==1 else 'H';step=struct.calcsize(fmt);idx=struct.unpack('<'+fmt*(len(indices)//step),indices)
        faces=[tuple(reversed(idx[i:i+3])) for i in range(0,len(idx),3)]
        mesh=bpy.data.meshes.new(path.stem);mesh.from_pydata(points,[],faces);mesh.update()
        ob=bpy.data.objects.new(path.stem,mesh);bpy.context.collection.objects.link(ob);ob.parent=group
        uv=mesh.uv_layers.new()
        for poly in mesh.polygons:
            poly.use_smooth=True
            for li in poly.loop_indices:uv.data[li].uv=uvs[mesh.loops[li].vertex_index]
        matguids=re.findall(r'guid: (\w+)',t.split('m_Materials:')[1].split('m_StaticBatchInfo:')[0]) if 'm_Materials:' in t else []
        for guid in matguids:
            material=paths[guid].read_text();mat=bpy.data.materials.new(paths[guid].stem);mat.use_nodes=True;bsdf=mat.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Roughness'].default_value=.72
            color=vec(material,'_BaseColor',4);bsdf.inputs['Base Color'].default_value=color if len(color)==4 else (.8,.8,.8,1)
            texture=re.search(r'm_Texture: \{fileID: \d+, guid: (\w+)',material)
            if texture and texture[1] in paths:
                tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(paths[texture[1]]),check_existing=True)
                multiply=mat.node_tree.nodes.new('ShaderNodeMixRGB');multiply.blend_type='MULTIPLY';multiply.inputs[0].default_value=1;multiply.inputs[2].default_value=color if len(color)==4 else (1,1,1,1)
                mat.node_tree.links.new(tex.outputs['Color'],multiply.inputs[1]);mat.node_tree.links.new(multiply.outputs[0],bsdf.inputs['Base Color']);mat.node_tree.links.new(tex.outputs['Alpha'],bsdf.inputs['Alpha'])
            mesh.materials.append(mat)
        subs=re.findall(r'firstByte: (\d+)\n\s+indexCount: (\d+)',mt)
        for mi,(first,num) in enumerate(subs):
            for poly in mesh.polygons[int(first)//step//3:(int(first)//step+int(num))//3]:poly.material_index=min(mi,len(mesh.materials)-1)
    lo=min(v.z for v in all_points);hi=max(v.z for v in all_points)
    scale=1.65/(hi-lo);group.scale=(scale,)*3;group.rotation_euler.z=angle;group.location=(*pos,-lo*scale)
    return group,1.65
characters=[('liubei',(-2.9,-1.2),2.5),('r_shield',(-.9,-1.5),2.5),('r_archer',(-3.0,.8),2.5),('r_villager',(-.9,.65),2.5),('bandit_archer',(1.4,2.4),-.5),('bandit_grunt',(3.4,1.2),-.5),('bandit_shaman',(3.6,3.5),-.5)]
anchors=[]
for name,pos,a in characters:
    group,height=load_character(name,pos,a);anchors.append({'id':name,'world':[*pos,height+.18]})
bpy.ops.mesh.primitive_plane_add(size=80);ground=bpy.context.object;ground.name='Natural_ground_no_grid'
mat=bpy.data.materials.new('Grass_and_dirt_generated_albedo');mat.use_nodes=True;bsdf=mat.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Roughness'].default_value=.96
tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(OUT/'ground-grass-dirt.png'))
mapping=mat.node_tree.nodes.new('ShaderNodeVectorMath');mapping.operation='SCALE';mapping.inputs[3].default_value=5;coord=mat.node_tree.nodes.new('ShaderNodeTexCoord');mat.node_tree.links.new(coord.outputs['UV'],mapping.inputs[0]);mat.node_tree.links.new(mapping.outputs[0],tex.inputs[0]);mat.node_tree.links.new(tex.outputs[0],bsdf.inputs['Base Color']);ground.data.materials.append(mat)
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.world=bpy.data.worlds.new('Daylight');scene.world.color=(.45,.45,.45)
def aim(o,p):o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.light_add(type='AREA',location=(-5,-4,12));light=bpy.context.object;light.data.energy=2400;light.data.size=9;aim(light,(0,0,0))
bpy.ops.object.camera_add(location=(8,-11,12));cam=bpy.context.object;aim(cam,(0,1,.3));cam.data.type='ORTHO';cam.data.ortho_scale=17.333333;cam.data.shift_x=.125;scene.camera=cam
scene.render.resolution_x=1600;scene.render.resolution_y=900;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX'
bpy.context.view_layer.update()
projected={}
for width,ratio in [(1600,'16:9'),(2000,'20:9')]:
    scene.render.resolution_x=width;bpy.context.view_layer.update();projected[ratio]=[]
    for a in anchors:
        p=world_to_camera_view(scene,cam,Vector(a['world']));projected[ratio].append({'id':a['id'],'x':p.x,'y':1-p.y})
    scene.render.filepath=str(OUT/f'battlefield-{width}.png');bpy.ops.render.render(write_still=True)
(OUT/'anchors.json').write_text(json.dumps(projected,indent=2))
bpy.context.preferences.filepaths.save_version=0;bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'battlefield.blend'))

