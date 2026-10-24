"""
═══════════════════════════════════════════════════════════════════════════════
  PLEROTUS PROCEDURAL MAP & ROOM GENERATOR FOR BLENDER (3.x, 4.x & 5.0)
  Projeto: Leaving Plerotus
  
  COMO USAR NO BLENDER:
  1. Abra o Blender.
  2. No menu do topo, clique na aba 'Scripting'.
  3. Clique no botão '+ New' (Novo Arquivo) no editor de texto.
  4. Cole este script completo e clique no botão 'Play' (▶ Run Script) ou tecle [Alt + P].
  5. Volte para a aba 'Layout' (3D Viewport) e pressione [ N ] no teclado para abrir a barra lateral direita.
  6. Clique na nova aba chamada 'Plerotus'!
  7. Escolha o preset da sala (Arena 4 Vias, Corredor, Beco, Lago), ajuste o tamanho e clique em '🔮 GERAR SALA PROCEDURAL'.
  8. Para levar para o jogo, basta clicar em '💾 EXPORTAR FBX PARA UNITY' (salva direto em Assets/_Project/Enviroment/Map/Generated/).
═══════════════════════════════════════════════════════════════════════════════
"""

import bpy
import bmesh
import math
import random
import os
from mathutils import Vector, Euler, Matrix

# ═══════════════════════════════════════════════════════════════════════════
# 1. PALETA DE CORES MATTE OFICIAL (LEAVING PLEROTUS)
# ═══════════════════════════════════════════════════════════════════════════

PALETTE = {
    "Floor":         (0.18, 0.15, 0.23, 1.0), # Chão Índigo / Pedra ardósia escurecida (#2E263B)
    "FloorTile":     (0.24, 0.20, 0.30, 1.0), # Lajes de pedra rachada (#3D334C)
    "FloorEdge":     (0.14, 0.12, 0.18, 1.0), # Borda de terra / poeira mineral
    "WallBasalt":    (0.11, 0.09, 0.15, 1.0), # Rocha basáltica profunda (#1C1726)
    "WallStrata":    (0.16, 0.13, 0.22, 1.0), # Camada intermediária da caverna (#292138)
    "WallTop":       (0.22, 0.18, 0.28, 1.0), # Topo das falésias (#382E47)
    "CrystalTeal":   (0.12, 0.88, 0.78, 1.0), # Bismuto / Cristal Cianeto Alquímico (#1FE0C7)
    "CrystalPurple": (0.62, 0.25, 0.92, 1.0), # Quartzo Ametista Alienígena (#9E40EB)
    "Vines":         (0.08, 0.07, 0.10, 1.0), # Raízes e cipós negros orgânicos (#14121A)
    "Water":         (0.08, 0.45, 0.42, 0.85),# Poça cáustica / Lago bioluminescente
}

def get_or_create_material(name, color_rgba, roughness=0.85, emission_strength=0.0):
    mat = bpy.data.materials.get(name)
    if not mat:
        mat = bpy.data.materials.new(name=name)
        if hasattr(mat, "use_nodes"):
            try:
                mat.use_nodes = True
            except Exception:
                pass
        nodes = mat.node_tree.nodes
        bsdf = nodes.get("Principled BSDF") or nodes.new("ShaderNodeBsdfPrincipled")
        
        if "Base Color" in bsdf.inputs:
            bsdf.inputs["Base Color"].default_value = color_rgba
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = roughness
        if "Specular IOR Level" in bsdf.inputs:
            bsdf.inputs["Specular IOR Level"].default_value = 0.1
        elif "Specular" in bsdf.inputs:
            bsdf.inputs["Specular"].default_value = 0.1
            
        if emission_strength > 0:
            if "Emission Color" in bsdf.inputs:
                bsdf.inputs["Emission Color"].default_value = color_rgba
            elif "Emission" in bsdf.inputs:
                bsdf.inputs["Emission"].default_value = color_rgba
            if "Emission Strength" in bsdf.inputs:
                bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat

def setup_all_materials():
    return {
        "Floor": get_or_create_material("Mat_Plerotus_Floor", PALETTE["Floor"], roughness=0.88),
        "FloorTile": get_or_create_material("Mat_Plerotus_FloorTile", PALETTE["FloorTile"], roughness=0.84),
        "WallBasalt": get_or_create_material("Mat_Plerotus_WallBasalt", PALETTE["WallBasalt"], roughness=0.92),
        "WallStrata": get_or_create_material("Mat_Plerotus_WallStrata", PALETTE["WallStrata"], roughness=0.90),
        "WallTop": get_or_create_material("Mat_Plerotus_WallTop", PALETTE["WallTop"], roughness=0.88),
        "CrystalTeal": get_or_create_material("Mat_Plerotus_Crystal_Teal", PALETTE["CrystalTeal"], roughness=0.28, emission_strength=1.8),
        "CrystalPurple": get_or_create_material("Mat_Plerotus_Crystal_Purple", PALETTE["CrystalPurple"], roughness=0.32, emission_strength=1.5),
        "Vines": get_or_create_material("Mat_Plerotus_Vines", PALETTE["Vines"], roughness=0.95),
        "Water": get_or_create_material("Mat_Plerotus_Water", PALETTE["Water"], roughness=0.15, emission_strength=0.9),
    }

# ═══════════════════════════════════════════════════════════════════════════
# 2. GERADOR PROCEDURAL DE CRISTAIS POLIGONAIS (BISMUTO & AMETISTA)
# ═══════════════════════════════════════════════════════════════════════════

def build_single_crystal(bm, center_pos, radius=0.35, height=1.6, tilt_angle=15, tilt_dir=0, segments=6):
    angle_step = (2 * math.pi) / segments
    rot_rad = math.radians(tilt_angle)
    dir_rad = math.radians(tilt_dir)
    tilt_axis = Vector((math.cos(dir_rad), math.sin(dir_rad), 0))
    tilt_mat = Matrix.Rotation(rot_rad, 4, tilt_axis)

    body_height = height * 0.72
    bottom_verts = []
    mid_verts = []
    
    for i in range(segments):
        a = i * angle_step
        v_bot = center_pos + (tilt_mat @ Vector((math.cos(a) * radius, math.sin(a) * radius, -0.05)))
        v_mid = center_pos + (tilt_mat @ Vector((math.cos(a) * (radius * 0.8), math.sin(a) * (radius * 0.8), body_height)))
        bottom_verts.append(bm.verts.new(v_bot))
        mid_verts.append(bm.verts.new(v_mid))
        
    top_vert = bm.verts.new(center_pos + (tilt_mat @ Vector((0, 0, height))))
    
    for i in range(segments):
        next_i = (i + 1) % segments
        bm.faces.new([bottom_verts[i], bottom_verts[next_i], mid_verts[next_i], mid_verts[i]])
        bm.faces.new([mid_verts[i], mid_verts[next_i], top_vert])
    bm.faces.new(list(reversed(bottom_verts)))

def create_crystal_cluster(location, count=5, radius=1.0, mat=None, col=None, parent_obj=None):
    mesh = bpy.data.meshes.new(name="CrystalCluster_Mesh")
    obj = bpy.data.objects.new(name="Crystals", object_data=mesh)
    
    bm = bmesh.new()
    for _ in range(count):
        offset = Vector((random.uniform(-radius, radius), random.uniform(-radius, radius), 0))
        c_pos = location + offset
        c_rad = random.uniform(0.18, 0.45)
        c_h = random.uniform(0.9, 2.6)
        c_tilt = random.uniform(6, 26)
        c_dir = random.uniform(0, 360)
        build_single_crystal(bm, c_pos, radius=c_rad, height=c_h, tilt_angle=c_tilt, tilt_dir=c_dir)
        
    bm.to_mesh(mesh)
    bm.free()
    
    if mat:
        obj.data.materials.append(mat)
    if col:
        col.objects.link(obj)
    if parent_obj:
        obj.parent = parent_obj
    return obj

# ═══════════════════════════════════════════════════════════════════════════
# 3. GERADOR PROCEDURAL DE CHÃO (WALKABLE TILES & CRACKED SLABS)
# ═══════════════════════════════════════════════════════════════════════════

def build_floor_mesh(width, length, tile_size=2.0, is_lake=False, mats=None, col=None, parent_obj=None):
    mesh = bpy.data.meshes.new(name="Floor_Mesh")
    obj = bpy.data.objects.new(name="Floor", object_data=mesh)
    
    bm = bmesh.new()
    half_w = width / 2.0
    half_l = length / 2.0
    
    nx = int(math.ceil(width / tile_size))
    ny = int(math.ceil(length / tile_size))
    step_x = width / nx
    step_y = length / ny
    
    lake_radius = min(half_w, half_l) * 0.45 if is_lake else 0.0

    for ix in range(nx):
        x0 = -half_w + (ix * step_x)
        x1 = x0 + step_x
        for iy in range(ny):
            y0 = -half_l + (iy * step_y)
            y1 = y0 + step_y
            
            cx = (x0 + x1) * 0.5
            cy = (y0 + y1) * 0.5
            dist_center = math.sqrt(cx*cx + cy*cy)
            
            z_tile = 0.0
            if is_lake and dist_center < lake_radius:
                depth_ratio = 1.0 - (dist_center / lake_radius)
                z_tile = -0.75 * math.sin(depth_ratio * (math.pi / 2.0))
                
            gap = 0.03
            v1 = bm.verts.new(Vector((x0 + gap, y0 + gap, z_tile)))
            v2 = bm.verts.new(Vector((x1 - gap, y0 + gap, z_tile)))
            v3 = bm.verts.new(Vector((x1 - gap, y1 - gap, z_tile)))
            v4 = bm.verts.new(Vector((x0 + gap, y1 - gap, z_tile)))
            
            face = bm.faces.new([v1, v2, v3, v4])
            face.material_index = 0 if (ix + iy) % 2 == 0 else 1

    bm.to_mesh(mesh)
    bm.free()
    
    if mats:
        obj.data.materials.append(mats["Floor"])
        obj.data.materials.append(mats["FloorTile"])
        
    if col:
        col.objects.link(obj)
    if parent_obj:
        obj.parent = parent_obj
        
    if is_lake:
        water_mesh = bpy.data.meshes.new(name="Lake_Water_Mesh")
        water_obj = bpy.data.objects.new(name="Lake_Water", object_data=water_mesh)
        w_bm = bmesh.new()
        bmesh.ops.create_circle(w_bm, cap_ends=True, radius=lake_radius * 1.05, segments=24)
        w_bm.to_mesh(water_mesh)
        w_bm.free()
        water_obj.location = Vector((0, 0, -0.25))
        if mats:
            water_obj.data.materials.append(mats["Water"])
        if col:
            col.objects.link(water_obj)
        if parent_obj:
            water_obj.parent = parent_obj

    return obj

# ═══════════════════════════════════════════════════════════════════════════
# 4. GERADOR DE PAREDES ESCARPADAS & PORTAIS (CAVERN CLIFFS)
# ═══════════════════════════════════════════════════════════════════════════

def build_cliff_wall_segment(bm, p_start, p_end, wall_height=4.0, wall_depth=3.0, strata_levels=3):
    dir_vec = (p_end - p_start).normalized()
    out_normal = Vector((dir_vec.y, -dir_vec.x, 0)).normalized()
    
    length = (p_end - p_start).length
    steps = max(2, int(length / 2.5))
    step_vec = (p_end - p_start) / steps
    
    tiers = strata_levels
    dz = wall_height / tiers
    dd = wall_depth / tiers
    
    grid = []
    for tier in range(tiers + 1):
        z = tier * dz
        depth_offset = out_normal * (tier * dd)
        row = []
        for i in range(steps + 1):
            base_pos = p_start + (step_vec * i)
            jitter_side = random.uniform(-0.15, 0.15) if (0 < i < steps) else 0.0
            jitter_z = random.uniform(-0.1, 0.1) if (0 < tier < tiers) else 0.0
            pos = base_pos + depth_offset + (dir_vec * jitter_side) + Vector((0, 0, jitter_z))
            row.append(bm.verts.new(pos))
        grid.append(row)
        
    for tier in range(tiers):
        for i in range(steps):
            v_bl = grid[tier][i]
            v_br = grid[tier][i + 1]
            v_tr = grid[tier + 1][i + 1]
            v_tl = grid[tier + 1][i]
            face = bm.faces.new([v_bl, v_br, v_tr, v_tl])
            face.material_index = 0 if tier == 0 else (1 if tier < tiers - 1 else 2)

def build_room_walls(width, length, wall_height, door_w, doors, mats=None, col=None, parent_obj=None):
    mesh = bpy.data.meshes.new(name="Walls_Mesh")
    obj = bpy.data.objects.new(name="Cavern_Walls", object_data=mesh)
    bm = bmesh.new()
    
    hw = width / 2.0
    hl = length / 2.0
    half_door = door_w / 2.0
    
    sides = [
        {"name": "North", "p1": Vector((-hw, hl, 0)), "p2": Vector((hw, hl, 0)), "has_door": doors.get("North", False)},
        {"name": "East",  "p1": Vector((hw, hl, 0)), "p2": Vector((hw, -hl, 0)), "has_door": doors.get("East", False)},
        {"name": "South", "p1": Vector((hw, -hl, 0)), "p2": Vector((-hw, -hl, 0)), "has_door": doors.get("South", False)},
        {"name": "West",  "p1": Vector((-hw, -hl, 0)), "p2": Vector((-hw, hl, 0)), "has_door": doors.get("West", False)},
    ]
    
    door_positions = []
    
    for s in sides:
        p1 = s["p1"]
        p2 = s["p2"]
        has_door = s["has_door"]
        
        if not has_door:
            build_cliff_wall_segment(bm, p1, p2, wall_height=wall_height)
        else:
            mid_pt = (p1 + p2) * 0.5
            door_positions.append({"name": s["name"], "center": mid_pt})
            dir_norm = (p2 - p1).normalized()
            
            door_start = mid_pt - (dir_norm * half_door)
            door_end   = mid_pt + (dir_norm * half_door)
            
            build_cliff_wall_segment(bm, p1, door_start, wall_height=wall_height)
            build_cliff_wall_segment(bm, door_end, p2, wall_height=wall_height)
            
    bm.to_mesh(mesh)
    bm.free()
    
    if mats:
        obj.data.materials.append(mats["WallBasalt"])
        obj.data.materials.append(mats["WallStrata"])
        obj.data.materials.append(mats["WallTop"])
        
    if col:
        col.objects.link(obj)
    if parent_obj:
        obj.parent = parent_obj
        
    return obj, door_positions

# ═══════════════════════════════════════════════════════════════════════════
# 5. GERADOR DE CIPÓS / RAÍZES ORGÂNICAS
# ═══════════════════════════════════════════════════════════════════════════

def build_organic_vines_mesh(corner_points, mat=None, col=None, parent_obj=None):
    mesh = bpy.data.meshes.new(name="Vines_Mesh")
    obj = bpy.data.objects.new(name="Organic_Vines", object_data=mesh)
    bm = bmesh.new()
    
    for pt in corner_points:
        for _ in range(random.randint(2, 4)):
            vine_len = random.uniform(2.5, 5.0)
            angle = random.uniform(0, 2 * math.pi)
            substeps = 8
            prev_v = None
            
            curr_pos = pt + Vector((random.uniform(-0.5, 0.5), random.uniform(-0.5, 0.5), 0.05))
            for s in range(substeps):
                t = s / substeps
                rad = 0.08 * (1.0 - t * 0.6)
                offset = Vector((math.cos(angle) * (vine_len / substeps),
                                 math.sin(angle) * (vine_len / substeps),
                                 math.sin(t * math.pi) * 0.15))
                curr_pos = curr_pos + offset
                angle += random.uniform(-0.35, 0.35)
                
                circle_verts = []
                for ca in [0, math.pi * 0.5, math.pi, math.pi * 1.5]:
                    c_pt = curr_pos + Vector((math.cos(ca) * rad, math.sin(ca) * rad, 0))
                    circle_verts.append(bm.verts.new(c_pt))
                    
                if prev_v is not None:
                    for ci in range(4):
                        nci = (ci + 1) % 4
                        bm.faces.new([prev_v[ci], prev_v[nci], circle_verts[nci], circle_verts[ci]])
                prev_v = circle_verts

    bm.to_mesh(mesh)
    bm.free()
    if mat:
        obj.data.materials.append(mat)
    if col:
        col.objects.link(obj)
    if parent_obj:
        obj.parent = parent_obj
    return obj

# ═══════════════════════════════════════════════════════════════════════════
# 6. GERADOR COMPLETO DA SALA (ORQUESTRADOR)
# ═══════════════════════════════════════════════════════════════════════════

def generate_plerotus_room(preset="ARENA_4WAYS", width=24.0, length=24.0, wall_height=4.0, door_w=5.0, crystal_density=7, add_vines=True, seed=42):
    random.seed(seed)
    
    doors = {"North": False, "South": False, "East": False, "West": False}
    is_lake = False
    
    if preset == "ARENA_4WAYS":
        doors = {"North": True, "South": True, "East": True, "West": True}
    elif preset == "ARENA_2WAYS_STRAIGHT":
        doors = {"North": True, "South": True, "East": False, "West": False}
    elif preset == "ARENA_2WAYS_CORNER":
        doors = {"North": True, "East": True, "South": False, "West": False}
    elif preset == "CORRIDOR":
        doors = {"North": True, "South": True, "East": False, "West": False}
        width = min(width, 10.0)
    elif preset == "DEAD_END":
        doors = {"South": True, "North": False, "East": False, "West": False}
    elif preset == "ANOMALY_LAKE":
        doors = {"North": True, "South": True, "East": False, "West": False}
        is_lake = True
        
    col_name = f"Plerotus_Room_{preset}"
    col = bpy.data.collections.get(col_name)
    if col:
        for o in list(col.objects):
            bpy.data.objects.remove(o, do_unlink=True)
    else:
        col = bpy.data.collections.new(col_name)
        bpy.context.scene.collection.children.link(col)
        
    room_root = bpy.data.objects.new(name=f"Room_{preset}", object_data=None)
    room_root.empty_display_type = 'ARROWS'
    room_root.empty_display_size = 2.0
    col.objects.link(room_root)
    
    mats = setup_all_materials()
    
    # 1. Chão
    floor_obj = build_floor_mesh(width, length, tile_size=2.0, is_lake=is_lake, mats=mats, col=col, parent_obj=room_root)
    
    # 2. Paredes e Portais
    walls_obj, door_positions = build_room_walls(width, length, wall_height, door_w, doors, mats=mats, col=col, parent_obj=room_root)
    
    # 3. ConnectionPoints (Compatíveis com LevelGenerator.cs do Unity)
    for d in door_positions:
        cp_name = f"ConnectionPoint_Saida_{d['name']}"
        cp_empty = bpy.data.objects.new(name=cp_name, object_data=None)
        cp_empty.empty_display_type = 'SINGLE_ARROW'
        cp_empty.empty_display_size = 1.5
        cp_empty.location = d['center']
        
        if d['name'] == "North":
            cp_empty.rotation_euler = Euler((0, 0, 0), 'XYZ')
        elif d['name'] == "South":
            cp_empty.rotation_euler = Euler((0, 0, math.pi), 'XYZ')
        elif d['name'] == "East":
            cp_empty.rotation_euler = Euler((0, 0, -math.pi * 0.5), 'XYZ')
        elif d['name'] == "West":
            cp_empty.rotation_euler = Euler((0, 0, math.pi * 0.5), 'XYZ')
            
        cp_empty.parent = room_root
        col.objects.link(cp_empty)
        
    # 4. Cristais nos cantos e paredes
    hw = width / 2.0
    hl = length / 2.0
    corner_spots = [
        Vector((-hw + 1.2, -hl + 1.2, 0)),
        Vector((hw - 1.2, -hl + 1.2, 0)),
        Vector((hw - 1.2, hl - 1.2, 0)),
        Vector((-hw + 1.2, hl - 1.2, 0)),
    ]
    
    wall_edge_spots = []
    if not doors["North"]: wall_edge_spots.append(Vector((0, hl - 1.0, 0)))
    if not doors["South"]: wall_edge_spots.append(Vector((0, -hl + 1.0, 0)))
    if not doors["East"]:  wall_edge_spots.append(Vector((hw - 1.0, 0, 0)))
    if not doors["West"]:  wall_edge_spots.append(Vector((-hw + 1.0, 0, 0)))
    
    crystal_spots = corner_spots + wall_edge_spots
    
    for i, spot in enumerate(crystal_spots):
        if random.random() < (crystal_density / 10.0):
            mat_crystal = mats["CrystalTeal"] if i % 2 == 0 else mats["CrystalPurple"]
            c_count = random.randint(3, 7)
            create_crystal_cluster(spot, count=c_count, radius=0.9, mat=mat_crystal, col=col, parent_obj=room_root)
            
    # Altar nos Becos Sem Saída
    if preset == "DEAD_END":
        altar_spot = Vector((0, hl - 2.5, 0))
        create_crystal_cluster(altar_spot, count=12, radius=1.8, mat=mats["CrystalTeal"], col=col, parent_obj=room_root)
        create_crystal_cluster(altar_spot + Vector((-1.2, 0.5, 0)), count=6, radius=1.0, mat=mats["CrystalPurple"], col=col, parent_obj=room_root)

    # 5. Cipós e raízes orgânicas
    if add_vines:
        build_organic_vines_mesh(corner_spots, mat=mats["Vines"], col=col, parent_obj=room_root)
        
    print(f"✨ Sala '{preset}' gerada com sucesso! Dimensões: {width}x{length}m, {len(door_positions)} Portas.")
    return room_root

# ═══════════════════════════════════════════════════════════════════════════
# 7. INTERFACE GRÁFICA NO BLENDER (N-PANEL)
# ═══════════════════════════════════════════════════════════════════════════

class PLEROTUS_PG_Settings(bpy.types.PropertyGroup):
    preset: bpy.props.EnumProperty(
        name="Preset de Sala",
        description="Escolha o tipo de sala para o Level Generator",
        items=[
            ('ARENA_4WAYS', "Arena 4 Vias (Cruz)", "Sala ampla de combate com 4 saídas"),
            ('ARENA_2WAYS_STRAIGHT', "Arena 2 Vias (Reta)", "Sala de combate com Entrada Sul e Saída Norte"),
            ('ARENA_2WAYS_CORNER', "Arena 2 Vias (Curva em L)", "Sala de combate com saída a 90 graus"),
            ('CORRIDOR', "Corredor de Transição", "Corredor reto estreito com 2 conexões"),
            ('DEAD_END', "Beco Sem Saída (Altar)", "Beco com altar de cristais e minérios no fundo"),
            ('ANOMALY_LAKE', "Sala de Anomalia (Lago)", "Sala ampla com lago bioluminescente central"),
        ],
        default='ARENA_4WAYS'
    )
    width: bpy.props.FloatProperty(name="Largura (X)", default=24.0, min=6.0, max=60.0, unit='LENGTH')
    length: bpy.props.FloatProperty(name="Comprimento (Y)", default=24.0, min=6.0, max=60.0, unit='LENGTH')
    wall_height: bpy.props.FloatProperty(name="Altura da Parede", default=4.0, min=2.0, max=10.0, unit='LENGTH')
    door_width: bpy.props.FloatProperty(name="Largura das Portas", default=5.0, min=2.0, max=10.0, unit='LENGTH')
    crystal_density: bpy.props.IntProperty(name="Densidade de Cristais", default=7, min=1, max=10)
    add_vines: bpy.props.BoolProperty(name="Adicionar Raízes e Cipós", default=True)
    seed: bpy.props.IntProperty(name="Seed Aleatória", default=101)

class PLEROTUS_OT_GenerateRoom(bpy.types.Operator):
    bl_idname = "plerotus.generate_room"
    bl_label = "🔮 GERAR SALA PROCEDURAL"
    bl_description = "Gera a sala completa com chão, paredes, cristais e conexões de porta"
    bl_options = {'REGISTER', 'UNDO'}
    
    def execute(self, context):
        settings = context.scene.plerotus_settings
        generate_plerotus_room(
            preset=settings.preset,
            width=settings.width,
            length=settings.length,
            wall_height=settings.wall_height,
            door_w=settings.door_width,
            crystal_density=settings.crystal_density,
            add_vines=settings.add_vines,
            seed=settings.seed
        )
        self.report({'INFO'}, f"Sala '{settings.preset}' gerada!")
        return {'FINISHED'}

class PLEROTUS_OT_ExportFBX(bpy.types.Operator):
    bl_idname = "plerotus.export_fbx"
    bl_label = "💾 EXPORTAR FBX PARA UNITY"
    bl_description = "Exporta a sala selecionada diretamente para a pasta Environment da Unity"
    bl_options = {'REGISTER', 'UNDO'}
    
    def execute(self, context):
        settings = context.scene.plerotus_settings
        target_dir = r"c:\Users\vicen\Documents\GitHub\RogueLikeProject\Assets\_Project\Enviroment\Map\Generated"
        os.makedirs(target_dir, exist_ok=True)
        
        filename = f"Room_{settings.preset}_{int(settings.width)}x{int(settings.length)}.fbx"
        full_path = os.path.join(target_dir, filename)
        
        col_name = f"Plerotus_Room_{settings.preset}"
        col = bpy.data.collections.get(col_name)
        if not col or len(col.objects) == 0:
            self.report({'ERROR'}, f"Coleção '{col_name}' não encontrada. Gere a sala primeiro!")
            return {'CANCELLED'}
            
        bpy.ops.object.select_all(action='DESELECT')
        for obj in col.objects:
            obj.select_set(True)
            
        bpy.ops.export_scene.fbx(
            filepath=full_path,
            use_selection=True,
            global_scale=1.0,
            apply_unit_scale=True,
            apply_scale_options='FBX_SCALE_ALL',
            axis_forward='-Z',
            axis_up='Y',
            bake_space_transform=True
        )
        
        self.report({'INFO'}, f"FBX exportado para: {full_path}")
        print(f"✅ FBX Exportado com sucesso: {full_path}")
        return {'FINISHED'}

class PLEROTUS_PT_MainPanel(bpy.types.Panel):
    bl_label = "Plerotus - Gerador de Mapas"
    bl_idname = "PLEROTUS_PT_main_panel"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = 'Plerotus'
    
    def draw(self, context):
        layout = self.layout
        settings = context.scene.plerotus_settings
        
        box = layout.box()
        box.label(text="Configuração da Sala", icon='SCENE_DATA')
        box.prop(settings, "preset")
        
        row = box.row()
        row.prop(settings, "width")
        row.prop(settings, "length")
        
        row2 = box.row()
        row2.prop(settings, "wall_height")
        row2.prop(settings, "door_width")
        
        box_det = layout.box()
        box_det.label(text="Detalhes & Bioma", icon='MOD_PHYSICS')
        box_det.prop(settings, "crystal_density")
        box_det.prop(settings, "add_vines")
        box_det.prop(settings, "seed")
        
        layout.separator()
        col = layout.column(align=True)
        col.scale_y = 1.6
        col.operator("plerotus.generate_room", icon='RESTRICT_RENDER_OFF')
        
        layout.separator()
        col2 = layout.column(align=True)
        col2.scale_y = 1.3
        col2.operator("plerotus.export_fbx", icon='EXPORT')

classes = [
    PLEROTUS_PG_Settings,
    PLEROTUS_OT_GenerateRoom,
    PLEROTUS_OT_ExportFBX,
    PLEROTUS_PT_MainPanel
]

def register():
    for c in classes:
        try:
            bpy.utils.register_class(c)
        except Exception:
            pass
    bpy.types.Scene.plerotus_settings = bpy.props.PointerProperty(type=PLEROTUS_PG_Settings)

def unregister():
    for c in reversed(classes):
        try:
            bpy.utils.unregister_class(c)
        except Exception:
            pass
    if hasattr(bpy.types.Scene, "plerotus_settings"):
        del bpy.types.Scene.plerotus_settings

if __name__ == "__main__":
    register()
    generate_plerotus_room(preset="ARENA_4WAYS", width=24.0, length=24.0, wall_height=4.0, door_w=5.0, crystal_density=7, add_vines=True, seed=101)
