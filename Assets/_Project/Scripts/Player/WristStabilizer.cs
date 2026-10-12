using UnityEngine;

public class WristStabilizer : MonoBehaviour
{
    private Animator anim;
    private Transform rightHand;
    private Quaternion baseLocalRotation;
    private bool hasBaseRotation = false;

    void Start()
    {
        anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            rightHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        }
    }

    void LateUpdate()
    {
        if (rightHand == null || anim == null) return;

        // Pega a informação de qual animação está tocando
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        
        // Se estiver no Idle, salva a rotação "correta" da mão como base
        if (stateInfo.IsName("Pistol Idle") || stateInfo.IsName("Idle"))
        {
            baseLocalRotation = rightHand.localRotation;
            hasBaseRotation = true;
        }

        // Se estiver atirando e a animação estiver entortando o pulso, forçamos o pulso a ficar reto
        if (hasBaseRotation && stateInfo.IsName("Shooting"))
        {
            rightHand.localRotation = baseLocalRotation;
        }
    }
}
