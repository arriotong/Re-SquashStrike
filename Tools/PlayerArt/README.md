# Player art pipeline

The real players are built from MakeHuman bodies with the MPFB add-on for Blender. A script generates each body and exports it as an FBX, with its textures and a parts list, into `Assets/SquashGame/Resources/Players/`.

## One-time setup
These are the 2026-11-01 downloads, kept in `C:\SQUASH\art-source\`, outside the repo.
- **Blender 5.2** with MPFB 2.0.18. Install it with `bpy.ops.extensions.package_install_files`.
- **MakeHuman asset packs**, loaded with `bpy.ops.mpfb.load_pack`: system assets, Skins 01–02, Shirts 01, Pants 03, Shoes 02, Underwear 04, Hair 02–03, Eyebrows 01, Eyelashes 01.
- **Quaternius Universal Animation Library [Standard]**.

## Make the 12 bodies
Run this once for each spec in `roster/` (m1..m6 and f1..f6: the Locker's six skin tones, male and female, with every Locker hair style):

```
blender -b --python Tools/PlayerArt/gen_player.py -- Tools/PlayerArt/roster/m1.json
```

The script does five things:
1. It builds the human: phenotype, the game_engine rig, the kit, eyes, eyebrows and the hair styles.
2. It pushes the shirt out from the shorts, so the two don't flicker through each other.
3. It bakes a T-pose rest, which Unity's Humanoid needs.
4. It exports the FBX.
5. It copies the textures (at most 1024) into `Players/Textures`. It also writes `<id>.json`, which lists each mesh's kind (skin, shirt, shorts, socks, shoes, hair with its style, eyes, eyebrows, eyelashes) and its textures.

## Motion capture
```
blender -b --python Tools/PlayerArt/ual_export.py -- "<UAL1_Standard.glb>" Assets/SquashGame/Resources/Players/Anim/UAL_Squash.fbx
```
This keeps only the clips the game uses, with no mannequin mesh. Mixamo clips (FBX for Unity, Without Skin, In Place) go in `Resources/Players/Anim/` too.

## In the game
- `Editor/PlayerArtImport.cs` imports everything as Humanoid.
- `Athlete/PlayerModels.cs` makes the materials and dresses the kit.
- `Athlete/AthleteBody.cs` draws the body from the code-built figure's pose, plus motion capture and IK.

Credits are in `Resources/Players/CREDITS.md`.
