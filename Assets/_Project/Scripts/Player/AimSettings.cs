using UnityEngine;
using UnityEngine.Rendering;

public enum AimMode
{
    MouseCursor = 0,     // Padrão: Mira no cursor do mouse na tela (cursor visível com a arma empunhada)
    PlayerDirection = 1  // Direção do personagem / WASD / Gamepad (cursor oculto)
}

public static class AimSettings
{
    public const string PREFS_KEY = "Settings_AimMode";

    public static event System.Action<AimMode> OnAimModeChanged;

    public static AimMode CurrentMode
    {
        get => (AimMode)PlayerPrefs.GetInt(PREFS_KEY, (int)AimMode.MouseCursor);
        set
        {
            AimMode previous = CurrentMode;
            PlayerPrefs.SetInt(PREFS_KEY, (int)value);
            PlayerPrefs.Save();
            if (previous != value || !PlayerPrefs.HasKey(PREFS_KEY))
            {
                OnAimModeChanged?.Invoke(value);
            }
        }
    }

    public static bool IsMouseAim => CurrentMode == AimMode.MouseCursor;
    public static bool IsPlayerDirectionAim => CurrentMode == AimMode.PlayerDirection;

    public static void SetMode(AimMode mode)
    {
        CurrentMode = mode;
    }

    public static void ToggleMode()
    {
        CurrentMode = IsMouseAim ? AimMode.PlayerDirection : AimMode.MouseCursor;
    }

    public static string GetModeDisplayName()
    {
        return IsMouseAim ? "Mira: Mouse (Padrão)" : "Mira: Direção (WASD)";
    }

    public static bool IsGameplayUIOpen()
    {
        if (DialogManager.BlocksGameplayInput || GamePause.IsPaused) return true;
        if (CheatConsole.IsOpen) return true;
        if (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen()) return true;
        if (SyntheticBagUI.Instance != null && SyntheticBagUI.Instance.IsOpen()) return true;
        if (CraftingUI.Instance != null && CraftingUI.Instance.IsOpen()) return true;
        if (JournalBookUI.Instance != null && JournalBookUI.Instance.IsOpen) return true;
        if (EptinhoMenuController.instancia != null && EptinhoMenuController.instancia.IsOpen()) return true;
        if (MerchantUIController.HasInstance && MerchantUIController.Instance.IsUiOpen()) return true;
        if (DeathScreenUI.Instance != null && DeathScreenUI.Instance.deathPanel != null && DeathScreenUI.Instance.deathPanel.activeSelf) return true;
        if (VictoryScreenUI.Instance != null && VictoryScreenUI.Instance.victoryPanel != null && VictoryScreenUI.Instance.victoryPanel.activeSelf) return true;
        return false;
    }

    public static void ShowAimCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public static void HideAimCursorIfNoUI()
    {
        if (!IsGameplayUIOpen())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public static Material CreateGroundIndicatorMaterial(Color color, string materialName, bool useVertexColors = false)
    {
        Shader shader = useVertexColors
            ? (Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit"))
            : (Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
        if (shader == null) return null;

        Color appliedColor = useVertexColors ? Color.white : color;
        Material mat = new Material(shader) { name = materialName };
        mat.color = appliedColor;
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
        if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)CullMode.Off);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", appliedColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", appliedColor);
        if (shader.name == "Universal Render Pipeline/Unlit")
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
        return mat;
    }
}
