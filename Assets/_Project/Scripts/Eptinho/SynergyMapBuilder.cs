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
        public List<string> parents = new List<string>(); // IDs dos ns que formam este
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
        nodes.Add(new SynergyNode { id = "golem_1", name = "Golem T1", tier = 1, position = new Vector2(-250, 100) });
        nodes.Add(new SynergyNode { id = "aranha_1", name = "Aranha T1", tier = 1, position = new Vector2(-250, 0) });
        nodes.Add(new SynergyNode { id = "goblin_1", name = "Goblin T1", tier = 1, position = new Vector2(-250, -100) });

        // T2
        nodes.Add(new SynergyNode { id = "golem_2", name = "Golem T2", tier = 2, position = new Vector2(-50, 100), parents = { "golem_1", "golem_1" } });
        nodes.Add(new SynergyNode { id = "aranha_2", name = "Aranha T2", tier = 2, position = new Vector2(-50, 0), parents = { "aranha_1", "aranha_1" } });
        nodes.Add(new SynergyNode { id = "goblin_2", name = "Goblin T2", tier = 2, position = new Vector2(-50, -100), parents = { "goblin_1", "goblin_1" } });

        // T3
        nodes.Add(new SynergyNode { id = "tank_3", name = "Tank T3", tier = 3, position = new Vector2(150, 50), parents = { "golem_2", "aranha_2" } });
        nodes.Add(new SynergyNode { id = "agil_3", name = "Agil T3", tier = 3, position = new Vector2(150, -50), parents = { "aranha_2", "goblin_2" } });

        // T4
        nodes.Add(new SynergyNode { id = "sharpblur_4", name = "SharpBlur T4\n(Explosive Dash)", tier = 4, position = new Vector2(350, 0), parents = { "tank_3", "agil_3" } });
    }

    private void BuildVisualMap()
    {
        // Limpa filhos antigos
        foreach (Transform child in transform) { Destroy(child.gameObject); }

        // Cria container panvel se necessrio (por enquanto esttico centralizado)
        GameObject mapContainer = new GameObject("MapContainer");
        mapContainer.transform.SetParent(transform, false);
        RectTransform mapRect = mapContainer.AddComponent<RectTransform>();
        mapRect.anchorMin = new Vector2(0.5f, 0.5f);
        mapRect.anchorMax = new Vector2(0.5f, 0.5f);
        mapRect.anchoredPosition = Vector2.zero;

        // 1. Desenha as Linhas primeiro (pra ficarem por baixo)
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

        // 2. Desenha os Ns por cima
        foreach (var node in nodes)
        {
            DrawNode(mapContainer.transform, node);
        }
    }

    private void DrawLine(Transform parent, Vector2 posA, Vector2 posB)
    {
        GameObject lineObj = new GameObject("Line");
        lineObj.transform.SetParent(parent, false);
        Image img = lineObj.AddComponent<Image>();
        img.color = new Color(0.4f, 0.2f, 0.6f, 0.5f); // Roxo escuro translcido

        RectTransform rect = lineObj.GetComponent<RectTransform>();
        Vector2 dir = (posB - posA).normalized;
        float distance = Vector2.Distance(posA, posB);
        
        rect.sizeDelta = new Vector2(distance, 4f); // Espessura da linha = 4
        rect.anchoredPosition = posA + dir * distance * 0.5f;
        
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rect.localRotation = Quaternion.Euler(0, 0, angle);
    }

    private void DrawNode(Transform parent, SynergyNode node)
    {
        GameObject nodeObj = new GameObject("Node_" + node.name);
        nodeObj.transform.SetParent(parent, false);
        Image img = nodeObj.AddComponent<Image>();
        
        // Cores por Tier
        if (node.tier == 1) img.color = Color.gray;
        else if (node.tier == 2) img.color = Color.green;
        else if (node.tier == 3) img.color = Color.blue;
        else img.color = new Color(1f, 0.84f, 0f); // Dourado T4

        RectTransform rect = nodeObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(40f, 40f);
        rect.anchoredPosition = node.position;

        // Texto do N
        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(nodeObj.transform, false);
        TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
        txt.text = node.name;
        txt.fontSize = 12f;
        txt.alignment = TextAlignmentOptions.Top;
        txt.color = Color.white;
        
        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.sizeDelta = new Vector2(150f, 30f);
        txtRect.anchoredPosition = new Vector2(0, -35f);
    }
}
