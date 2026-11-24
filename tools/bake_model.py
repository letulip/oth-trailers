"""Bake an author's trailer model (FBX) into the bench's own binary model, embedded in TestTrailer.dll.

Run inside Blender (5.x):
  blender -b --python tools/bake_model.py -- "models/source/Trailer V1.fbx" src/TestTrailer/Models/M.bin preview/model/baked

What it does (docs/specs/11-first-model.md):
- drops the parts the bench draws itself: the wheels, hubs, axle and leaf springs (the car's own wheels and our axle and
  springs are used), the chassis, the A-frame tongue and the coupler (our frame and Z drawbar), the jockey wheel and its
  clamp (spec 06 draws its own); the number plate, a 108 000-triangle CAD part, goes too for now;
- decimates the heavy accessories (gas bottle, jerry cans, spare wheel) to about 1500 triangles each;
- scales the model by 0.5 (it is built about twice real size: its tyres are 1.55 m across, its jerry cans 0.92 m tall);
- moves it into the trailer's space as the game uses it: x right, y up, z toward the car; the body's bottom at y 0, its
  front at z 0, its back toward -z; the winding left as it is, which is right for Unity after the mirror between
  Blender's right-handed and Unity's left-handed axes (checked on the roof below);
- writes one vertex per face corner and normal (hard edges stay hard), one submesh per material with its colour
  (sRGB), metallic and roughness; a collider mesh from the body, roof and front box; and the measures the layout needs.

Binary layout (little endian): b"OTHM", int version 1, then floats length, width, height, axleFromFront, wheelX,
railX, archFront, archBack, archTop, then int vertexCount, vertexCount x (3 floats position, 3 floats normal), int
submeshCount, per submesh: 4 floats colour (sRGB), float metallic, float smoothness, int nameLength + utf-8 name,
int indexCount + indices; then int colliderVertexCount + positions, int colliderIndexCount + indices.
"""
import bpy
import bmesh
import struct
import sys
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
source, out = args[0], args[1]
preview = args[2] if len(args) > 2 else None

SCALE = 0.5
DROP = {
    "AxleAssembly", "SuspensionVisuals", "CHASSIS_MAIN", "TONGUE_MAIN", "TONGUE_LEFT ANGLE", "TONGUE_RIGHT ANGLE",
    "HITCH_COUPLER", "Jockey Wheel Clamp 48mm 300kg", "Jockey_Wheel_300_kg", "CA Lic Plate",
}
DECIMATE = {"GASBOTTLE_ACTUAL": 1500, "JERRYCAN_ACTUAL1": 1200, "JERRYCAN_ACTUAL2": 1200, "RIM_SPARE": 1500, "TYRE_SPARE": 1500}
COLLIDER = {"BODY_MAIN", "ROOF_MAIN", "STORAGE_FRONT.001"}
# Measures in the model's own units (Blender, Z up, the front toward -Y), read from the FBX
BODY_BOTTOM_Z = 0.9          # BODY_MAIN's lowest vertices
FRONT_Y = -3.67              # the front box's face
BACK_Y = 3.0                 # the body's rear face (the spare wheel stands out behind it)
TOP_Z = 3.905                # the roof's top
HALF_WIDTH = 2.4             # the body's sides
AXLE_Y = 0.45                # AxleAssembly
WHEEL_X = (1.914 + 2.506) / 2  # the tyres' middle
RAIL_X = 1.7 - 0.08          # inside the model's own chassis rails (x ±1.7), clear of the wheel wells (x ±1.9)
ARCH_FRONT_Y, ARCH_BACK_Y, ARCH_TOP_Z = -0.42, 1.32, 1.62   # the wheel well's opening in BODY_MAIN at its bottom, its top


def to_trailer(v):
    """Blender model space (Z up, front -Y, right -X) to the trailer's: x right, y up from the body's bottom, z toward
    the car from the body's front, scaled."""
    return Vector((-v.x * SCALE, (v.z - BODY_BOTTOM_Z) * SCALE, -(v.y - FRONT_Y) * SCALE))


def srgb(c):
    return c * 12.92 if c <= 0.0031308 else 1.055 * (c ** (1 / 2.4)) - 0.055


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=source)


def drop(o):
    for c in list(o.children):
        drop(c)
    bpy.data.objects.remove(o, do_unlink=True)


dropped = []
for name in [o.name for o in bpy.data.objects]:
    if name in DROP and name in bpy.data.objects:
        dropped.append(name)
        drop(bpy.data.objects[name])
missing = DROP - set(dropped)
if missing:
    sys.exit(f"not found to drop: {sorted(missing)}")

# Decimate the heavy accessories
for name, target in DECIMATE.items():
    o = bpy.data.objects[name]
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    m = o.modifiers.new("decimate", "DECIMATE")
    m.ratio = min(1.0, target / max(1, tris))
    bpy.context.view_layer.objects.active = o
    with bpy.context.temp_override(object=o, active_object=o):
        bpy.ops.object.modifier_apply(modifier=m.name)

# Gather every remaining mesh, triangulated, in the trailer's space
depsgraph = bpy.context.evaluated_depsgraph_get()
positions, normals, index_of = [], [], {}
groups = {}          # material name -> (colour, metallic, smoothness, indices)
collider_pos, collider_idx, collider_of = [], [], {}
parts = []
for o in bpy.data.objects:
    if o.type != "MESH":
        continue
    ev = o.evaluated_get(depsgraph)
    me = ev.to_mesh()
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.to_mesh(me)
    bm.free()
    world = o.matrix_world
    normal_matrix = world.to_3x3().inverted_safe().transposed()
    corner = me.corner_normals
    parts.append((o.name, len(me.polygons)))
    for poly in me.polygons:
        mat = me.materials[poly.material_index] if me.materials and poly.material_index < len(me.materials) else None
        key = mat.name if mat else "none"
        if key not in groups:
            colour, metallic, roughness = (0.5, 0.5, 0.5, 1.0), 0.0, 0.5
            if mat and mat.use_nodes:
                for n in mat.node_tree.nodes:
                    if n.type == "BSDF_PRINCIPLED":
                        colour = tuple(n.inputs["Base Color"].default_value)
                        metallic = n.inputs["Metallic"].default_value
                        roughness = n.inputs["Roughness"].default_value
            groups[key] = ((srgb(colour[0]), srgb(colour[1]), srgb(colour[2]), colour[3]), metallic, 1.0 - roughness, [])
        tri = []
        for li in poly.loop_indices:
            vi = me.loops[li].vertex_index
            p = to_trailer(world @ me.vertices[vi].co)
            nb = (normal_matrix @ Vector(corner[li].vector)).normalized()
            n = Vector((-nb.x, nb.z, -nb.y))
            k = (round(p.x, 5), round(p.y, 5), round(p.z, 5), round(n.x, 3), round(n.y, 3), round(n.z, 3))
            if k not in index_of:
                index_of[k] = len(positions)
                positions.append(p)
                normals.append(n)
            tri.append(index_of[k])
            if o.name in COLLIDER:
                ck = (round(p.x, 4), round(p.y, 4), round(p.z, 4))
                if ck not in collider_of:
                    collider_of[ck] = len(collider_pos)
                    collider_pos.append(p)
                collider_idx.append(collider_of[ck])
        groups[key][3].extend(tri)
    ev.to_mesh_clear()

# The winding: Unity draws a triangle's front where (b - a) x (c - a) points; on the roof's top that must be up
roof_up = 0
for key, (_, _, _, idx) in groups.items():
    for i in range(0, len(idx), 3):
        a, b, c = (positions[idx[i + j]] for j in range(3))
        if min(a.y, b.y, c.y) > (TOP_Z - BODY_BOTTOM_Z) * SCALE - 0.01:
            roof_up += 1 if (b - a).cross(c - a).y > 0 else -1
flip = roof_up < 0
if flip:
    for key in groups:
        idx = groups[key][3]
        for i in range(0, len(idx), 3):
            idx[i + 1], idx[i + 2] = idx[i + 2], idx[i + 1]
    for i in range(0, len(collider_idx), 3):
        collider_idx[i + 1], collider_idx[i + 2] = collider_idx[i + 2], collider_idx[i + 1]

measures = dict(
    length=(BACK_Y - FRONT_Y) * SCALE, width=2 * HALF_WIDTH * SCALE, height=(TOP_Z - BODY_BOTTOM_Z) * SCALE,
    axleFromFront=(AXLE_Y - FRONT_Y) * SCALE, wheelX=WHEEL_X * SCALE, railX=RAIL_X * SCALE,
    archFront=-(ARCH_FRONT_Y - FRONT_Y) * SCALE, archBack=-(ARCH_BACK_Y - FRONT_Y) * SCALE,
    archTop=(ARCH_TOP_Z - BODY_BOTTOM_Z) * SCALE)

with open(out, "wb") as f:
    f.write(b"OTHM")
    f.write(struct.pack("<i", 1))
    for k in ("length", "width", "height", "axleFromFront", "wheelX", "railX", "archFront", "archBack", "archTop"):
        f.write(struct.pack("<f", measures[k]))
    f.write(struct.pack("<i", len(positions)))
    for p, n in zip(positions, normals):
        f.write(struct.pack("<6f", p.x, p.y, p.z, n.x, n.y, n.z))
    f.write(struct.pack("<i", len(groups)))
    for key, (colour, metallic, smooth, idx) in groups.items():
        f.write(struct.pack("<4f", *colour))
        f.write(struct.pack("<2f", metallic, smooth))
        name = key.encode("utf-8")
        f.write(struct.pack("<i", len(name)) + name)
        f.write(struct.pack("<i", len(idx)))
        f.write(struct.pack(f"<{len(idx)}i", *idx))
    f.write(struct.pack("<i", len(collider_pos)))
    for p in collider_pos:
        f.write(struct.pack("<3f", p.x, p.y, p.z))
    f.write(struct.pack("<i", len(collider_idx)))
    f.write(struct.pack(f"<{len(collider_idx)}i", *collider_idx))

lo = Vector((min(p.x for p in positions), min(p.y for p in positions), min(p.z for p in positions)))
hi = Vector((max(p.x for p in positions), max(p.y for p in positions), max(p.z for p in positions)))
print(f"BAKED {out}: {len(positions)} vertices, {sum(len(g[3]) for g in groups.values()) // 3} triangles, "
      f"{len(groups)} materials, collider {len(collider_pos)} vertices; bounds {tuple(round(v, 3) for v in lo)}..{tuple(round(v, 3) for v in hi)}; "
      f"winding {'flipped' if flip else 'as it is'} (roof {roof_up:+d}); measures {({k: round(v, 3) for k, v in measures.items()})}")
print("DROPPED", dropped)
print("PARTS", parts)
for key, (colour, metallic, smooth, idx) in groups.items():
    print("MATERIAL", key, tuple(round(c, 3) for c in colour), round(metallic, 2), round(smooth, 2), len(idx) // 3)
