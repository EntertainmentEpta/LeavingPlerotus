using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Sistema Visual de Blindagem Modular (Placas de Armadura) no HUD.
/// Reflete o game design oficial:
///   • Início da run (60 armadura): 3 placas modulares robustas alinhadas.
///   • Upgrades e infusões (MaxArmor aumentado): novas placas são criadas e acopladas dinamicamente à direita!
///   • Dano recebido: as placas esvaziam e trincam da direita para a esquerda.
///   • Regeneração: as placas se soldam e recarregam em sequência.
///   • Pacto do Mercador (0 armadura): placas estilhaçadas e bloqueadas.
/// </summary>
public class ModularArmorHUD : MonoBehaviour
{
    public static ModularArmorHUD Instance { get; private set; }

    [Header("Configuração de Escala")]
    [Tooltip("Pontos de armadura representados por cada placa individual (Padrão: 20 pts = 3 placas para 60 base)")]
    public int armorPerPlate = 20;

    [Tooltip("Tamanho de cada placa de armadura no HUD")]
    public Vector2 plateSize = new Vector2(24f, 32f);

    [Tooltip("Espaçamento entre as placas")]
    public float plateSpacing = 5f;

    [Header("Cores da Blindagem")]
    public Color fullArmorColor = new Color(0.0f, 0.94f, 1.0f, 1.0f);       // Ciano Luminescente (#00F0FF)
    public Color damagedArmorColor = new Color(0.1f, 0.7f, 0.9f, 0.85f);    // Ciano Aço desgastado
    public Color emptyPlateColor = new Color(0.12f, 0.16f, 0.22f, 0.5f);     // Grafite escuro vazado
    public Color plateOutlineColor = new Color(0.25f, 0.45f, 0.60f, 1.0f);   // Moldura de titânio (#407399)
    public Color newPlateHighlightColor = new Color(0.3f, 1.0f, 0.5f, 1.0f); // Brilho verde/ciano ao forjar nova placa

    private PlayerHealth playerHealth;
    private GameObject hudContainer;
    private RectTransform hudRect;
    private List<PlateVisual> activePlates = new List<PlateVisual>();

    private Sprite cachedShieldOutline;
    private Sprite cachedShieldFill;

    private class PlateVisual
    {
        public GameObject root;
        public RectTransform rect;
        public Image bgImage;
        public Image fillImage;
        public Image outlineImage;
        public Coroutine pulseRoutine;
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        playerHealth = GetComponent<PlayerHealth>() ?? Object.FindFirstObjectByType<PlayerHealth>();
        LoadSprites();
        EnsureHUDContainer();

        if (playerHealth != null)
        {
            UpdatePlates(playerHealth.CurrentArmor, playerHealth.maxArmor, playerHealth.canRegenArmor);
        }
    }

    private void LoadSprites()
    {
        // Tenta carregar os sprites oficiais de escudo
        Sprite[] shieldSprites = Resources.LoadAll<Sprite>("SHIELD");
        if (shieldSprites != null && shieldSprites.Length > 0)
        {
            cachedShieldOutline = shieldSprites[0];
        }

        Sprite[] fillSprites = Resources.LoadAll<Sprite>("FILL SHIELD");
        if (fillSprites != null && fillSprites.Length > 0)
        {
            cachedShieldFill = fillSprites[0];
        }

#if UNITY_EDITOR
        if (cachedShieldOutline == null)
        {
            cachedShieldOutline = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Player/UI/SHIELD.png");
        }
        if (cachedShieldFill == null)
        {
            cachedShieldFill = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Player/UI/FILL SHIELD.png");
        }
#endif
    }

    private void EnsureHUDContainer()
    {
        if (hudContainer != null) return;

        // Tenta ancorar no ArmorBarContainer ou HealthBar_Container existente
        GameObject parentObj = GameObject.Find("ArmorBarContainer") ?? 
                               GameObject.Find("HealthBar_Container");

        if (parentObj == null)
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null) parentObj = canvas.gameObject;
        }

        if (parentObj != null)
        {
            hudContainer = new GameObject("ModularArmorPlates_Container");
            hudContainer.transform.SetParent(parentObj.transform, false);

            hudRect = hudContainer.AddComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0f, 1f);
            hudRect.anchorMax = new Vector2(0f, 1f);
            hudRect.pivot = new Vector2(0f, 0.5f);
            hudRect.anchoredPosition = new Vector2(14f, 26f); // Fica logo acima da barra de vida/armadura
            hudRect.sizeDelta = new Vector2(300f, 40f);
        }
    }

    /// <summary>
    /// Chamado sempre que a armadura do jogador muda (dano, regen ou upgrade de MaxArmor).
    /// </summary>
    public void UpdatePlates(int currentArmor, int maxArmor, bool canRegen)
    {
        if (hudContainer == null) EnsureHUDContainer();
        if (hudContainer == null) return;

        int requiredPlates = Mathf.Max(1, Mathf.RoundToInt((float)maxArmor / Mathf.Max(1, armorPerPlate)));

        // Cria novas placas se a armadura máxima aumentou
        while (activePlates.Count < requiredPlates)
        {
            int index = activePlates.Count;
            PlateVisual newPlate = CreatePlate(index);
            activePlates.Add(newPlate);
        }

        // Desativa placas sobressalentes caso a armadura máxima diminua
        for (int i = 0; i < activePlates.Count; i++)
        {
            bool shouldBeActive = i < requiredPlates;
            if (activePlates[i].root.activeSelf != shouldBeActive)
            {
                activePlates[i].root.SetActive(shouldBeActive);
            }
        }

        if (!canRegen || maxArmor <= 0)
        {
            // Pacto do Mercador ou armadura nula: todas as placas estilhaçadas
            for (int i = 0; i < activePlates.Count; i++)
            {
                if (activePlates[i].fillImage != null)
                    activePlates[i].fillImage.fillAmount = 0f;
                if (activePlates[i].outlineImage != null)
                    activePlates[i].outlineImage.color = new Color(0.5f, 0.1f, 0.1f, 0.6f);
            }
            return;
        }

        // Distribui o valor da armadura atual entre as placas
        float armorRemaining = currentArmor;
        float pointsPerSlot = (float)maxArmor / requiredPlates;

        for (int i = 0; i < requiredPlates; i++)
        {
            PlateVisual p = activePlates[i];
            float plateHealth = Mathf.Clamp(armorRemaining - (i * pointsPerSlot), 0f, pointsPerSlot);
            float fraction = pointsPerSlot > 0f ? (plateHealth / pointsPerSlot) : 0f;

            if (p.fillImage != null)
            {
                p.fillImage.fillAmount = fraction;
                if (fraction >= 0.99f)
                {
                    p.fillImage.color = fullArmorColor;
                }
                else if (fraction > 0f)
                {
                    p.fillImage.color = damagedArmorColor;
                }
            }

            if (p.outlineImage != null)
            {
                p.outlineImage.color = fraction > 0f ? plateOutlineColor : new Color(0.2f, 0.25f, 0.3f, 0.45f);
            }
        }
    }

    /// <summary>
    /// Chamado quando MaxArmor é aumentado via infusão, melhoria ou item coletado.
    /// Cria a placa visual e toca a animação de acoplamento com feedback ao jogador.
    /// </summary>
    public void OnMaxArmorIncreased(int oldArmor, int newArmor)
    {
        if (hudContainer == null) EnsureHUDContainer();

        int oldPlates = Mathf.Max(1, Mathf.RoundToInt((float)oldArmor / Mathf.Max(1, armorPerPlate)));
        int newPlates = Mathf.Max(1, Mathf.RoundToInt((float)newArmor / Mathf.Max(1, armorPerPlate)));

        UpdatePlates(playerHealth != null ? playerHealth.CurrentArmor : newArmor, newArmor, true);

        if (newPlates > oldPlates)
        {
            int addedCount = newPlates - oldPlates;
            Debug.Log($"🛡️ [BLINDAGEM MODULAR] {addedCount} nova(s) placa(s) acoplada(s) ao chassi! Total: {newPlates} placas ({newArmor} MaxArmor)");

            // Aplica efeito punch de escala nas novas placas
            for (int i = oldPlates; i < newPlates && i < activePlates.Count; i++)
            {
                StartCoroutine(PunchPlateScale(activePlates[i]));
            }

            if (EptinhoPopupController.instancia != null)
            {
                EptinhoPopupController.instancia.MostrarPopupAviso($"BLINDAGEM MODULAR EXPANDIDA!\n+{addedCount} Placa de Armadura acoplada (+{newArmor - oldArmor} Max)!");
            }
        }
    }

    private PlateVisual CreatePlate(int index)
    {
        GameObject plateGO = new GameObject($"ModularPlate_{index}");
        plateGO.transform.SetParent(hudRect.transform, false);

        RectTransform rt = plateGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = plateSize;
        rt.anchoredPosition = new Vector2(index * (plateSize.x + plateSpacing), 0f);

        // 1. Background (soquete escuro da placa)
        GameObject bgGO = new GameObject("BG");
        bgGO.transform.SetParent(plateGO.transform, false);
        RectTransform bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = emptyPlateColor;
        bgImg.raycastTarget = false;

        // 2. Preenchimento de Energia / Cristal de Armadura
        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(plateGO.transform, false);
        RectTransform fillRT = fillGO.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.sizeDelta = new Vector2(-4f, -4f); // Margem interna
        Image fillImg = fillGO.AddComponent<Image>();
        if (cachedShieldFill != null)
        {
            fillImg.sprite = cachedShieldFill;
        }
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Vertical;
        fillImg.fillOrigin = (int)Image.OriginVertical.Bottom;
        fillImg.fillAmount = 1.0f;
        fillImg.color = fullArmorColor;
        fillImg.raycastTarget = false;

        // 3. Moldura Externa de Titânio
        GameObject outlineGO = new GameObject("Outline");
        outlineGO.transform.SetParent(plateGO.transform, false);
        RectTransform outRT = outlineGO.AddComponent<RectTransform>();
        outRT.anchorMin = Vector2.zero;
        outRT.anchorMax = Vector2.one;
        outRT.sizeDelta = Vector2.zero;
        Image outImg = outlineGO.AddComponent<Image>();
        if (cachedShieldOutline != null)
        {
            outImg.sprite = cachedShieldOutline;
        }
        outImg.type = Image.Type.Simple;
        outImg.color = plateOutlineColor;
        outImg.raycastTarget = false;

        PlateVisual pv = new PlateVisual
        {
            root = plateGO,
            rect = rt,
            bgImage = bgImg,
            fillImage = fillImg,
            outlineImage = outImg
        };

        return pv;
    }

    private IEnumerator PunchPlateScale(PlateVisual plate)
    {
        if (plate == null || plate.rect == null) yield break;

        Vector3 originalScale = Vector3.one;
        plate.rect.localScale = Vector3.one * 1.5f;

        if (plate.outlineImage != null)
        {
            plate.outlineImage.color = newPlateHighlightColor;
        }

        float elapsed = 0f;
        float duration = 0.35f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            plate.rect.localScale = Vector3.Lerp(Vector3.one * 1.5f, originalScale, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        plate.rect.localScale = originalScale;
        if (plate.outlineImage != null)
        {
            plate.outlineImage.color = plateOutlineColor;
        }
    }
}
