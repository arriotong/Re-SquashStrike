# Blender - a Mixamo character (FBX for Unity, T-pose) as one of the game's players:
#   blender -b --python mixamo_export.py -- <character.fbx> <out dir (Resources/Players)> <id>
# the FBX again (armature + meshes, no animation), its textures (at most 1024) in <out>/Textures, and <id>.json: each
# mesh's kind (skin, shirt, shorts, shoes, hair, eyes, eyelashes) and its textures - the same parts list as the MakeHuman bodies.
import bpy, sys, os, json

src, out_dir, pid = sys.argv[sys.argv.index("--") + 1:][:3]
bpy.ops.wm.read_homefile(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
rig = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
if rig.animation_data: rig.animation_data.action = None
meshes = [o for o in bpy.data.objects if o.type == 'MESH']

def kind(name):
    n = name.lower()
    if "eyelash" in n: return "eyelashes"
    if "eye" in n: return "eyes"
    if "hair" in n: return "hair"
    if any(k in n for k in ("shirt", "top", "hood", "coat", "jacket", "tshirt")): return "shirt"
    if any(k in n for k in ("short", "bottom", "pant", "trouser", "jean")): return "shorts"
    if any(k in n for k in ("sneaker", "shoe", "boot")): return "shoes"
    if "sock" in n: return "socks"
    return "skin"

tex_dir = os.path.join(out_dir, "Textures")
os.makedirs(tex_dir, exist_ok=True)

def save(img, tag):
    """An image of the FBX (packed or beside it) saved at most 1024, named for this player."""
    name = pid + "_" + tag
    dst = os.path.join(tex_dir, name + ".png")
    im = img.copy()
    w, h = im.size
    k = min(1.0, 1024.0 / max(w, h, 1))
    if k < 1.0: im.scale(int(w * k), int(h * k))
    im.filepath_raw = dst; im.file_format = 'PNG'; im.save()
    return name

manifest = []
done = {}
for o in meshes:
    entry = {"name": o.name, "kind": kind(o.name), "style": ""}
    m = o.material_slots[0].material if o.material_slots else None
    # a single mesh with several materials (Ch12): one entry per material is not supported - its first material speaks
    if m and m.use_nodes:
        for n in m.node_tree.nodes:
            if n.type != 'TEX_IMAGE' or not n.image: continue
            links = [l.to_socket.name for l in n.outputs[0].links] + [l.to_node.type for l in n.outputs[0].links]
            role = "normal" if any("Normal" in str(x) or x == 'NORMAL_MAP' for x in links) else \
                   "rough" if any("Rough" in str(x) or "Spec" in str(x) for x in links) else \
                   "albedo" if any("Base Color" in str(x) for x in links) else None
            if role is None or role in entry: continue
            key = (n.image.name, role)
            if key not in done: done[key] = save(n.image, (m.name + "_" + role).replace(" ", "_").replace(":", "_"))
            entry[role] = done[key]
    manifest.append(entry)

for o in bpy.data.objects: o.select_set(o == rig or o in meshes)
bpy.context.view_layer.objects.active = rig
fbx = os.path.join(out_dir, pid + ".fbx")
bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=False,
                         path_mode='AUTO', embed_textures=False, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL',
                         mesh_smooth_type='FACE', use_armature_deform_only=True)
json.dump({"meshes": manifest, "female": False, "skin": "", "hair": ""}, open(os.path.join(out_dir, pid + ".json"), "w"), indent=1)
print("MIXAMO", pid, json.dumps(manifest))
