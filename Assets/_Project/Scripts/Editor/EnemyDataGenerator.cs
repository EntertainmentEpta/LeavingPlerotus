using UnityEditor;
using UnityEngine;

public class EnemyDataGenerator
{
    [MenuItem("Tools/Generate Missing Enemy Data")]
    public static void Generate()
    {
        CreateEnemyData("Geobionte", "Elite Tank", "Criatura colossal de pedra e cristais que absorve minerais do solo para reparar sua carapaça.", 450, 45, 10, "Assets/_Project/Enemies/Boss/Geobionte Data.asset");
        CreateEnemyData("SharpBlur", "Elite Assassin", "Humanoide corrompido que se move em velocidades absurdas. Usa lâminas de energia focada.", 180, 50, 8, "Assets/_Project/Enemies/Boss/SharpBlur Data.asset");
        CreateEnemyData("Sentinela", "Guardian", "Estrutura bípede automatizada. Defende áreas cruciais projetando escudos de energia densa.", 300, 30, 8, "Assets/_Project/Enemies/Boss/Sentinela Data.asset");
        
        AssetDatabase.SaveAssets();
        Debug.Log("Generated missing EnemyData assets.");
    }

    private static void CreateEnemyData(string name, string eclass, string desc, int hp, float dmg, int cost, string path)
    {
        EnemyData ed = ScriptableObject.CreateInstance<EnemyData>();
        ed.enemyName = name;
        ed.enemyClass = eclass;
        ed.descricao = desc;
        ed.vidaBase = hp;
        ed.danoBase = dmg;
        ed.custoBudget = cost;
        AssetDatabase.CreateAsset(ed, path);
    }
}
