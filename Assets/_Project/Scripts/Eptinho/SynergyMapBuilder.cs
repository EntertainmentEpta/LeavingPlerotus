using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Construtor de nós e constelação de sinergias para exibição dentro do Oráculo Eptinho (Aba 3).
/// Sincronizado integralmente com o SynergyDatabase e SynergyCatalogManager.
/// </summary>
public class SynergyMapBuilder : MonoBehaviour
{
    private RectTransform contentRect;
    private static Sprite _cachedCircle;

    private void Awake()
    {
        contentRect = GetComponent<RectTransform>();
        if (contentRect == null) contentRect = gameObject.AddComponent<RectTransform>();
    }

    private void OnEnable()
    {
        BuildVisualMap();
    }

    private void BuildVisualMap()
    {
        // Limpa filhos existentes
        foreach (Transform child in transform) { Destroy(child.gameObject); }

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(transform, false);
        RectTransform vpRect = viewport.AddComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.sizeDelta = Vector2.zero;
        
        Image vpBg = viewport.AddComponent<Image>();
        vpBg.color = new Color(0.04f, 0.05f, 0.08f, 1f);
        viewport.AddComponent<Mask>().showMaskGraphic = true;

        GameObject mapContainer = new GameObject("MapContent");
        mapContainer.transform.SetParent(viewport.transform, false);
        RectTransform mapRect = mapContainer.AddComponent<RectTransform>();
        mapRect.anchorMin = new Vector2(0.5f, 0.5f);
        mapRect.anchorMax = new Vector2(0.5f, 0.5f);
        mapRect.sizeDelta = new Vector2(1600f, 1200f);
        mapRect.anchoredPosition = Vector2.zero;

        ScrollRect scroll = gameObject.GetComponent<ScrollRect>();
        if (scroll == null) scroll = gameObject.AddComponent<ScrollRect>();
        
        scroll.content = mapRect;
        scroll.viewport = vpRect;
        scroll.horizontal = true;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.inertia = true;

        Sprite circleSprite = GetCircleSprite();
        var recipes = SynergyDatabase.AllRecipes;

        Dictionary<string, bool> discMap = new Dictionary<string, bool>();
        foreach (var r in recipes)
        {
            discMap[r.id] = SynergyCatalogManager.Instance != null && SynergyCatalogManager.Instance.IsNodeDiscovered(r.id);
        }

        // 1. Linhas entre os nós
        for (int i = 0; i < 8 && i < recipes.Count; i++)
        {
            int nextIdx = (i + 1) % 8;
            DrawLine(mapContainer.transform, recipes[i].mapPosition, recipes[nextIdx].mapPosition,
                     discMap[recipes[i].id] && discMap[recipes[nextIdx].id]);
        }

        if (recipes.Count >= 10)
        {
            DrawLine(mapContainer.transform, recipes[8].mapPosition, recipes[9].mapPosition,
                     discMap[recipes[8].id] && discMap[recipes[9].id]);
        }

        // 2. Nós
        for (int i = 0; i < recipes.Count; i++)
        {
            var r = recipes[i];
            bool isDisc = discMap[r.id];
            DrawNode(mapContainer.transform, r, i + 1, isDisc, circleSprite);
        }
    }

    private void DrawLine(Transform parent, Vector2 posA, Vector2 posB, bool bothDiscovered)
    {
        GameObject lineObj = new GameObject("Line");
        lineObj.transform.SetParent(parent, false);
        Image img = lineObj.AddComponent<Image>();
        img.color = bothDiscovered
            ? new Color(0.2f, 0.95f, 0.9f, 0.7f)
            : new Color(0.35f, 0.45f, 0.65f, 0.2f);

        RectTransform rect = lineObj.GetComponent<RectTransform>();
        Vector2 dir = (posB - posA).normalized;
        float distance = Vector2.Distance(posA, posB);
        
        rect.sizeDelta = new Vector2(distance, bothDiscovered ? 4f : 2f);
        rect.anchoredPosition = posA + dir * distance * 0.5f;
        
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rect.localRotation = Quaternion.Euler(0, 0, angle);
    }

    private void DrawNode(Transform parent, SynergyRecipe recipe, int index, bool isDiscovered, Sprite circleSprite)
    {
        GameObject nodeObj = new GameObject("Node_" + recipe.name);
        nodeObj.transform.SetParent(parent, false);
        
        Image img = nodeObj.AddComponent<Image>();
        if (circleSprite != null) img.sprite = circleSprite;
        
        img.color = isDiscovered
            ? recipe.themeColor
            : new Color(0.12f, 0.15f, 0.22f, 0.95f);

        RectTransform rect = nodeObj.GetComponent<RectTransform>();
        float size = (index >= 9) ? 85f : 70f;
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = recipe.mapPosition;

        // Borda
        Outline outline = nodeObj.AddComponent<Outline>();
        outline.effectColor = isDiscovered ? new Color(1f, 1f, 1f, 0.5f) : new Color(0.2f, 0.3f, 0.4f, 0.4f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // Texto com número romano ou ?
        GameObject glyphObj = new GameObject("Glyph");
        glyphObj.transform.SetParent(nodeObj.transform, false);
        TextMeshProUGUI glyph = glyphObj.AddComponent<TextMeshProUGUI>();
        glyph.text = isDiscovered ? index.ToString() : "?";
        glyph.fontSize = 22f;
        glyph.fontStyle = FontStyles.Bold;
        glyph.alignment = TextAlignmentOptions.Center;
        glyph.color = isDiscovered ? new Color(0.05f, 0.05f, 0.1f, 0.95f) : new Color(0.5f, 0.6f, 0.7f, 0.8f);

        RectTransform gRect = glyphObj.GetComponent<RectTransform>();
        gRect.anchorMin = Vector2.zero; gRect.anchorMax = Vector2.one;
        gRect.sizeDelta = Vector2.zero;

        // Rótulo abaixo da bolinha
        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(nodeObj.transform, false);
        TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
        
        txt.text = isDiscovered ? recipe.name : "[ ? ? ? ]";
        txt.fontSize = 13f;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = isDiscovered ? new Color(0.9f, 0.95f, 1f) : new Color(0.45f, 0.5f, 0.6f);
        txt.fontStyle = FontStyles.Bold;
        
        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.sizeDelta = new Vector2(220f, 30f);
        txtRect.anchoredPosition = new Vector2(0, -48f);
    }

    private static Sprite GetCircleSprite()
    {
        if (_cachedCircle != null) return _cachedCircle;

        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        float radius = center - 1.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist < radius - 1f)
                    tex.SetPixel(x, y, Color.white);
                else if (dist < radius)
                    tex.SetPixel(x, y, new Color(1, 1, 1, radius - dist));
                else
                    tex.SetPixel(x, y, Color.clear);
            }
        }
        tex.Apply();
        _cachedCircle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
        return _cachedCircle;
    }
}
