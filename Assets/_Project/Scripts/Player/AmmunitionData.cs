using UnityEngine;

[CreateAssetMenu(fileName = "New Ammunition Data", menuName = "Plerotus/Armas/Ammunition Data", order = 1)]
public class AmmunitionData : ScriptableObject
{
    [Header("Visual e Prefab")]
    [Tooltip("O prefab do tiro que será instanciado")]
    public GunProjectile projectilePrefab;
    
    [Tooltip("Modelo 3D da arma para esta munição (opcional)")]
    public GameObject weaponModelPrefab;

    [Header("Atributos")]
    public int damage = 25;
    public float fireCooldown = 0.35f;
    
    [Header("Offset do Cano (Muzzle)")]
    [Tooltip("A posição de onde a bala vai sair (relativo à arma)")]
    public Vector3 muzzleOffset;
}
