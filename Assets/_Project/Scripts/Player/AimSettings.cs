using UnityEngine;

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
}
