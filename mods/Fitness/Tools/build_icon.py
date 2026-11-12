"""Render the original treadmill icon. Run: blender-3.6 -b -t 2 -P this_file.
Geometry follows TreadmillVisual.cs; all materials are procedural, not game assets.
"""
from pathlib import Path
import math
import bpy
from mathutils import Vector

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = 96
scene.cycles.use_denoising = False
scene.render.resolution_x = scene.render.resolution_y = 256
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.view_settings.view_transform = 'Filmic'
scene.world.color = (0.12, 0.15, 0.13)

def material(name, dark, light, scale):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    shader = nodes.get('Principled BSDF')
    shader.inputs['Roughness'].default_value = 0.88
    coords = nodes.new('ShaderNodeTexCoord')
    mapping = nodes.new('ShaderNodeVectorMath')
    mapping.operation = 'MULTIPLY'
    mapping.inputs[1].default_value = scale
    links.new(coords.outputs['Generated'], mapping.inputs[0])
    noise = nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = 5
    noise.inputs['Detail'].default_value = 3
    links.new(mapping.outputs['Vector'], noise.inputs['Vector'])
    ramp = nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position = 0.2
    ramp.color_ramp.elements[0].color = (*dark, 1)
    ramp.color_ramp.elements[1].position = 0.8
    ramp.color_ramp.elements[1].color = (*light, 1)
    links.new(noise.outputs['Fac'], ramp.inputs[0])
    links.new(ramp.outputs['Color'], shader.inputs['Base Color'])
    bump = nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.22
    bump.inputs['Distance'].default_value = 0.025
    links.new(noise.outputs['Fac'], bump.inputs['Height'])
    links.new(bump.outputs['Normal'], shader.inputs['Normal'])
    return mat

wood = material('Weathered warm wood', (0.12,0.055,0.022), (0.43,0.24,0.095), (3,3,0.22))
hide = material('Original mottled deer hide', (0.025,0.009,0.004), (0.16,0.065,0.024), (2,3,1))
edge = material('Pale hide edging', (0.25,0.17,0.09), (0.64,0.50,0.31), (2,3,1))


def box(name, unity_position, unity_size, mat):
    x,y,z = unity_position
    sx,sy,sz = unity_size
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x,z,y))
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = (sx,sz,sy)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    obj.data.materials.append(mat)
    obj.data.use_auto_smooth = True
    bevel = obj.modifiers.new('Worn edges','BEVEL')
    bevel.width = 0.018
    bevel.segments = 2
    obj.modifiers.new('Weighted corner normals','WEIGHTED_NORMAL')
    return obj

for side in (-1,1):
    box('Side frame',(side*.83,.4,0),(.18,.26,2.95),wood)
    for end in (-1,1):
        box('Foot',(side*.83,.13,end*1.08),(.26,.26,.42),wood)
    box('Tall post',(side*.83,1.08,.95),(.14,1.4,.14),wood)
    box('Handrail',(side*.83,1.56,.2),(.12,.12,1.65),wood)
    box('Rear post',(side*.83,1.01,-.56),(.1,1.1,.1),wood)
box('Front crossbar',(0,1.76,.95),(1.8,.14,.14),wood)
for end in (-1,1):
    box('Deck crossbar',(0,.2,end*1.05),(1.7,.14,.18),wood)
    bpy.ops.mesh.primitive_cylinder_add(vertices=24,radius=.15,depth=1.55,location=(0,end*1.15,.32),rotation=(0,math.pi/2,0))
    bpy.context.object.data.materials.append(wood)
# Same forty-slat continuous belt path as the live model.
length = 4*1.15+2*math.pi*.15
for i in range(40):
    distance = i*length/40
    if distance < 2.3:
        z,y,angle = 1.15-distance,.47,0
    elif distance < 2.3+math.pi*.15:
        theta=(distance-2.3)/.15
        z,y,angle=-1.15-math.sin(theta)*.15,.32+math.cos(theta)*.15,theta
    elif distance < 4.6+math.pi*.15:
        z,y,angle=-1.15+distance-2.3-math.pi*.15,.17,math.pi
    else:
        theta=(distance-4.6-math.pi*.15)/.15
        z,y,angle=1.15+math.sin(theta)*.15,.32-math.cos(theta)*.15,math.pi+theta
    slat=box('Hide belt slat',(0,y,z),(1.42,.055,.115),hide)
    slat.rotation_euler.x=angle
    # Narrow pale hide edges, not a second invented game texture.
    for side in (-1,1):
        trim=box('Hide edge',(side*.68,y+.029,z),(.045,.004,.115),edge)
        trim.rotation_euler.x=angle

floor=bpy.data.materials.new('Dark forest green ground')
floor.diffuse_color=(.022,.042,.03,1)
box('Ground',(0,-.08,0),(200,.1,200),floor)


def aim(obj, target):
    obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()

bpy.ops.object.camera_add(location=(3.6,-4.5,3.8))
camera=bpy.context.object
camera.data.type='ORTHO'
camera.data.ortho_scale=3.8
aim(camera,(0,0,.85))
scene.camera=camera
for name,location,power,size,color in [
    ('Warm softbox',(-3,-4,6),650,4,(1,.82,.60)),
    ('Cool rim',(2,3,4),750,3,(.7,.85,1)),
    ('Front fill',(4,-2,3),300,4,(1,.95,.85)),
]:
    bpy.ops.object.light_add(type='AREA',location=location)
    lamp=bpy.context.object
    lamp.name=name
    lamp.data.energy=power
    lamp.data.shape='DISK'
    lamp.data.size=size
    lamp.data.color=color
    aim(lamp,(0,0,.7))
output=Path(__file__).resolve().parents[1]/'package/icon.png'
output.parent.mkdir(parents=True,exist_ok=True)
scene.render.filepath=str(output)
bpy.ops.render.render(write_still=True)
print(output)
