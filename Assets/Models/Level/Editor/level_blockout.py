import bpy, os

# Blockout del nivell FPS (A003FPS). Unitats en metres, Z amunt (Unity el converteix a Y amunt).
# Cada peça és un objecte amb nom propi, sense rotacions ni escales aplicades, per editar-lo després.

for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
for m in list(bpy.data.meshes):
    bpy.data.meshes.remove(m)

col = bpy.data.collections.get("Level") or bpy.data.collections.new("Level")
if col.name not in bpy.context.scene.collection.children:
    bpy.context.scene.collection.children.link(col)

mat = bpy.data.materials.get("Blockout") or bpy.data.materials.new("Blockout")
mat.diffuse_color = (0.85, 0.85, 0.85, 1)

FACES = [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]

def box_verts(x0, y0, z0, x1, y1, z1):
    return [(x0,y0,z0),(x1,y0,z0),(x1,y1,z0),(x0,y1,z0),(x0,y0,z1),(x1,y0,z1),(x1,y1,z1),(x0,y1,z1)]

def make(name, boxes=(), wedges=()):
    verts, faces = [], []
    for b in boxes:
        o = len(verts); verts += box_verts(*b)
        faces += [tuple(i+o for i in f) for f in FACES]
    for (x0, y0, x1, y1, h) in wedges:   # rampa que puja en +Y, de 0 a h
        o = len(verts)
        verts += [(x0,y0,0),(x1,y0,0),(x1,y1,0),(x0,y1,0),(x1,y1,h),(x0,y1,h)]
        faces += [tuple(i+o for i in f) for f in [(0,3,2,1),(0,1,4,5),(1,2,4),(0,5,3),(3,5,4,2)]]
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.update()
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me)
    col.objects.link(ob)
    return ob

H = 4.0   # alçada de paret
T = 0.4   # gruix de paret
S = 15.0  # mig costat del recinte (30 x 30 m)

make("Floor", boxes=[(-S, -S, -0.2, S, S, 0)])
make("OuterWalls", boxes=[
    (-S, -S, 0, S, -S+T, H), (-S, S-T, 0, S, S, H),
    (-S, -S+T, 0, -S+T, S-T, H), (S-T, -S+T, 0, S, S-T, H)])
# Passadís central amb una porta
make("InnerWalls", boxes=[
    (-6, 2, 0, -1.2, 2+T, H), (1.2, 2, 0, 6, 2+T, H),
    (6, 2, 0, 6+T, 10, H), (-6-T, 2, 0, -6, 7, H)])
# Plataforma elevada amb rampa d'accés
make("Platform", boxes=[(7, 4, 0, S-T, S-T, 2.0)])
make("Ramp", wedges=[(9, -4, 12, 4, 2.0)])
# Escales cap a una segona plataforma
make("Stairs", boxes=[(-13 + i*0.6, -6, 0, -13 + (i+1)*0.6, -2, 0.25*(i+1)) for i in range(8)])
make("UpperPlatform", boxes=[(-8.2, -10, 0, -3, -2, 2.0)])
# Cobertes / caixes
make("Cover", boxes=[
    (-2, -6, 0, 0, -5, 1.1), (2, -9, 0, 3, -7, 1.1),
    (-11, 9, 0, -9, 11, 1.5), (9, -10, 0, 11, -8, 2.2), (-3, 10, 0, 3, 11, 1.2)])

bpy.ops.object.select_all(action='DESELECT')
path = r"C:\Users\òscar\A003FPS\Assets\Models\Level\Level.blend"
os.makedirs(os.path.dirname(path), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=path)
print("Desat:", path)
