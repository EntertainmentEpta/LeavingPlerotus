using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameCursorStyle
{
    None,
    NormalWhite,
    NormalDark,
    Attack,
    AttackAim
}

/// <summary>
/// Gerenciador global dos cursores customizados do jogo (carregados de UI/Resources/Cursors).
/// Inicializado automaticamente em qualquer cena sem precisar configurar manualmente.
/// </summary>
public class GameCursorManager : MonoBehaviour
{
    public static GameCursorManager Instance { get; private set; }

    private const string NormalWhitePath = "Cursors/PONTEIRO";
    private const string NormalDarkPath = "Cursors/PONTEIRO - PRETO";
    private const string AttackPath = "Cursors/PONTEIRO - ATAQUE";
    private const string AttackAimPath = "Cursors/PONTEIRO - ATAQUE - mirado";

    private Texture2D normalWhiteTex;
    private Texture2D normalDarkTex;
    private Texture2D attackTex;
    private Texture2D attackAimTex;

    private GameCursorStyle currentStyle = GameCursorStyle.None;
    private Player_WeaponManager cachedWeaponManager;
    private float nextWeaponManagerLookupTime;

    /// <summary>
    /// Se verdadeiro, usa o ponteiro normal preto nos menus gerais.
    /// </summary>
    public static bool UseDarkNormalCursor { get; set; } = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance != null) return;

        GameObject go = new GameObject("GameCursorManager");
        DontDestroyOnLoad(go);
        go.AddComponent<GameCursorManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadCursorTextures();
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyStyle(GameCursorStyle.NormalWhite, force: true);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        cachedWeaponManager = null;
        nextWeaponManagerLookupTime = 0f;
        UpdateCursorStyle(force: true);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            UpdateCursorStyle(force: true);
        }
    }

    private void LateUpdate()
    {
        UpdateCursorStyle(force: false);
    }

    private void LoadCursorTextures()
    {
        normalWhiteTex = Resources.Load<Texture2D>(NormalWhitePath);
        normalDarkTex = Resources.Load<Texture2D>(NormalDarkPath);
        attackTex = Resources.Load<Texture2D>(AttackPath);
        attackAimTex = Resources.Load<Texture2D>(AttackAimPath);
    }

    private void UpdateCursorStyle(bool force)
    {
        GameCursorStyle desired = ResolveDesiredStyle();
        ApplyStyle(desired, force);
    }

    private GameCursorStyle ResolveDesiredStyle()
    {
        // Telas de fundo claro (Diário / Journal Book): ponteiro preto para máximo contraste
        if (JournalBookUI.Instance != null && JournalBookUI.Instance.IsOpen)
        {
            return normalDarkTex != null ? GameCursorStyle.NormalDark : GameCursorStyle.NormalWhite;
        }

        // Qualquer UI de jogo aberta ou menu principal (sem player ativo)
        if (AimSettings.IsGameplayUIOpen())
        {
            return UseDarkNormalCursor && normalDarkTex != null
                ? GameCursorStyle.NormalDark
                : GameCursorStyle.NormalWhite;
        }

        Player_WeaponManager wm = GetWeaponManager();
        if (wm == null)
        {
            return UseDarkNormalCursor && normalDarkTex != null
                ? GameCursorStyle.NormalDark
                : GameCursorStyle.NormalWhite;
        }

        // Em gameplay com arma equipada e cursor visível (mira por mouse)
        if (wm.currentWeapon != null)
        {
            BoomerangWeapon boomerang = wm.currentWeapon.GetComponent<BoomerangWeapon>();
            if (boomerang != null)
            {
                return (AimSettings.IsMouseAim || boomerang.alwaysShowCursorWhenEquipped)
                    ? GameCursorStyle.AttackAim
                    : GameCursorStyle.Attack;
            }

            GunAttack gun = wm.currentWeapon.GetComponent<GunAttack>();
            if (gun != null)
            {
                return AimSettings.IsMouseAim
                    ? GameCursorStyle.AttackAim
                    : GameCursorStyle.Attack;
            }

            // Arma corpo a corpo (Adaga / Machado) quando cursor está livre em combate
            if (Cursor.visible && Cursor.lockState == CursorLockMode.None)
            {
                return GameCursorStyle.Attack;
            }
        }

        return UseDarkNormalCursor && normalDarkTex != null
            ? GameCursorStyle.NormalDark
            : GameCursorStyle.NormalWhite;
    }

    private Player_WeaponManager GetWeaponManager()
    {
        if (cachedWeaponManager != null && cachedWeaponManager.gameObject.activeInHierarchy)
            return cachedWeaponManager;

        if (Time.unscaledTime < nextWeaponManagerLookupTime)
            return cachedWeaponManager;

        nextWeaponManagerLookupTime = Time.unscaledTime + 0.5f;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            cachedWeaponManager = player.GetComponent<Player_WeaponManager>()
                               ?? player.GetComponentInChildren<Player_WeaponManager>();
        }

        if (cachedWeaponManager == null)
        {
            cachedWeaponManager = FindFirstObjectByType<Player_WeaponManager>();
        }

        return cachedWeaponManager;
    }

    public void ApplyStyle(GameCursorStyle style, bool force = false)
    {
        if (!force && style == currentStyle) return;

        Texture2D tex = null;
        Vector2 hotspot = Vector2.zero;

        switch (style)
        {
            case GameCursorStyle.NormalWhite:
                tex = normalWhiteTex ?? normalDarkTex;
                hotspot = Vector2.zero;
                break;

            case GameCursorStyle.NormalDark:
                tex = normalDarkTex ?? normalWhiteTex;
                hotspot = Vector2.zero;
                break;

            case GameCursorStyle.Attack:
                tex = attackTex ?? normalWhiteTex;
                hotspot = Vector2.zero;
                break;

            case GameCursorStyle.AttackAim:
                tex = attackAimTex ?? attackTex ?? normalWhiteTex;
                if (tex != null)
                    hotspot = new Vector2(tex.width * 0.5f, tex.height * 0.5f);
                break;
        }

        if (tex != null)
        {
            Cursor.SetCursor(tex, hotspot, CursorMode.Auto);
            currentStyle = style;
        }
    }
}
