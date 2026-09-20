using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

[DefaultExecutionOrder(-100)]
public class FixMainMenuUIInteractivity : MonoBehaviour
{
    private void Awake()
    {
        UnlockAllInteractions();
    }

    private void Start()
    {
        UnlockAllInteractions();
    }

    public void UnlockAllInteractions()
    {
        // 1. Destrava o cursor do mouse
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 1f;

        // 2. Garante EventSystem
        EventSystem es = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
        if (es == null)
        {
            GameObject esGo = new GameObject("EventSystem_AutoCreated");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();
        }
        else if (!es.gameObject.activeInHierarchy)
        {
            es.gameObject.SetActive(true);
        }

        // 3. Garante GraphicRaycaster no Canvas
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas c in canvases)
        {
            if (c != null)
            {
                GraphicRaycaster gr = c.GetComponent<GraphicRaycaster>();
                if (gr == null) c.gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        // 4. DESATIVA Raycast apenas da RawImage (Vídeo) para não bloquear a tela
        RawImage[] rawImages = FindObjectsByType<RawImage>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (RawImage raw in rawImages)
        {
            if (raw != null) raw.raycastTarget = false;
        }

        // 5. Desativa Raycast dos textos para não atrapalharem os cliques
        TextMeshProUGUI[] allTexts = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (TextMeshProUGUI txt in allTexts)
        {
            if (txt != null) txt.raycastTarget = false;
        }

        // (Foi REMOVIDO o bloco que desativava as Images normais e quebrava o Slider!)
    }
}