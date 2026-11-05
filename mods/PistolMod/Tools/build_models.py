"""Original firearm art. blender -b --python mods/PistolMod/Tools/build_models.py

Produces embedded FHM1 meshes (Unity +Y up, +Z forward) and transparent inventory
icons. No game assets required. Mesh groups preserve slide/magazine pivots.
"""
import bpy
import math
import struct
from pathlib import Path
from mathutils import Vector

OUT = Path(__file__).resolve().parents[1] / "Assets" / "Models"
OUT.mkdir(parents=True, exist_ok=True)
COLORS = [(0.035, .041, .048, 1), (.13, .15, .17, 1), (.008, .012, .016, 1),
          (.30, .105, .035, 1), (.24, .29, .12, 1), (.62, .40, .12, 1), (.7, .73, .65, 1)]
MATERIALS = []
for i, color in enumerate(COLORS):
    mat = bpy.data.materials.new("Finish" + str(i))
    mat.diffuse_color = color
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Metallic"].default_value = .65 if i in (1, 5, 6) else .05
    bsdf.inputs["Roughness"].default_value = .32 if i in (1, 5, 6) else .65
    MATERIALS.append(mat)


def xyz(p):
    # Unity left-handed coordinates -> Blender right-handed Z-up.
    return (p[0], p[2], p[1])


def part(name, pos, size, material=0, cylinder=False, rotate=0, bevel=.002):
    if cylinder:
        bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=1, depth=1, location=xyz(pos))
        obj = bpy.context.object
        obj.rotation_euler.x = math.pi / 2
        obj.scale = (size[0] / 2, size[1] / 2, size[2])
    else:
        bpy.ops.mesh.primitive_cube_add(size=1, location=xyz(pos))
        obj = bpy.context.object
        obj.scale = xyz(size)
        obj.rotation_euler.x = math.radians(rotate)
    obj.name = name
    obj.data.materials.append(MATERIALS[material])
    obj["material"] = material
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new("Machined edges", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj


def pistol():
    part("Frame", (0, .055, .042), (.037, .034, .17))
    part("Grip", (0, -.003, -.025), (.033, .105, .048), rotate=-12, bevel=.006)
    for side in (-1, 1):
        for row in range(8):
            for col in range(5):
                part("Stipple", (side * .017, -.037 + row * .009, -.041 + col * .007),
                     (.0015, .002, .002), 2, bevel=0)
    part("Slide", (0, .089, .044), (.039, .037, .19), 1, bevel=.004)
    part("Slide_Port", (.020, .094, .018), (.001, .015, .032), 2, bevel=.001)
    part("Slide_Chamber", (.021, .096, .019), (.001, .009, .022), 6, bevel=.001)
    for side in (-1, 1):
        for i in range(7):
            part("Slide_Serration", (side * .020, .086, -.037 + .005 * i), (.002, .022, .002), 2, bevel=0)
    part("Barrel", (0, .084, .143), (.019, .019, .012), 1, True)
    part("Bore", (0, .084, .150), (.010, .010, .001), 2, True, bevel=0)
    part("Slide_FrontSight", (0, .112, .121), (.005, .009, .010), 2)
    for side in (-1, 1):
        part("Slide_RearSight", (side * .011, .112, -.041), (.006, .009, .012), 2)
        part("Slide_SightDot", (side * .011, .113, -.048), (.003, .003, .001), 6, bevel=0)
    part("GuardFront", (0, .026, .058), (.007, .044, .008), rotate=15)
    part("GuardBase", (0, .009, .028), (.007, .008, .058))
    part("Trigger", (0, .029, .027), (.006, .029, .007), 1, rotate=15)
    part("TriggerSafety", (0, .028, .032), (.002, .014, .003), 2)
    part("Magazine", (0, -.005, -.025), (.027, .089, .035), 1, rotate=-12)
    part("Magazine_Base", (0, -.056, -.014), (.037, .009, .049), 2)
    part("SlideRelease", (-.021, .052, -.02), (.003, .006, .014), 1)
    part("MagazineRelease", (-.019, .024, -.006), (.003, .008, .007), 2)
    for i in range(3):
        part("AccessoryRail", (0, .035, .087 + i * .014), (.030, .005, .007), 2)


def rifle():
    part("Receiver", (0, .075, .02), (.056, .075, .29), 1, bevel=.004)
    part("DustCover", (0, .118, .005), (.051, .021, .25), 1, bevel=.008)
    for side in (-1, 1):
        for z in (-.09, -.055, .07, .12):
            part("Rivet", (side * .029, .067, z), (.003, .007, .007), 2)
    part("Stock", (0, .055, -.255), (.043, .101, .27), 3, rotate=-4, bevel=.015)
    part("Buttplate", (0, .046, -.39), (.048, .107, .012), 2)
    part("Grip", (0, -.002, -.025), (.039, .117, .06), 3, rotate=-16, bevel=.009)
    part("GuardBottom", (0, .005, .035), (.009, .008, .06), 2)
    part("GuardFront", (0, .025, .065), (.009, .045, .008), 2)
    part("Trigger", (0, .029, .035), (.008, .033, .008), 2, rotate=20)
    part("Foregrip", (0, .069, .225), (.061, .062, .15), 3, bevel=.012)
    part("GasWood", (0, .12, .218), (.047, .028, .135), 3, bevel=.008)
    for i in range(4):
        for side in (-1, 1):
            part("CoolingSlot", (side * .029, .092, .177 + i * .027), (.004, .010, .013), 2)
    part("Barrel", (0, .084, .394), (.020, .020, .25), 1, True)
    part("GasTube", (0, .127, .303), (.021, .021, .10), 1, True)
    part("GasBlock", (0, .106, .351), (.030, .060, .025), 1)
    part("FrontSight", (0, .123, .478), (.027, .075, .020), 1)
    part("FrontSightNotch", (0, .165, .478), (.007, .012, .012), 2)
    part("MuzzleBrake", (0, .084, .535), (.030, .030, .036), 1, True)
    part("Bore", (0, .084, .554), (.013, .013, .002), 2, True, bevel=0)
    part("RearSight", (0, .137, .127), (.033, .015, .036), 2)
    part("Slide", (.033, .100, .036), (.010, .014, .11), 2)
    part("Slide_Handle", (.047, .100, -.009), (.032, .012, .012), 1)
    # Segmented curved magazine with overlapping bevelled sections.
    for i in range(7):
        y = .007 - i * .026
        z = .101 + .002 * i * i
        part("Magazine" if i == 0 else "Magazine_Segment", (0, y, z), (.037, .037, .070), 1, rotate=-i * 5)
        for side in (-1, 1):
            part("Magazine_Rib", (side * .020, y, z), (.004, .031, .047), 2, rotate=-i * 5, bevel=.001)
    part("SafetyLever", (.032, .061, -.035), (.006, .010, .11), 1, rotate=8)


def launcher():
    part("LaunchTube", (0, .155, .02), (.084, .084, .91), 4, True, bevel=.003)
    part("Venturi", (0, .155, -.46), (.14, .14, .11), 1, True)
    part("RearOpening", (0, .155, -.517), (.105, .105, .002), 2, True, bevel=0)
    for z in (-.25, -.20, -.15, -.10):
        part("HeatShield", (0, .155, z), (.097, .097, .041), 3, True)
    part("Grip", (0, .015, 0), (.037, .115, .059), 3, rotate=-8, bevel=.007)
    part("GripMount", (0, .088, .01), (.043, .064, .09), 1)
    part("SupportGrip", (0, .025, -.21), (.038, .13, .050), 3, rotate=10, bevel=.008)
    part("TriggerGuard", (0, .037, .06), (.008, .062, .009), 1)
    part("GuardBase", (0, .008, .039), (.008, .009, .055), 1)
    part("Trigger", (0, .046, .038), (.006, .036, .008), 2, rotate=12)
    part("SightMount", (-.059, .18, .06), (.049, .018, .10), 1)
    part("Optic", (-.086, .23, .06), (.046, .046, .16), 4, True)
    part("OpticLens", (-.086, .23, -.022), (.032, .032, .002), 2, True)
    rocket(.49, "Magazine")


def rocket(z=0, prefix="Rocket"):
    part(prefix, (0, .155 if prefix == "Magazine" else 0, z), (.065, .065, .30), 4, True)
    y = .155 if prefix == "Magazine" else 0
    # Ogive silhouette built as tapered rings (not a flat cylinder).
    for i, radius in enumerate((.045, .061, .074, .080, .075, .061, .042, .022)):
        part(prefix + "_Warhead", (0, y, z + .11 + i * .023), (radius, radius, .028), 4, True, bevel=.003)
    part(prefix + "_Tip", (0, y, z + .299), (.014, .014, .020), 1, True)
    if prefix == "Rocket":
        for side in (-1, 1):
            part(prefix + "_Fin", (side * .046, y, z - .12), (.048, .003, .065), 1, bevel=.001)
            part(prefix + "_Fin", (0, y + side * .046, z - .12), (.003, .048, .065), 1, bevel=.001)


def cartridge(rifle_round=False):
    scale = 1.35 if rifle_round else 1
    part("Case", (0, 0, 0), (.010, .010, .019 * scale), 5, True, bevel=.0005)
    part("Rim", (0, 0, -.011 * scale), (.011, .011, .002), 5, True, bevel=.0003)
    part("Bullet", (0, 0, .014 * scale), (.008, .008, .012 * scale), 3, True, bevel=.002)


def export(name):
    # Aggregate meshes by moving group and material; reduce draw calls.
    buckets = {}
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for obj in list(bpy.context.scene.objects):
        if obj.type != "MESH":
            continue
        group = "Slide" if obj.name.startswith("Slide") else "Magazine" if obj.name.startswith("Magazine") else "Body"
        key = (group, int(obj["material"]))
        verts, normals, triangles = buckets.setdefault(key, ([], [], []))
        mesh = obj.evaluated_get(depsgraph).to_mesh()
        mesh.calc_loop_triangles()
        normal_matrix = obj.matrix_world.to_3x3().inverted().transposed()
        # Split triangle corners to preserve Blender's bevel face normals.
        for tri in mesh.loop_triangles:
            # Thin bevelled caps can contain collapsed faces; omit zero-area
            # triangles rather than exporting invalid zero-length normals.
            a, b, c = (mesh.vertices[i].co for i in tri.vertices)
            if (b - a).cross(c - a).length_squared < 1e-20 or tri.normal.length_squared < .5:
                continue
            base = len(verts)
            for index in tri.vertices:
                p = obj.matrix_world @ mesh.vertices[index].co
                n = (normal_matrix @ tri.normal).normalized()
                verts.append((p.x, p.z, p.y))
                normals.append((n.x, n.z, n.y))
            triangles.extend((base, base + 2, base + 1))
        obj.evaluated_get(depsgraph).to_mesh_clear()
    with (OUT / (name + ".mesh")).open("wb") as f:
        f.write(b"FHM1")
        f.write(struct.pack("<i", len(buckets)))
        for (group, material), (verts, normals, triangles) in buckets.items():
            encoded = group.encode()
            f.write(struct.pack("<i", len(encoded)) + encoded)
            f.write(struct.pack("<ii", material, len(verts)))
            for p, n in zip(verts, normals):
                f.write(struct.pack("<6f", *p, *n))
            f.write(struct.pack("<i", len(triangles)))
            f.write(struct.pack("<" + "i" * len(triangles), *triangles))
    print(name, sum(len(v[2]) // 3 for v in buckets.values()), "triangles")


def icon(name):
    scene = bpy.context.scene
    meshes = [o for o in scene.objects if o.type == "MESH"]
    points = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    low = Vector(tuple(min(p[i] for p in points) for i in range(3)))
    high = Vector(tuple(max(p[i] for p in points) for i in range(3)))
    center = (low + high) / 2
    bpy.ops.object.camera_add(location=center + Vector((2, -.7, 1.1)))
    camera = bpy.context.object
    camera.rotation_euler = (center - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = max(high - low) * 1.2
    scene.camera = camera
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 24
    scene.world.color = (.25, .25, .25)
    for offset, energy, size in [((1, -1, 2), 220, 2), ((-1, .5, 1), 150, 1.5)]:
        bpy.ops.object.light_add(type="AREA", location=center + Vector(offset))
        light = bpy.context.object
        light.data.energy = energy
        light.data.shape = "DISK"
        light.data.size = size
        light.rotation_euler = (center - light.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.resolution_x = scene.render.resolution_y = 256
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'PNG'
    scene.render.filepath = str(OUT / (name + ".png"))
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    for name, create in [("pistol", pistol), ("rifle", rifle), ("launcher", launcher),
                         ("ammo9mm", cartridge), ("ammo762", lambda: cartridge(True)), ("rocket", rocket)]:
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.object.delete(use_global=False)
        create()
        bpy.context.view_layer.update()
        export(name)
        icon(name)
