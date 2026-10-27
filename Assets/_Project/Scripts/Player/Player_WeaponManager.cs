using UnityEngine;
using System.Collections;

public class Player_WeaponManager : MonoBehaviour
{
    public Transform rightHand;
    public PrimaryAttackKnife attackScript;
    
    [Header("Animação")]
    public Animator playerAnimator; 
    private RuntimeAnimatorController defaultAnimatorController;

    [Header("Arma Ativa / Coldre")]
    public GameObject currentWeapon;
    public bool isWeaponDrawn = true;
    public KeyCode holsterKey = KeyCode.G;
    [Tooltip("Tempo em segundos para a arma sumir da mão após iniciar a animação de guardar.")]
    public float holsterDelay = 0.6f;
    [Tooltip("Tempo em segundos para a arma aparecer na mão após iniciar a animação de empunhar.")]
    public float drawDelay = 0.3f;
    private RuntimeAnimatorController activeWeaponController;
    private Coroutine holsterCoroutine;
    private Coroutine drawCoroutine;
    private string lastSceneName;

    void Awake()
    {
        if (attackScript == null)
        {
            attackScript = GetComponent<PrimaryAttackKnife>() ?? GetComponentInChildren<PrimaryAttackKnife>() ?? GetComponentInParent<PrimaryAttackKnife>();
        }
        if (playerAnimator == null)
        {
            playerAnimator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        }
    }

    void Start()
    {
        if (attackScript == null)
        {
            attackScript = GetComponent<PrimaryAttackKnife>() ?? GetComponentInChildren<PrimaryAttackKnife>() ?? GetComponentInParent<PrimaryAttackKnife>();
        }

        if (playerAnimator != null)
        {
            defaultAnimatorController = playerAnimator.runtimeAnimatorController;
        }

        lastSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        // Tenta detectar arma já acoplada na inicialização
        if (rightHand == null)
        {
            rightHand = transform.Find("RightHand") ?? transform.Find("Hand_R") ?? transform.Find("Hand.R");
        }
        if (rightHand != null && rightHand.childCount > 0)
        {
            // Filtra os filhos para garantir que apenas objetos com WeaponOffset sejam considerados armas
            foreach (Transform child in rightHand)
            {
                WeaponOffset offset = child.GetComponent<WeaponOffset>();
                if (offset != null)
                {
                    currentWeapon = child.gameObject;
                    isWeaponDrawn = currentWeapon.activeSelf;
                    if (attackScript != null)
                    {
                        bool isMelee = offset.weaponType != WeaponType.Gun;
                        attackScript.hasWeapon = isWeaponDrawn && isMelee;
                    }
                    if (offset.weaponAnimatorOverride != null)
                    {
                        activeWeaponController = offset.weaponAnimatorOverride;
                    }
                    break;
                }
            }
        }
    }

    void Update()
    {
        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        // Se mudou de cena e NÃO está na base, força a empunhar a arma imediatamente
        if (activeScene != lastSceneName)
        {
            lastSceneName = activeScene;
            bool isInBase = (activeScene == "Base" || activeScene == "BaseLab");
            if (!isInBase && currentWeapon != null)
            {
                if (!isWeaponDrawn)
                {
                    DrawWeapon();
                }
                else
                {
                    WeaponOffset offset = currentWeapon.GetComponent<WeaponOffset>();
                    if (attackScript != null)
                    {
                        bool isMelee = offset == null || offset.weaponType != WeaponType.Gun;
                        attackScript.hasWeapon = isMelee;
                    }
                    if (playerAnimator != null && activeWeaponController != null)
                    {
                        playerAnimator.runtimeAnimatorController = activeWeaponController;
                    }
                }
            }
        }

        // Só permite guardar/empunhar manualmente se estiver na Base (Base ou BaseLab)
        bool isInBaseNow = (activeScene == "Base" || activeScene == "BaseLab");
        if (isInBaseNow && Input.GetKeyDown(holsterKey))
        {
            ToggleWeaponDrawState();
        }
    }

    public void ToggleWeaponDrawState()
    {
        if (currentWeapon == null) return;

        if (isWeaponDrawn)
        {
            HolsterWeapon();
        }
        else
        {
            DrawWeapon();
        }
    }

    public void HolsterWeaponImmediate()
    {
        if (holsterCoroutine != null) StopCoroutine(holsterCoroutine);
        if (drawCoroutine != null) StopCoroutine(drawCoroutine);

        isWeaponDrawn = false;

        if (attackScript != null)
        {
            attackScript.hasWeapon = false;
        }

        DashM dashM = GetComponent<DashM>() ?? GetComponentInParent<DashM>();
        if (dashM != null)
        {
            dashM.SetBonusDashes(0);
        }

        if (playerAnimator != null && defaultAnimatorController != null)
        {
            playerAnimator.runtimeAnimatorController = defaultAnimatorController;
        }

        if (currentWeapon != null)
        {
            currentWeapon.SetActive(false);
            Debug.Log("[Player_WeaponManager] Arma guardada imediatamente.");
        }
    }

    public void HolsterWeapon()
    {
        if (currentWeapon == null) return;

        if (holsterCoroutine != null) StopCoroutine(holsterCoroutine);
        if (drawCoroutine != null) StopCoroutine(drawCoroutine);

        holsterCoroutine = StartCoroutine(DoHolsterWeapon());
    }

    private IEnumerator DoHolsterWeapon()
    {
        isWeaponDrawn = false;

        // Desativa a capacidade de ataque do script de ataque primário imediatamente
        if (attackScript != null)
        {
            attackScript.hasWeapon = false;
        }

        // Restaura o Animator Controller padrão (Unarmed) e dispara o trigger
        if (playerAnimator != null)
        {
            playerAnimator.runtimeAnimatorController = defaultAnimatorController;
            playerAnimator.SetTrigger("HolsterWeapon");
        }

        // Espera o tempo da animação antes de sumir com o visual da arma
        yield return new WaitForSeconds(holsterDelay);

        if (currentWeapon != null && !isWeaponDrawn)
        {
            currentWeapon.SetActive(false);
            Debug.Log("[Player_WeaponManager] Arma guardada (Holstered) no final da animação.");
        }
    }

    public void DrawWeapon()
    {
        if (currentWeapon == null) return;

        if (holsterCoroutine != null) StopCoroutine(holsterCoroutine);
        if (drawCoroutine != null) StopCoroutine(drawCoroutine);

        drawCoroutine = StartCoroutine(DoDrawWeapon());
    }

    private IEnumerator DoDrawWeapon()
    {
        isWeaponDrawn = true;

        // Reativa a capacidade de ataque imediatamente ao puxar a arma (apenas para armas brancas)
        if (attackScript != null)
        {
            WeaponOffset offset = currentWeapon != null ? currentWeapon.GetComponent<WeaponOffset>() : null;
            bool isMelee = offset == null || offset.weaponType != WeaponType.Gun;
            attackScript.hasWeapon = isMelee;
        }

        // Aplica o moveset específico da arma e dispara o trigger
        if (playerAnimator != null)
        {
            if (activeWeaponController != null)
            {
                playerAnimator.runtimeAnimatorController = activeWeaponController;
            }
            else
            {
                playerAnimator.runtimeAnimatorController = defaultAnimatorController;
            }
            playerAnimator.Rebind();
            playerAnimator.Update(0f);
            playerAnimator.SetTrigger("DrawWeapon");
        }

        // Espera o tempo do saque (meio da animação) antes de tornar a arma visível
        yield return new WaitForSeconds(drawDelay);

        if (currentWeapon != null && isWeaponDrawn)
        {
            currentWeapon.SetActive(true);
            Debug.Log("[Player_WeaponManager] Arma empunhada (Drawn) e visível.");
        }
    }

    public void EquipDagger(GameObject weapon) => EquipWeapon(weapon);

    public GameObject EquipWeaponPrefab(GameObject weaponPrefab)
    {
        if (weaponPrefab == null) return null;
        if (currentWeapon != null)
        {
            Destroy(currentWeapon);
            currentWeapon = null;
        }

        GameObject spawned = Instantiate(weaponPrefab);
        spawned.name = weaponPrefab.name;
        EquipWeapon(spawned);
        return spawned;
    }

    public void EquipWeapon(GameObject weapon)
    {
        if (weapon == null) return;
        Debug.Log($"[Player_WeaponManager] EquipWeapon chamado para o objeto: {weapon.name}");

        // Se já havia uma arma diferente equipada, desacopla e destrói para evitar acúmulo na mão
        if (currentWeapon != null && currentWeapon != weapon)
        {
            currentWeapon.SetActive(false);
            currentWeapon.transform.SetParent(null);
            Destroy(currentWeapon);
            currentWeapon = null;
        }

        currentWeapon = weapon;
        isWeaponDrawn = true;
        weapon.SetActive(true); // Garante que a arma fique ativa no momento do equip
        WeaponOffset offsetData = weapon.GetComponent<WeaponOffset>();
        string lowerName = weapon.name.ToLower();
        
        if (offsetData == null)
        {
            Debug.LogWarning($"[Player_WeaponManager] WeaponOffset ausente em '{weapon.name}'! Criando dinamicamente...");
            offsetData = weapon.AddComponent<WeaponOffset>();

            if (lowerName.Contains("axe") || lowerName.Contains("machado"))
            {
                offsetData.weaponType = WeaponType.Axe;
                offsetData.weaponAnimatorOverride = Resources.Load<RuntimeAnimatorController>("AxeAnimation")
                    ?? Resources.Load<RuntimeAnimatorController>("Animations/Weapons/AxeAnimation");
            }
            else if (lowerName.Contains("gun") || lowerName.Contains("pistol") || lowerName.Contains("fake"))
            {
                offsetData.weaponType = WeaponType.Gun;
                offsetData.weaponAnimatorOverride = Resources.Load<RuntimeAnimatorController>("GunAnimation")
                    ?? Resources.Load<RuntimeAnimatorController>("Animations/Weapons/GunAnimation");
            }
            else
            {
                offsetData.weaponType = WeaponType.Dagger;
                offsetData.weaponAnimatorOverride = Resources.Load<RuntimeAnimatorController>("DaggerAnimation")
                    ?? Resources.Load<RuntimeAnimatorController>("Animations/Weapons/DaggerAnimation");
            }

            offsetData.equipPosition = Vector3.zero;
            offsetData.equipRotation = Vector3.zero;
        }
        else if (offsetData.weaponAnimatorOverride == null)
        {
            if (offsetData.weaponType == WeaponType.Axe || lowerName.Contains("axe") || lowerName.Contains("machado"))
            {
                offsetData.weaponAnimatorOverride = Resources.Load<RuntimeAnimatorController>("AxeAnimation")
                    ?? Resources.Load<RuntimeAnimatorController>("Animations/Weapons/AxeAnimation");
            }
            else if (offsetData.weaponType == WeaponType.Gun || lowerName.Contains("gun") || lowerName.Contains("pistol"))
            {
                offsetData.weaponAnimatorOverride = Resources.Load<RuntimeAnimatorController>("GunAnimation")
                    ?? Resources.Load<RuntimeAnimatorController>("Animations/Weapons/GunAnimation");
            }
            else if (offsetData.weaponType == WeaponType.Dagger)
            {
                offsetData.weaponAnimatorOverride = Resources.Load<RuntimeAnimatorController>("DaggerAnimation")
                    ?? Resources.Load<RuntimeAnimatorController>("Animations/Weapons/DaggerAnimation");
            }
        }

        if (rightHand == null)
        {
            Debug.LogWarning("[Player_WeaponManager] Mão Direita (rightHand) está nula no script. Tentando encontrar osso 'RightHand' nos filhos...");
            // Procura osso nos filhos
            Transform foundHand = transform.Find("RightHand") ?? transform.Find("Hand_R") ?? transform.Find("Hand.R");
            if (foundHand != null)
            {
                rightHand = foundHand;
                Debug.Log($"[Player_WeaponManager] Mão Direita encontrada dinamicamente: {rightHand.name}");
            }
        }

        weapon.transform.SetParent(rightHand);
        weapon.transform.localPosition = offsetData.equipPosition;
        weapon.transform.localRotation = Quaternion.Euler(offsetData.equipRotation);
        Debug.Log($"[Player_WeaponManager] Objeto {weapon.name} acoplado à mão: {(rightHand != null ? rightHand.name : "null")}");

        // Salva o controller da nova arma
        if (offsetData.weaponAnimatorOverride != null)
        {
            activeWeaponController = offsetData.weaponAnimatorOverride;
        }
        else
        {
            activeWeaponController = defaultAnimatorController;
        }

        if (playerAnimator != null)
        {
            playerAnimator.runtimeAnimatorController = activeWeaponController;
            playerAnimator.Rebind();
            playerAnimator.Update(0f);
            if (attackScript != null)
            {
                attackScript.animator = playerAnimator;
            }
            PlayerM pm = GetComponent<PlayerM>() ?? GetComponentInParent<PlayerM>();
            if (pm != null)
            {
                pm.animator = playerAnimator;
            }
            Debug.Log("Moveset alterado para: " + weapon.name);
        }
        // -------------------------------------------------------

        // Procura colisor na arma ou em seus filhos
        Collider weaponCollider = weapon.GetComponent<Collider>() ?? weapon.GetComponentInChildren<Collider>();
        if (weaponCollider != null) 
        {
            weaponCollider.enabled = false; 

            // Garante que o colisor tenha o componente WeaponHitbox para aplicar dano
            WeaponHitbox hitboxScript = weaponCollider.GetComponent<WeaponHitbox>();
            if (hitboxScript == null)
            {
                hitboxScript = weaponCollider.gameObject.AddComponent<WeaponHitbox>();
                Debug.Log($"[Player_WeaponManager] WeaponHitbox adicionado dinamicamente ao colisor de '{weapon.name}'");
            }
            hitboxScript.primaryAttackScript = attackScript; // Garante referência atualizada no Player
        } 

        if (weapon.GetComponent<Rigidbody>() != null) weapon.GetComponent<Rigidbody>().isKinematic = true;

        ItemFloat floatScript = weapon.GetComponent<ItemFloat>();
        if (floatScript != null) floatScript.enabled = false;

        Dagger_Pickup daggerPickup = weapon.GetComponent<Dagger_Pickup>();
        if (daggerPickup != null) daggerPickup.enabled = false;

        DashM dashM = GetComponent<DashM>() ?? GetComponentInParent<DashM>();
        if (dashM != null)
        {
            if (offsetData.weaponType == WeaponType.Dagger)
            {
                dashM.SetBonusDashes(1); // +1 Dash passivo de base ao empunhar Adaga!
            }
            else
            {
                dashM.SetBonusDashes(0);
            }
        }

        if (attackScript != null)
        {
            if (offsetData.weaponType == WeaponType.Dagger && weaponCollider != null)
            {
                attackScript.EquipDaggerWeapon(weaponCollider);
                attackScript.hasWeapon = true;
            }
            else if (offsetData.weaponType == WeaponType.Axe && weaponCollider != null)
            {
                attackScript.EquipAxeWeapon(weaponCollider);
                attackScript.hasWeapon = true;
            }
            else if (offsetData.weaponType == WeaponType.Gun)
            {
                attackScript.hasWeapon = false; // Ataque balístico gerenciado pelo GunAttack
            }
        }
    }

    /// <summary>
    /// Desequipa a arma atualmente empunhada, destruindo-a da mão e restaurando o estado desarmado do player.
    /// </summary>
    public void UnequipCurrentWeapon()
    {
        if (currentWeapon != null)
        {
            Destroy(currentWeapon);
            currentWeapon = null;
        }

        isWeaponDrawn = false;

        if (attackScript != null)
        {
            attackScript.hasWeapon = false;
        }

        if (playerAnimator != null && defaultAnimatorController != null)
        {
            playerAnimator.runtimeAnimatorController = defaultAnimatorController;
            playerAnimator.Rebind();
            playerAnimator.Update(0f);
        }

        Debug.Log("[Player_WeaponManager] Arma desequipada com sucesso. O jogador está com as mãos livres.");
    }
}