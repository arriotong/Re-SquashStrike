import bpy, sys
src, out = sys.argv[sys.argv.index("--") + 1:][:2]
keep = {"Idle_Loop", "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop", "Roll", "Crouch_Idle_Loop", "Crouch_Fwd_Loop", "Jump_Start", "Jump_Land", "Hit_Chest", "Dance_Loop", "Death01", "Push_Loop"}
bpy.ops.wm.read_homefile(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
for o in list(bpy.data.objects):
    if o.type != 'ARMATURE': bpy.data.objects.remove(o, do_unlink=True)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
for a in list(bpy.data.actions):
    if a.name not in keep: bpy.data.actions.remove(a)
    else: a.use_fake_user = True
arm.animation_data_create()
# each kept action as its own NLA strip, so the FBX gets one take per clip
for a in bpy.data.actions:
    t = arm.animation_data.nla_tracks.new(); t.name = a.name; t.strips.new(a.name, int(a.frame_range[0]), a)
arm.animation_data.action = None
for o in bpy.data.objects: o.select_set(o == arm)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={'ARMATURE'}, add_leaf_bones=False, bake_anim=True,
                         bake_anim_use_all_actions=False, bake_anim_use_nla_strips=True, bake_anim_force_startend_keying=True,
                         axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS', bake_anim_simplify_factor=0.5)
print("UAL", out, sorted(a.name for a in bpy.data.actions))
