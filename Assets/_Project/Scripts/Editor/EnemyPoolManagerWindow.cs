using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Janela visual para gerenciar o pool global de inimigos da dungeon (EnemyPoolConfig).
/// Acesse via Menu Unity: EPTA Tools -> Gerenciador de Inimigos (Enemy Pools)
/// </summary>
public class EnemyPoolManagerWindow : EditorWindow
{
    private EnemyPoolConfig targetPool;
    private SerializedObject serializedPool;
    private Vector2 scrollPos;

    private const string RESOURCE_PATH = "Assets/_Project/Resources/DefaultEnemyPool.asset";

    [MenuItem("EPTA Tools/Gerenciador de Inimigos (Enemy Pools)")]
    public static void OpenWindow()
    {
        EnemyPoolManagerWindow window = GetWindow<EnemyPoolManagerWindow>("Enemy Pool Manager");
        window.minSize = new Vector2(550, 600);
        window.LoadOrCreateDefaultPool();
        window.Show();
    }

    private void OnEnable()
    {
        LoadOrCreateDefaultPool();
    }

    private void LoadOrCreateDefaultPool()
    {
        targetPool = Resources.Load<EnemyPoolConfig>("DefaultEnemyPool");

        if (targetPool == null)
        {
            // Cria a pasta Resources se não existir
            string dir = Path.GetDirectoryName(RESOURCE_PATH);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            targetPool = AssetDatabase.LoadAssetAtPath<EnemyPoolConfig>(RESOURCE_PATH);
            if (targetPool == null)
            {
                targetPool = CreateInstance<EnemyPoolConfig>();
                AssetDatabase.CreateAsset(targetPool, RESOURCE_PATH);
                AssetDatabase.SaveAssets();
                Debug.Log($"[EnemyPoolManager] Criado novo asset central: {RESOURCE_PATH}");
            }
        }

        if (targetPool != null)
        {
            serializedPool = new SerializedObject(targetPool);
        }
    }

    private void OnGUI()
    {
        if (targetPool == null)
        {
            LoadOrCreateDefaultPool();
            if (targetPool == null)
            {
                EditorGUILayout.HelpBox("Não foi possível carregar ou criar o EnemyPoolConfig em Resources.", MessageType.Error);
                return;
            }
        }

        if (serializedPool == null)
            serializedPool = new SerializedObject(targetPool);

        serializedPool.Update();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("👾 Gerenciador Global de Inimigos (Enemy Pools)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Configure os monstros aqui uma única vez. Todas as salas da dungeon usarão esta mesma lista!", EditorStyles.miniLabel);
        EditorGUILayout.Space(4);

        // Campo do Asset Atual
        EditorGUILayout.BeginHorizontal();
        targetPool = (EnemyPoolConfig)EditorGUILayout.ObjectField("Asset Central Atual:", targetPool, typeof(EnemyPoolConfig), false);
        if (GUILayout.Button("Ping", GUILayout.Width(50)))
        {
            EditorGUIUtility.PingObject(targetPool);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // ── BOTÕES DE AÇÃO RÁPIDA ────────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.3f, 0.85f, 1f);
        if (GUILayout.Button("🔍 Auto-Detectar e Preencher Inimigos do Projeto", GUILayout.Height(28)))
        {
            AutoDetectProjectEnemies();
        }

        GUI.backgroundColor = new Color(0.4f, 1f, 0.4f);
        if (GUILayout.Button("🚀 Vincular Pool a Todas as Salas", GUILayout.Height(28)))
        {
            SyncPoolToAllRooms();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // ── LISTAS VISUAIS DE CATEGORIAS ─────────────────────────────────────
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        DrawCategory("🐜 Mobs Menores (1 pt — Enxame / Leve)", "mobMenorPrefabs", new Color(0.8f, 1f, 0.8f));
        DrawCategory("🏹 Atiradores (2 pts — Projéteis à Distância)", "atiradorPrefabs", new Color(1f, 0.9f, 0.6f));
        DrawCategory("🛡️ Tanques (4 pts — Vida Alta / Pesados)", "tanquePrefabs", new Color(0.7f, 0.85f, 1f));
        DrawCategory("👑 Elites (10 pts — Mini-Boss / Sub-Chefe)", "elitePrefabs", new Color(1f, 0.7f, 0.7f));
        DrawCategory("🧪 Suportes (3 pts — Buffs / Healers)", "suportePrefabs", new Color(0.9f, 0.7f, 1f));

        EditorGUILayout.EndScrollView();

        serializedPool.ApplyModifiedProperties();
    }

    private void DrawCategory(string label, string propertyName, Color boxColor)
    {
        Color prev = GUI.backgroundColor;
        GUI.backgroundColor = boxColor;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.backgroundColor = prev;

        SerializedProperty listProp = serializedPool.FindProperty(propertyName);
        if (listProp != null)
        {
            EditorGUILayout.PropertyField(listProp, new GUIContent(label), true);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4);
    }

    /// <summary>
    /// Varre as pastas de inimigos do projeto e preenche o EnemyPoolConfig com os prefabs corretos.
    /// </summary>
    private void AutoDetectProjectEnemies()
    {
        Undo.RecordObject(targetPool, "Auto-Detectar Inimigos");

        targetPool.mobMenorPrefabs.Clear();
        targetPool.atiradorPrefabs.Clear();
        targetPool.tanquePrefabs.Clear();
        targetPool.elitePrefabs.Clear();
        targetPool.suportePrefabs.Clear();

        void AddIfValid(List<GameObject> list, string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && !list.Contains(prefab)) list.Add(prefab);
        }

        // 1. Mobs Menores (1 pt)
        AddIfValid(targetPool.mobMenorPrefabs, "Assets/_Project/Enemies/Spider/Spider.prefab");
        AddIfValid(targetPool.mobMenorPrefabs, "Assets/_Project/Enemies/SharpBlur/Meshy_AI_Amethyst_Wraith_biped/ANt_Sharp.prefab");
        AddIfValid(targetPool.mobMenorPrefabs, "Assets/_Project/Enemies/Totem/Totem 1.prefab");

        // 2. Atiradores (2 pts)
        AddIfValid(targetPool.atiradorPrefabs, "Assets/_Project/Enemies/Peixe/Peixe.prefab");
        AddIfValid(targetPool.atiradorPrefabs, "Assets/_Project/Enemies/CrystalWatcher/CrystalWatcher.prefab");
        AddIfValid(targetPool.atiradorPrefabs, "Assets/_Project/Enemies/Goblin/Goblin.prefab");

        // 3. Tanques (4 pts)
        AddIfValid(targetPool.tanquePrefabs, "Assets/_Project/Enemies/Golem/Golem.prefab");
        AddIfValid(targetPool.tanquePrefabs, "Assets/_Project/Enemies Shortcut/Enemies/MagicStoneEnemy.prefab");
        AddIfValid(targetPool.tanquePrefabs, "Assets/_Project/Enemies/ShardSwarm/Star.prefab");

        // 4. Elites (10 pts)
        AddIfValid(targetPool.elitePrefabs, "Assets/GameAssets/Prefabs-Gabriel/Enemies Prefabs/Geobionte.prefab");

        // 5. Suporte (3 pts)
        AddIfValid(targetPool.suportePrefabs, "Assets/_Project/Enemies/Cristalus/Cristalus.prefab");
        AddIfValid(targetPool.suportePrefabs, "Assets/_Project/Enemies/CrystalTunner/CrystalTuner_Root.prefab");

        EditorUtility.SetDirty(targetPool);
        AssetDatabase.SaveAssets();

        int totalFound = targetPool.GetAllUniquePrefabs().Count;
        EditorUtility.DisplayDialog("Inimigos Detectados", $"{totalFound} tipos de inimigos únicos foram catalogados e organizados nas 5 categorias!", "OK");
    }

    /// <summary>
    /// Atualiza todas as salas de combate do projeto e da cena ativa para vincularem o EnemyPoolConfig
    /// </summary>
    private void SyncPoolToAllRooms()
    {
        serializedPool?.ApplyModifiedProperties();
        EditorUtility.SetDirty(targetPool);
        AssetDatabase.SaveAssets();

        RoomControllerEnemyPopulator.PopulateAllRoomControllers();
    }
}
