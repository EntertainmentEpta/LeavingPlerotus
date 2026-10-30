using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Interface de usuário do sistema de Crafting.
/// Singleton — criado programaticamente.
/// 
/// Agora inclui um painel lateral esquerdo para exibir o modelo 3D do Robô
/// em tempo real, conectado ao RobotPreviewManager.
/// </summary>
public class CraftingUI : MonoBehaviour
{
    public static CraftingUI Instance { get; private set; }

    // Referências internas
    private Canvas craftingCanvas;
    private GameObject canvasObject;
    private GameObject backgroundObject;
    private GameObject panelObject;
    private RectTransform panelRect;

    // Seção de receitas
    private Transform recipesContainer;
    private List<RecipeSlotUI> recipeSlots = new List<RecipeSlotUI>();

    // Seção de detalhes
    private TextMeshProUGUI detailNameText;
    private TextMeshProUGUI detailDescText;
    private TextMeshProUGUI ingredientsText;
    private TextMeshProUGUI resultText;
    private Button craftButton;
    private TextMeshProUGUI craftButtonText;

    // Seção de equipamentos
    private Transform equipmentContainer;
    private List<GameObject> equipmentSlotObjects = new List<GameObject>();

    // Estado
    private bool isOpen = false;
    private bool uiBuilt = false;
    private CraftingRecipe selectedRecipe;
    private int currentTab = 0; // 0 = Player/Traje, 1 = Base, 2 = Armas
    private Image playerTabImage;
    private Image baseTabImage;
    private Image weaponsTabImage;
    private ObjectiveMarker tabWeaponsMarker;
    private int currentViewTab = 0;

    private GameObject recipeListPanel;
    private GameObject detailPanel;
    private GameObject equipmentPanel;
    private readonly List<Image> viewTabImages = new List<Image>();

    public TMP_FontAsset customFont;

    // Dimensões Fixas da Mesa
    private const float PANEL_WIDTH = 1180f;
    private const float PANEL_HEIGHT = 820f;

    [Header("Preview do Robô (Layout)")]
    [Tooltip("Exibir a renderização 3D do robô ao lado do crafting?")]
    public bool showRobotPreview = true;
    [Tooltip("Posição da Mesa de Crafting (Deslocada para a direita)")]
    public Vector3 craftingPanelPosition = new Vector3(230f, 0f, 0f);
    [Tooltip("Posição do Painel do Robô (Deslocado para a esquerda)")]
    public Vector3 previewPanelPosition = new Vector3(-500f, 0f, 0f);
    [Tooltip("Tamanho do painel do Robô")]
    public Vector2 previewPanelSize = new Vector2(390f, 680f);

    private GameObject previewPanelObject;
    private RawImage previewRawImage;

    [Header("Configurações de Design")]
    [Range(30f, 200f)] [SerializeField] private float recipeSlotHeight = 140f;
    [Range(220f, 420f)] [SerializeField] private float equipmentSlotSize = 340f;
    private const float EQUIPMENT_CARD_MIN_WIDTH = 340f;
    private const float EQUIPMENT_CARD_HEIGHT = 360f;

    [SerializeField] private Color panelBg = new Color(0.055f, 0.075f, 0.095f, 0.98f);
    [SerializeField] private Color panelBorder = new Color(0.33f, 0.82f, 0.76f, 0.28f);
    [SerializeField] private Color sectionBg = new Color(0.085f, 0.11f, 0.135f, 0.98f);
    [SerializeField] private Color headerColor = new Color(0.94f, 0.96f, 0.94f, 1f);
    [SerializeField] private Color accentColor = new Color(0.32f, 0.82f, 0.75f, 1f);

    private static readonly Color BTN_CRAFT_ENABLED = new Color(0.19f, 0.68f, 0.58f, 1f);
    private static readonly Color BTN_CRAFT_DISABLED = new Color(0.16f, 0.19f, 0.22f, 1f);
    private static readonly Color BTN_CRAFT_NO_MATERIALS = new Color(0.78f, 0.18f, 0.18f, 1f);
    private static readonly Color BTN_EQUIP = new Color(0.22f, 0.52f, 0.66f, 1f);
    private static readonly Color BTN_UNEQUIP = new Color(0.68f, 0.28f, 0.27f, 1f);

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (customFont == null)
        {
            customFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF");
        }
    }

    void Start()
    {
        CreateCraftingUI();
        
        panelObject.SetActive(false);
        if (backgroundObject != null) backgroundObject.SetActive(false);
        if (previewPanelObject != null) previewPanelObject.SetActive(false);
        SetCraftingView(0);
        
        uiBuilt = true;

        // Conecta o RawImage criado ao Manager do Robô assim que o jogo começa
        if (showRobotPreview && RobotPreviewManager.Instance != null && previewRawImage != null)
        {
            RobotPreviewManager.Instance.SetupPreview(previewRawImage);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying && uiBuilt && canvasObject != null)
        {
            bool wasOpen = isOpen;
            Destroy(canvasObject);
            CreateCraftingUI();
            
            panelObject.SetActive(wasOpen);
            if (previewPanelObject != null) previewPanelObject.SetActive(wasOpen && showRobotPreview);

            // Refaz a conexão da câmera caso a UI seja reconstruída em tempo real no Editor
            if (showRobotPreview && RobotPreviewManager.Instance != null && previewRawImage != null)
            {
                RobotPreviewManager.Instance.SetupPreview(previewRawImage);
            }
            
            RefreshUI();
        }
    }
#endif

    void OnEnable()
    {
        CraftingManager.OnCraftCompleted += OnCraftCompleted;
        SaveManager.OnBaseResourcesChanged += RefreshUI;
        EquipmentManager.OnEquipmentStateChanged += RefreshEquipmentSection;
        SaveManager.OnEquipmentChanged += RefreshEquipmentSection;
    }

    void OnDisable()
    {
        CraftingManager.OnCraftCompleted -= OnCraftCompleted;
        SaveManager.OnBaseResourcesChanged -= RefreshUI;
        EquipmentManager.OnEquipmentStateChanged -= RefreshEquipmentSection;
        SaveManager.OnEquipmentChanged -= RefreshEquipmentSection;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            if (canvasObject != null) Destroy(canvasObject);
        }
    }

    void Update()
    {
        if (isOpen)
        {
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible) Cursor.visible = true;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseCrafting();
                if (RobotPreviewManager.Instance != null) RobotPreviewManager.Instance.Deactivate();
            }

            if (Input.GetKeyDown(KeyCode.T))
            {
                if (selectedRecipe != null && CraftingManager.Instance != null)
                {
                    if (CraftingManager.Instance.CanCraft(selectedRecipe))
                    {
                        OnCraftButtonClicked();
                    }
                    else
                    {
                        // Sem materiais — toca som de falha
                        if (CraftingTableInteraction.Instance != null)
                            CraftingTableInteraction.Instance.PlaySynthesizeFailSound();
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.P))
            {
                if (ItemDatabase.Instance != null && SaveManager.instance != null)
                {
                    foreach (var item in ItemDatabase.Instance.allItems)
                    {
                        if (item != null && item.returnsToBase)
                        {
                            SaveManager.instance.AddResourceToBase(item.itemId, 20);
                        }
                    }
                    RefreshUI();
                    Debug.Log("[DEBUG CHEAT] Adicionados 20 de cada recurso de base!");
                }
            }
        }
    }

    // ─── API PÚBLICA ─────────────────────────────────────────────────────────

    public void OpenCrafting()
    {
        if (isOpen) return;
        isOpen = true;
        
        UpdateCanvasCamera();
        if (canvasObject != null) canvasObject.SetActive(true);
        if (craftingCanvas != null) craftingCanvas.enabled = true;

        panelObject.SetActive(true);
        if (backgroundObject != null) backgroundObject.SetActive(true);
        if (showRobotPreview && previewPanelObject != null) previewPanelObject.SetActive(true);
        
        selectedRecipe = null;
        SetCraftingView(0);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (showRobotPreview && RobotPreviewManager.Instance != null && previewRawImage != null)
        {
            RobotPreviewManager.Instance.SetupPreview(previewRawImage);
        }

        RefreshUI();
        if (tabWeaponsMarker != null) tabWeaponsMarker.CheckVisibility();
        Debug.Log("[CRAFTING] UI aberta com sucesso");
    }

    public void CloseCrafting()
    {
        if (!isOpen) return;
        isOpen = false;
        
        panelObject.SetActive(false);
        if (backgroundObject != null) backgroundObject.SetActive(false);
        if (previewPanelObject != null) previewPanelObject.SetActive(false);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (RobotPreviewManager.Instance != null)
        {
            RobotPreviewManager.Instance.Deactivate();
        }

        Debug.Log("[CRAFTING] UI fechada");
    }

    public bool IsOpen() => gameObject.activeInHierarchy && isOpen;

    // ─── REFRESH ─────────────────────────────────────────────────────────────

    private void RefreshUI()
    {
        if (!uiBuilt || CraftingManager.Instance == null) return;

        RefreshRecipeList();
        RefreshDetails();
        RefreshEquipmentSection();
        SetCraftingView(currentViewTab);
    }

    private bool IsWeaponRecipe(CraftingRecipe r)
    {
        if (r == null) return false;
        string id = (r.recipeId ?? "").ToLower();
        string name = (r.recipeName ?? "").ToLower();
        return id.Contains("arma") || id.Contains("weapon") || id.Contains("axe") || id.Contains("dagger") ||
               name.Contains("arma") || name.Contains("adaga") || name.Contains("machado") || name.Contains("pistola");
    }

    private void RefreshRecipeList()
    {
        foreach (var slot in recipeSlots)
        {
            if (slot != null) Destroy(slot.gameObject);
        }
        recipeSlots.Clear();

        if (currentTab == 2)
        {
            // === ABA 2: ARMAS (6 ARMAS) ===
            BuildWeaponSlotsTab();
            return;
        }

        // === ABAS 0 (PLAYER) E 1 (BASE) ===
        List<CraftingRecipe> allRecipes = CraftingManager.Instance != null ? CraftingManager.Instance.GetAllRecipes() : new List<CraftingRecipe>();
        List<CraftingRecipe> recipes = new List<CraftingRecipe>();
        
        foreach (var r in allRecipes)
        {
            if (r == null) continue;
            if (IsWeaponRecipe(r)) continue; // Armas exclusivas na aba Armas!

            // Itens já craftados somem do robozinho como possibilidade de craftar
            if (IsRecipeAlreadyCrafted(r))
            {
                continue;
            }

            // Se for receita de blueprint de escritura ancestral, só exibe no Robozinho se já foi desbloqueada
            bool isInfusionUpgrade = r.resultEquipment != null && (r.resultEquipment.equipmentId.StartsWith("equip_camara_infusao") || r.resultEquipment.equipmentId.StartsWith("equip_capacidade_infusao") || r.resultEquipment.equipmentId == "equip_reservatorio_cura");
            if (r.resultEquipment != null && r.resultEquipment.equipmentId.StartsWith("equip_") && r.resultEquipment.equipmentId != "equip_detector_barreiras" && !isInfusionUpgrade)
            {
                if (LoreManager.Instance != null && !LoreManager.Instance.IsBlueprintUnlocked(r.resultEquipment.equipmentId))
                {
                    continue; // Bloqueado até o jogador encontrar a escritura no mapa!
                }
            }

            bool isBase = r.resultEquipment != null && r.resultEquipment.isBaseUpgrade;
            if (currentTab == 0 && !isBase) recipes.Add(r);
            else if (currentTab == 1 && isBase) recipes.Add(r);
        }

        foreach (var recipe in recipes)
        {
            GameObject slotObj = new GameObject("RecipeSlot_" + recipe.recipeId);
            slotObj.transform.SetParent(recipesContainer, false);

            RecipeSlotUI slot = slotObj.AddComponent<RecipeSlotUI>();
            float effSlotHeight = Mathf.Max(recipeSlotHeight, 82f);
            slot.Initialize(OnRecipeSelected, 230f, effSlotHeight);

            bool canCraft = CraftingManager.Instance != null && CraftingManager.Instance.CanCraft(recipe);
            slot.SetRecipe(recipe, canCraft);
            slot.SetSelected(selectedRecipe == recipe);

            recipeSlots.Add(slot);
        }
    }

    private void BuildWeaponSlotsTab()
    {
        // 1. ADAGA DE PLASMA (Já Forjada / Disponível no Expositor)
        CraftingRecipe daggerRecipe = ScriptableObject.CreateInstance<CraftingRecipe>();
        daggerRecipe.recipeId = "weapon_dagger";
        daggerRecipe.recipeName = "ADAGA DE PLASMA";
        daggerRecipe.description = "Armamento cortante inicial leve e veloz com acúmulo de combos rápidos. Já forjada e disponível para empunhar no Expositor de Armas.";
        CreateWeaponSlotUI(daggerRecipe, "ADAGA DE PLASMA", "★ JÁ FORJADA • NO RACK", true, false, false);

        // 2. MACHADO DE CRISTAL (Opaco / Desbloqueado para forjar agora)
        CraftingRecipe axeRecipe = CraftingManager.Instance != null ? CraftingManager.Instance.allRecipes.Find(r => r != null && (r.recipeId == "recipe_crystal_axe" || r.recipeId == "recipe_machado_cristal")) : null;
        if (axeRecipe == null)
        {
            axeRecipe = ScriptableObject.CreateInstance<CraftingRecipe>();
            axeRecipe.recipeId = "recipe_crystal_axe";
            axeRecipe.recipeName = "MACHADO DE CRISTAL";
            axeRecipe.description = "Armamento pesado de corte cristalino. Causa alto impacto, golpes em área e estilhaços devastadores.";
            axeRecipe.ingredients = new List<CraftingIngredient>
            {
                new CraftingIngredient { itemId = "shard_splinter_t1", quantity = 3 },
                new CraftingIngredient { itemId = "cristal_explosivo", quantity = 2 }
            };
            axeRecipe.resultType = CraftingResultType.Equipment;
        }
        bool axeOwned = SaveManager.instance != null && SaveManager.instance.GetAllCraftedEquipmentIds().Contains("eq_crystal_axe");
        bool canCraftAxe = CraftingManager.Instance != null && CraftingManager.Instance.CanCraft(axeRecipe);
        string axeStatus = axeOwned ? "★ JÁ FORJADA • NO RACK" : (canCraftAxe ? "Pronto para Forjar" : "Requer Materiais");
        CreateWeaponSlotUI(axeRecipe, "MACHADO DE CRISTAL", axeStatus, axeOwned, canCraftAxe, false);

        // 3. PISTOLA DE PLASMA - GUNFAKE (Opaco / Desbloqueado para forjar)
        CraftingRecipe gunRecipe = CraftingManager.Instance != null ? CraftingManager.Instance.allRecipes.Find(r => r != null && r.recipeId == "recipe_arma_projetil") : null;
        if (gunRecipe == null)
        {
            gunRecipe = ScriptableObject.CreateInstance<CraftingRecipe>();
            gunRecipe.recipeId = "recipe_arma_projetil";
            gunRecipe.recipeName = "PISTOLA DE PLASMA (GUNFAKE)";
            gunRecipe.description = "Armamento de disparos balísticos de plasma a distância com rajadas e recarga de bateria.";
            gunRecipe.ingredients = new List<CraftingIngredient>
            {
                new CraftingIngredient { itemId = "resta_geobionte", quantity = 2 },
                new CraftingIngredient { itemId = "cristal_explosivo", quantity = 5 }
            };
            gunRecipe.resultType = CraftingResultType.Equipment;
        }
        bool gunOwned = SaveManager.instance != null && SaveManager.instance.GetAllCraftedEquipmentIds().Contains("eq_arma_projetil");
        bool canCraftGun = CraftingManager.Instance != null && CraftingManager.Instance.CanCraft(gunRecipe);
        string gunStatus = gunOwned ? "★ JÁ FORJADA • NO RACK" : (canCraftGun ? "Pronto para Forjar" : "Requer Materiais");
        CreateWeaponSlotUI(gunRecipe, "PISTOLA DE PLASMA (GUNFAKE)", gunStatus, gunOwned, canCraftGun, false);

        // 4, 5, 6. TRÊS ARMAS APAGADAS / CLASSIFICADAS COM TEXTO ILEGÍVEL
        string[] classifiedTitles = {
            "█ █ █ █ █ █ █  [PROTÓTIPO #04]",
            "█ █ █ █ █ █ █  [CANHÃO CINÉTICO #05]",
            "█ █ █ █ █ █ █  [LÂMINA RESSONANTE #06]"
        };
        for (int i = 0; i < 3; i++)
        {
            int index = i + 4;
            CraftingRecipe classifiedRecipe = ScriptableObject.CreateInstance<CraftingRecipe>();
            classifiedRecipe.recipeId = $"weapon_classified_{index}";
            classifiedRecipe.recipeName = classifiedTitles[i];
            classifiedRecipe.description = "DADOS CORROMPIDOS // PROJETO CLASSIFICADO.\nOs registros de blueprints e esquemas deste armamento estão inacessíveis na bancada de dados atual.";
            CreateWeaponSlotUI(classifiedRecipe, classifiedTitles[i], "DADOS ILEGÍVEIS • REQUER NÚCLEO", false, false, true);
        }
    }

    private void CreateWeaponSlotUI(CraftingRecipe recipe, string displayName, string status, bool alreadyBuilt, bool craftable, bool faded)
    {
        GameObject slotObj = new GameObject("RecipeSlot_" + recipe.recipeId);
        slotObj.transform.SetParent(recipesContainer, false);

        RecipeSlotUI slot = slotObj.AddComponent<RecipeSlotUI>();
        float effSlotHeight = Mathf.Max(recipeSlotHeight, 82f);
        slot.Initialize(OnRecipeSelected, 230f, effSlotHeight);
        slot.SetWeaponSlot(recipe, displayName, status, alreadyBuilt, craftable, faded);
        slot.SetSelected(selectedRecipe == recipe);

        recipeSlots.Add(slot);
    }

    private void RefreshDetails()
    {
        if (selectedRecipe == null)
        {
            detailNameText.text = "Selecione uma receita";
            detailDescText.text = "";
            ingredientsText.text = "";
            resultText.text = "";
            craftButton.interactable = false;
            craftButtonText.text = "CRAFTAR";
            craftButton.GetComponent<Image>().color = BTN_CRAFT_DISABLED;
            return;
        }

        // Verifica se é uma das armas classificadas (4, 5, 6)
        if (selectedRecipe.recipeId != null && selectedRecipe.recipeId.StartsWith("weapon_classified_"))
        {
            detailNameText.text = "<color=#667788>PROJETO CLASSIFICADO [DADOS CORROMPIDOS]</color>";
            detailDescText.text = "<color=#556677>" + selectedRecipe.description + "</color>";
            ingredientsText.text = "<color=#445566><b>Ingredientes:</b>\n  • ??????? (DADOS NÃO ENCONTRADOS)</color>";
            resultText.text = "<color=#667788><b>Status:</b> Desbloqueio bloqueado</color>";
            craftButton.interactable = false;
            craftButton.GetComponent<Image>().color = BTN_CRAFT_DISABLED;
            craftButtonText.text = "PROJETO BLOQUEADO";
            return;
        }

        // Verifica se é a Adaga de Plasma ou arma já forjada
        bool isAxe = selectedRecipe.recipeId == "recipe_crystal_axe" || selectedRecipe.recipeId == "recipe_machado_cristal";
        bool isGun = selectedRecipe.recipeId == "recipe_arma_projetil";
        bool isDagger = selectedRecipe.recipeId == "weapon_dagger" || selectedRecipe.recipeId == "recipe_adaga";

        bool isAxeCrafted = isAxe && SaveManager.instance != null && SaveManager.instance.GetAllCraftedEquipmentIds().Contains("eq_crystal_axe");
        bool isGunCrafted = isGun && SaveManager.instance != null && SaveManager.instance.GetAllCraftedEquipmentIds().Contains("eq_arma_projetil");

        if (isDagger || isAxeCrafted || isGunCrafted)
        {
            detailNameText.text = selectedRecipe.recipeName;
            detailDescText.text = selectedRecipe.description;
            ingredientsText.text = "<color=#00FFAA><b>Status do Arsenal:</b>\n  ✔ Armamento já construído e forjado!</color>\n<color=#AADDFF>  Disponível no Expositor de Armas (Weapon Rack) do Lobby para empunhar com [ F ].</color>";
            resultText.text = "<b>Resultado:</b> Arma Pronta no Expositor";
            craftButton.interactable = false;
            craftButton.GetComponent<Image>().color = BTN_CRAFT_DISABLED;
            craftButtonText.text = "JÁ NO EXPOSITOR [ F ]";
            return;
        }

        detailNameText.text = selectedRecipe.recipeName;
        detailDescText.text = selectedRecipe.description;

        string ingText = "<b>Ingredientes:</b>\n";
        foreach (var ing in selectedRecipe.ingredients)
        {
            int available = CraftingManager.Instance != null ? CraftingManager.Instance.GetAvailableAmount(ing.itemId) : 0;
            string itemName = ing.itemId;

            if (ItemDatabase.Instance != null)
            {
                ItemData itemData = ItemDatabase.Instance.GetItemData(ing.itemId);
                if (itemData != null)
                    itemName = itemData.itemName;
            }

            string color = available >= ing.quantity ? "#4AE04A" : "#E04A4A";
            ingText += $"  <color={color}>{available}/{ing.quantity}</color> {itemName}\n";
        }
        ingredientsText.text = ingText;

        if (isAxe || isGun)
        {
            resultText.text = $"<b>Resultado:</b>\n  {selectedRecipe.recipeName}\n  <color=#00FFAA>Forja a arma fisicamente no Expositor do Lobby!</color>";
        }
        else
        {
            switch (selectedRecipe.resultType)
            {
                case CraftingResultType.Equipment:
                    if (selectedRecipe.resultEquipment != null)
                    {
                        var eq = selectedRecipe.resultEquipment;
                        resultText.text = $"<b>Resultado:</b>\n  {GetEquipmentDisplayName(eq)}\n  <color=#9B7FD4>{GetEffectDescription(eq)}</color>";
                    }
                    break;
                case CraftingResultType.Item:
                    string resultName = selectedRecipe.resultItemId;
                    if (ItemDatabase.Instance != null)
                    {
                        ItemData rd = ItemDatabase.Instance.GetItemData(selectedRecipe.resultItemId);
                        if (rd != null) resultName = rd.itemName;
                    }
                    resultText.text = $"<b>Resultado:</b>\n  {resultName}";
                    break;
            }
        }

        bool canCraft = CraftingManager.Instance != null && CraftingManager.Instance.CanCraft(selectedRecipe);
        craftButton.interactable = true; // Sempre clicável para permitir feedback sonoro
        craftButton.GetComponent<Image>().color = canCraft ? BTN_CRAFT_ENABLED : BTN_CRAFT_NO_MATERIALS;
        craftButtonText.text = canCraft ? "CRAFTAR [T]" : "MATERIAIS INSUFICIENTES [T]";
    }

    private void RefreshEquipmentSection()
    {
        if (!uiBuilt || EquipmentManager.Instance == null) return;

        foreach (var obj in equipmentSlotObjects)
        {
            if (obj != null) Destroy(obj);
        }
        equipmentSlotObjects.Clear();

        List<EquipmentData> owned = EquipmentManager.Instance.GetOwnedEquipment();
        owned.RemoveAll(e => e.isBaseUpgrade);

        foreach (var equip in owned)
        {
            GameObject slotObj = CreateEquipmentSlot(equip);
            slotObj.transform.SetParent(equipmentContainer, false);
            equipmentSlotObjects.Add(slotObj);

        }
    }

    // ─── CALLBACKS ───────────────────────────────────────────────────────────

    private void OnRecipeSelected(CraftingRecipe recipe)
    {
        selectedRecipe = recipe;

        foreach (var slot in recipeSlots)
        {
            slot.SetSelected(false);
        }

        if (CraftingManager.Instance != null)
        {
            var recipes = CraftingManager.Instance.GetAllRecipes();
            for (int i = 0; i < recipes.Count && i < recipeSlots.Count; i++)
            {
                if (recipes[i] == recipe)
                {
                    recipeSlots[i].SetSelected(true);
                    break;
                }
            }
        }

        RefreshDetails();
        SetCraftingView(1);
    }

    private void OnCraftButtonClicked()
    {
        if (selectedRecipe == null || CraftingManager.Instance == null) return;

        // Se não pode craftar, toca som de falha e retorna
        if (!CraftingManager.Instance.CanCraft(selectedRecipe))
        {
            Debug.LogWarning($"[CRAFTING UI] Materiais insuficientes para: {selectedRecipe.recipeName}");
            if (CraftingTableInteraction.Instance != null)
                CraftingTableInteraction.Instance.PlaySynthesizeFailSound();
            return;
        }

        if (CraftingManager.Instance.Craft(selectedRecipe))
        {
            Debug.Log($"[CRAFTING UI] Craft realizado: {selectedRecipe.recipeName}");
            if (CraftingTableInteraction.Instance != null)
                CraftingTableInteraction.Instance.PlaySynthesizeSuccessSound();
        }
        else
        {
            Debug.LogWarning($"[CRAFTING UI] Falha ao sintetizar: {selectedRecipe.recipeName}");
            if (CraftingTableInteraction.Instance != null)
                CraftingTableInteraction.Instance.PlaySynthesizeFailSound();
        }
    }

    private void OnCraftCompleted(CraftingRecipe recipe)
    {
        if (selectedRecipe == recipe && IsRecipeAlreadyCrafted(recipe))
        {
            selectedRecipe = null;
        }

        RefreshUI();

        if (recipe != null && IsWeaponRecipe(recipe))
        {
            TutorialManager.Instance?.CompleteStep(TutorialStep.ForjarArma);
        }
    }

    private bool IsRecipeAlreadyCrafted(CraftingRecipe r)
    {
        if (r == null || SaveManager.instance == null) return false;

        string equipId = r.resultEquipment != null ? r.resultEquipment.equipmentId : null;
        if (string.IsNullOrEmpty(equipId))
        {
            equipId = r.recipeId.StartsWith("recipe_") ? r.recipeId.Replace("recipe_", "equip_") : r.recipeId;
        }

        if (!string.IsNullOrEmpty(equipId))
        {
            if (SaveManager.instance.GetAllCraftedEquipmentIds().Contains(equipId) || 
                SaveManager.instance.GetCraftedEquipmentCount(equipId) > 0)
            {
                return true;
            }
        }

        if (r.recipeId == "recipe_crystal_axe" || r.recipeId == "recipe_machado_cristal")
        {
            if (SaveManager.instance.GetAllCraftedEquipmentIds().Contains("eq_crystal_axe")) return true;
        }
        if (r.recipeId == "recipe_arma_projetil")
        {
            if (SaveManager.instance.GetAllCraftedEquipmentIds().Contains("eq_arma_projetil")) return true;
        }

        return false;
    }

    // ─── HELPERS ─────────────────────────────────────────────────────────────

    private string GetEffectDescription(EquipmentData equip)
    {
        switch (equip.effectType)
        {
            case EquipmentEffectType.InventorySlotExpansion:
                return $"+{equip.effectValue:0} Slots de Inventário";
            case EquipmentEffectType.MaxHealthBoost:
                return $"+{equip.effectValue:0} Vida Máxima";
            case EquipmentEffectType.MaxArmorBoost:
                return $"+{equip.effectValue:0} Armadura Máxima";
            case EquipmentEffectType.SpeedBoost:
                return $"×{equip.effectValue:F1} Velocidade";
            case EquipmentEffectType.DamageBoost:
                return $"×{equip.effectValue:F1} Dano Base";
            case EquipmentEffectType.CritChanceBoost:
                return $"+{equip.effectValue:0}% Chance Crítico";
            case EquipmentEffectType.ArmorRegenBoost:
                return $"+{equip.effectValue:F1} Regen. Armadura";
            default:
                return equip.effectValue.ToString();
        }
    }

    private string GetEquipmentDisplayName(EquipmentData equip)
    {
        if (equip == null || string.IsNullOrWhiteSpace(equip.equipmentName))
            return "noname";

        return equip.equipmentName;
    }

    // ─── CRIAÇÃO DA UI ───────────────────────────────────────────────────────

    public void UpdateCanvasCamera()
    {
        if (craftingCanvas == null) return;

        craftingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        craftingCanvas.sortingOrder = 500;

        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer < 0) uiLayer = 5;
        if (canvasObject != null)
        {
            SetLayerRecursively(canvasObject, uiLayer);
        }
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            if (child != null)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }

    private void CreateCraftingUI()
    {
        // Garante EventSystem para interação de mouse e botões na UI
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            DontDestroyOnLoad(esObj);
        }

        canvasObject = new GameObject("CraftingUI_Canvas");
        craftingCanvas = canvasObject.AddComponent<Canvas>();
        UpdateCanvasCamera();

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasObject);

        CreateCraftingBackground();

        // Painel principal da Mesa
        panelObject = new GameObject("CraftingPanel");
        panelObject.transform.SetParent(craftingCanvas.transform, false);

        panelRect = panelObject.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(PANEL_WIDTH, PANEL_HEIGHT);
        panelRect.anchoredPosition3D = craftingPanelPosition; // <--- Deslocado para a direita

        Image mainPanelBg = panelObject.AddComponent<Image>();
        mainPanelBg.color = panelBg;
        mainPanelBg.raycastTarget = false;

        Shadow panelShadow = panelObject.AddComponent<Shadow>();
        panelShadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        panelShadow.effectDistance = new Vector2(6f, -6f);

        GameObject glassHighlightObj = new GameObject("GlassHighlight");
        glassHighlightObj.transform.SetParent(panelObject.transform, false);
        RectTransform glassHighlightRect = glassHighlightObj.AddComponent<RectTransform>();
        glassHighlightRect.anchorMin = new Vector2(0f, 1f);
        glassHighlightRect.anchorMax = new Vector2(1f, 1f);
        glassHighlightRect.pivot = new Vector2(0.5f, 1f);
        glassHighlightRect.anchoredPosition = new Vector2(0f, -2f);
        glassHighlightRect.sizeDelta = new Vector2(-4f, 2f);
        glassHighlightObj.AddComponent<CanvasRenderer>();
        Image glassHighlightImg = glassHighlightObj.AddComponent<Image>();
        glassHighlightImg.color = new Color(1f, 1f, 1f, 0.12f);
        glassHighlightImg.raycastTarget = false;

        CreateBorder(panelObject.transform);
        CreateTopAccent(panelObject.transform);
        CreateHeader(panelObject.transform);
        CreateViewTabs(panelObject.transform);
        CreateRecipeListSection(panelObject.transform);
        CreateDetailSection(panelObject.transform);
        CreateEquipmentSection(panelObject.transform);
        CreateCloseButton(panelObject.transform);
        // CreateCheatButton(panelObject.transform); // Removido a pedido do usuario

        // === CRIA O PAINEL DO ROBÔ ===
        if (showRobotPreview)
        {
            CreateRobotPreviewPanel();
        }

        UpdateCanvasCamera();
        Debug.Log("[CRAFTING] UI criada.");
    }

    private void CreateViewTabs(Transform parent)
    {
        GameObject tabsObject = new GameObject("CraftingViewTabs");
        tabsObject.transform.SetParent(parent, false);

        RectTransform tabsRect = tabsObject.AddComponent<RectTransform>();
        tabsRect.anchorMin = new Vector2(0f, 1f);
        tabsRect.anchorMax = new Vector2(1f, 1f);
        tabsRect.pivot = new Vector2(0.5f, 1f);
        tabsRect.anchoredPosition = new Vector2(0f, -48f);
        tabsRect.sizeDelta = new Vector2(-32f, 48f);

        string[] labels = { "SELECIONAR CRAFT", "DETALHES DO CRAFT", "MELHORIAS CRAFTADAS" };
        for (int index = 0; index < labels.Length; index++)
        {
            GameObject tabObject = new GameObject(labels[index]);
            tabObject.transform.SetParent(tabsObject.transform, false);

            RectTransform tabRect = tabObject.AddComponent<RectTransform>();
            tabRect.anchorMin = new Vector2(index / 3f, 0f);
            tabRect.anchorMax = new Vector2((index + 1) / 3f, 1f);
            tabRect.offsetMin = new Vector2(2f, 0f);
            tabRect.offsetMax = new Vector2(-2f, 0f);

            Image tabImage = tabObject.AddComponent<Image>();
            tabImage.color = sectionBg;
            viewTabImages.Add(tabImage);

            Button tabButton = tabObject.AddComponent<Button>();
            int capturedIndex = index;
            tabButton.onClick.AddListener(() => SetCraftingView(capturedIndex));

            TextMeshProUGUI label = CreateTextElement(tabObject.transform, "Label",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, 22f, FontStyles.Bold, headerColor);
            label.text = labels[index];
            label.alignment = TextAlignmentOptions.Center;
        }
    }

    private void SetCraftingView(int viewIndex)
    {
        currentViewTab = Mathf.Clamp(viewIndex, 0, 2);

        if (recipeListPanel != null) recipeListPanel.SetActive(currentViewTab == 0);
        if (detailPanel != null) detailPanel.SetActive(currentViewTab == 1);
        if (equipmentPanel != null) equipmentPanel.SetActive(currentViewTab == 2);

        for (int index = 0; index < viewTabImages.Count; index++)
        {
            viewTabImages[index].color = index == currentViewTab
                ? new Color(0.18f, 0.55f, 0.52f, 1f)
                : new Color(0.10f, 0.14f, 0.17f, 1f);
        }
    }

    private void CreateCraftingBackground()
    {
        backgroundObject = new GameObject("CraftingBackgroundImage");
        backgroundObject.transform.SetParent(canvasObject.transform, false);

        RectTransform backgroundRect = backgroundObject.AddComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        Image backgroundImage = backgroundObject.AddComponent<Image>();
        backgroundImage.color = new Color(0.02f, 0.03f, 0.05f, 0.85f);
        backgroundImage.raycastTarget = false;
        backgroundObject.transform.SetSiblingIndex(0);
    }

    private void CreateRobotPreviewPanel()
    {
        previewPanelObject = new GameObject("RobotPreviewPanel");
        previewPanelObject.transform.SetParent(craftingCanvas.transform, false);

        RectTransform r = previewPanelObject.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0.5f, 0.5f);
        r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition3D = previewPanelPosition;
        r.sizeDelta = previewPanelSize;

        previewPanelObject.AddComponent<CanvasRenderer>();
        Image bg = previewPanelObject.AddComponent<Image>();
        bg.color = panelBg;
        bg.raycastTarget = false;

        Shadow shadow = previewPanelObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        shadow.effectDistance = new Vector2(6f, -6f);

        GameObject highlightObj = new GameObject("GlassHighlight");
        highlightObj.transform.SetParent(previewPanelObject.transform, false);
        RectTransform hr = highlightObj.AddComponent<RectTransform>();
        hr.anchorMin = new Vector2(0f, 1f);
        hr.anchorMax = new Vector2(1f, 1f);
        hr.pivot = new Vector2(0.5f, 1f);
        hr.anchoredPosition = new Vector2(0f, -2f);
        hr.sizeDelta = new Vector2(-4f, 2f);
        highlightObj.AddComponent<CanvasRenderer>();
        Image hImg = highlightObj.AddComponent<Image>();
        hImg.color = new Color(1f, 1f, 1f, 0.12f);
        hImg.raycastTarget = false;

        GameObject borderObj = new GameObject("Border");
        borderObj.transform.SetParent(previewPanelObject.transform, false);
        RectTransform br = borderObj.AddComponent<RectTransform>();
        br.anchorMin = Vector2.zero;
        br.anchorMax = Vector2.one;
        br.sizeDelta = new Vector2(4f, 4f);
        borderObj.AddComponent<CanvasRenderer>();
        Image bImg = borderObj.AddComponent<Image>();
        bImg.color = panelBorder;
        bImg.type = Image.Type.Sliced;
        bImg.fillCenter = false;
        bImg.raycastTarget = false;

        GameObject rawImgObj = new GameObject("RawImage");
        rawImgObj.transform.SetParent(previewPanelObject.transform, false);
        RectTransform rr = rawImgObj.AddComponent<RectTransform>();
        rr.anchorMin = Vector2.zero;
        rr.anchorMax = Vector2.one;
        rr.sizeDelta = new Vector2(-20f, -20f); // Margem interna
        rawImgObj.AddComponent<CanvasRenderer>();
        
        previewRawImage = rawImgObj.AddComponent<RawImage>();
        previewRawImage.color = Color.white;
        previewRawImage.raycastTarget = false;
    }

    private void CreateBorder(Transform parent)
    {
        GameObject borderObj = new GameObject("PanelBorder");
        borderObj.transform.SetParent(parent, false);
        RectTransform r = borderObj.AddComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.sizeDelta = new Vector2(4f, 4f);
        borderObj.AddComponent<CanvasRenderer>();
        Image img = borderObj.AddComponent<Image>();
        img.color = panelBorder;
        img.type = Image.Type.Sliced;
        img.fillCenter = false;
        img.raycastTarget = false;
    }

    private void CreateTopAccent(Transform parent)
    {
        GameObject obj = new GameObject("TopAccent");
        obj.transform.SetParent(parent, false);
        RectTransform r = obj.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0.05f, 1f);
        r.anchorMax = new Vector2(0.95f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.sizeDelta = new Vector2(0f, 3f);
        obj.AddComponent<CanvasRenderer>();
        Image img = obj.AddComponent<Image>();
        img.color = accentColor;
        img.raycastTarget = false;
    }

    private void CreateHeader(Transform parent)
    {
        GameObject obj = new GameObject("Header");
        obj.transform.SetParent(parent, false);
        RectTransform r = obj.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 1f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = new Vector2(0f, -6f);
        r.sizeDelta = new Vector2(-40f, 40f);
        obj.AddComponent<CanvasRenderer>();
        TextMeshProUGUI txt = obj.AddComponent<TextMeshProUGUI>();
        txt.text = "MESA DE TRABALHO";
        txt.fontSize = 24f; 
        txt.fontStyle = FontStyles.Bold;
        txt.color = headerColor;
        txt.alignment = TextAlignmentOptions.Center;
        txt.raycastTarget = false;
        if (customFont != null) txt.font = customFont;
    }

    private void CreateCheatButton(Transform parent)
    {
        GameObject btnObj = new GameObject("CheatButton");
        btnObj.transform.SetParent(parent, false);

        RectTransform r = btnObj.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 1f);
        r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(16f, -10f);
        r.sizeDelta = new Vector2(180f, 30f);

        btnObj.AddComponent<CanvasRenderer>();
        Image bg = btnObj.AddComponent<Image>();
        bg.color = new Color(0.18f, 0.58f, 0.48f, 0.95f);

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() =>
        {
            if (DevCheatConsole.Instance != null)
            {
                DevCheatConsole.Instance.GiveAllResources(999);
            }
            else
            {
                if (SaveManager.instance != null && ItemDatabase.Instance != null)
                {
                    foreach (var item in ItemDatabase.Instance.allItems)
                    {
                        if (item != null) SaveManager.instance.AddResourceToBase(item.itemId, 999);
                    }
                }
            }
            RefreshUI();
        });

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform tr = txtObj.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.sizeDelta = Vector2.zero;

        txtObj.AddComponent<CanvasRenderer>();
        TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
        txt.text = "+999 RECURSOS";
        txt.fontSize = 10f;
        txt.fontStyle = FontStyles.Bold;
        txt.color = Color.white;
        txt.alignment = TextAlignmentOptions.Center;
    }

    private void CreateRecipeListSection(Transform parent)
    {
        recipeListPanel = new GameObject("RecipeListPanel");
        GameObject listPanel = recipeListPanel;
        listPanel.transform.SetParent(parent, false);
        RectTransform r = listPanel.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 0.08f);
        r.anchorMax = new Vector2(1f, 0.88f);
        r.offsetMin = new Vector2(12f, 0f);
        r.offsetMax = new Vector2(-12f, 0f);

        listPanel.AddComponent<CanvasRenderer>();
        Image bg = listPanel.AddComponent<Image>();
        bg.color = sectionBg;
        bg.raycastTarget = true;

        // Aba 0: MELHORIAS NO PLAYER
        GameObject tabPlayer = new GameObject("TabPlayer");
        tabPlayer.transform.SetParent(listPanel.transform, false);
        RectTransform rtPlayer = tabPlayer.AddComponent<RectTransform>();
        rtPlayer.anchorMin = new Vector2(0f, 1f);
        rtPlayer.anchorMax = new Vector2(0.333f, 1f);
        rtPlayer.pivot = new Vector2(0.5f, 1f);
        rtPlayer.sizeDelta = new Vector2(0f, 42f);
        rtPlayer.anchoredPosition = new Vector2(0f, 0f);
        Image imgPlayer = tabPlayer.AddComponent<Image>();
        playerTabImage = imgPlayer;
        Button btnPlayer = tabPlayer.AddComponent<Button>();
        btnPlayer.onClick.AddListener(() => { currentTab = 0; RefreshRecipeList(); UpdateRecipeCategoryTabs(); });
        
        GameObject txtPlayer = new GameObject("Text");
        txtPlayer.transform.SetParent(tabPlayer.transform, false);
        TextMeshProUGUI labelPlayer = txtPlayer.AddComponent<TextMeshProUGUI>();
        labelPlayer.text = "PLAYER";
        labelPlayer.fontSize = 24f;
        labelPlayer.fontStyle = FontStyles.Bold;
        labelPlayer.color = accentColor;
        labelPlayer.alignment = TextAlignmentOptions.Center;
        if (customFont != null) labelPlayer.font = customFont;
        RectTransform rtTxt1 = txtPlayer.GetComponent<RectTransform>();
        rtTxt1.anchorMin = Vector2.zero; rtTxt1.anchorMax = Vector2.one; rtTxt1.sizeDelta = Vector2.zero;

        // Aba 1: MELHORIAS NA BASE
        GameObject tabBase2 = new GameObject("TabBase");
        tabBase2.transform.SetParent(listPanel.transform, false);
        RectTransform rtBase = tabBase2.AddComponent<RectTransform>();
        rtBase.anchorMin = new Vector2(0.333f, 1f);
        rtBase.anchorMax = new Vector2(0.666f, 1f);
        rtBase.pivot = new Vector2(0.5f, 1f);
        rtBase.sizeDelta = new Vector2(0f, 42f);
        rtBase.anchoredPosition = new Vector2(0f, 0f);
        Image imgBase = tabBase2.AddComponent<Image>();
        baseTabImage = imgBase;
        Button btnBase = tabBase2.AddComponent<Button>();
        btnBase.onClick.AddListener(() => { currentTab = 1; RefreshRecipeList(); UpdateRecipeCategoryTabs(); });
        
        GameObject txtBase = new GameObject("Text");
        txtBase.transform.SetParent(tabBase2.transform, false);
        TextMeshProUGUI labelBase = txtBase.AddComponent<TextMeshProUGUI>();
        labelBase.text = "BASE";
        labelBase.fontSize = 24f;
        labelBase.fontStyle = FontStyles.Bold;
        labelBase.color = accentColor;
        labelBase.alignment = TextAlignmentOptions.Center;
        if (customFont != null) labelBase.font = customFont;
        RectTransform rtTxt2 = txtBase.GetComponent<RectTransform>();
        rtTxt2.anchorMin = Vector2.zero; rtTxt2.anchorMax = Vector2.one; rtTxt2.sizeDelta = Vector2.zero;

        // Aba 2: ARMAS
        GameObject tabWeapons = new GameObject("TabWeapons");
        tabWeapons.transform.SetParent(listPanel.transform, false);
        RectTransform rtWeapons = tabWeapons.AddComponent<RectTransform>();
        rtWeapons.anchorMin = new Vector2(0.666f, 1f);
        rtWeapons.anchorMax = new Vector2(1f, 1f);
        rtWeapons.pivot = new Vector2(0.5f, 1f);
        rtWeapons.sizeDelta = new Vector2(0f, 42f);
        rtWeapons.anchoredPosition = new Vector2(0f, 0f);
        Image imgWeapons = tabWeapons.AddComponent<Image>();
        weaponsTabImage = imgWeapons;
        Button btnWeapons = tabWeapons.AddComponent<Button>();
        btnWeapons.onClick.AddListener(() => { 
            currentTab = 2; 
            RefreshRecipeList(); 
            UpdateRecipeCategoryTabs(); 
            TutorialManager.Instance?.CompleteStep(TutorialStep.AbrirAbaArmas);
            if (tabWeaponsMarker != null) tabWeaponsMarker.CheckVisibility();

            // Como o Machado de Cristal já está liberado de graça, completa o passo de forjar e direciona para equipar no Rack
            if (TutorialManager.Instance != null && TutorialManager.Instance.currentStep == TutorialStep.ForjarArma)
            {
                bool hasAxe = SaveManager.instance != null && SaveManager.instance.GetAllCraftedEquipmentIds().Contains("eq_crystal_axe");
                if (hasAxe)
                {
                    TutorialManager.Instance.CompleteStep(TutorialStep.ForjarArma);
                }
            }
        });

        tabWeaponsMarker = tabWeapons.AddComponent<ObjectiveMarker>();
        tabWeaponsMarker.condition = ObjectiveMarker.TriggerCondition.ByTutorialStep;
        tabWeaponsMarker.activeOnStep = TutorialStep.AbrirAbaArmas;
        tabWeaponsMarker.mode = ObjectiveMarker.MarkerMode.CanvasUI;
        tabWeaponsMarker.markerSize = new Vector2(24f, 24f);
        
        GameObject txtWeapons = new GameObject("Text");
        txtWeapons.transform.SetParent(tabWeapons.transform, false);
        TextMeshProUGUI labelWeapons = txtWeapons.AddComponent<TextMeshProUGUI>();
        labelWeapons.text = "ARMAS";
        labelWeapons.fontSize = 24f;
        labelWeapons.fontStyle = FontStyles.Bold;
        labelWeapons.color = accentColor;
        labelWeapons.alignment = TextAlignmentOptions.Center;
        if (customFont != null) labelWeapons.font = customFont;
        RectTransform rtTxt3 = txtWeapons.GetComponent<RectTransform>();
        rtTxt3.anchorMin = Vector2.zero; rtTxt3.anchorMax = Vector2.one; rtTxt3.sizeDelta = Vector2.zero;

        GameObject scrollObj = new GameObject("RecipeScroll");
        scrollObj.transform.SetParent(listPanel.transform, false);
        RectTransform sr = scrollObj.AddComponent<RectTransform>();
        sr.anchorMin = new Vector2(0f, 0f);
        sr.anchorMax = new Vector2(1f, 1f);
        sr.offsetMin = new Vector2(4f, 4f);
        sr.offsetMax = new Vector2(-34f, -46f);

        scrollObj.AddComponent<CanvasRenderer>();
        Image scrollMask = scrollObj.AddComponent<Image>();
        scrollMask.color = new Color(0, 0, 0, 0.01f);
        scrollObj.AddComponent<Mask>().showMaskGraphic = false;

        GameObject contentObj = new GameObject("RecipeContent");
        contentObj.transform.SetParent(scrollObj.transform, false);
        RectTransform cr = contentObj.AddComponent<RectTransform>();
        cr.anchorMin = new Vector2(0f, 1f);
        cr.anchorMax = new Vector2(1f, 1f);
        cr.pivot = new Vector2(0.5f, 1f);
        cr.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4f;
        vlg.padding = new RectOffset(2, 2, 2, 2);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;

        ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.content = cr;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 20f;

        Scrollbar recipeScrollbar = CreateVerticalScrollbar(listPanel.transform, "RecipeScrollbar");
        scroll.verticalScrollbar = recipeScrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        recipesContainer = contentObj.transform;
        UpdateRecipeCategoryTabs();
    }

    private void UpdateRecipeCategoryTabs()
    {
        Color activeColor = new Color(0.18f, 0.55f, 0.52f, 1f);
        Color inactColor = new Color(0.10f, 0.14f, 0.17f, 1f);

        if (playerTabImage != null)
        {
            playerTabImage.color = currentTab == 0 ? activeColor : inactColor;
        }

        if (baseTabImage != null)
        {
            baseTabImage.color = currentTab == 1 ? activeColor : inactColor;
        }

        if (weaponsTabImage != null)
        {
            weaponsTabImage.color = currentTab == 2 ? activeColor : inactColor;
        }

        if (tabWeaponsMarker != null)
        {
            tabWeaponsMarker.CheckVisibility();
        }
    }

    private Scrollbar CreateVerticalScrollbar(Transform parent, string objectName)
    {
        GameObject scrollbarObject = new GameObject(objectName);
        scrollbarObject.transform.SetParent(parent, false);

        RectTransform scrollbarRect = scrollbarObject.AddComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.anchoredPosition = new Vector2(-6f, -13f);
        scrollbarRect.sizeDelta = new Vector2(12f, -42f);

        Image trackImage = scrollbarObject.AddComponent<Image>();
        trackImage.color = new Color(0.04f, 0.07f, 0.08f, 0.9f);
        trackImage.raycastTarget = true;

        GameObject handleObject = new GameObject("Handle");
        handleObject.transform.SetParent(scrollbarObject.transform, false);
        RectTransform handleRect = handleObject.AddComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = new Vector2(2f, 2f);
        handleRect.offsetMax = new Vector2(-2f, -2f);

        Image handleImage = handleObject.AddComponent<Image>();
        handleImage.color = accentColor;
        handleImage.raycastTarget = true;

        Scrollbar scrollbar = scrollbarObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;
        return scrollbar;
    }

    private void CreateDetailSection(Transform parent)
    {
        detailPanel = new GameObject("DetailPanel");
        detailPanel.transform.SetParent(parent, false);
        RectTransform r = detailPanel.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 0.08f);
        r.anchorMax = new Vector2(1f, 0.88f);
        r.offsetMin = new Vector2(12f, 0f);
        r.offsetMax = new Vector2(-12f, 0f);

        detailPanel.AddComponent<CanvasRenderer>();
        Image bg = detailPanel.AddComponent<Image>();
        bg.color = sectionBg;
        bg.raycastTarget = false;

        detailNameText = CreateTextElement(detailPanel.transform, "DetailName",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -15f), new Vector2(-16f, 46f), 30f, FontStyles.Bold, headerColor);

        detailDescText = CreateTextElement(detailPanel.transform, "DetailDesc",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -68f), new Vector2(-16f, 60f), 22f, FontStyles.Italic,
            new Color(0.61f, 0.70f, 0.72f, 1f));

        ingredientsText = CreateTextElement(detailPanel.transform, "Ingredients",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -140f), new Vector2(-16f, 220f), 24f, FontStyles.Normal,
            new Color(0.86f, 0.89f, 0.87f, 1f));
        ingredientsText.richText = true;

        resultText = CreateTextElement(detailPanel.transform, "Result",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -385f), new Vector2(-16f, 110f), 24f, FontStyles.Normal,
            new Color(0.86f, 0.89f, 0.87f, 1f));
        resultText.richText = true;

        GameObject btnObj = new GameObject("CraftButton");
        btnObj.transform.SetParent(detailPanel.transform, false);
        RectTransform br = btnObj.AddComponent<RectTransform>();
        br.anchorMin = new Vector2(0.1f, 0f);
        br.anchorMax = new Vector2(0.9f, 0f);
        br.pivot = new Vector2(0.5f, 0f);
        br.anchoredPosition = new Vector2(0f, 16f);
        br.sizeDelta = new Vector2(0f, 50f);

        btnObj.AddComponent<CanvasRenderer>();
        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = BTN_CRAFT_DISABLED;
        btnBg.raycastTarget = true;

        craftButton = btnObj.AddComponent<Button>();
        craftButton.onClick.AddListener(OnCraftButtonClicked);
        craftButton.interactable = false;

        GameObject btnTextObj = new GameObject("CraftBtnText");
        btnTextObj.transform.SetParent(btnObj.transform, false);
        RectTransform btr = btnTextObj.AddComponent<RectTransform>();
        btr.anchorMin = Vector2.zero;
        btr.anchorMax = Vector2.one;
        btr.sizeDelta = Vector2.zero;
        btnTextObj.AddComponent<CanvasRenderer>();
        craftButtonText = btnTextObj.AddComponent<TextMeshProUGUI>();
        craftButtonText.text = "CRAFTAR";
        craftButtonText.fontSize = 26f; 
        craftButtonText.fontStyle = FontStyles.Bold;
        craftButtonText.color = Color.white;
        craftButtonText.alignment = TextAlignmentOptions.Center;
        craftButtonText.raycastTarget = false;
        if (customFont != null) craftButtonText.font = customFont;

        detailNameText.text = "Selecione uma receita";
        detailDescText.text = "";
        ingredientsText.text = "";
        resultText.text = "";
    }

    private void CreateEquipmentSection(Transform parent)
    {
        equipmentPanel = new GameObject("EquipmentPanel");
        GameObject equipPanel = equipmentPanel;
        equipPanel.transform.SetParent(parent, false);
        RectTransform r = equipPanel.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 0.08f);
        r.anchorMax = new Vector2(1f, 0.88f);
        r.offsetMin = new Vector2(12f, 8f);
        r.offsetMax = new Vector2(-12f, 0f);

        equipPanel.AddComponent<CanvasRenderer>();
        Image bg = equipPanel.AddComponent<Image>();
        bg.color = sectionBg;
        bg.raycastTarget = false;

        GameObject labelObj = new GameObject("EquipmentLabel");
        labelObj.transform.SetParent(equipPanel.transform, false);
        RectTransform lr = labelObj.AddComponent<RectTransform>();
        lr.anchorMin = new Vector2(0f, 1f);
        lr.anchorMax = new Vector2(1f, 1f);
        lr.pivot = new Vector2(0.5f, 1f);
        lr.sizeDelta = new Vector2(0f, 20f);
        lr.anchoredPosition = new Vector2(0f, -2f);
        labelObj.AddComponent<CanvasRenderer>();
        TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
        label.text = "MELHORIAS CRAFTADAS";
        label.fontSize = 22f;
        label.fontStyle = FontStyles.Bold;
        label.color = accentColor;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        if (customFont != null) label.font = customFont;

        GameObject scrollObj = new GameObject("EquipScroll");
        scrollObj.transform.SetParent(equipPanel.transform, false);
        RectTransform sr = scrollObj.AddComponent<RectTransform>();
        sr.anchorMin = new Vector2(0f, 0f);
        sr.anchorMax = new Vector2(1f, 1f);
        sr.offsetMin = new Vector2(4f, 4f);
        sr.offsetMax = new Vector2(-4f, -34f);

        scrollObj.AddComponent<CanvasRenderer>();
        Image scrollMask = scrollObj.AddComponent<Image>();
        scrollMask.color = new Color(0, 0, 0, 0.01f);
        scrollObj.AddComponent<Mask>().showMaskGraphic = false;

        GameObject contentObj = new GameObject("EquipContent");
        contentObj.transform.SetParent(scrollObj.transform, false);
        RectTransform cr = contentObj.AddComponent<RectTransform>();
        cr.anchorMin = new Vector2(0f, 1f);
        cr.anchorMax = new Vector2(1f, 1f);
        cr.pivot = new Vector2(0.5f, 1f);
        cr.sizeDelta = new Vector2(0f, 0f);

        GridLayoutGroup glg = contentObj.AddComponent<GridLayoutGroup>();
        float cardWidth = Mathf.Max(equipmentSlotSize, EQUIPMENT_CARD_MIN_WIDTH);
        glg.cellSize = new Vector2(cardWidth, EQUIPMENT_CARD_HEIGHT);
        glg.spacing = new Vector2(16f, 16f);
        glg.padding = new RectOffset(14, 14, 14, 14);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 3;
        glg.childAlignment = TextAnchor.UpperLeft;

        ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.content = cr;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        Scrollbar equipmentScrollbar = CreateVerticalScrollbar(equipPanel.transform, "EquipmentScrollbar");
        scroll.verticalScrollbar = equipmentScrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        equipmentContainer = contentObj.transform;
    }

    private GameObject CreateEquipmentSlot(EquipmentData equip)
    {
        bool isEquipped = EquipmentManager.Instance.IsEquipped(equip.equipmentId);
        string displayName = GetEquipmentDisplayName(equip);

        GameObject slotObj = new GameObject("EquipSlot_" + equip.equipmentId);
        RectTransform r = slotObj.AddComponent<RectTransform>();
        r.sizeDelta = new Vector2(Mathf.Max(equipmentSlotSize, EQUIPMENT_CARD_MIN_WIDTH), EQUIPMENT_CARD_HEIGHT);

        slotObj.AddComponent<CanvasRenderer>();
        Image bg = slotObj.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);
        bg.raycastTarget = true; 

        GameObject borderObj = new GameObject("Border");
        borderObj.transform.SetParent(slotObj.transform, false);
        RectTransform borderRect = borderObj.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = Vector2.zero;
        borderObj.AddComponent<CanvasRenderer>();
        Image borderImg = borderObj.AddComponent<Image>();
        borderImg.color = new Color(1f, 1f, 1f, 0.08f);
        borderImg.type = Image.Type.Sliced;
        borderImg.fillCenter = false;
        borderImg.raycastTarget = false;

        float nameOffsetLeft = 6f;

        if (equip.icon != null)
        {
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(slotObj.transform, false);
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.34f, 0.66f);
            iconRect.anchorMax = new Vector2(0.66f, 0.94f);
            iconRect.sizeDelta = Vector2.zero;
            
            iconObj.AddComponent<CanvasRenderer>();
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = equip.icon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

        }

        GameObject nameObj = new GameObject("Name");
        nameObj.transform.SetParent(slotObj.transform, false);
        RectTransform nr = nameObj.AddComponent<RectTransform>();
        nr.anchorMin = new Vector2(0.05f, 0.50f);
        nr.anchorMax = new Vector2(0.95f, 0.68f);
        nr.sizeDelta = Vector2.zero;
        nr.offsetMin = Vector2.zero;
        nr.offsetMax = Vector2.zero;
        nameObj.AddComponent<CanvasRenderer>();
        TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.text = displayName;
        nameText.fontSize = 23f; 
        nameText.fontStyle = FontStyles.Bold;
        nameText.color = new Color(0.9f, 0.88f, 0.95f);
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.raycastTarget = false;
        nameText.textWrappingMode = TextWrappingModes.Normal;
        if (customFont != null) nameText.font = customFont;

        GameObject descriptionObj = new GameObject("Description");
        descriptionObj.transform.SetParent(slotObj.transform, false);
        RectTransform descriptionRect = descriptionObj.AddComponent<RectTransform>();
        descriptionRect.anchorMin = new Vector2(0.06f, 0.32f);
        descriptionRect.anchorMax = new Vector2(0.94f, 0.49f);
        descriptionRect.sizeDelta = Vector2.zero;
        descriptionObj.AddComponent<CanvasRenderer>();
        TextMeshProUGUI descriptionText = descriptionObj.AddComponent<TextMeshProUGUI>();
        descriptionText.text = string.IsNullOrWhiteSpace(equip.description) ? "noname" : equip.description;
        descriptionText.fontSize = 17f;
        descriptionText.fontStyle = FontStyles.Italic;
        descriptionText.color = new Color(0.76f, 0.78f, 0.82f, 1f);
        descriptionText.alignment = TextAlignmentOptions.Top;
        descriptionText.raycastTarget = false;
        descriptionText.textWrappingMode = TextWrappingModes.Normal;
        descriptionText.overflowMode = TextOverflowModes.Ellipsis;
        if (customFont != null) descriptionText.font = customFont;

        GameObject effectObj = new GameObject("Effect");
        effectObj.transform.SetParent(slotObj.transform, false);
        RectTransform er = effectObj.AddComponent<RectTransform>();
        er.anchorMin = new Vector2(0.06f, 0.21f);
        er.anchorMax = new Vector2(0.94f, 0.30f);
        er.sizeDelta = Vector2.zero;
        effectObj.AddComponent<CanvasRenderer>();
        TextMeshProUGUI effectText = effectObj.AddComponent<TextMeshProUGUI>();
        effectText.text = GetEffectDescription(equip);
        effectText.fontSize = 19f; 
        effectText.color = new Color(0.4f, 0.85f, 0.4f);
        effectText.alignment = TextAlignmentOptions.Center;
        effectText.raycastTarget = false;
        effectText.textWrappingMode = TextWrappingModes.Normal;
        if (customFont != null) effectText.font = customFont;

        GameObject btnObj = new GameObject("EquipBtn");
        btnObj.transform.SetParent(slotObj.transform, false);
        RectTransform br = btnObj.AddComponent<RectTransform>();
        br.anchorMin = new Vector2(0.12f, 0.06f);
        br.anchorMax = new Vector2(0.88f, 0.21f);
        br.sizeDelta = Vector2.zero;

        btnObj.AddComponent<CanvasRenderer>();
        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = isEquipped ? BTN_UNEQUIP : BTN_EQUIP;
        btnBg.raycastTarget = true;

        Button btn = btnObj.AddComponent<Button>();
        string eqId = equip.equipmentId;
        btn.onClick.AddListener(() =>
        {
            if (EquipmentManager.Instance == null) return;
            if (EquipmentManager.Instance.IsEquipped(eqId))
                EquipmentManager.Instance.Unequip(eqId);
            else
                EquipmentManager.Instance.Equip(eqId);
        });

        GameObject btnTextObj = new GameObject("BtnText");
        btnTextObj.transform.SetParent(btnObj.transform, false);
        RectTransform btr = btnTextObj.AddComponent<RectTransform>();
        btr.anchorMin = Vector2.zero;
        btr.anchorMax = Vector2.one;
        btr.sizeDelta = Vector2.zero;
        btnTextObj.AddComponent<CanvasRenderer>();
        TextMeshProUGUI btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
        btnText.text = isEquipped ? "DESEQUIPAR" : "EQUIPAR";
        btnText.fontSize = 20f; 
        btnText.fontStyle = FontStyles.Bold;
        btnText.color = Color.white;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.raycastTarget = false;
        if (customFont != null) btnText.font = customFont;

        return slotObj;
    }

    private void CreateCloseButton(Transform parent)
    {
        GameObject btnObj = new GameObject("CloseButton");
        btnObj.transform.SetParent(parent, false);
        RectTransform r = btnObj.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(1f, 1f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(1f, 1f);
        r.anchoredPosition = new Vector2(-8f, -8f);
        r.sizeDelta = new Vector2(28f, 28f);

        btnObj.AddComponent<CanvasRenderer>();
        Image bg = btnObj.AddComponent<Image>();
        bg.color = new Color(0.8f, 0.2f, 0.2f, 0.7f);

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(CloseCrafting);

        ColorBlock colors = btn.colors;
        colors.normalColor = new Color(0.8f, 0.2f, 0.2f, 0.7f);
        colors.highlightedColor = new Color(1f, 0.3f, 0.3f, 0.9f);
        colors.pressedColor = new Color(0.6f, 0.1f, 0.1f, 1f);
        btn.colors = colors;

        GameObject xObj = new GameObject("X");
        xObj.transform.SetParent(btnObj.transform, false);
        RectTransform xr = xObj.AddComponent<RectTransform>();
        xr.anchorMin = Vector2.zero;
        xr.anchorMax = Vector2.one;
        xr.sizeDelta = Vector2.zero;
        xObj.AddComponent<CanvasRenderer>();
        TextMeshProUGUI x = xObj.AddComponent<TextMeshProUGUI>();
        x.text = "X";
        x.fontSize = 18f; 
        x.color = Color.white;
        x.alignment = TextAlignmentOptions.Center;
        x.raycastTarget = false;
        if (customFont != null) x.font = customFont;
    }

    private TextMeshProUGUI CreateTextElement(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta,
        float fontSize, FontStyles style, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform r = obj.AddComponent<RectTransform>();
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.pivot = pivot;
        r.anchoredPosition = anchoredPos;
        r.sizeDelta = sizeDelta;
        r.offsetMin = new Vector2(12f, r.offsetMin.y);
        r.offsetMax = new Vector2(-12f, r.offsetMax.y);
        obj.AddComponent<CanvasRenderer>();
        TextMeshProUGUI txt = obj.AddComponent<TextMeshProUGUI>();
        txt.fontSize = fontSize;
        txt.fontStyle = style;
        txt.color = color;
        txt.alignment = TextAlignmentOptions.TopLeft;
        txt.raycastTarget = false;
        txt.textWrappingMode = TextWrappingModes.Normal;
        if (customFont != null) txt.font = customFont;
        return txt;
    }

}