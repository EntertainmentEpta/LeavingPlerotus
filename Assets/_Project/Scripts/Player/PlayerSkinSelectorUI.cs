using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

[RequireComponent(typeof(Collider))]
public class PlayerSkinSelectorUI : MonoBehaviour
{
    [Header("Feature Toggle")]
    [Tooltip("Se falso, o armário de skins fica completamente desativado.")]
    public bool skinsEnabled = false;

    [Header("Settings")]
    public KeyCode interactionKey = KeyCode.T;
    public string promptMessage = "Aperte [T] para abrir o Armário de Skins";

    [Header("UI Prefabs (Opcional)")]
    [Tooltip("Arraste aqui um painel de UI customizado se tiver. Se deixar null, criaremos uma UI automática na tela!")]
    public GameObject customUIPanel;

    [Header("Automatic UI Styling")]
    public Color panelColor = new Color(0.05f, 0.05f, 0.05f, 0.85f);
    public Color buttonColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    public Color buttonHoverColor = new Color(1f, 0.75f, 0f, 1f);
    public Color buttonPressedColor = new Color(0.8f, 0.6f, 0f, 1f);
    public Color titleColor = new Color(1f, 0.75f, 0f);
    public Color textColor = Color.white;
    public float titleFontSize = 26f;
    public float buttonFontSize = 18f;

    [Header("Automatic UI Layout")]
    public Vector2 panelSize = new Vector2(400f, 500f);
    public Vector2 buttonSize = new Vector2(300f, 42f);
    public float buttonStartY = -120f;
    public float buttonSpacingY = -55f;

    private bool playerInRange = false;
    private PlayerSkinManager cachedSkinManager;
    private GameObject activeUIInstance;
    private GameObject runtimePromptInstance;

    private void Start()
    {
        if (!skinsEnabled)
        {
            enabled = false;
            gameObject.SetActive(false);
            return;
        }

        // Garante que o collider é um trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        // Criar o prompt flutuante na tela se não houver um customizado
        CreateRuntimePrompt();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!skinsEnabled) return;
        if (other.CompareTag("Player") || other.GetComponent<PlayerM>() != null)
        {
            cachedSkinManager = other.GetComponent<PlayerSkinManager>();
            if (cachedSkinManager != null)
            {
                playerInRange = true;
                if (runtimePromptInstance != null)
                {
                    runtimePromptInstance.SetActive(true);
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponent<PlayerM>() != null)
        {
            playerInRange = false;
            CloseMenu();
            if (runtimePromptInstance != null)
            {
                runtimePromptInstance.SetActive(false);
            }
        }
    }

    private void Update()
    {
        if (playerInRange && Input.GetKeyDown(interactionKey))
        {
            if (activeUIInstance != null && activeUIInstance.activeSelf)
            {
                CloseMenu();
            }
            else
            {
                OpenMenu();
            }
        }
    }

    public void OpenMenu()
    {
        if (cachedSkinManager == null) return;

        // Oculta o prompt enquanto o menu está aberto
        if (runtimePromptInstance != null) runtimePromptInstance.SetActive(false);

        if (customUIPanel != null)
        {
            customUIPanel.SetActive(true);
            activeUIInstance = customUIPanel;
        }
        else
        {
            // Criar uma UI simples e bonita automaticamente
            activeUIInstance = CreateRuntimeSelectionMenu();
        }

        // Liberar cursor do mouse
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Desativar movimento do player para não andar com menu aberto
        PlayerM playerMovement = cachedSkinManager.GetComponent<PlayerM>();
        if (playerMovement != null) playerMovement.enabled = false;
    }

    public void CloseMenu()
    {
        if (activeUIInstance != null)
        {
            if (customUIPanel != null)
            {
                customUIPanel.SetActive(false);
            }
            else
            {
                Destroy(activeUIInstance);
                activeUIInstance = null;
            }
        }

        // Bloquear cursor novamente
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        // Reativar movimento do player
        if (cachedSkinManager != null)
        {
            PlayerM playerMovement = cachedSkinManager.GetComponent<PlayerM>();
            if (playerMovement != null) playerMovement.enabled = true;

            if (playerInRange && runtimePromptInstance != null)
            {
                runtimePromptInstance.SetActive(true);
            }
        }
    }

    public void SelectSkin(string skinID)
    {
        if (cachedSkinManager != null)
        {
            cachedSkinManager.SetSkin(skinID);
            
            // Salvar automaticamente após mudar
            SaveManager.instance?.SavePersistentData();
        }
        CloseMenu();
    }

    // --- CRIAÇÃO DE UI RUNTIME (FAILSAFE) ---

    private TMP_FontAsset GetDefaultFont()
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font == null)
        {
            font = TMP_Settings.defaultFontAsset;
        }
        return font;
    }

    private void CreateRuntimePrompt()
    {
        GameObject canvasGO = new GameObject("SkinSelectorPromptCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        
        GameObject textGO = new GameObject("PromptText");
        textGO.transform.SetParent(canvasGO.transform, false);
        
        TextMeshProUGUI text = textGO.AddComponent<TextMeshProUGUI>();
        text.font = GetDefaultFont();
        text.text = promptMessage;
        text.fontSize = 24;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        
        RectTransform rect = textGO.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(0f, -250f); // Centralizado na parte inferior
        rect.sizeDelta = new Vector2(600f, 50f);

        runtimePromptInstance = canvasGO;
        runtimePromptInstance.SetActive(false);
        
        // Garante que o canvas não seja destruído
        DontDestroyOnLoad(canvasGO);
    }

    private TextMeshProUGUI activeSkinSubtitleText;

    private void UpdateActiveSkinSubtitle()
    {
        if (activeSkinSubtitleText != null && cachedSkinManager != null)
        {
            string activeId = cachedSkinManager.ActiveSkinID;
            string activeName = activeId;
            foreach (var config in cachedSkinManager.skins)
            {
                if (config.skinID == activeId && !string.IsNullOrEmpty(config.skinName))
                {
                    activeName = config.skinName;
                    break;
                }
            }
            if (activeName == activeId && !string.IsNullOrEmpty(activeName))
            {
                activeName = char.ToUpper(activeName[0]) + activeName.Substring(1);
            }
            activeSkinSubtitleText.text = $"Atual: {activeName}";
        }
    }

    private GameObject CreateRuntimeSelectionMenu()
    {
        TMP_FontAsset fontAsset = GetDefaultFont();

        // 1. Criar Canvas
        GameObject canvasGO = new GameObject("SkinSelectionCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // 2. Fundo Escuro (Panel)
        GameObject panelGO = new GameObject("BackgroundPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        Image panelImage = panelGO.AddComponent<Image>();
        panelImage.color = panelColor;
        
        RectTransform panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = panelSize;

        // 3. Título
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(panelGO.transform, false);
        TextMeshProUGUI titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.font = fontAsset;
        titleText.text = "Guarda-Roupa de Skins";
        titleText.fontSize = titleFontSize;
        titleText.color = titleColor;
        titleText.alignment = TextAlignmentOptions.Center;

        RectTransform titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -30f);
        titleRect.sizeDelta = new Vector2(350f, 40f);

        // 3b. Subtítulo (Mostra a skin atual equipada)
        GameObject subtitleGO = new GameObject("Subtitle");
        subtitleGO.transform.SetParent(panelGO.transform, false);
        activeSkinSubtitleText = subtitleGO.AddComponent<TextMeshProUGUI>();
        activeSkinSubtitleText.font = fontAsset;
        activeSkinSubtitleText.fontSize = buttonFontSize;
        activeSkinSubtitleText.color = textColor;
        activeSkinSubtitleText.alignment = TextAlignmentOptions.Center;
        UpdateActiveSkinSubtitle();

        RectTransform subRect = subtitleGO.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.5f, 1f);
        subRect.anchorMax = new Vector2(0.5f, 1f);
        subRect.anchoredPosition = new Vector2(0f, -70f);
        subRect.sizeDelta = new Vector2(350f, 30f);

        // 4. Botões de Skin
        int index = 0;

        foreach (var skinConfig in cachedSkinManager.skins)
        {
            string skinId = skinConfig.skinID;
            // Fallback se skinName estiver vazio ou nulo
            string skinName = !string.IsNullOrEmpty(skinConfig.skinName) 
                ? skinConfig.skinName 
                : (char.ToUpper(skinId[0]) + skinId.Substring(1));

            // Criar objeto do botão
            GameObject btnGO = new GameObject($"Button_{skinId}");
            btnGO.transform.SetParent(panelGO.transform, false);

            Image btnImage = btnGO.AddComponent<Image>();
            btnImage.color = buttonColor;

            Button btn = btnGO.AddComponent<Button>();
            btn.onClick.AddListener(() => {
                SelectSkin(skinId);
                UpdateActiveSkinSubtitle();
            });

            RectTransform btnRect = btnGO.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 1f);
            btnRect.anchorMax = new Vector2(0.5f, 1f);
            btnRect.anchoredPosition = new Vector2(0f, buttonStartY + (index * buttonSpacingY));
            btnRect.sizeDelta = buttonSize;

            // Adicionar Text do Botão
            GameObject btnTextGO = new GameObject("Text");
            btnTextGO.transform.SetParent(btnGO.transform, false);
            TextMeshProUGUI btnText = btnTextGO.AddComponent<TextMeshProUGUI>();
            btnText.font = fontAsset;
            btnText.text = skinName;
            btnText.fontSize = buttonFontSize;
            btnText.color = textColor;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.textWrappingMode = TextWrappingModes.NoWrap;
            btnText.overflowMode = TextOverflowModes.Overflow;

            RectTransform btnTextRect = btnTextGO.GetComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.sizeDelta = Vector2.zero;

            // Efeitos de Hover
            ColorBlock cb = btn.colors;
            cb.highlightedColor = buttonHoverColor;
            cb.pressedColor = buttonPressedColor;
            btn.colors = cb;

            index++;
        }

        // 5. Botão Fechar
        GameObject closeBtnGO = new GameObject("Button_Close");
        closeBtnGO.transform.SetParent(panelGO.transform, false);
        Image closeImg = closeBtnGO.AddComponent<Image>();
        closeImg.color = new Color(0.6f, 0.1f, 0.1f, 1f); // Vermelho

        Button closeBtn = closeBtnGO.AddComponent<Button>();
        closeBtn.onClick.AddListener(CloseMenu);

        RectTransform closeRect = closeBtnGO.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.5f, 0f);
        closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.anchoredPosition = new Vector2(0f, 40f);
        closeRect.sizeDelta = buttonSize;

        GameObject closeTextGO = new GameObject("Text");
        closeTextGO.transform.SetParent(closeBtnGO.transform, false);
        TextMeshProUGUI closeText = closeTextGO.AddComponent<TextMeshProUGUI>();
        closeText.font = fontAsset;
        closeText.text = "Fechar";
        closeText.fontSize = buttonFontSize;
        closeText.color = textColor;
        closeText.alignment = TextAlignmentOptions.Center;
        closeText.textWrappingMode = TextWrappingModes.NoWrap;
        closeText.overflowMode = TextOverflowModes.Overflow;

        RectTransform closeTextRect = closeTextGO.GetComponent<RectTransform>();
        closeTextRect.anchorMin = Vector2.zero;
        closeTextRect.anchorMax = Vector2.one;
        closeTextRect.sizeDelta = Vector2.zero;

        ColorBlock closeCb = closeBtn.colors;
        closeCb.highlightedColor = new Color(0.8f, 0.2f, 0.2f, 1f);
        closeBtn.colors = closeCb;

        return canvasGO;
    }

    private void OnDestroy()
    {
        if (runtimePromptInstance != null)
        {
            Destroy(runtimePromptInstance);
        }
        if (activeUIInstance != null && customUIPanel == null)
        {
            Destroy(activeUIInstance);
        }
    }
}
