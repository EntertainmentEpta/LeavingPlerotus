using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class SynergyMapBuilder : MonoBehaviour
{
    private RectTransform contentRect;
    
    // Classes de Dados
    public class SynergyNode
    {
        public string id;
        public string name;
        public int tier;
        public Vector2 position;
        public List<string> parents = new List<string>();
    }

    private List<SynergyNode> nodes = new List<SynergyNode>();

    private void Awake()
    {
        contentRect = GetComponent<RectTransform>();
        if (contentRect == null) contentRect = gameObject.AddComponent<RectTransform>();

        LoadSynergyData();
    }

    private void OnEnable()
    {
        BuildVisualMap();
    }

    private void LoadSynergyData()
    {
        nodes.Clear();

        // T1
        nodes.Add(new SynergyNode { id = "golem_1", name = "Golem T1", tier = 1, position = new Vector2(-400, 200) });
        nodes.Add(new SynergyNode { id = "aranha_1", name = "Aranha T1", tier = 1, position = new Vector2(-400, 0) });
        nodes.Add(new SynergyNode { id = "goblin_1", name = "Goblin T1", tier = 1, position = new Vector2(-400, -200) });

        // T2
        nodes.Add(new SynergyNode { id = "golem_2", name = "Golem T2", tier = 2, position = new Vector2(-150, 200), parents = new List<string> { "golem_1" } });
        nodes.Add(new SynergyNode { id = "aranha_2", name = "Aranha T2", tier = 2, position = new Vector2(-150, 0), parents = new List<string> { "aranha_1" } });
        nodes.Add(new SynergyNode { id = "goblin_2", name = "Goblin T2", tier = 2, position = new Vector2(-150, -200), parents = new List<string> { "goblin_1" } });

        // T3
        nodes.Add(new SynergyNode { id = "tank_3", name = "Tank T3", tier = 3, position = new Vector2(150, 100), parents = new List<string> { "golem_2", "aranha_2" } });
        nodes.Add(new SynergyNode { id = "agil_3", name = "Agil T3", tier = 3, position = new Vector2(150, -100), parents = new List<string> { "aranha_2", "goblin_2" } });

        // T4
        nodes.Add(new SynergyNode { id = "sharpblur_4", name = "SharpBlur T4\n(Explosive Dash)", tier = 4, position = new Vector2(400, 0), parents = new List<string> { "tank_3", "agil_3" } });
    }

    private void BuildVisualMap()
    {
        foreach (Transform child in transform) { Destroy(child.gameObject); }

        // Mscara e ScrollRect para poder arrastar o mapa grande
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(transform, false);
        RectTransform vpRect = viewport.AddComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero; vpRect.anchorMax = Vector2.one;
        vpRect.sizeDelta = Vector2.zero;
        
        Image vpBg = viewport.AddComponent<Image>();
        vpBg.color = new Color(0.08f, 0.08f, 0.12f, 1f); // Fundo escuro azulado
        viewport.AddComponent<Mask>().showMaskGraphic = true;

        GameObject mapContainer = new GameObject("MapContent");
        mapContainer.transform.SetParent(viewport.transform, false);
        RectTransform mapRect = mapContainer.AddComponent<RectTransform>();
        mapRect.anchorMin = new Vector2(0.5f, 0.5f);
        mapRect.anchorMax = new Vector2(0.5f, 0.5f);
        mapRect.sizeDelta = new Vector2(1500f, 1000f); // Tamanho gigante pra arrastar
        mapRect.anchoredPosition = Vector2.zero;

        ScrollRect scroll = gameObject.GetComponent<ScrollRect>();
        if (scroll == null) scroll = gameObject.AddComponent<ScrollRect>();
        
        scroll.content = mapRect;
        scroll.viewport = vpRect;
        scroll.horizontal = true;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.inertia = true;

        // Pega sprites default da Unity pra fazer o crculo
        Sprite circleSprite = null;
#if UNITY_EDITOR
        circleSprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
#endif

        // Verifica o progresso
        bool hasUnlockAll = (SaveManager.instance != null && SaveManager.instance.CachedData != null && SaveManager.instance.CachedData.inimigosDescobertos.Count > 3);

        // 1. Linhas
        foreach (var node in nodes)
        {
            foreach (var parentId in node.parents)
            {
                SynergyNode parentNode = nodes.Find(n => n.id == parentId);
                if (parentNode != null)
                {
                    DrawLine(mapContainer.transform, parentNode.position, node.position);
                }
            }
        }

        // 2. Ns
        foreach (var node in nodes)
        {
            bool isUnlocked = false;
            if (hasUnlockAll) isUnlocked = true;
            else
            {
                // Verifica bestirio bsico
                if (node.id.Contains("golem") && SaveManager.instance.CachedData.inimigosDescobertos.Contains("Golem")) isUnlocked = true;
                if (node.id.Contains("aranha") && SaveManager.instance.CachedData.inimigosDescobertos.Contains("Aranha")) isUnlocked = true;
                if (node.id.Contains("goblin") && SaveManager.instance.CachedData.inimigosDescobertos.Contains("Goblin")) isUnlocked = true;
            }

            DrawNode(mapContainer.transform, node, circleSprite, isUnlocked);
        }
    }

    private void DrawLine(Transform parent, Vector2 posA, Vector2 posB)
    {
        GameObject lineObj = new GameObject("Line");
        lineObj.transform.SetParent(parent, false);
        Image img = lineObj.AddComponent<Image>();
        img.color = new Color(0.5f, 0.3f, 0.8f, 0.4f); // Roxo translcido

        RectTransform rect = lineObj.GetComponent<RectTransform>();
        Vector2 dir = (posB - posA).normalized;
        float distance = Vector2.Distance(posA, posB);
        
        rect.sizeDelta = new Vector2(distance, 6f);
        rect.anchoredPosition = posA + dir * distance * 0.5f;
        
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rect.localRotation = Quaternion.Euler(0, 0, angle);
    }

    private void DrawNode(Transform parent, SynergyNode node, Sprite circleSprite, bool isUnlocked)
    {
        GameObject nodeObj = new GameObject("Node_" + node.name);
        nodeObj.transform.SetParent(parent, false);
        
        Image img = nodeObj.AddComponent<Image>();
        if (circleSprite != null) img.sprite = circleSprite;
        
        // Cores Bonitas
        if (!isUnlocked) img.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        else if (node.tier == 1) img.color = new Color(0.7f, 0.7f, 0.7f); // Prata
        else if (node.tier == 2) img.color = new Color(0.2f, 0.8f, 0.3f); // Verde
        else if (node.tier == 3) img.color = new Color(0.2f, 0.5f, 1.0f); // Azul
        else img.color = new Color(1f, 0.84f, 0f); // Dourado T4

        RectTransform rect = nodeObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(70f, 70f); // Bolas maiores!
        rect.anchoredPosition = node.position;

        // Efeito de Borda brilhante
        Outline outline = nodeObj.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 0.5f);
        outline.effectDistance = new Vector2(2, -2);

        // Texto do N
        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(nodeObj.transform, false);
        TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
        
        txt.text = isUnlocked ? node.name : "???";
        txt.fontSize = 18f;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = isUnlocked ? Color.white : Color.gray;
        txt.fontStyle = FontStyles.Bold;
        
        // Sombra no texto
        txtObj.AddComponent<Shadow>().effectColor = Color.black;
        
        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.sizeDelta = new Vector2(200f, 40f);
        txtRect.anchoredPosition = new Vector2(0, -60f); // Texto fica debaixo da bolinha
    }
}
