using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Script Editor automático ([InitializeOnLoad]) que configura:
/// 1. As animações Mixamo do Bumerangue (Astronaut_tirandodascostas_boomerang, Astronaut_guardando_boomerang, Astronaut_defense)
///    como Humanoid e vincula os AnimationClips diretamente no BoomerangAnimation.controller.
/// 2. O material do Bumerangue com a textura boomerang_texture.png.
/// 3. O Prefab completo do Bumerangue (em Assets/_Project/Items_and_Crafting/Boomerang/Boomerang.prefab e
///    em Assets/_Project/Props/WeaponRack/Resources/Boomerang.prefab) com todos os componentes visuais, VFX
///    e a Máquina de Estados.
/// </summary>
[InitializeOnLoad]
public static class BoomerangSetupHelper
{
    private const string BOOMERANG_FOLDER = "Assets/_Project/Items_and_Crafting/Boomerang";
    private const string BOOMERANG_FBX_PATH = BOOMERANG_FOLDER + "/boomerang.fbx";
    private const string BOOMERANG_TEX_PATH = BOOMERANG_FOLDER + "/boomerang_texture.png";
    private const string BOOMERANG_MAT_PATH = BOOMERANG_FOLDER + "/Boomerang_Material.mat";
    private const string BOOMERANG_PREFAB_MAIN = BOOMERANG_FOLDER + "/Boomerang.prefab";
    private const string BOOMERANG_PREFAB_RESOURCES = "Assets/_Project/Props/WeaponRack/Resources/Boomerang.prefab";

    private const string ANIM_DRAW_FBX = BOOMERANG_FOLDER + "/Astronaut_tirandodascostas_boomerang.fbx";
    private const string ANIM_HOLSTER_FBX = BOOMERANG_FOLDER + "/Astronaut_guardando_boomerang.fbx";
    private const string ANIM_DEFENSE_FBX = BOOMERANG_FOLDER + "/Astronaut_defense.fbx";

    private const string CONTROLLER_MAIN = "Assets/_Project/Animations/Weapons/BoomerangAnimation.controller";
    private const string CONTROLLER_RESOURCES = "Assets/_Project/Props/WeaponRack/Resources/BoomerangAnimation.controller";

    static BoomerangSetupHelper()
    {
        EditorApplication.delayCall += RunAutomaticSetup;
    }

    private static void RunAutomaticSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        // Se o prefab já existe em Resources e possui BoomerangWeapon configurado, apenas garante os clips do Animator
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BOOMERANG_PREFAB_RESOURCES);
        if (existingPrefab != null && existingPrefab.GetComponent<BoomerangWeapon>() != null)
        {
            WireAnimatorClips(CONTROLLER_MAIN);
            WireAnimatorClips(CONTROLLER_RESOURCES);
            return;
        }

        BuildAllBoomerangAssets();
    }

    [MenuItem("Tools/Boomerang/Reconstruir Prefab e Animator do Bumerangue")]
    public static void BuildAllBoomerangAssets()
    {
        ConfigureHumanoidFBX(ANIM_DRAW_FBX, false);
        ConfigureHumanoidFBX(ANIM_HOLSTER_FBX, false);
        ConfigureHumanoidFBX(ANIM_DEFENSE_FBX, true);

        WireAnimatorClips(CONTROLLER_MAIN);
        WireAnimatorClips(CONTROLLER_RESOURCES);

        Material boomerangMat = EnsureBoomerangMaterial();
        BuildBoomerangPrefab(BOOMERANG_PREFAB_MAIN, boomerangMat);
        BuildBoomerangPrefab(BOOMERANG_PREFAB_RESOURCES, boomerangMat);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BoomerangSetupHelper] Prefabs, Material e AnimatorController do Bumerangue configurados com sucesso!");
    }

    [MenuItem("Tools/Boomerang/Equipar Bumerangue no Player Agora (Play Mode)")]
    public static void EquipBoomerangOnPlayerNow()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[BoomerangSetupHelper] Entre em Play Mode para equipar o Bumerangue diretamente no Player.");
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[BoomerangSetupHelper] Player não encontrado na cena.");
            return;
        }

        Player_WeaponManager wm = player.GetComponent<Player_WeaponManager>() ?? player.GetComponentInChildren<Player_WeaponManager>();
        GameObject prefab = Resources.Load<GameObject>("Boomerang")
                         ?? AssetDatabase.LoadAssetAtPath<GameObject>(BOOMERANG_PREFAB_MAIN);

        if (wm != null && prefab != null)
        {
            wm.EquipWeaponPrefab(prefab);
            Debug.Log("[BoomerangSetupHelper] Bumerangue equipado no Player!");
        }
    }

    private static void ConfigureHumanoidFBX(string fbxPath, bool loopTime)
    {
        ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null) return;

        bool changed = false;
        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            changed = true;
        }

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        if (clips != null && clips.Length > 0)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].loopTime != loopTime)
                {
                    clips[i].loopTime = loopTime;
                    clips[i].loopPose = loopTime;
                    changed = true;
                }
            }
            importer.clipAnimations = clips;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static AnimationClip LoadClipFromFBX(string fbxPath)
    {
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        if (allAssets == null) return null;

        foreach (Object obj in allAssets)
        {
            if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                return clip;
            }
        }
        return null;
    }

    private static void WireAnimatorClips(string controllerPath)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null || controller.layers == null || controller.layers.Length == 0)
            return;

        AnimationClip drawClip = LoadClipFromFBX(ANIM_DRAW_FBX);
        AnimationClip holsterClip = LoadClipFromFBX(ANIM_HOLSTER_FBX);
        AnimationClip defenseClip = LoadClipFromFBX(ANIM_DEFENSE_FBX);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        bool modified = false;

        foreach (ChildAnimatorState childState in sm.states)
        {
            if (childState.state == null) continue;

            if (childState.state.name == "DrawWeapon" && drawClip != null && childState.state.motion != drawClip)
            {
                childState.state.motion = drawClip;
                modified = true;
            }
            else if (childState.state.name == "Holstering" && holsterClip != null && childState.state.motion != holsterClip)
            {
                childState.state.motion = holsterClip;
                modified = true;
            }
            else if (childState.state.name == "Defending" && defenseClip != null && childState.state.motion != defenseClip)
            {
                childState.state.motion = defenseClip;
                modified = true;
            }
        }

        if (modified)
        {
            EditorUtility.SetDirty(controller);
        }
    }

    private static Material EnsureBoomerangMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(BOOMERANG_MAT_PATH);
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(BOOMERANG_TEX_PATH);

        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Standard");
            mat = new Material(shader);
            mat.name = "Boomerang_Material";
            AssetDatabase.CreateAsset(mat, BOOMERANG_MAT_PATH);
        }

        if (tex != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            EditorUtility.SetDirty(mat);
        }

        return mat;
    }

    private static void BuildBoomerangPrefab(string targetPrefabPath, Material boomerangMat)
    {
        GameObject fbxModel = AssetDatabase.LoadAssetAtPath<GameObject>(BOOMERANG_FBX_PATH);
        if (fbxModel == null)
        {
            Debug.LogWarning("[BoomerangSetupHelper] boomerang.fbx não encontrado em: " + BOOMERANG_FBX_PATH);
            return;
        }

        GameObject rootGO = new GameObject("Boomerang");
        rootGO.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);

        GameObject visualPivotGO = new GameObject("VisualPivot");
        visualPivotGO.transform.SetParent(rootGO.transform, false);
        visualPivotGO.transform.localPosition = Vector3.zero;
        visualPivotGO.transform.localRotation = Quaternion.identity;
        visualPivotGO.transform.localScale = Vector3.one;

        GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbxModel, visualPivotGO.transform);
        modelInstance.name = "boomerang";
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.identity;

        // Garante que o nó interno "boomerang" (que vem do Blockbench com Y=150) fique centralizado em (0,0,0)
        foreach (Transform child in modelInstance.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.Equals("boomerang", System.StringComparison.OrdinalIgnoreCase))
            {
                child.localPosition = Vector3.zero;
            }
        }

        if (boomerangMat != null)
        {
            foreach (Renderer r in modelInstance.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = boomerangMat;
            }
        }

        // Componentes de VFX e Arma
        BoomerangAirSpinVFX airSpin = rootGO.AddComponent<BoomerangAirSpinVFX>();
        BoomerangElectricCrystalVFX electricCrystal = rootGO.AddComponent<BoomerangElectricCrystalVFX>();
        BoomerangEchoVFX echoVFX = rootGO.AddComponent<BoomerangEchoVFX>();
        BoomerangShieldVFX shieldVFX = rootGO.AddComponent<BoomerangShieldVFX>();
        BoomerangWeapon boomerangWeapon = rootGO.AddComponent<BoomerangWeapon>();
        boomerangWeapon.BindAndNormalizeModelHierarchy();

        // Componentes de integração com Player_WeaponManager e WeaponRack
        WeaponOffset offset = rootGO.AddComponent<WeaponOffset>();
        offset.weaponType = WeaponType.Boomerang;
        offset.equipPosition = new Vector3(-0.04f, 0.06f, 0.02f);
        offset.equipRotation = new Vector3(0f, 90f, 90f);
        offset.stowedBackPosition = new Vector3(0f, 0.02f, -0.22f);
        offset.stowedBackRotation = new Vector3(0f, 90f, 35f);
        offset.weaponAnimatorOverride = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CONTROLLER_RESOURCES)
                                      ?? AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CONTROLLER_MAIN);

        ItemFloat itemFloat = rootGO.AddComponent<ItemFloat>();
        itemFloat.floatHeight = 0.25f;
        itemFloat.floatSpeed = 1f;
        itemFloat.rotationSpeed = 15f;
        itemFloat.rotationAxis = Vector3.up;

        Dagger_Pickup pickup = rootGO.AddComponent<Dagger_Pickup>();
        pickup.actionText = "Pegar Bumerangue de Cristal";
        pickup.fadeSpeed = 6f;
        pickup.promptOffset = new Vector3(0f, 0.65f, 0f);

        BoxCollider boxCol = rootGO.AddComponent<BoxCollider>();
        boxCol.isTrigger = true;
        boxCol.size = new Vector3(0.65f, 1.55f, 0.35f);
        boxCol.center = Vector3.zero;

        PrefabUtility.SaveAsPrefabAsset(rootGO, targetPrefabPath);
        Object.DestroyImmediate(rootGO);
    }
}
