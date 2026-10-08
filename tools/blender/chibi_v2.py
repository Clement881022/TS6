"""Original articulated Q-style combat meshes. Blender 5.x, FBX + .blend sources.
blender -b --python tools/blender/chibi_v2.py -- client/Assets/Resources/ChibiModels
Front is -Y, Z up. Pivots match CharacterView; pose_* identifies combat motion.
"""
import bpy, math, os, sys, importlib.util
from mathutils import Vector
spec=importlib.util.spec_from_file_location('old',os.path.join(os.path.dirname(__file__),'chibi.py'))
old=importlib.util.module_from_spec(spec); spec.loader.exec_module(old)
GOLD=(.82,.58,.20); IVORY=(.93,.88,.74); INK=(.075,.085,.11); SKIN=(.94,.69,.49)

def obj(kind,name,loc,size,color,parent,rot=(0,0,0)):
    o=old.prim(kind,name,loc,size,color,parent,rot)
    if kind=='cube':
        bpy.context.view_layer.objects.active=o
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
        bevel=o.modifiers.new('Crafted edges','BEVEL'); bevel.width=.025; bevel.segments=3
        bpy.ops.object.modifier_apply(modifier=bevel.name)
        o.modifiers.new('Smooth normals','WEIGHTED_NORMAL')
    return o

def line(name,points,radius,color,parent):
    data=bpy.data.curves.new(name,'CURVE'); data.dimensions='3D'; data.bevel_depth=radius; data.bevel_resolution=3
    sp=data.splines.new('POLY'); sp.points.add(len(points)-1)
    for p,co in zip(sp.points,points): p.co=(*co,1)
    o=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(o)
    o.data.materials.append(old.material(color)); old.attach(o,parent)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active=o
    bpy.ops.object.convert(target='MESH'); return o

def blade(name,x,y,z,outline,color,parent):
    vertices=[(x+a,y+b,z+c) for b in (-.025,.025) for a,c in outline]
    n=len(outline); faces=[tuple(range(n-1,-1,-1)),tuple(range(n,n*2))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(vertices,[],faces); mesh.update()
    o=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(o)
    o.data.materials.append(old.material(color)); old.attach(o,parent)
    bevel=o.modifiers.new('Blade edge','BEVEL'); bevel.width=.018; bevel.segments=2
    return o

def weapon(kind,r,l):
    x=.52; steel=(.72,.80,.84); wood=(.24,.14,.08)
    if kind in ('glaive','spear','halberd'):
        line('weapon_shaft',[(x,-.07,.14),(x,-.07,2.05)],.035,wood,r)
        for z in (.30,.68,1.18,1.70): obj('cyl','shaft_gold',(x,-.07,z),(.043,.043,.045),GOLD,r)
        if kind=='glaive':
            blade('crescent_glaive',x,-.07,1.75,[(0,0),(.28,.07),(.38,.39),(.19,.62),(-.08,.53),(-.06,.13)],steel,r)
            line('blade_spine',[(x+.06,-.07,1.85),(x+.06,-.07,2.26),(x-.17,-.07,2.35)],.024,GOLD,r)
        else:
            blade('spear_tip',x,-.07,2.04,[(-.08,0),(-.10,.16),(0,.43),(.10,.16),(.08,0)],steel,r)
            if kind=='halberd':
                for sign in (-1,1):
                    blade('halberd_crescent',x,-.07,1.95,[(sign*.06,0),(sign*.28,.04),(sign*.34,.30),(sign*.20,.22)],GOLD,r)
        line('weapon_tassel',[(x,-.04,1.85),(x+.13,-.08,1.67),(x+.18,-.08,1.40)],.042,(.65,.12,.10),r)
    elif kind=='bow':
        line('recurve_bow',[(-.49,-.25,.40),(-.67,-.25,.56),(-.72,-.25,.89),(-.67,-.25,1.20),(-.49,-.25,1.37)],.038,GOLD,l)
        line('bow_string',[(-.49,-.25,.40),(-.41,-.28,.88),(-.49,-.25,1.37)],.008,IVORY,l)
        line('arrow',[(-.45,-.30,.88),(.34,-.30,.88)],.012,wood,r)
        blade('arrow_head',-.45,-.3,.88,[(0,-.04),(-.12,0),(0,.04)],steel,r)
        obj('cyl','quiver',(.27,.25,.90),(.12,.12,.30),wood,r)
    elif kind in ('staff','fan','healer'):
        line('spell_staff',[(x,-.02,.25),(x,-.02,1.60)],.035,wood,r)
        obj('torus','staff_ring',(x,-.02,1.66),(.17,.17,.17),GOLD,r,rot=(math.pi/2,0,0))
        obj('sphere','spell_gem',(x,-.04,1.68),(.11,.11,.13),(.28,.60,.70) if kind=='healer' else (.49,.22,.70),r)
        if kind=='fan':
            for i in range(7):
                feather=obj('sphere','feather',(-.53+(i-3)*.035,-.22,.98+abs(i-3)*.012),(.04,.025,.20),IVORY,l)
                feather.rotation_euler.y=(i-3)*.18
        else: obj('cube','medicine_book',(-.53,-.18,.82),(.16,.06,.18),(.35,.52,.31),l)
    else:
        blade('dao_blade',x,-.09,.70,[(-.04,0),(-.07,.70),(.04,.94),(.11,.68),(.06,0)],steel,r)
        obj('cube','sword_guard',(x,-.09,.72),(.16,.035,.035),GOLD,r)
        line('sword_grip',[(x,-.09,.47),(x,-.09,.70)],.033,wood,r)
        if kind=='shield':
            obj('cube','shield_edge',(-.53,-.26,.88),(.29,.075,.39),GOLD,l)
            obj('cube','shield_face',(-.53,-.35,.88),(.24,.025,.33),(.18,.32,.31),l)
            obj('sphere','shield_boss',(-.53,-.395,.90),(.09,.045,.09),GOLD,l)
            for sx in (-1,1):
                for sz in (-1,1): obj('sphere','shield_rivet',(-.53+sx*.18,-.39,.88+sz*.25),(.025,.02,.025),GOLD,l)

def build(name,p):
    root=bpy.data.objects.new(name,None); bpy.context.collection.objects.link(root)
    color=p.get('robe',(.18,.40,.35)); hair=p.get('hair',INK); skin=p.get('skin',SKIN)
    kind=p.get('weapon','sword'); armored=kind not in ('staff','fan','healer')
    torso=old.pivot('pivot_torso',(0,0,.62),root)
    head=old.pivot('pivot_head',(0,0,1.55),torso)
    r=old.pivot('pivot_arm_R',(.40,0,1.22),torso); l=old.pivot('pivot_arm_L',(-.40,0,1.22),torso)
    pose='archer' if kind=='bow' else 'caster' if not armored else 'guard' if kind=='shield' else 'polearm' if kind in ('glaive','spear','halberd') else 'sword'
    old.pivot('pose_'+pose,(0,0,0),root)
    for sign in (-1,1):
        leg=old.pivot('pivot_leg_'+('R' if sign==1 else 'L'),(sign*.18,0,.43),root)
        obj('cyl','leg',(sign*.18,0,.28),(.13,.13,.20),INK,leg)
        obj('sphere','boot',(sign*.19,-.085,.095),(.17,.23,.10),(.14,.16,.18),leg)
        obj('cube','boot_trim',(sign*.19,-.19,.12),(.13,.07,.04),GOLD,leg)
    obj('sphere','body',(0,0,.92),(.35,.25,.43),color,torso)
    obj('cyl','robe_skirt',(0,0,.50),(.37,.27,.20),color,torso)
    obj('cyl','belt',(0,0,.69),(.37,.27,.045),GOLD,torso)
    obj('sphere','jade_buckle',(0,-.29,.69),(.10,.035,.10),(.20,.55,.43),torso)
    for sx,pivot in ((1,r),(-1,l)):
        obj('sphere','sleeve',(sx*.44,0,1.02),(.16,.16,.25),color,pivot)
        obj('sphere','gauntlet',(sx*.49,-.01,.78),(.12,.12,.10),GOLD if armored else color,pivot)
        obj('sphere','hand',(sx*.52,-.055,.70),(.105,.09,.105),skin,pivot)
        if armored:
            obj('sphere','shoulder_gold',(sx*.40,0,1.21),(.23,.20,.13),GOLD,pivot)
            obj('sphere','shoulder_plate',(sx*.40,-.015,1.24),(.19,.17,.10),color,pivot)
            for k in range(3): obj('sphere','shoulder_stud',(sx*.40+(k-1)*.10,-.165,1.24),(.023,.015,.025),GOLD,pivot)
    if armored:
        for row in range(3):
            for col in range(4):
                obj('cube','lamellar',(col*.12-.18,-.247,.81+row*.12),(.053,.035,.055),p.get('metal',(.47,.51,.49)),torso)
        line('collar',[(-.26,-.20,1.26),(0,-.28,1.12),(.26,-.20,1.26)],.035,GOLD,torso)
    else:
        line('robe_lapel',[(-.26,-.20,1.26),(.07,-.28,.82),(.09,-.27,.71)],.025,IVORY,torso)
    if armored:
        for i in range(5):
            xx=(i-2)*.135
            blade('armored_tasset',xx,-.25,.62,[(-.065,0),(-.072,-.23),(.06,-.25),(.065,0)],GOLD,torso)
        obj('sphere','chest_medallion',(0,-.32,1.09),(.105,.04,.105),GOLD,torso)
        obj('sphere','medallion_jade',(0,-.365,1.10),(.038,.018,.048),color,torso)
    # Large sculpted face, layered eyes and shaped eyebrows.
    obj('sphere','face',(0,0,1.79),(.52,.43,.46),skin,head)
    obj('sphere','hair_mass',(0,.10,1.97),(.54,.43,.40),hair,head)
    for i in range(5):
        xx=(i-2)*.17
        obj('sphere','sculpted_fringe',(xx,-.34,2.06),(.105,.055,.09),hair,head,rot=(0,(i-2)*.14,0))
    for sign in (-1,1):
        obj('sphere','ear',(sign*.51,.005,1.78),(.085,.07,.12),skin,head)
        obj('sphere','ear_inner',(sign*.545,-.055,1.78),(.03,.02,.07),(.73,.39,.28),head)
        obj('sphere','eye_white',(sign*.19,-.387,1.80),(.135,.065,.115),IVORY,head)
        obj('sphere','iris',(sign*.185,-.447,1.80),(.075,.027,.09),(.20,.11,.06),head)
        obj('sphere','pupil',(sign*.18,-.473,1.805),(.04,.012,.065),INK,head)
        obj('sphere','eye_light',(sign*.18-.018,-.487,1.84),(.020,.008,.023),(1,1,.98),head)
        line('eyebrow',[(sign*.08,-.43,1.94),(sign*.20,-.44,1.97),(sign*.32,-.40,1.94)],.029,hair,head)
        obj('sphere','cheek',(sign*.32,-.355,1.64),(.075,.022,.038),(.86,.42,.33),head)
    obj('sphere','nose',(0,-.444,1.71),(.06,.055,.065),skin,head)
    line('mouth',[(-.065,-.421,1.60),(0,-.441,1.58),(.065,-.421,1.60)],.014,(.44,.19,.14),head)
    if p.get('beard'):
        for i in range(5):
            obj('sphere','beard_lock',((i-2)*.06,-.34,1.49-abs(i-2)*.035),(.065,.07,.22 if p['beard']=='long' else .09),hair,head)
        line('moustache',[(-.18,-.40,1.64),(0,-.455,1.65),(.18,-.40,1.64)],.038,hair,head)
    if p.get('long_hair'):
        for i in range(5):
            obj('sphere','long_hair',((i-2)*.15,.28,1.54),(.14,.14,.44),hair,head)
        for i in range(3):
            obj('sphere','flower',(-.40+i*.055,-.25,2.10),(.07,.025,.07),IVORY,head)
    hat=p.get('headgear','bun')
    if hat in ('helmet','plumes'):
        obj('sphere','helmet',(0,.10,2.11),(.55,.45,.27),GOLD if p.get('gold_hat') else (.70,.77,.79),head)
        line('helmet_brow',[(-.43,-.22,2.05),(0,-.40,2.02),(.43,-.22,2.05)],.036,GOLD,head)
        obj('sphere','helmet_jewel',(0,-.405,2.08),(.075,.035,.105),color,head)
        for s in (-1,1): obj('cube','cheek_guard',(s*.45,-.035,1.83),(.075,.11,.20),GOLD,head,rot=(0,s*.20,0))
        for s in ((-1,1) if hat=='plumes' else (0,)):
            line('plume',[(s*.12,.08,2.29),(s*.20,.10,2.57),(s*.28,.25,2.74),(s*.40,.46,2.65)],.075,p.get('plume_color',(.64,.12,.10)),head)
    elif hat in ('headband','yellow_turban','green_cap'):
        c=(.70,.60,.20) if hat=='yellow_turban' else color
        obj('torus','headband',(0,0,2.03),(.51,.42,.45),c,head)
        if hat=='green_cap': obj('sphere','green_cap',(0,.09,2.19),(.40,.32,.25),color,head)
        else: obj('sphere','hair_bun',(0,.16,2.30),(.17,.15,.16),hair,head)
        line('headband_tail',[(.47,.18,2.01),(.58,.32,1.91),(.57,.43,1.69)],.055,c,head)
    else:
        obj('sphere','hair_bun',(0,.13,2.29),(.17,.15,.15),hair,head)
        obj('cyl','bun_band',(0,.13,2.28),(.18,.16,.032),GOLD,head)
        if hat=='scholar_hat': obj('cube','scholar_crown',(0,.10,2.21),(.26,.22,.16),color,head)
    if p.get('eyepatch'):
        obj('sphere','eyepatch',(-.19,-.486,1.82),(.14,.023,.13),INK,head)
        line('eyepatch_strap',[(-.47,-.12,2.00),(-.19,-.46,1.89),(.39,-.28,1.56)],.025,INK,head)
    # Cape has an actual curved silhouette, not a flat rectangular plane.
    blade('cape',0,.26,.92,[(-.32,.29),(-.43,-.18),(-.36,-.48),(0,-.40),(.42,-.53),(.47,-.16),(.32,.29)],color,torso)
    weapon(kind,r,l)
    # Different silhouette and hand position for each weapon family.
    r.rotation_euler.y=math.radians(18 if kind in ('glaive','halberd','spear') else 15 if kind=='bow' else 25)
    r.rotation_euler.x=math.radians(-65 if kind=='bow' else -28 if pose=='caster' else 0)
    l.rotation_euler.x=math.radians(-65 if kind=='bow' else -35 if pose=='caster' else -30 if pose=='guard' else 5)
    head.rotation_euler.z=math.radians(-4 if pose=='archer' else 3 if pose=='caster' else 0)
    return root

HEROES={
 'guanyu':dict(robe=(.13,.38,.24),weapon='glaive',headgear='green_cap',beard='long',skin=(.78,.47,.31)),
 'liubei':dict(robe=(.47,.51,.24),weapon='sword',headgear='bun',beard='short'),
 'zhangfei':dict(robe=(.58,.18,.15),weapon='spear',headgear='headband',beard='short'),
 'r_shield':dict(robe=(.63,.22,.17),weapon='shield',headgear='helmet',gold_hat=True),
 'r_sword':dict(robe=(.63,.22,.17),weapon='sword',headgear='headband'),
 'r_archer':dict(robe=(.15,.35,.59),weapon='bow',headgear='headband'),
 'r_healer':dict(robe=(.22,.49,.36),weapon='healer',long_hair=True),
 'r_mage':dict(robe=(.42,.24,.57),weapon='staff',headgear='scholar_hat'),
 'r_strategist':dict(robe=(.30,.45,.57),weapon='fan',headgear='scholar_hat'),
 'lvbu':dict(robe=(.64,.15,.16),weapon='halberd',headgear='plumes',gold_hat=True),
 'xiahoudun':dict(robe=(.17,.25,.48),weapon='sword',headgear='bun',beard='short',eyepatch=True),
 'gongsunzan':dict(robe=(.66,.77,.82),weapon='bow',headgear='helmet',hair=(.16,.13,.11),plume_color=IVORY),
 'zhangjiao':dict(robe=(.68,.49,.14),weapon='staff',headgear='scholar_hat',beard='long',hair=(.81,.81,.73)),
 'xunyu':dict(robe=(.41,.26,.52),weapon='fan',headgear='scholar_hat'),
 'huatuo':dict(robe=(.30,.48,.24),weapon='healer',headgear='green_cap',beard='long',hair=(.82,.82,.75)),
 'zhaoyun':dict(robe=(.48,.65,.75),weapon='spear',headgear='helmet'),
 'huangzhong':dict(robe=(.61,.39,.19),weapon='bow',headgear='helmet',beard='long',hair=(.75,.75,.72)),
 'pangtong':dict(robe=(.48,.30,.38),weapon='staff',headgear='scholar_hat',beard='short'),
 'zhugeliang':dict(robe=(.56,.62,.55),weapon='fan',headgear='scholar_hat'),
}
for name,kind,c in [('zhoucang','shield',(.33,.35,.25)),('huangfusong','shield',(.59,.30,.19)),('huaxiong','sword',(.40,.22,.18)),('zhujun','sword',(.30,.41,.40)),('handang','bow',(.56,.32,.18)),('zoujing','bow',(.22,.36,.59)),('zhangbao','staff',(.40,.25,.58)),('yuji','staff',(.46,.56,.54)),('jianyong','fan',(.50,.40,.27)),('luzhi','fan',(.34,.42,.58)),('zhangzhongjing','healer',(.46,.49,.33)),('ganfuren','healer',(.34,.48,.38))]:
    HEROES[name]=dict(robe=c,weapon=kind,headgear='helmet' if kind in ('shield','sword','bow') else 'scholar_hat')
HEROES['ur_guanyu']=HEROES['guanyu']; HEROES['ur_zhangfei']=HEROES['zhangfei']
for name,p in old.ENEMIES.items():
    HEROES[name]=dict(robe=p['robe'],weapon='bow' if p.get('weapon')=='bow' else 'staff' if p.get('weapon')=='fan' else 'shield' if 'ironbrute' in name else 'sword',headgear='yellow_turban',beard=p.get('beard'))
for alias,base in {'r_militia':'r_sword','r_villager':'r_strategist','bandit_grunt':'yt_soldier','bandit_archer':'yt_archer','bandit_marksman':'yt_sharpshooter','bandit_ironbrute':'yt_ironbrute','bandit_shaman':'yt_warlock','bandit_second':'yt_lieutenant','bandit_deputy':'yt_brute','bandit_king':'yt_chief','tutorial_hunter':'r_archer','tutorial_scholar':'r_strategist','tutorial_wanderer':'r_sword'}.items(): HEROES[alias]=HEROES[base]

def main():
    args=sys.argv[sys.argv.index('--')+1:]; out=os.path.abspath(args[0]); os.makedirs(out,exist_ok=True)
    for name,p in HEROES.items():
        old.clear_scene(); root=build(name,p)
        bpy.ops.object.select_all(action='SELECT')
        bpy.ops.export_scene.fbx(filepath=os.path.join(out,name+'.fbx'),use_selection=True,object_types={'EMPTY','MESH'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_ALL',add_leaf_bones=False,mesh_smooth_type='FACE',use_mesh_modifiers=True)
        print('CHIBI_V2',name,flush=True)
    # Editable native source for the representative playable models.
    old.clear_scene()
    for i,name in enumerate(('guanyu','r_shield','r_archer','r_healer','lvbu','xiahoudun','gongsunzan')):
        root=build(name,HEROES[name]); root.location.x=i*2.7
    source=os.path.abspath(os.path.join(out,'../../../../art/chibi-combat-v2.blend'))
    os.makedirs(os.path.dirname(source),exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=source)
if __name__=='__main__': main()
