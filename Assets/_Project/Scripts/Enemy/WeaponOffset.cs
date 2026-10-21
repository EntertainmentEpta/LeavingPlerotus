using UnityEngine;

public enum WeaponType { Dagger, Sword, Axe, Gun }

public class WeaponOffset : MonoBehaviour
{
    [Header("Identificação")]
    public WeaponType weaponType;

    [Header("Configurações de Encaixe na Mão")]
    public Vector3 equipPosition;
    public Vector3 equipRotation;

    [Header("Animações (Moveset)")]

    public RuntimeAnimatorController weaponAnimatorOverride; 

#if UNITY_EDITOR
    private void Update()
    {
        // Permite alterar o offset em tempo real no Inspector durante o Play Mode
        if (Application.isPlaying && transform.parent != null)
        {
            transform.localPosition = equipPosition;
            transform.localEulerAngles = equipRotation;
        }
    }
#endif
}