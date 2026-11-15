using UnityEngine;

public enum WeaponType { Dagger, Sword, Axe, Gun, Boomerang }

public class WeaponOffset : MonoBehaviour
{
    [Header("Identificação")]
    public WeaponType weaponType;

    [Header("Configurações de Encaixe na Mão")]
    public Vector3 equipPosition;
    public Vector3 equipRotation;

    [Header("Configurações de Encaixe nas Costas (Stowed / Boomerang)")]
    public Vector3 stowedBackPosition = new Vector3(0f, 0.18f, -0.18f);
    public Vector3 stowedBackRotation = new Vector3(0f, 90f, 35f);

    [HideInInspector]
    public bool isStowedOnBack = false;

    [HideInInspector]
    public bool disableEditorLiveHandSnap = false;

    [Header("Animações (Moveset)")]
    public RuntimeAnimatorController weaponAnimatorOverride; 

#if UNITY_EDITOR
    private void Update()
    {
        // Permite alterar o offset em tempo real no Inspector durante o Play Mode
        if (Application.isPlaying && transform.parent != null && !disableEditorLiveHandSnap)
        {
            if (isStowedOnBack)
            {
                transform.localPosition = stowedBackPosition;
                transform.localEulerAngles = stowedBackRotation;
            }
            else
            {
                transform.localPosition = equipPosition;
                transform.localEulerAngles = equipRotation;
            }
        }
    }
#endif
}