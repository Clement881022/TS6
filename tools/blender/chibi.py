"""
Q 版武將程式化建模（Blender 腳本，不需開 GUI）。

用法（在專案根目錄）：
  預覽圖： blender -b --python tools/blender/chibi.py -- preview build/previews
  匯出 FBX：blender -b --python tools/blender/chibi.py -- export client/Assets/Resources/Characters

座標：Blender 為 Z 朝上，角色面向 -Y。每名角色由參數表（HEROES / ENEMIES）描述，
新增角色只要加一筆參數，不需要改建模程式。

匯出的節點階層（Unity 端用名稱尋找這些樞紐做程式動畫）：
  <id>
   ├ legs / shoes（靜態）
   └ pivot_torso
      ├ 身體、腰帶、肩甲
      ├ pivot_head   （頭、臉、髮、帽、鬍子、耳）
      ├ pivot_arm_R  （右臂、右手、右手武器）
      └ pivot_arm_L  （左臂、左手、左手武器）
"""
import math
import os
import sys

import bpy


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.objects):
        for item in list(block):
            block.remove(item)
    _mat_cache.clear()


_mat_cache = {}


def material(color):
    key = tuple(round(c, 3) for c in color)
    if key in _mat_cache:
        return _mat_cache[key]
    m = bpy.data.materials.new("m_%s" % "".join("%02x" % int(c * 255) for c in color))
    m.diffuse_color = (color[0], color[1], color[2], 1.0)
    try:
        m.use_nodes = True
    except Exception:
        pass
    if m.node_tree:
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = (color[0], color[1], color[2], 1.0)
            bsdf.inputs["Roughness"].default_value = 0.8
    _mat_cache[key] = m
    return m


def attach(obj, parent):
    """掛到 parent 底下並保持世界位置（用 parent 的逆矩陣補償）。"""
    obj.parent = parent
    bpy.context.view_layer.update()
    obj.matrix_parent_inverse = parent.matrix_world.inverted()


def pivot(name, loc, parent):
    e = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(e)
    e.location = loc
    attach(e, parent)
    return e


def prim(kind, name, loc, scale, color, parent, rot=(0, 0, 0), minor=0.1):
    """建立一個基本形體；scale 即各軸半徑 / 半高，rot 為弧度，loc 為世界座標。"""
    if kind == "sphere":
        bpy.ops.mesh.primitive_uv_sphere_add(segments=28, ring_count=16, radius=1, location=loc, rotation=rot)
    elif kind == "cyl":
        bpy.ops.mesh.primitive_cylinder_add(vertices=28, radius=1, depth=2, location=loc, rotation=rot)
    elif kind == "cone":
        bpy.ops.mesh.primitive_cone_add(vertices=28, radius1=1, radius2=0, depth=2, location=loc, rotation=rot)
    elif kind == "cube":
        bpy.ops.mesh.primitive_cube_add(size=2, location=loc, rotation=rot)
    elif kind == "torus":
        bpy.ops.mesh.primitive_torus_add(major_radius=1, minor_radius=minor, major_segments=32,
                                         minor_segments=12, location=loc, rotation=rot)
    else:
        raise ValueError(kind)
    o = bpy.context.active_object
    o.name = name
    o.scale = scale
    o.data.materials.append(material(color))
    if kind in ("sphere", "cyl", "cone", "torus"):
        bpy.ops.object.shade_smooth()
    attach(o, parent)
    return o


SKIN = (0.96, 0.78, 0.64)
DARK = (0.08, 0.07, 0.09)
GOLD = (0.9, 0.7, 0.2)
STEEL = (0.78, 0.82, 0.88)
WOOD = (0.45, 0.3, 0.18)


def build_weapon(kind, arm_r, arm_l, hand_r, hand_l):
    """武器掛在手臂樞紐上，手臂揮動時武器跟著動。"""
    x = hand_r[0]
    if kind == "guandao":
        prim("cyl", "wp_shaft", (x, -0.02, 0.62), (0.018, 0.018, 0.62), WOOD, arm_r)
        prim("cube", "wp_blade", (x - 0.1, -0.02, 1.2), (0.11, 0.012, 0.2), (0.35, 0.8, 0.6), arm_r)
        prim("cube", "wp_blade_back", (x + 0.03, -0.02, 1.2), (0.03, 0.014, 0.16), GOLD, arm_r)
        prim("cone", "wp_tip", (x, -0.02, 1.47), (0.03, 0.03, 0.09), STEEL, arm_r)
    elif kind == "serpent":
        prim("cyl", "wp_shaft", (x, -0.02, 0.62), (0.02, 0.02, 0.62), (0.15, 0.12, 0.14), arm_r)
        prim("cone", "wp_tip", (x, -0.02, 1.35), (0.045, 0.02, 0.17), STEEL, arm_r)
    elif kind == "twin_swords":
        for hx, parent in ((hand_r[0], arm_r), (hand_l[0], arm_l)):
            prim("cube", "wp_blade", (hx, -0.08, 0.5), (0.035, 0.012, 0.22), STEEL, parent, rot=(math.radians(20), 0, 0))
            prim("cube", "wp_guard", (hx, -0.04, 0.31), (0.075, 0.012, 0.016), GOLD, parent)
    elif kind == "fan":
        prim("sphere", "wp_fan", (hand_l[0] - 0.04, -0.1, 0.42), (0.13, 0.014, 0.17), (0.97, 0.97, 0.95), arm_l,
             rot=(0, math.radians(25), math.radians(10)))
        prim("cyl", "wp_fan_handle", (hand_l[0] - 0.02, -0.1, 0.28), (0.012, 0.012, 0.06), WOOD, arm_l)
    elif kind == "silver_spear":
        prim("cyl", "wp_shaft", (x, -0.02, 0.62), (0.016, 0.016, 0.62), STEEL, arm_r)
        prim("cone", "wp_tip", (x, -0.02, 1.33), (0.035, 0.02, 0.14), STEEL, arm_r)
        prim("sphere", "wp_tassel", (x, -0.02, 1.18), (0.04, 0.04, 0.05), (0.85, 0.15, 0.15), arm_r)
    elif kind == "bow":
        hx = hand_l[0]
        prim("cube", "wp_bow_top", (hx - 0.06, -0.05, 0.66), (0.014, 0.02, 0.1), WOOD, arm_l, rot=(0, math.radians(-22), 0))
        prim("cube", "wp_bow_mid", (hx - 0.09, -0.05, 0.5), (0.016, 0.02, 0.09), WOOD, arm_l)
        prim("cube", "wp_bow_bot", (hx - 0.06, -0.05, 0.34), (0.014, 0.02, 0.1), WOOD, arm_l, rot=(0, math.radians(22), 0))
        prim("cube", "wp_string", (hx - 0.035, -0.05, 0.5), (0.004, 0.004, 0.26), (0.9, 0.9, 0.85), arm_l)
    elif kind == "club":
        prim("cyl", "wp_shaft", (x, -0.02, 0.5), (0.03, 0.03, 0.4), WOOD, arm_r)
        prim("sphere", "wp_head", (x, -0.02, 0.98), (0.09, 0.09, 0.13), (0.3, 0.25, 0.25), arm_r)


def build_hero(name, p):
    root = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(root)
    s = p.get("width", 1.0)

    skin = p.get("skin", SKIN)
    robe = p["robe"]
    pants = p.get("pants", DARK)
    belt = p.get("belt", GOLD)
    hair = p.get("hair", DARK)

    torso = pivot("pivot_torso", (0, 0, 0.32), root)
    head = pivot("pivot_head", (0, 0, 0.62), torso)
    arm_r = pivot("pivot_arm_R", (0.27 * s, 0, 0.52), torso)
    arm_l = pivot("pivot_arm_L", (-0.27 * s, 0, 0.52), torso)

    for sx in (-1, 1):
        prim("cyl", "leg", (sx * 0.09 * s, 0, 0.12), (0.075 * s, 0.075 * s, 0.12), pants, root)
        prim("sphere", "shoe", (sx * 0.09 * s, -0.04, 0.03), (0.09 * s, 0.13, 0.05), DARK, root)

    prim("sphere", "body", (0, 0, 0.42), (0.23 * s, 0.18 * s, 0.22), robe, torso)
    prim("cyl", "belt", (0, 0, 0.34), (0.228 * s, 0.178 * s, 0.025), belt, torso)

    hands = {}
    for sx, pv in ((1, arm_r), (-1, arm_l)):
        prim("sphere", "arm_mesh", (sx * 0.27 * s, 0, 0.43), (0.07, 0.07, 0.14), robe, pv, rot=(0, sx * 0.3, 0))
        hp = (sx * 0.31 * s, -0.02, 0.31)
        prim("sphere", "hand_mesh", hp, (0.065, 0.065, 0.065), skin, pv)
        hands[sx] = hp

    prim("sphere", "head_mesh", (0, 0, 0.82), (0.31, 0.28, 0.28), skin, head)
    for sx in (-1, 1):
        prim("sphere", "eye", (sx * 0.1, -0.262, 0.83), (0.034, 0.02, 0.05), DARK, head)
        prim("sphere", "blush", (sx * 0.17, -0.245, 0.77), (0.04, 0.012, 0.025), (0.95, 0.55, 0.5), head)
    prim("sphere", "mouth", (0, -0.272, 0.745), (0.03, 0.01, 0.012), (0.55, 0.25, 0.25), head)

    prim("sphere", "hair", (0, 0.045, 0.91), (0.32, 0.29, 0.22), hair, head)

    headgear = p.get("headgear")
    if headgear == "bun":
        prim("sphere", "bun", (0, 0.02, 1.13), (0.1, 0.1, 0.09), hair, head)
    elif headgear == "scholar_hat":
        c = p.get("hat_color", (0.95, 0.95, 0.92))
        prim("cube", "hat", (0, 0.03, 1.1), (0.15, 0.13, 0.13), c, head)
        prim("cube", "hat_top", (0, 0.03, 1.27), (0.1, 0.09, 0.05), c, head)
    elif headgear == "headband":
        prim("torus", "band", (0, 0.0, 0.97), (0.31, 0.28, 0.31), p.get("band_color", (0.9, 0.15, 0.15)), head, minor=0.045)
    elif headgear == "helmet":
        prim("sphere", "helmet", (0, 0.04, 0.95), (0.335, 0.31, 0.2), p.get("hat_color", STEEL), head)
        prim("cone", "plume", (0, 0.08, 1.16), (0.04, 0.04, 0.16), (0.85, 0.15, 0.15), head)
    elif headgear == "yellow_turban":
        prim("torus", "band", (0, 0.0, 0.97), (0.31, 0.28, 0.31), (0.95, 0.8, 0.15), head, minor=0.06)
        prim("sphere", "knot", (0.28, 0.0, 0.97), (0.07, 0.07, 0.07), (0.95, 0.8, 0.15), head)

    beard = p.get("beard")
    if beard == "long":
        prim("cone", "beard", (0, -0.2, 0.55), (0.15, 0.07, 0.25), p.get("beard_color", DARK), head, rot=(math.pi, 0, 0))
    elif beard == "bushy":
        prim("sphere", "beard", (0, -0.2, 0.69), (0.2, 0.1, 0.1), p.get("beard_color", DARK), head)
        prim("sphere", "mustache", (0, -0.26, 0.74), (0.12, 0.04, 0.03), p.get("beard_color", DARK), head)

    if p.get("big_ears"):
        for sx in (-1, 1):
            prim("sphere", "ear", (sx * 0.31, 0, 0.82), (0.05, 0.035, 0.09), skin, head)

    if p.get("pauldrons"):
        for sx, pv in ((1, arm_r), (-1, arm_l)):
            prim("sphere", "pauldron", (sx * 0.27 * s, 0, 0.55), (0.1, 0.1, 0.06), p["pauldrons"], pv)

    build_weapon(p.get("weapon", ""), arm_r, arm_l, hands[1], hands[-1])
    return root


HEROES = {
    "liubei": dict(robe=(0.2, 0.55, 0.38), belt=GOLD, headgear="bun", big_ears=True, weapon="twin_swords"),
    "guanyu": dict(robe=(0.12, 0.5, 0.26), skin=(0.85, 0.4, 0.33), beard="long", beard_color=(0.07, 0.07, 0.08),
                   headgear="helmet", hat_color=(0.12, 0.5, 0.26), pauldrons=(0.85, 0.7, 0.2), weapon="guandao"),
    "zhangfei": dict(robe=(0.2, 0.2, 0.25), skin=(0.78, 0.55, 0.42), beard="bushy", width=1.18,
                     headgear="headband", band_color=(0.85, 0.2, 0.15), pauldrons=(0.45, 0.45, 0.5), weapon="serpent"),
    "zhugeliang": dict(robe=(0.85, 0.9, 0.92), belt=(0.3, 0.55, 0.7), headgear="scholar_hat", weapon="fan"),
    "zhaoyun": dict(robe=(0.92, 0.94, 0.98), belt=(0.25, 0.4, 0.8), headgear="helmet", hat_color=STEEL,
                    pauldrons=(0.3, 0.45, 0.85), weapon="silver_spear"),
    "r_villager": dict(robe=(0.6, 0.5, 0.35), belt=(0.4, 0.3, 0.2), width=0.9, headgear="bun"),
    "r_militia": dict(robe=(0.55, 0.45, 0.3), belt=(0.4, 0.3, 0.2), headgear="headband", band_color=(0.85, 0.85, 0.8),
                      weapon="twin_swords"),
    "r_shield": dict(robe=(0.4, 0.42, 0.5), width=1.15, belt=(0.3, 0.3, 0.35), headgear="helmet",
                     hat_color=(0.5, 0.52, 0.58), pauldrons=(0.45, 0.47, 0.52), weapon="serpent"),
    "r_archer": dict(robe=(0.35, 0.5, 0.35), width=0.92, belt=(0.4, 0.3, 0.2), headgear="headband",
                     band_color=(0.3, 0.6, 0.35), weapon="bow"),
    "r_healer": dict(robe=(0.9, 0.9, 0.85), belt=(0.35, 0.65, 0.4), width=0.95, headgear="bun", weapon="fan"),
    "huangzhong": dict(robe=(0.55, 0.2, 0.18), belt=GOLD, hair=(0.82, 0.82, 0.85), beard="long",
                       beard_color=(0.85, 0.85, 0.88), headgear="headband", band_color=(0.9, 0.7, 0.2),
                       pauldrons=(0.6, 0.45, 0.15), weapon="bow"),
}

ENEMIES = {
    "yt_soldier": dict(robe=(0.65, 0.5, 0.3), headgear="yellow_turban", weapon="serpent"),
    "yt_archer": dict(robe=(0.55, 0.45, 0.3), headgear="yellow_turban", width=0.9, weapon="bow"),
    "yt_sharpshooter": dict(robe=(0.5, 0.2, 0.18), headgear="yellow_turban", width=0.95, pauldrons=(0.4, 0.3, 0.2),
                            weapon="bow"),
    "yt_priest": dict(robe=(0.85, 0.75, 0.3), belt=(0.45, 0.3, 0.2), width=0.92, headgear="yellow_turban", beard="long",
                      beard_color=(0.85, 0.85, 0.88), weapon="fan"),
    "yt_chief": dict(robe=(0.75, 0.15, 0.12), skin=(0.82, 0.58, 0.42), beard="long", beard_color=(0.1, 0.1, 0.1), width=1.3,
                     headgear="helmet", hat_color=(0.85, 0.7, 0.2), pauldrons=(0.85, 0.7, 0.2), weapon="guandao"),
    "yt_warlock": dict(robe=(0.45, 0.2, 0.55), belt=(0.85, 0.7, 0.2), width=0.92, headgear="yellow_turban", beard="long",
                       beard_color=(0.15, 0.15, 0.2), weapon="fan"),
    "yt_lieutenant": dict(robe=(0.6, 0.25, 0.15), skin=(0.82, 0.58, 0.42), beard="bushy", beard_color=(0.1, 0.1, 0.1), width=1.2,
                          headgear="helmet", hat_color=(0.7, 0.6, 0.2), pauldrons=(0.7, 0.6, 0.2), weapon="serpent"),
    "yt_zhangjiao": dict(robe=(0.95, 0.8, 0.2), belt=(0.6, 0.15, 0.15), width=1.25, headgear="yellow_turban", beard="long",
                         beard_color=(0.9, 0.9, 0.92), hair=(0.9, 0.9, 0.92), pauldrons=(0.85, 0.15, 0.15), weapon="fan"),
    "yt_ironbrute": dict(robe=(0.4, 0.4, 0.45), skin=(0.8, 0.6, 0.45), beard="bushy", width=1.45,
                         headgear="helmet", hat_color=(0.45, 0.47, 0.52), pauldrons=(0.5, 0.52, 0.58), weapon="club"),
    "yt_brute": dict(robe=(0.5, 0.38, 0.22), skin=(0.8, 0.6, 0.45), beard="bushy", width=1.35,
                     headgear="yellow_turban", weapon="club"),
}


def setup_render(width, height, ortho):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.image_settings.file_format = "PNG"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_object_outline = True
    scene.display.shading.object_outline_color = (0, 0, 0)
    scene.display.render_aa = "8"
    world = bpy.data.worlds.new("w")
    world.color = (0.13, 0.14, 0.18)
    scene.world = world

    cam_data = bpy.data.cameras.new("cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = ortho
    cam = bpy.data.objects.new("cam", cam_data)
    bpy.context.collection.objects.link(cam)
    cam.location = (0, -8, 1.45)
    cam.rotation_euler = (math.radians(84), 0, 0)
    scene.camera = cam


def preview(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    clear_scene()
    names = list(HEROES) + list(ENEMIES)
    spacing = 1.15
    start = -(len(names) - 1) * spacing / 2
    for i, name in enumerate(names):
        root = build_hero(name, HEROES.get(name) or ENEMIES[name])
        root.location = (start + i * spacing, 0, 0)

    setup_render(2000, 520, ortho=len(names) * spacing + 0.4)
    bpy.context.scene.render.filepath = os.path.join(out_dir, "heroes_preview.png")
    bpy.ops.render.render(write_still=True)
    print("rendered", bpy.context.scene.render.filepath)


def export_all(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    for name, params in list(HEROES.items()) + list(ENEMIES.items()):
        clear_scene()
        root = build_hero(name, params)
        bpy.ops.object.select_all(action="DESELECT")
        root.select_set(True)
        for child in root.children_recursive:
            child.select_set(True)
        path = os.path.join(out_dir, name + ".fbx")
        bpy.ops.export_scene.fbx(
            filepath=path,
            use_selection=True,
            object_types={"EMPTY", "MESH"},
            apply_scale_options="FBX_SCALE_ALL",
            axis_forward="-Z",
            axis_up="Y",
            mesh_smooth_type="FACE",
            add_leaf_bones=False,
        )
        print("exported", path)


def main():
    argv = sys.argv
    args = argv[argv.index("--") + 1:] if "--" in argv else []
    mode = args[0] if args else "preview"
    out_dir = args[1] if len(args) > 1 else "."
    if mode == "export":
        export_all(out_dir)
    else:
        preview(out_dir)


if __name__ == "__main__":
    main()
