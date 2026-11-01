# Blender (MPFB) - build one squash player from a JSON spec, render a preview, optionally export FBX.
#   blender -b --python gen_player.py -- spec.json
import bpy, sys, json, math, os
from mathutils import Vector
from bl_ext.user_default.mpfb.services.humanservice import HumanService

spec = json.load(open(sys.argv[sys.argv.index("--") + 1]))

bpy.ops.wm.read_homefile(use_empty=True)

info = HumanService._create_default_human_info_dict()
info["phenotype"] = spec["phenotype"]
info["rig"] = spec.get("rig", "game_engine")
info["eyes"] = spec.get("eyes", "low-poly.mhclo")
info["eyebrows"] = spec.get("eyebrows", "eyebrow001.mhclo")
info["eyelashes"] = spec.get("eyelashes", "eyelashes01.mhclo")
info["teeth"] = spec.get("teeth", "")
info["hair"] = spec.get("hair", "short02.mhclo")
info["clothes"] = spec.get("clothes", [])
info["skin_mhmat"] = spec["skin"]
info["skin_material_type"] = spec.get("skin_type", "ENHANCED")
info["eyes_material_type"] = "MAKESKIN"
info["alternative_materials"] = {}
info["targets"] = spec.get("targets", [])
settings = HumanService.get_default_deserialization_settings()
settings["subdiv_levels"] = 0
basemesh = HumanService.deserialize_from_dict(info, settings)
print("HUMAN", basemesh.name, len(basemesh.data.vertices))
from bl_ext.user_default.mpfb.services.assetservice import AssetService
hair_style = {}
for style, asset in spec.get("hairs", {}).items():
    if not asset: continue
    path = AssetService.find_asset_absolute_path(asset, asset_subdir="hair")
    if path is None: print("NOHAIR", asset); continue
    before = set(bpy.data.objects)
    HumanService.add_mhclo_asset(path, basemesh, asset_type="hair", subdiv_levels=0, material_type="MAKESKIN")
    for o in set(bpy.data.objects) - before:
        if o.type == 'MESH': hair_style[o.name] = style
for o in bpy.data.objects: print("OBJ", o.type, o.name, len(o.data.vertices) if o.type == 'MESH' else "")

# ---- the cartoon look (2026-11-01, the user's reference): clay skin, thick loose clothes standing off the body ----
if spec.get("style") == "cartoon":
    def flat(name, rgb, rough, keep_alpha_from=None):
        m = bpy.data.materials.new(name); m.use_nodes = True
        b = m.node_tree.nodes["Principled BSDF"]
        b.inputs["Base Color"].default_value = (*rgb, 1); b.inputs["Roughness"].default_value = rough
        if "Specular IOR Level" in b.inputs: b.inputs["Specular IOR Level"].default_value = 0.25
        if keep_alpha_from is not None:
            for n in keep_alpha_from.node_tree.nodes:
                if n.type == 'TEX_IMAGE' and n.image:
                    t = m.node_tree.nodes.new('ShaderNodeTexImage'); t.image = n.image
                    m.node_tree.links.new(t.outputs["Alpha"], b.inputs["Alpha"]); m.blend_method = 'CLIP' if hasattr(m, "blend_method") else None
                    break
        return m
    skin_rgb = tuple(spec.get("skin_rgb", (0.78, 0.52, 0.38)))
    from bl_ext.user_default.mpfb.services.objectservice import ObjectService as _OS
    for kb in (basemesh.data.shape_keys.key_blocks if basemesh.data.shape_keys else []):
        boost = spec.get("boost", {}).get(kb.name)
        if boost: kb.slider_max = max(kb.slider_max, boost); kb.value = boost
    for o in [basemesh] + [c for c in basemesh.parent.children_recursive if c.type == 'MESH' and c != basemesh] if basemesh.parent else [basemesh]:
        n = o.name.lower()
        if o == basemesh:
            o.data.materials.clear(); o.data.materials.append(flat("clay_skin", skin_rgb, 0.55))
            sub = o.modifiers.new("smooth", 'SUBSURF'); sub.levels = 1; sub.render_levels = 1
        elif str(_OS.get_object_type(o) or "").lower() == "hair":
            old = o.material_slots[0].material if o.material_slots else None
            o.data.materials.clear(); o.data.materials.append(flat("hair_" + o.name, tuple(spec.get("hair_rgb", (0.05, 0.03, 0.02))), 0.6, old))
            for v in o.data.vertices: v.co += v.normal * 0.004
            sol = o.modifiers.new("thick", 'SOLIDIFY'); sol.thickness = 0.006; sol.offset = 1.0
        elif any(k in n for k in ("shirt", "polo", "top", "short", "trunk", "pant", "sock")):
            col = spec.get("kit", {}).get("shirt" if any(k in n for k in ("shirt", "polo", "top")) else "socks" if "sock" in n else "shorts", (0.9, 0.9, 0.9))
            o.data.materials.clear(); o.data.materials.append(flat("cloth_" + o.name, tuple(col), 0.75))
            # thickness, and away from the body: the garment hangs, it isn't painted on
            push = 0.018 if any(k in n for k in ("shirt", "polo", "top")) else 0.012 if "sock" not in n else 0.003
            for v in o.data.vertices: v.co += v.normal * push
            sol = o.modifiers.new("thick", 'SOLIDIFY'); sol.thickness = 0.008 if "sock" not in n else 0.003; sol.offset = 1.0
            sub = o.modifiers.new("smooth", 'SUBSURF'); sub.levels = 1; sub.render_levels = 1
        elif "shoe" in n or "sneaker" in n:
            o.data.materials.clear(); o.data.materials.append(flat("shoes", tuple(spec.get("kit", {}).get("shoes", (0.95, 0.95, 0.95))), 0.5))
        elif n.split(".")[-1] in ("short01", "short02", "short03", "short04", "ponytail01", "bob02") or "hair" in n or "afro" in n or "bun" in n or "curly" in n or "side_do" in n:
            old = o.material_slots[0].material if o.material_slots else None
            o.data.materials.clear(); o.data.materials.append(flat("hair_" + o.name, tuple(spec.get("hair_rgb", (0.05, 0.03, 0.02))), 0.6, old))
            for v in o.data.vertices: v.co += v.normal * 0.004
            sol = o.modifiers.new("thick", 'SOLIDIFY'); sol.thickness = 0.006; sol.offset = 1.0
        elif "eyebrow" in n:
            old = o.material_slots[0].material if o.material_slots else None
            o.data.materials.clear(); o.data.materials.append(flat("brows", (0.06, 0.04, 0.03), 0.8, old))
# ---- preview render ----
if spec.get("render"):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE' if 'BLENDER_EEVEE' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE_NEXT'
    scene.render.resolution_x, scene.render.resolution_y = 640, 960
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("w"); scene.world = world; world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.18, 0.2, 0.24, 1); world.node_tree.nodes["Background"].inputs[1].default_value = 0.6
    def light(kind, loc, energy, size=2.0):
        l = bpy.data.lights.new(kind + str(loc), kind); l.energy = energy
        if kind == 'AREA': l.size = size
        o = bpy.data.objects.new(l.name, l); scene.collection.objects.link(o); o.location = loc
        o.rotation_euler = (Vector((0, 0, 1.0)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    light('AREA', (2.2, -3.0, 2.8), 900, 3); light('AREA', (-2.5, -1.5, 1.8), 300, 3); light('AREA', (0.5, 3.0, 2.5), 500, 2)
    cam_d = bpy.data.cameras.new("cam"); cam_d.lens = 60
    cam = bpy.data.objects.new("cam", cam_d); scene.collection.objects.link(cam); scene.camera = cam
    out = spec["render"]
    for i, (ang, z, tz, dist) in enumerate(spec.get("views", [(0, 1.0, 0.95, 4.6), (35, 1.0, 0.95, 4.6), (90, 1.0, 0.95, 4.6), (20, 1.62, 1.62, 1.1)])):
        a = math.radians(ang)
        cam.location = (dist * math.sin(a), -dist * math.cos(a), z)
        cam.rotation_euler = (Vector((0, 0, tz)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = out + "_%d.png" % i
        bpy.ops.render.render(write_still=True)
        print("RENDER", scene.render.filepath)

if spec.get("blend"):
    bpy.ops.wm.save_as_mainfile(filepath=spec["blend"])

# ---- FBX for Unity: masks and helpers baked away, one armature with its meshes, textures copied next to it ----
if spec.get("fbx"):
    from bl_ext.user_default.mpfb.services.exportservice import ExportService
    rig = basemesh.parent
    ExportService.bake_modifiers_remove_helpers(basemesh, bake_masks=True, bake_subdiv=False, remove_helpers=True, also_proxy=True)
    # ---- the shirt a few millimetres out from the shorts it hangs over (they flickered through each other at the waist) ----
    for o in rig.children_recursive:
        if o.type == 'MESH' and any(k in o.name.lower() for k in ("shirt", "polo", "top")):
            o.data.update()
            for v in o.data.vertices: v.co += v.normal * 0.006
            o.data.update()
    # ---- a T-pose rest (Unity's Humanoid retargets from a T-pose; MakeHuman stands in an A-pose) ----
    from mathutils import Matrix
    meshes = [o for o in rig.children_recursive if o.type == 'MESH']
    for o in meshes:
        bpy.context.view_layer.objects.active = o
        if o.data.shape_keys: bpy.ops.object.shape_key_remove(all=True, apply_mix=True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='POSE')
    for side, x in (("l", 1.0), ("r", -1.0)):
        for b in ("upperarm_", "lowerarm_", "hand_"):
            pb = rig.pose.bones.get(b + side)
            if pb is None: continue
            bpy.context.view_layer.update()
            cur = (pb.tail - pb.head).normalized()
            want = Vector((x, 0.0, 0.0))
            R = cur.rotation_difference(want).to_matrix().to_4x4()
            h = pb.head.copy()
            pb.matrix = Matrix.Translation(h) @ R @ Matrix.Translation(-h) @ pb.matrix
    bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode='OBJECT')
    for o in meshes:
        mods = [m for m in o.modifiers if m.type == 'ARMATURE']
        if not mods: continue
        bpy.context.view_layer.objects.active = o
        name = mods[0].name
        bpy.ops.object.modifier_apply(modifier=name)
        m = o.modifiers.new(name, 'ARMATURE'); m.object = rig
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='POSE')
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    print("TPOSE", [round(v, 2) for v in (rig.data.bones["upperarm_l"].tail_local - rig.data.bones["upperarm_l"].head_local).normalized()])
    for o in bpy.data.objects: o.select_set(False)
    rig.select_set(True)
    for o in rig.children_recursive:
        if o.type == 'MESH': o.select_set(True)
    out = spec["fbx"]
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
                             bake_anim=False, path_mode='AUTO', embed_textures=False, axis_forward='-Z', axis_up='Y',
                             apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE', use_armature_deform_only=True)
    print("FBX", out, [b.name for b in rig.data.bones][:12], len(rig.data.bones))
    for o in rig.children_recursive:
        if o.type == 'MESH':
            print("MESH", o.name, len(o.data.vertices), [s.material.name if s.material else None for s in o.material_slots])
    # ---- textures: every image a material uses (also inside node groups), copied and listed for Unity's importer ----
    import shutil
    def images(tree, seen):
        for n in tree.nodes:
            if n.type == 'TEX_IMAGE' and n.image: yield n
            if n.type == 'GROUP' and n.node_tree and n.node_tree.name not in seen:
                seen.add(n.node_tree.name); yield from images(n.node_tree, seen)
    tex_dir = os.path.join(os.path.dirname(out), "Textures")          # shared by every player (hair, kit, eyes...)
    os.makedirs(tex_dir, exist_ok=True)
    from bl_ext.user_default.mpfb.services.objectservice import ObjectService
    def kind(o):
        t = str(ObjectService.get_object_type(o) or "").lower()
        if t == "basemesh": return "skin"
        if t != "clothes": return t                                     # hair, eyes, eyebrows, eyelashes, teeth
        n = o.name.lower()
        if any(k in n for k in ("shirt", "polo", "top")): return "shirt"
        if any(k in n for k in ("short", "trunk", "pant")): return "shorts"
        if "sock" in n: return "socks"
        if any(k in n for k in ("shoe", "sneaker")): return "shoes"
        return "clothes"
    manifest = []
    for o in rig.children_recursive:
        if o.type != 'MESH' or not o.material_slots or not o.material_slots[0].material: continue
        mat = o.material_slots[0].material
        entry = {}
        if mat.node_tree:
            for n in images(mat.node_tree, set()):
                src = bpy.path.abspath(n.image.filepath)
                low = os.path.basename(src).lower()
                role = "normal" if "normal" in low else "bump" if "bump" in low else "rough" if ("spec" in low or "rough" in low) else "displace" if "displace" in low else "albedo"
                if role in ("bump", "displace"): continue
                if not os.path.isfile(src): continue
                dst = os.path.join(tex_dir, os.path.basename(src))
                if not os.path.isfile(dst):
                    im = bpy.data.images.load(src, check_existing=True)
                    w, h = im.size
                    k = min(1.0, spec.get("tex_max", 1024) / max(w, h, 1))
                    if k < 1.0:
                        im2 = im.copy(); im2.scale(int(w * k), int(h * k)); im2.filepath_raw = dst; im2.file_format = 'PNG'; im2.save()
                    else: shutil.copyfile(src, dst)
                entry.setdefault(role, os.path.basename(src))
        manifest.append({"name": o.name, "kind": kind(o), "style": hair_style.get(o.name, ""), **{k: os.path.splitext(v)[0] for k, v in entry.items()}})
    fbm = os.path.splitext(out)[0] + ".fbm"
    if os.path.isdir(fbm) and not os.listdir(fbm): os.rmdir(fbm)
    json.dump({"meshes": manifest, "female": spec["phenotype"]["gender"] < 0.5, "skin": spec.get("skin", ""), "hair": spec.get("hair", "")}, open(os.path.splitext(out)[0] + ".json", "w"), indent=1)
    print("MANIFEST", json.dumps(manifest))
