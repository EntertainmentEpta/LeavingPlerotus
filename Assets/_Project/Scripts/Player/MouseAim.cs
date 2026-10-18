using UnityEngine;

public class MouseAim : MonoBehaviour
{
    private Animator anim;
    private Transform spine;
    private Transform rightHand;
    
    [Header("Configurações")]
    public bool enableMouseAim = true;
    public float aimSmoothness = 10f;
    
    [Tooltip("Ajuste fino caso a arma aponte meio torto pro mouse")]
    public Vector3 aimOffsetRotation = Vector3.zero;

    private void Start()
    {
        anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            spine = anim.GetBoneTransform(HumanBodyBones.Spine);
            rightHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        }
    }

    private void LateUpdate()
    {
        if (!enableMouseAim || spine == null) return;
        if (DialogManager.BlocksGameplayInput || GamePause.IsPaused) return;
        if (CraftingUI.Instance != null && CraftingUI.Instance.IsOpen()) return;
        if (JournalBookUI.Instance != null && JournalBookUI.Instance.IsOpen) return;

        // 1. Descobrir onde o mouse está no mundo 3D
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, spine.position.y, 0));
        
        Vector3 targetPoint;
        if (groundPlane.Raycast(ray, out float enter))
        {
            targetPoint = ray.GetPoint(enter);
        }
        else
        {
            targetPoint = transform.position + transform.forward * 10f; // Failsafe
        }

        // 2. Fazer o personagem olhar pro mouse (Eixo Y apenas)
        Vector3 lookDir = targetPoint - transform.position;
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, aimSmoothness * Time.deltaTime);
        }

        // 3. Forçar a espinha (tronco) a acompanhar a mira para não entortar o braço sozinho
        // Com isso, a animação de tiro (Shooting) não vai conseguir virar a arma para fora, 
        // pois o IK LateUpdate tem a última palavra!
    }
}
