#!/usr/bin/env python
"""
Render the REAL capsule turntable sequence (BUILD_PLAN section 5, C2/C3; register T11b) from the
licence-clean 3D model in assets/reference/models/:

    GameData/DragonScreen/art/cover/dragon_turn_000.png ... dragon_turn_035.png

This is a BLENDER script - it will not run under plain python. Run it headless:

    "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" -b --factory-startup -noaudio \
        -P plugin/build/render_turntable.py

It replaces the marked stand-in frames written by make_turntable.py (T11a), which stays in the tree
as the generator of record for those - see its header for why the stand-in looked the way it did.

---- SOURCE + LICENCE ----
"Crew Dragon Falcon 9" by MaTte0 (@matteomansion), Sketchfab, CC-BY 4.0. Attribution is REQUIRED and
is recorded in assets/ASSET_PROVENANCE.md alongside these outputs. The model file itself is NOT in
git (all of assets/reference/ is gitignored by repo convention); it sits on disk at the path below,
which is why this script exists: the frames are the tracked artefact, and this is how they are
reproduced from the untracked source.

---- WHAT IS ISOLATED, AND HOW ----
The model is the WHOLE launch stack - Dragon, trunk, Falcon 9, legs, Merlins. Section 5 wants
"capsule-with-trunk", so everything else is deleted before the camera is placed. The split is made on
MATERIAL name, not object name: an object called "Trunk_Trunk1_0" is a naming convention the artist
chose and a re-export could change it, whereas the material assignment is what actually distinguishes
the Dragon shells from the booster. Both sets are asserted present, so a swapped or re-exported model
fails loudly here instead of quietly rendering a Falcon 9 onto the vehicle page.

---- THE ROTATION CONVENTION (this is the half that has to agree with src/pure/Turntable.cs) ----
Turntable.cs fixes two things this render must honour:

    frame 0     the front - the face the vehicle shows when the page opens and when the reset tap
                fires.
    direction   drag RIGHT advances the frame index, and the NEAR face must follow the finger.

Take a point on the near surface at body azimuth 0. With the camera on -Y looking towards +Y, screen
x = R*sin(alpha) after the vehicle is turned by alpha about +Z - so INCREASING alpha walks the near
face to the RIGHT, which is what "advances the frame index" has to mean. Frame i is therefore the
vehicle at alpha = i * 360/COUNT about +Z.

Rather than rotate the vehicle (whose parts hang off imported empties with baked transforms), the
CAMERA AND LIGHTS are parented to one rig empty on the axis and the rig is turned by -alpha. That is
the same picture, and it has the property that matters for a turntable: the lighting is fixed
relative to the CAMERA, so the lit side is always the same side of the screen and the vehicle turns
underneath it. Lights fixed in world space instead would make the sequence flicker between frames.

FRONT_AZIMUTH_DEG below is the one dial for "which way is the front": it rotates the whole sequence
against the model without touching the direction or the step.

---- DETERMINISTIC ----
Fixed camera, fixed lights, fixed seed. Cycles CPU is used on purpose - a GPU path would make the
output depend on which machine ran it.
"""
import math
import os
import sys

import bpy
import mathutils

# ---- the sequence. MUST match src/pure/Turntable.cs: Count, FrameW, FrameH, KeyPrefix. ----
COUNT = 36
W, H = 512, 1024
PREFIX = 'dragon_turn_'

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
MODEL = os.path.join(REPO, 'assets', 'reference', 'models', 'crew_dragon_falcon_9 (1).glb')
OUT_DIR = os.path.join(HERE, '..', 'GameData', 'DragonScreen', 'art', 'cover')

# The 1k-texture export is the one used: at 512 px across the whole vehicle the 4k twin
# (crew_dragon_falcon_9.glb, 32 MB) resolves to the same pixels and costs ten times the import.

# ---- the isolation, by material (see the header) ----
KEEP_MATERIALS = {'SpaceX_Dragon2.001', 'Capsule_trunk', 'Trunk1', 'Trunk2'}
DROP_MATERIALS = {'Falcon9', 'Legs', 'Merlin'}

# ---- framing ----
# Fraction of the sprite's WIDTH the vehicle spans. Width, not height, is the binding constraint: the
# vehicle is ~1.7 tall for every 1 across and the sprite is 1:2, so fitting the height would push the
# hull past both edges. The remaining vertical slack is split evenly.
WIDTH_FILL = 0.90

# Which azimuth OF THE MODEL is frame 0 - the face the vehicle shows the crew. It rotates the whole
# sequence together and changes nothing else; the value is set from the sweep (see the register note).
FRONT_AZIMUTH_DEG = 0.0

# ---- render settings ----
SAMPLES = 160


# =====================================================================================
def argv_after_dashes():
    return sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []


def opt(args, name, default=None):
    return args[args.index(name) + 1] if name in args else default


def import_model(path):
    if not os.path.exists(path):
        raise SystemExit('MODEL NOT FOUND: %s\n'
                         'assets/reference/ is gitignored - the model must be on disk here.' % path)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)


def isolate_dragon():
    """Delete everything that is not the Dragon capsule or its trunk. Returns the kept meshes."""
    keep, drop, unknown = [], [], []
    for o in list(bpy.data.objects):
        if o.type != 'MESH':
            continue
        mats = set(m.name for m in o.data.materials if m)
        if mats & KEEP_MATERIALS:
            keep.append(o)
        elif mats & DROP_MATERIALS:
            drop.append(o)
        else:
            unknown.append((o.name, sorted(mats)))

    if unknown:
        raise SystemExit('UNRECOGNISED MESH MATERIALS - the model is not the one this script was '
                         'written against, so the Dragon/booster split cannot be trusted: %s'
                         % unknown)
    if not keep:
        raise SystemExit('no Dragon meshes found (wanted materials %s)' % sorted(KEEP_MATERIALS))
    if not drop:
        raise SystemExit('no booster meshes found - expected the full stack (materials %s). '
                         'Refusing to render rather than guess what was isolated.'
                         % sorted(DROP_MATERIALS))

    print('KEEP (Dragon + trunk): ' + ', '.join(sorted(o.name for o in keep)))
    print('DROP (Falcon 9 stack): ' + ', '.join(sorted(o.name for o in drop)))
    for o in drop:
        bpy.data.objects.remove(o, do_unlink=True)
    return keep


def vehicle_bounds(meshes):
    """Rotation-invariant bounds about the +Z axis: (radius, zmin, zmax).

    The radius is the largest distance of any vertex from the axis, so it is the widest the
    silhouette can EVER be at any azimuth - fitting to it means no frame in the sequence can clip,
    which a per-frame fit could not promise and an axis-aligned bounding box does not measure.
    """
    r2 = 0.0
    zmin, zmax = float('inf'), float('-inf')
    for o in meshes:
        mw = o.matrix_world
        for v in o.data.vertices:
            p = mw @ v.co
            r2 = max(r2, p.x * p.x + p.y * p.y)
            zmin = min(zmin, p.z)
            zmax = max(zmax, p.z)
    return math.sqrt(r2), zmin, zmax


def build_world():
    """A dim, slightly cool ambient - not a background: the film is transparent."""
    world = bpy.data.worlds.new('TurntableWorld')
    world.use_nodes = True
    bg = world.node_tree.nodes['Background']
    bg.inputs[0].default_value = (0.090, 0.100, 0.130, 1.0)
    bg.inputs[1].default_value = 1.0
    bpy.context.scene.world = world


def build_rig(radius, zmin, zmax, width, height):
    """Camera + lights on one empty that spins about the vehicle axis. Returns the rig."""
    zc = 0.5 * (zmin + zmax)
    span = max(zmax - zmin, 1e-6)

    rig = bpy.data.objects.new('TurntableRig', None)
    rig.location = (0.0, 0.0, 0.0)
    bpy.context.scene.collection.objects.link(rig)

    # ---- camera: orthographic, so the silhouette width is the same in every frame and the vehicle
    # cannot "breathe" as it turns. A perspective lens would also foreshorten the trunk differently
    # frame to frame, which reads as a wobble in a sprite sequence.
    cam_data = bpy.data.cameras.new('TurntableCam')
    cam_data.type = 'ORTHO'
    frame_w_world = (2.0 * radius) / WIDTH_FILL
    cam_data.ortho_scale = frame_w_world * (float(height) / width)   # ortho_scale = the LONG side
    cam_data.clip_start = 1.0
    cam_data.clip_end = span * 40.0
    cam = bpy.data.objects.new('TurntableCam', cam_data)
    cam.location = (0.0, -span * 8.0, zc)
    cam.rotation_euler = (math.radians(90.0), 0.0, 0.0)              # -Y, looking to +Y, +Z up
    bpy.context.scene.collection.objects.link(cam)
    cam.parent = rig
    bpy.context.scene.camera = cam

    # ---- lights. Three, all parented to the rig so they are fixed RELATIVE TO THE CAMERA: the lit
    # side stays the same side of the sprite while the vehicle turns underneath, which is what makes
    # 36 frames read as one object rather than as a strobe.
    #
    # Sizes and distances are in vehicle spans, and the energies scale with span^2, so the setup
    # survives the model being re-exported at another scale.
    def area(name, loc, size, energy, colour):
        d = bpy.data.lights.new(name, type='AREA')
        d.size = span * size
        d.energy = energy
        d.color = colour
        ob = bpy.data.objects.new(name, d)
        ob.location = loc
        direction = (mathutils.Vector((0.0, 0.0, zc)) - mathutils.Vector(loc)).normalized()
        ob.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
        bpy.context.scene.collection.objects.link(ob)
        ob.parent = rig
        return ob

    p = span
    e = p * p
    # KEY - front, high, camera-left. Sunlight on a vehicle in daylight: one clear direction, so the
    # cylindrical trunk gets a gradient across it and reads as round.
    area('Key',  (-1.5 * p, -2.2 * p, zc + 1.6 * p), 2.5, 42.0 * e, (1.00, 0.98, 0.95))
    # FILL - opposite side, weak and cool, so the shaded flank separates from the background instead
    # of merging into it. The vehicle page's ground is near-black; an unfilled dark side would break
    # the silhouette.
    area('Fill', ( 2.4 * p, -1.4 * p, zc + 0.2 * p), 3.5, 6.5 * e, (0.82, 0.88, 1.00))
    # RIM - behind and above, catching the top edge of the capsule and the trunk's shoulder. This is
    # the light doing the most work at these sizes: it keeps the hull's edge legible when the sprite
    # is only a few hundred px wide on the glass.
    area('Rim',  ( 1.0 * p,  2.6 * p, zc + 2.0 * p), 3.0, 12.0 * e, (0.90, 0.95, 1.00))

    return rig


def configure_render(width, height, samples):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'                 # deterministic across machines; the model is tiny
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.cycles.seed = 0
    sc.render.resolution_x = width
    sc.render.resolution_y = height
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = True        # the page composites the sprite over its own ground
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    sc.render.image_settings.color_depth = '8'
    sc.render.image_settings.compression = 100
    # STANDARD, not AgX/Filmic: the sprite is UI art composited onto a flat dark panel, so the
    # textures must land on screen as they were authored. A filmic curve would grey the white hull
    # and pull it towards the panel it has to stand out from.
    sc.view_settings.view_transform = 'Standard'
    sc.view_settings.look = 'None'


def render_sequence(rig, out_dir, count, prefix, frames):
    if not os.path.isdir(out_dir):
        os.makedirs(out_dir)
    for i in frames:
        alpha = math.radians(FRONT_AZIMUTH_DEG + i * 360.0 / count)
        rig.rotation_euler = (0.0, 0.0, -alpha)      # header: rig by -alpha == body by +alpha
        path = os.path.join(out_dir, '%s%03d.png' % (prefix, i))
        bpy.context.scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        print('  frame %02d  az %3d  -> %s' % (i, round(i * 360.0 / count), path))


def main():
    args = argv_after_dashes()
    model = opt(args, '--model', MODEL)
    out_dir = os.path.abspath(opt(args, '--out', OUT_DIR))
    count = int(opt(args, '--count', COUNT))
    width = int(opt(args, '--width', W))
    height = int(opt(args, '--height', H))
    samples = int(opt(args, '--samples', SAMPLES))
    only = opt(args, '--only', None)
    frames = [int(x) for x in only.split(',')] if only else list(range(count))

    import_model(model)
    meshes = isolate_dragon()
    radius, zmin, zmax = vehicle_bounds(meshes)
    print('vehicle: radius %.1f  z [%.1f, %.1f]  height %.1f  h/w %.3f'
          % (radius, zmin, zmax, zmax - zmin, (zmax - zmin) / (2.0 * radius)))

    build_world()
    rig = build_rig(radius, zmin, zmax, width, height)
    configure_render(width, height, samples)
    print('rendering %d of %d frames at %dx%d, %d samples -> %s'
          % (len(frames), count, width, height, samples, out_dir))
    render_sequence(rig, out_dir, count, opt(args, '--prefix', PREFIX), frames)
    print('done: %d frames' % len(frames))


if __name__ == '__main__':
    main()
