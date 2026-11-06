"""Original Rotheart art: blender -b --python mods/Rotheart/Tools/build_models.py.

FHM1 uses Unity metres, +Y up, +Z front; its pivot is the heart centre.
Material indices are a file-format contract shared with RotheartAssets.cs.
No external art, game assets, or .blend file is needed.
"""
import math
import struct
from pathlib import Path

import bpy
from mathutils import Vector

OUT = Path(__file__).resolve().parents[1] / "Assets" / "Models"
# index, name, RGBA, metallic, roughness, emission intensity
PALETTE = [
    ("Iron", (.095, .115, .105, 1), .72, .70, 0),
    ("Rust", (.29, .115, .045, 1), .22, .92, 0),
    ("Bone", (.69, .65, .43, 1), 0, .86, 0),
    ("Cord", (.19, .145, .075, 1), 0, 1, 0),
    ("Ooze", (.38, .59, .19, 1), 0, .36, .32),
    ("PaleOoze", (.69, .86, .39, 1), 0, .30, .65),
    ("DarkOoze", (.16, .27, .075, 1), 0, .58, .12),
    ("IronEdge", (.25, .28, .22, 1), .65, .65, 0),
]
MATERIALS = []


def xyz(p):
    return (p[0], p[2], p[1])


def mesh_object(name, vertices, faces, material):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([xyz(p) for p in vertices], [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(MATERIALS[material])
    obj["material"] = material
    return obj


def tube(name, points, radius, material, sides=6, closed=False):
    points = [Vector(p) for p in points]
    verts = []
    for i, p in enumerate(points):
        previous = points[(i - 1) % len(points)] if closed or i > 0 else p
        following = points[(i + 1) % len(points)] if closed or i + 1 < len(points) else p
        tangent = (following - previous).normalized()
        side = tangent.cross(Vector((0, 0, 1)))
        if side.length_squared < .01:
            side = tangent.cross(Vector((0, 1, 0)))
        side.normalize()
        up = tangent.cross(side).normalized()
        for j in range(sides):
            angle = j * math.tau / sides
            verts.append(p + radius * (math.cos(angle) * side + math.sin(angle) * up))
    faces = []
    for i in range(len(points) if closed else len(points) - 1):
        k = (i + 1) % len(points)
        for j in range(sides):
            faces.append((i * sides + j, k * sides + j, k * sides + (j + 1) % sides, i * sides + (j + 1) % sides))
    if not closed:
        faces.extend([tuple(reversed(range(sides))), tuple((len(points) - 1) * sides + j for j in range(sides))])
    # Swapping Unity Y/Z reverses handedness.
    return mesh_object(name, verts, [tuple(reversed(face)) for face in faces], material)


def stone(name, position, scale, material, subdivisions=1):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1, location=xyz(position))
    obj = bpy.context.object
    obj.name = name
    obj.scale = xyz(scale)
    obj.data.materials.append(MATERIALS[material])
    obj["material"] = material
    return obj


def heart():
    # A faceted, asymmetric heart rather than a round bottle. Outline runs
    # clockwise from the cleft; radial layers give it a plump solid front/back.
    count, rings = 28, 7
    verts = [(0, .012, .037)]
    for ring in range(1, rings + 1):
        phi = math.pi * ring / (rings + 1)
        r = math.sin(phi)
        for j in range(count):
            a = math.tau * j / count
            x = .0045 * 16 * math.sin(a) ** 3
            y = .0052 * (13 * math.cos(a) - 5 * math.cos(2 * a) - 2 * math.cos(3 * a) - math.cos(4 * a))
            verts.append((x * r, .012 + y * r, .037 * math.cos(phi)))
    verts.append((0, .012, -.037))
    faces = [(0, 1 + j, 1 + (j + 1) % count) for j in range(count)]
    for ring in range(rings - 1):
        for j in range(count):
            a = 1 + ring * count + j
            b = 1 + ring * count + (j + 1) % count
            faces.append((a, a + count, b + count, b))
    last = 1 + (rings - 1) * count
    faces.extend((len(verts) - 1, last + (j + 1) % count, last + j) for j in range(count))
    obj = mesh_object("LivingOoze", verts, faces, 4)
    obj.data.materials.append(MATERIALS[5])
    obj.data.materials.append(MATERIALS[6])
    for face in obj.data.polygons:
        # Deterministic broad facet variation, not noisy random speckles.
        face.material_index = 1 if face.center.z > .034 and face.index % 5 == 0 else 2 if face.index % 17 == 3 else 0


def build():
    heart()
    # Longitudinal forged ribs embrace the heart without obscuring its lobes.
    for side in (-1, 1):
        for front in (-1, 1):
            points = [(side * x, y, front * z) for x, y, z in [(.008, .096, .008), (.042, .078, .023), (.057, .038, .033), (.045, -.018, .038), (.024, -.058, .027), (0, -.087, .004)]]
            tube("CageRib", points, .0051, 0)
            tube("WornRib", points[2:4], .00535, 1)
    for y, rx, rz in [(.035, .075, .037), (-.038, .044, .034)]:
        points = [(rx * math.cos(a * math.tau / 16), y, rz * math.sin(a * math.tau / 16)) for a in range(16)]
        tube("IronBinding", points, .0046, 0, closed=True)
        for side in (-1, 1):
            stone("RustRivet", (side * rx * .70, y, rz * .72), (.006, .006, .004), 1)
    # Bone stopper and dark neck collar, with a diagonal notch and cord lashings.
    tube("Neck", [(0, .071, 0), (0, .106, 0)], .017, 0, sides=8)
    tube("BoneStopper", [(-.045, .109, 0), (-.024, .115, 0), (.023, .112, 0), (.048, .118, 0)], .010, 2, sides=6)
    for side in (-1, 1):
        stone("BoneKnuckle", (side * .044, .113 + side * .004, 0), (.014, .014, .013), 2)
    for y in (.097, .102, .107):
        tube("Lashing", [(.019 * math.cos(a * math.tau / 12), y, .019 * math.sin(a * math.tau / 12)) for a in range(12)], .0024, 3, closed=True)
    tube("HangingCord", [(-.009, .107, -.003), (-.025, .143, 0), (-.027, .178, 0), (-.010, .195, 0), (.010, .193, 0), (.025, .172, 0), (.019, .140, 0), (.006, .107, .003)], .0038, 3, sides=6)
    tube("LooseCord", [(.013, .107, .017), (.028, .090, .023), (.023, .070, .025)], .0026, 3)
    stone("Knot", (.011, .108, .020), (.006, .007, .005), 3)
    # Sparse raised ooze veins catch the light between cage ribs.
    tube("OozeVein", [(-.016, .057, .032), (-.025, .039, .039), (-.011, .016, .040), (-.017, -.006, .038), (.002, -.033, .028)], .0027, 5, sides=5)
    tube("OozeBranch", [(-.011, .016, .040), (.012, .021, .041), (.025, .042, .035)], .002, 5, sides=5)
    for position, scale in [((.023, -.063, .018), (.009, .019, .009)), ((-.011, -.086, .012), (.008, .015, .007)), ((.052, .003, .027), (.007, .010, .006))]:
        stone("EscapingSlime", position, scale, 5)
    stone("BottomIronTip", (0, -.086, 0), (.011, .011, .010), 7)


def export():
    buckets = {}
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        mesh.calc_loop_triangles()
        normal_matrix = obj.matrix_world.to_3x3().inverted().transposed()
        for tri in mesh.loop_triangles:
            a, b, c = (mesh.vertices[i].co for i in tri.vertices)
            if (b - a).cross(c - a).length_squared < 1e-20:
                continue
            material = MATERIALS.index(obj.data.materials[tri.material_index])
            verts, normals, indices = buckets.setdefault(material, ([], [], []))
            base = len(verts)
            for index in tri.vertices:
                p = obj.matrix_world @ mesh.vertices[index].co
                n = (normal_matrix @ tri.normal).normalized()
                verts.append((p.x, p.z, p.y))
                normals.append((n.x, n.z, n.y))
            indices.extend((base, base + 2, base + 1))
        evaluated.to_mesh_clear()
    OUT.mkdir(parents=True, exist_ok=True)
    with (OUT / "rotheart.mesh").open("wb") as stream:
        stream.write(b"FHM1" + struct.pack("<i", len(buckets)))
        for material, (verts, normals, indices) in sorted(buckets.items()):
            group = b"Heart"
            stream.write(struct.pack("<i", len(group)) + group)
            stream.write(struct.pack("<ii", material, len(verts)))
            for vertex, normal in zip(verts, normals):
                stream.write(struct.pack("<6f", *vertex, *normal))
            stream.write(struct.pack("<i", len(indices)))
            stream.write(struct.pack("<" + "i" * len(indices), *indices))
    print("Rotheart:", sum(len(bucket[2]) // 3 for bucket in buckets.values()), "triangles;", len(buckets), "material batches")


def render_icon():
    scene = bpy.context.scene
    center = Vector(xyz((0, .046, 0)))
    bpy.ops.object.camera_add(location=center + Vector((.30, .85, .28)))
    camera = bpy.context.object
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = .335
    scene.camera = camera
    scene.render.engine = "BLENDER_EEVEE"
    scene.world.color = (.10, .10, .10)
    for offset, energy, size, color in [((-.35, .45, .6), 6, .5, (1, .88, .64)), ((.4, -.15, .3), 4, .4, (.53, .75, 1)), ((.1, .5, -.1), .7, .25, (.73, 1, .50))]:
        bpy.ops.object.light_add(type="AREA", location=center + Vector(offset))
        lamp = bpy.context.object
        lamp.data.energy = energy
        lamp.data.size = size
        lamp.data.color = color
        lamp.rotation_euler = (center - lamp.location).to_track_quat("-Z", "Y").to_euler()
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "None"
    scene.render.resolution_x = scene.render.resolution_y = 256
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.filepath = str(OUT / "rotheart.png")
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for name, color, metallic, roughness, emission in PALETTE:
        material = bpy.data.materials.new(name)
        material.diffuse_color = color
        material.use_nodes = True
        bsdf = material.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
        bsdf.inputs["Emission Color"].default_value = color
        bsdf.inputs["Emission Strength"].default_value = emission
        MATERIALS.append(material)
    build()
    bpy.context.view_layer.update()
    export()
    render_icon()
