# -*- coding: utf-8 -*-
"""
Script de Calibração Direta de Prefabs e Drops:
1. Golem: Reduz essência de 100 para 30.
2. Cristalus: Reduz essência de 100 para 15.
3. Peixe: Corrige maxHealth de 300 para 75 e anexa EnemyDrops com 4 tiers.
4. Goblin: Anexa EnemyDrops com 4 tiers (coin, braço, amulet, bomb).
5. Star: Configura EnemyDrops com 4 tiers de shard e essência 20.
6. CrystalWatcher: Adiciona 4º tier (watcher_beacon_t4) e calibra weights.
"""

import os
import re

ESSENCE_GUID = "aac8898d50c757240944683fe67b0d7c"
ESSENCE_FILEID = "1526004001283604428"
ENEMY_DROPS_SCRIPT_GUID = "182d9df1045b668438bf37c0018f5e08"

def update_file(path, modify_fn):
    if not os.path.exists(path):
        print(f"[MISSING] {path}")
        return False
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()
    new_content = modify_fn(content)
    if new_content != content:
        with open(path, 'w', encoding='utf-8', newline='\n') as f:
            f.write(new_content)
        print(f"[UPDATED] {path}")
        return True
    else:
        print(f"[UNCHANGED] {path}")
        return False

# 1. Golem
def mod_golem(txt):
    # reduce essenceAmount: 100 -> 30
    return re.sub(r'(EnemyDrops[\s\S]*?essenceAmount:\s*)100', r'\g<1>30', txt)

# 2. Cristalus
def mod_cristalus(txt):
    # reduce essenceAmount: 100 -> 15
    return re.sub(r'(EnemyDrops[\s\S]*?essenceAmount:\s*)100', r'\g<1>15', txt)

# 3. Peixe
def mod_peixe(txt):
    # 1. fix maxHealth to 75
    txt = re.sub(r'(DummyHealth[\s\S]*?maxHealth:\s*)300', r'\g<1>75', txt)
    
    # 2. check if EnemyDrops already present
    if 'EnemyDrops' in txt:
        return txt
        
    peixe_drops_mb_id = "8945618291029384751"
    root_gameobject_id = "6455373743372300023"
    
    # add to m_Component of root GameObject
    comp_entry = f"  - component: {{fileID: {peixe_drops_mb_id}}}\n"
    # insert before m_Layer of Peixe
    target_needle = f"--- !u!1 &{root_gameobject_id}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  serializedVersion: 6\n  m_Component:\n"
    if target_needle in txt:
        txt = txt.replace(target_needle, target_needle + comp_entry)
    else:
        # fallback search
        pattern = rf"(--- !u!1 &{root_gameobject_id}[\s\S]*?m_Component:\n)"
        txt = re.sub(pattern, rf"\g<1>{comp_entry}", txt)
        
    # append EnemyDrops MonoBehaviour
    mb_block = f"""--- !u!114 &{peixe_drops_mb_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {root_gameobject_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {ENEMY_DROPS_SCRIPT_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::EnemyDrops
  essencePrefab: {{fileID: {ESSENCE_FILEID}, guid: {ESSENCE_GUID}, type: 3}}
  essenceAmount: 12
  essenceVariation: 3
  globalDropChance: 0.18
  itemAmount: 1
  lootPool:
  - itemPrefab: {{fileID: 3032503514866833266, guid: 76b7b03a4e2e8bf4383a94ebda98a6a7, type: 3}}
    weight: 60
  - itemPrefab: {{fileID: 3032503514866833266, guid: 228b3dd20599a464bbaa2c9ba8a073c3, type: 3}}
    weight: 25
  - itemPrefab: {{fileID: 3032503514866833266, guid: 7565ab52d13de0d41b12c37c8e222163, type: 3}}
    weight: 10
  - itemPrefab: {{fileID: 3032503514866833266, guid: 4d565b28f0bf7c647ac7ba91637823b9, type: 3}}
    weight: 5
  spawnHeight: 0.5
  spawnRadius: 0.5
  launchForce: 3
"""
    txt = txt.rstrip() + "\n" + mb_block
    return txt

# 4. Goblin
def mod_goblin(txt):
    if 'EnemyDrops' in txt:
        return txt
    goblin_drops_mb_id = "8945618291029384752"
    goblin_root_id = "3359006950562635497"
    
    mb_block = f"""--- !u!114 &{goblin_drops_mb_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {goblin_root_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {ENEMY_DROPS_SCRIPT_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::EnemyDrops
  essencePrefab: {{fileID: {ESSENCE_FILEID}, guid: {ESSENCE_GUID}, type: 3}}
  essenceAmount: 12
  essenceVariation: 3
  globalDropChance: 0.18
  itemAmount: 1
  lootPool:
  - itemPrefab: {{fileID: 3089044448427026720, guid: cd5363bd7755ba74a85b266b7d99ef00, type: 3}}
    weight: 60
  - itemPrefab: {{fileID: 6505549017638245554, guid: 769ebadc573a10d429b42596ae615992, type: 3}}
    weight: 25
  - itemPrefab: {{fileID: 5476218115834836325, guid: d00579b0f66fc7446a2a50932fecb5a0, type: 3}}
    weight: 10
  - itemPrefab: {{fileID: 956904379249230389, guid: f84164d1cf7cf324284c542bb4f7f0ce, type: 3}}
    weight: 5
  spawnHeight: 0.5
  spawnRadius: 0.5
  launchForce: 3
"""
    txt = txt.rstrip() + "\n" + mb_block
    return txt

# 5. Star (Shard Swarm)
def mod_star(txt):
    old_block = """  essencePrefab: {fileID: 0}
  essenceAmount: 10
  essenceVariation: 3
  globalDropChance: 0.3
  itemAmount: 1
  lootPool: []"""

    new_block = f"""  essencePrefab: {{fileID: {ESSENCE_FILEID}, guid: {ESSENCE_GUID}, type: 3}}
  essenceAmount: 20
  essenceVariation: 4
  globalDropChance: 0.35
  itemAmount: 1
  lootPool:
  - itemPrefab: {{fileID: 2386725423553547639, guid: fadb8518fec226e4f8da46868770d7ab, type: 3}}
    weight: 40
  - itemPrefab: {{fileID: 472676249650753938, guid: 3337efe9b0710d04dae49eb31b5d7ad0, type: 3}}
    weight: 35
  - itemPrefab: {{fileID: 1754924502174001934, guid: 8b323a7c549a9344d88c48a7aeabb7fb, type: 3}}
    weight: 20
  - itemPrefab: {{fileID: 2432034188845710544, guid: 1e06954331061f945a8696e4d94bdb19, type: 3}}
    weight: 5"""

    if old_block in txt:
        return txt.replace(old_block, new_block)
    
    # regex fallback
    return re.sub(
        r'  essencePrefab: \{fileID: 0\}[\s\S]*?lootPool: \[\]',
        new_block,
        txt
    )

# 6. Crystal Watcher
def mod_watcher(txt):
    old_pool = """  lootPool:
  - itemPrefab: {fileID: 1066167381274076914, guid: 7b639c0e4681c43489f312080083bfa6, type: 3}
    weight: 50
  - itemPrefab: {fileID: 1066167381274076914, guid: 3bf3a6a977e94db4cb2472416f907426, type: 3}
    weight: 30
  - itemPrefab: {fileID: 1066167381274076914, guid: d2915385f60760446987c1c69ec0ff4b, type: 3}
    weight: 20"""

    new_pool = """  globalDropChance: 0.18
  itemAmount: 1
  lootPool:
  - itemPrefab: {fileID: 1066167381274076914, guid: 7b639c0e4681c43489f312080083bfa6, type: 3}
    weight: 60
  - itemPrefab: {fileID: 1066167381274076914, guid: 3bf3a6a977e94db4cb2472416f907426, type: 3}
    weight: 25
  - itemPrefab: {fileID: 1066167381274076914, guid: d2915385f60760446987c1c69ec0ff4b, type: 3}
    weight: 10
  - itemPrefab: {fileID: 4348939969585618732, guid: 3dce3c0fc194c1646a5342b7749aeea6, type: 3}
    weight: 5"""

    target_match = """  globalDropChance: 1
  itemAmount: 1
""" + old_pool

    if target_match in txt:
        return txt.replace(target_match, new_pool)
    return txt

if __name__ == "__main__":
    print("Iniciando calibração direta de prefabs...")
    update_file("Assets/_Project/Enemies/Golem/Golem.prefab", mod_golem)
    update_file("Assets/_Project/Enemies/Cristalus/Cristalus.prefab", mod_cristalus)
    update_file("Assets/_Project/Enemies/Peixe/Peixe.prefab", mod_peixe)
    update_file("Assets/_Project/Enemies/Goblin/Goblin.prefab", mod_goblin)
    update_file("Assets/_Project/Enemies/ShardSwarm/Star.prefab", mod_star)
    update_file("Assets/_Project/Enemies/CrystalWatcher/CrystalWatcher.prefab", mod_watcher)
    print("Calibração concluída!")
