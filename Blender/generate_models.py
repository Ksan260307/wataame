"""わたあめメーカー: 3D モデル生成スクリプト (Blender 4.2+ / 5.x)

わたあめ機と棒のメッシュをプロシージャルに作り、Unity 用の FBX として書き出す。

使い方:
    blender -b --python Blender/generate_models.py -- --out Assets/Resources/Models [--blend Blender/wataame_models.blend]

寸法はメートル単位。Unity 側のゲーム定数 (GameConfig.cs) と一致させている:
    ヘッド中心の高さ 0.24 / ボウル縁の高さ 0.40 / ボウル内径 0.30 / 棒の長さ 0.42
"""

import argparse
import math
import os
import sys

import bmesh
import bpy

SEGMENTS = 64

# 名前は Unity 側 (MachineView.cs) でマテリアル割り当てに使う
COLORS = {
    "Body": (1.00, 0.78, 0.84, 1.0),
    "Accent": (0.98, 0.97, 0.95, 1.0),
    "Metal": (0.80, 0.82, 0.86, 1.0),
    "Dark": (0.35, 0.30, 0.34, 1.0),
    "Lamp": (1.00, 0.35, 0.40, 1.0),
    "Switch": (1.00, 0.45, 0.55, 1.0),
    "Wood": (0.93, 0.80, 0.60, 1.0),
}


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", default="Assets/Resources/Models")
    parser.add_argument("--blend", default="")
    return parser.parse_args(argv)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0


def material(name):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        mat.diffuse_color = COLORS[name]
        if mat.node_tree is None:
            mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        if bsdf is not None:
            bsdf.inputs["Base Color"].default_value = COLORS[name]
            bsdf.inputs["Roughness"].default_value = 0.25 if name == "Metal" else 0.5
            bsdf.inputs["Metallic"].default_value = 1.0 if name == "Metal" else 0.0
    return mat


def make_object(name, bm, location=(0.0, 0.0, 0.0), mat_name="Body", smooth=True, bevel=0.0):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for poly in mesh.polygons:
        poly.use_smooth = smooth
    obj = bpy.data.objects.new(name, mesh)
    obj.location = location
    obj.data.materials.append(material(mat_name))
    if bevel > 0.0:
        # 角の丸みは Bevel モディファイアで付ける (FBX 書き出し時に適用される)
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 3
        mod.limit_method = "ANGLE"
    bpy.context.collection.objects.link(obj)
    return obj


def lathe(profile, segments=SEGMENTS, closed=False, radius_fn=None, origin_z=0.0):
    """(r, z) のプロファイルを Z 軸まわりに回転させたメッシュを作る。

    r == 0 の点は軸上の 1 頂点にまとめる。closed=True なら最後の点と最初の点もつなぐ。
    radius_fn(theta, r) で角度ごとに半径を変形できる (ヘッドの溝など)。
    """
    bm = bmesh.new()
    rings = []
    for r, z in profile:
        if r <= 1e-6:
            rings.append([bm.verts.new((0.0, 0.0, z - origin_z))])
            continue
        ring = []
        for i in range(segments):
            theta = 2.0 * math.pi * i / segments
            rr = radius_fn(theta, r) if radius_fn else r
            ring.append(bm.verts.new((rr * math.cos(theta), rr * math.sin(theta), z - origin_z)))
        rings.append(ring)

    pairs = list(zip(rings, rings[1:]))
    if closed:
        pairs.append((rings[-1], rings[0]))
    for a, b in pairs:
        if len(a) == 1 and len(b) == 1:
            continue
        for i in range(segments):
            j = (i + 1) % segments
            if len(a) == 1:
                bm.faces.new((a[0], b[i], b[j]))
            elif len(b) == 1:
                bm.faces.new((a[i], b[0], a[j]))
            else:
                bm.faces.new((a[i], b[i], b[j], a[j]))
    return bm


def box(size):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co.x *= size[0]
        v.co.y *= size[1]
        v.co.z *= size[2]
    return bm


def build_machine():
    objects = []

    # 足元のリング
    objects.append(make_object("Foot", lathe([(0.0, 0.0), (0.30, 0.0), (0.31, 0.01), (0.30, 0.022), (0.0, 0.022)]), mat_name="Dark"))

    # 本体 (角の丸い円柱)
    body = [(0.0, 0.02), (0.31, 0.02)]
    for k in range(7):
        a = -math.pi / 2 + (math.pi / 2) * k / 6
        body.append((0.31 + 0.03 * math.cos(a), 0.05 + 0.03 * math.sin(a)))
    for k in range(7):
        a = (math.pi / 2) * k / 6
        body.append((0.31 + 0.03 * math.cos(a), 0.14 + 0.03 * math.sin(a)))
    body.append((0.0, 0.17))
    objects.append(make_object("Base", lathe(body), mat_name="Body"))

    # 操作パネル (正面 = -Y)
    objects.append(make_object("Panel", box((0.30, 0.07, 0.10)), location=(0.0, -0.315, 0.095), mat_name="Accent", smooth=False, bevel=0.012))

    # スイッチ: 原点がレバーの根元。Unity 側でローカル X 軸まわりに倒す
    lever = box((0.024, 0.05, 0.016))
    bmesh.ops.translate(lever, verts=lever.verts[:], vec=(0.0, -0.022, 0.0))
    objects.append(make_object("Switch", lever, location=(-0.07, -0.352, 0.095), mat_name="Switch", smooth=False, bevel=0.006))
    objects.append(make_object("SwitchPlate", box((0.06, 0.006, 0.05)), location=(-0.07, -0.352, 0.095), mat_name="Dark", smooth=False, bevel=0.002))

    # 電源ランプ
    lamp = bmesh.new()
    bmesh.ops.create_uvsphere(lamp, u_segments=24, v_segments=12, radius=0.014)
    objects.append(make_object("Lamp", lamp, location=(0.08, -0.352, 0.095), mat_name="Lamp"))

    # ボウル (厚みのある回転体)。内側を上り、縁で折り返して外側を下る
    inner = [(0.035, 0.178), (0.10, 0.178), (0.17, 0.188), (0.23, 0.215), (0.27, 0.26), (0.292, 0.32), (0.300, 0.385)]
    lip = []
    for k in range(9):
        a = math.pi - math.pi * k / 8
        lip.append((0.308 + 0.008 * math.cos(a), 0.392 + 0.010 * math.sin(a)))
    outer = [(0.308, 0.32), (0.282, 0.255), (0.24, 0.205), (0.175, 0.176), (0.10, 0.166), (0.035, 0.166)]
    objects.append(make_object("Bowl", lathe(inner + lip + outer, closed=True), mat_name="Metal"))

    # 回転軸
    objects.append(make_object("Spindle", lathe([(0.0, 0.16), (0.013, 0.16), (0.013, 0.21), (0.0, 0.21)], segments=24), mat_name="Metal"))

    # ヘッド (ざらめを溶かして飛ばす回転部)。原点を中心に置き Unity で Y 軸回転させる
    head_center = 0.24
    head_profile = [(0.0, 0.205), (0.040, 0.205), (0.052, 0.212), (0.056, 0.225), (0.056, 0.258), (0.050, 0.268), (0.030, 0.274), (0.0, 0.276)]
    grooves = lambda theta, r: r * (1.0 - 0.05 * max(0.0, math.cos(theta * 18.0)) ** 8) if r > 0.05 else r
    objects.append(make_object("Head", lathe(head_profile, radius_fn=grooves, origin_z=head_center), location=(0.0, 0.0, head_center), mat_name="Metal"))

    return objects


def build_stick():
    # 原点 = 棒の先端 (下端)。先を少しとがらせた木の棒
    profile = [(0.0, 0.0), (0.0035, 0.006), (0.0055, 0.02), (0.0055, 0.415), (0.004, 0.42), (0.0, 0.42)]
    return [make_object("Stick", lathe(profile, segments=16), mat_name="Wood")]


def export_fbx(objects, path):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        bake_anim=False,
    )
    print(f"[wataame] exported {path}")


def main():
    args = parse_args()
    out_dir = os.path.abspath(args.out)
    os.makedirs(out_dir, exist_ok=True)

    reset_scene()
    machine = build_machine()
    stick = build_stick()
    stick[0].location.x = 0.5  # .blend 上で重ならないように横へ置く

    export_fbx(machine, os.path.join(out_dir, "WataameMachine.fbx"))
    stick[0].location.x = 0.0
    export_fbx(stick, os.path.join(out_dir, "Stick.fbx"))
    stick[0].location.x = 0.5

    if args.blend:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(args.blend))
        print(f"[wataame] saved {args.blend}")


if __name__ == "__main__":
    main()
