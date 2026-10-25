using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ferramenta de Editor que decodifica arquivos .GIF (GIF87a / GIF89a com LZW) em C# puro,
/// extrai cada quadro como Sprite Pixel Art (FilterMode.Point, Uncompressed) em
/// Assets/_Project/Resources/Loading/Frames/ e configura automaticamente o LoadingSpinner!
/// </summary>
public class EptinhoGifImporterTool : AssetPostprocessor
{
    private const string OUTPUT_FOLDER = "Assets/_Project/Resources/Loading/Frames";
    private const string PLAYER_CANVAS_PREFAB_PATH = "Assets/_Project/Player/Player Canvas Real.prefab";

    [MenuItem("Tools/RogueLike/Importar GIF do Eptinho (Loading)...")]
    public static void ImportGifFromMenu()
    {
        string path = EditorUtility.OpenFilePanel(
            "Selecione o arquivo .GIF do Eptinho Andando",
            Application.dataPath,
            "gif");

        if (string.IsNullOrEmpty(path)) return;

        ProcessGifAndSetupLoading(path);
    }

    // Detecta automaticamente quando o usuário arrasta um arquivo .gif para dentro do projeto Unity!
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (importedAssets == null) return;

        foreach (string assetPath in importedAssets)
        {
            if (assetPath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
            {
                string fileName = Path.GetFileName(assetPath);
                string fullPath = Path.GetFullPath(assetPath);

                // Usa delayCall para rodar após o término da importação atual da Unity
                EditorApplication.delayCall += () =>
                {
                    if (EditorUtility.DisplayDialog(
                        "GIF Detectado para o Loading!",
                        $"Você acabou de adicionar o arquivo '{fileName}' ao projeto.\n\n" +
                        "Deseja extrair todos os quadros desse GIF (com nitidez Pixel Art) e configurá-los automaticamente na animação de Loading do Eptinho?",
                        "Sim, Configurar Loading!",
                        "Agora não"))
                    {
                        ProcessGifAndSetupLoading(fullPath);
                    }
                };
                break;
            }
        }
    }

    public static void ProcessGifAndSetupLoading(string gifFullPath)
    {
        if (!File.Exists(gifFullPath))
        {
            EditorUtility.DisplayDialog("Erro", $"Arquivo não encontrado:\n{gifFullPath}", "OK");
            return;
        }

        try
        {
            byte[] gifBytes = File.ReadAllBytes(gifFullPath);
            List<GifFrame> frames = DecodeGif(gifBytes);

            if (frames == null || frames.Count == 0)
            {
                EditorUtility.DisplayDialog("Erro ao Ler GIF", "Não foi possível extrair os quadros deste arquivo .GIF.", "OK");
                return;
            }

            // Garante que a pasta de destino exista
            if (!Directory.Exists(OUTPUT_FOLDER))
            {
                Directory.CreateDirectory(OUTPUT_FOLDER);
            }

            // Limpa frames antigos na pasta
            string[] existingFiles = Directory.GetFiles(OUTPUT_FOLDER, "EptinhoWalk_*.png");
            foreach (string f in existingFiles)
            {
                AssetDatabase.DeleteAsset(f.Replace("\\", "/"));
            }

            float totalDelaySeconds = 0f;
            List<string> savedAssetPaths = new List<string>();

            for (int i = 0; i < frames.Count; i++)
            {
                GifFrame frame = frames[i];
                totalDelaySeconds += frame.delaySeconds;

                Texture2D tex = new Texture2D(frame.width, frame.height, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                tex.SetPixels32(frame.pixels);
                tex.Apply();

                byte[] pngBytes = tex.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(tex);

                string assetPath = $"{OUTPUT_FOLDER}/EptinhoWalk_{i:D2}.png";
                File.WriteAllBytes(assetPath, pngBytes);
                savedAssetPaths.Add(assetPath);
            }

            AssetDatabase.Refresh();

            // Configura cada PNG como Sprite Pixel Art (FilterMode.Point + Uncompressed)
            List<Sprite> generatedSprites = new List<Sprite>();
            foreach (string assetPath in savedAssetPaths)
            {
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.filterMode = FilterMode.Point;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.SaveAndReimport();
                }

                Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sp != null)
                {
                    generatedSprites.Add(sp);
                }
            }

            // Calcula o FPS médio original do GIF
            float avgFrameDelay = totalDelaySeconds / Mathf.Max(1, frames.Count);
            float calculatedFps = avgFrameDelay > 0.005f ? Mathf.Clamp(Mathf.Round(1f / avgFrameDelay), 4f, 30f) : 10f;

            // Aplica no Prefab Player Canvas Real e na cena aberta
            int updatedCount = ApplySpritesToLoadingSpinners(generatedSprites.ToArray(), calculatedFps);

            EditorUtility.DisplayDialog(
                "Loading do Eptinho Configurado!",
                $"Sucesso! {generatedSprites.Count} quadros foram extraídos do GIF em:\n{OUTPUT_FOLDER}\n\n" +
                $"• Filtro: Pixel Art (Point / Sem compressão)\n" +
                $"• Velocidade: {calculatedFps} FPS\n" +
                $"• Componentes atualizados: {updatedCount}",
                "Perfeito!");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[EptinhoGifImporterTool] Erro ao processar GIF: {ex}");
            EditorUtility.DisplayDialog("Erro ao Importar GIF", ex.Message, "OK");
        }
    }

    public static int ApplySpritesToLoadingSpinners(Sprite[] sprites, float fps)
    {
        if (sprites == null || sprites.Length == 0) return 0;

        int count = 0;

        // 1. Atualiza o Prefab principal (Player Canvas Real.prefab)
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_CANVAS_PREFAB_PATH);
        if (prefabRoot != null)
        {
            LoadingSpinner[] prefabSpinners = prefabRoot.GetComponentsInChildren<LoadingSpinner>(true);
            foreach (var sp in prefabSpinners)
            {
                ConfigureSpinnerComponent(sp, sprites, fps);
                count++;
            }
            EditorUtility.SetDirty(prefabRoot);
            PrefabUtility.SavePrefabAsset(prefabRoot);
        }

        // 2. Atualiza instâncias na cena aberta
        LoadingSpinner[] sceneSpinners = Resources.FindObjectsOfTypeAll<LoadingSpinner>();
        foreach (var sp in sceneSpinners)
        {
            if (sp != null && sp.gameObject.scene.IsValid())
            {
                ConfigureSpinnerComponent(sp, sprites, fps);
                EditorUtility.SetDirty(sp);
                EditorUtility.SetDirty(sp.gameObject);
                count++;
            }
        }

        AssetDatabase.SaveAssets();
        return count;
    }

    private static void ConfigureSpinnerComponent(LoadingSpinner spinner, Sprite[] sprites, float fps)
    {
        Undo.RecordObject(spinner, "Configurar GIF do Eptinho no Loading");
        spinner.walkingFrames = sprites;
        spinner.framesPerSecond = fps;
        spinner.enforcePixelArtFilter = true;
        spinner.transform.localRotation = Quaternion.identity;

        Image img = spinner.GetComponent<Image>();
        if (img != null)
        {
            Undo.RecordObject(img, "Atualizar Sprite Inicial do Eptinho");
            img.sprite = sprites[0];
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            EditorUtility.SetDirty(img);
        }
    }

    // =========================================================================
    // DECODIFICADOR GIF87a / GIF89a EM C# PURO (LZW + TRANSPARÊNCIA + DISPOSAL)
    // =========================================================================

    private class GifFrame
    {
        public int width;
        public int height;
        public float delaySeconds;
        public Color32[] pixels; // Orientado de baixo para cima (padrão Texture2D da Unity)
    }

    private static List<GifFrame> DecodeGif(byte[] data)
    {
        List<GifFrame> result = new List<GifFrame>();
        using (MemoryStream ms = new MemoryStream(data))
        using (BinaryReader br = new BinaryReader(ms))
        {
            string header = new string(br.ReadChars(6));
            if (header != "GIF87a" && header != "GIF89a")
                throw new Exception("O arquivo selecionado não é um GIF válido (cabeçalho GIF87a/GIF89a ausente).");

            int screenWidth = br.ReadUInt16();
            int screenHeight = br.ReadUInt16();
            byte packed = br.ReadByte();
            byte bgColorIndex = br.ReadByte();
            br.ReadByte(); // pixel aspect ratio

            bool hasGlobalColorTable = (packed & 0x80) != 0;
            int gctSize = 1 << ((packed & 0x07) + 1);

            Color32[] globalPalette = null;
            if (hasGlobalColorTable)
            {
                globalPalette = ReadPalette(br, gctSize);
            }

            Color32[] canvas = new Color32[screenWidth * screenHeight];
            Color32[] previousCanvas = new Color32[screenWidth * screenHeight];

            // Estado do Graphic Control Extension (GCE) para o próximo frame
            int disposalMethod = 0;
            bool hasTransparency = false;
            int transparentIndex = -1;
            float delaySeconds = 0.1f;

            while (ms.Position < ms.Length)
            {
                byte blockId = br.ReadByte();

                if (blockId == 0x3B) // Fim do GIF (Trailer)
                {
                    break;
                }
                else if (blockId == 0x21) // Extension Block
                {
                    byte extLabel = br.ReadByte();
                    if (extLabel == 0xF9) // Graphic Control Extension
                    {
                        br.ReadByte(); // Block size (4)
                        byte gcePacked = br.ReadByte();
                        disposalMethod = (gcePacked >> 2) & 0x07;
                        hasTransparency = (gcePacked & 0x01) != 0;
                        int delayHundredths = br.ReadUInt16();
                        delaySeconds = delayHundredths > 0 ? delayHundredths / 100f : 0.1f;
                        transparentIndex = br.ReadByte();
                        br.ReadByte(); // Block terminator (0)
                    }
                    else
                    {
                        SkipSubBlocks(br);
                    }
                }
                else if (blockId == 0x2C) // Image Descriptor (Frame)
                {
                    int left = br.ReadUInt16();
                    int top = br.ReadUInt16();
                    int width = br.ReadUInt16();
                    int height = br.ReadUInt16();
                    byte imgPacked = br.ReadByte();

                    bool hasLocalColorTable = (imgPacked & 0x80) != 0;
                    bool interlaced = (imgPacked & 0x40) != 0;
                    int lctSize = 1 << ((imgPacked & 0x07) + 1);

                    Color32[] activePalette = hasLocalColorTable ? ReadPalette(br, lctSize) : globalPalette;

                    int lzwMinCodeSize = br.ReadByte();
                    byte[] compressedData = ReadSubBlocks(br);

                    byte[] indexStream = DecompressLzw(compressedData, lzwMinCodeSize, width * height);

                    // Salva cópia do canvas caso o disposalMethod seja 3 (Restore to Previous)
                    Array.Copy(canvas, previousCanvas, canvas.Length);

                    int[] rowMapping = interlaced ? BuildInterlaceRowMap(height) : null;

                    for (int y = 0; y < height; y++)
                    {
                        int srcRow = interlaced ? rowMapping[y] : y;
                        int dstYTopDown = top + y;
                        if (dstYTopDown < 0 || dstYTopDown >= screenHeight) continue;

                        // Converte coordenada Y de cima-para-baixo (GIF) para baixo-para-cima (Unity Texture2D)
                        int unityY = (screenHeight - 1) - dstYTopDown;

                        for (int x = 0; x < width; x++)
                        {
                            int dstX = left + x;
                            if (dstX < 0 || dstX >= screenWidth) continue;

                            int pixelIdx = srcRow * width + x;
                            if (pixelIdx >= indexStream.Length) continue;

                            int colorIdx = indexStream[pixelIdx];
                            if (hasTransparency && colorIdx == transparentIndex)
                                continue;

                            if (activePalette != null && colorIdx < activePalette.Length)
                            {
                                canvas[unityY * screenWidth + dstX] = activePalette[colorIdx];
                            }
                        }
                    }

                    // Clona o estado composto atual do canvas para o frame
                    Color32[] framePixels = new Color32[screenWidth * screenHeight];
                    Array.Copy(canvas, framePixels, canvas.Length);

                    result.Add(new GifFrame
                    {
                        width = screenWidth,
                        height = screenHeight,
                        delaySeconds = delaySeconds,
                        pixels = framePixels
                    });

                    // Aplica Disposal Method após renderizar o frame
                    if (disposalMethod == 2) // Restore to background (transparente)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            int dstYTopDown = top + y;
                            if (dstYTopDown < 0 || dstYTopDown >= screenHeight) continue;
                            int unityY = (screenHeight - 1) - dstYTopDown;
                            for (int x = 0; x < width; x++)
                            {
                                int dstX = left + x;
                                if (dstX < 0 || dstX >= screenWidth) continue;
                                canvas[unityY * screenWidth + dstX] = new Color32(0, 0, 0, 0);
                            }
                        }
                    }
                    else if (disposalMethod == 3) // Restore to previous
                    {
                        Array.Copy(previousCanvas, canvas, canvas.Length);
                    }

                    // Reseta GCE para valores padrão
                    disposalMethod = 0;
                    hasTransparency = false;
                    transparentIndex = -1;
                    delaySeconds = 0.1f;
                }
                else
                {
                    // Bloco desconhecido ou padding
                    break;
                }
            }
        }

        return result;
    }

    private static Color32[] ReadPalette(BinaryReader br, int count)
    {
        Color32[] palette = new Color32[count];
        for (int i = 0; i < count; i++)
        {
            byte r = br.ReadByte();
            byte g = br.ReadByte();
            byte b = br.ReadByte();
            palette[i] = new Color32(r, g, b, 255);
        }
        return palette;
    }

    private static void SkipSubBlocks(BinaryReader br)
    {
        while (true)
        {
            byte size = br.ReadByte();
            if (size == 0) break;
            br.ReadBytes(size);
        }
    }

    private static byte[] ReadSubBlocks(BinaryReader br)
    {
        using (MemoryStream subMs = new MemoryStream())
        {
            while (true)
            {
                byte size = br.ReadByte();
                if (size == 0) break;
                byte[] chunk = br.ReadBytes(size);
                subMs.Write(chunk, 0, chunk.Length);
            }
            return subMs.ToArray();
        }
    }

    private static int[] BuildInterlaceRowMap(int height)
    {
        int[] map = new int[height];
        int srcRow = 0;
        int[] starts = { 0, 4, 2, 1 };
        int[] steps = { 8, 8, 4, 2 };
        for (int pass = 0; pass < 4; pass++)
        {
            for (int y = starts[pass]; y < height; y += steps[pass])
            {
                if (srcRow < height)
                {
                    map[y] = srcRow++;
                }
            }
        }
        return map;
    }

    private static byte[] DecompressLzw(byte[] compressed, int minCodeSize, int expectedPixelCount)
    {
        byte[] output = new byte[expectedPixelCount];
        int outPos = 0;

        int clearCode = 1 << minCodeSize;
        int endCode = clearCode + 1;
        int nextCode = endCode + 1;
        int codeSize = minCodeSize + 1;
        int codeMask = (1 << codeSize) - 1;

        int[] prefix = new int[4096];
        byte[] suffix = new byte[4096];
        byte[] firstChar = new byte[4096];
        byte[] stack = new byte[4096];

        for (int i = 0; i < clearCode; i++)
        {
            prefix[i] = -1;
            suffix[i] = (byte)i;
            firstChar[i] = (byte)i;
        }

        int bitPos = 0;
        int totalBits = compressed.Length * 8;
        int prevCode = -1;

        while (bitPos + codeSize <= totalBits && outPos < expectedPixelCount)
        {
            // Lê os próximos 'codeSize' bits (LSB-first)
            int code = 0;
            for (int b = 0; b < codeSize; b++)
            {
                int byteIdx = (bitPos + b) >> 3;
                int bitIdx = (bitPos + b) & 7;
                if (((compressed[byteIdx] >> bitIdx) & 1) != 0)
                {
                    code |= (1 << b);
                }
            }
            bitPos += codeSize;

            if (code == clearCode)
            {
                codeSize = minCodeSize + 1;
                codeMask = (1 << codeSize) - 1;
                nextCode = endCode + 1;
                prevCode = -1;
                continue;
            }

            if (code == endCode)
            {
                break;
            }

            int curCode = code;
            int stackPtr = 0;

            if (curCode >= nextCode)
            {
                if (prevCode < 0) break;
                stack[stackPtr++] = firstChar[prevCode];
                curCode = prevCode;
            }

            while (curCode >= clearCode && stackPtr < 4096)
            {
                stack[stackPtr++] = suffix[curCode];
                curCode = prefix[curCode];
            }
            stack[stackPtr++] = suffix[curCode];
            byte firstByte = suffix[curCode];

            for (int i = stackPtr - 1; i >= 0 && outPos < expectedPixelCount; i--)
            {
                output[outPos++] = stack[i];
            }

            if (prevCode >= 0 && nextCode < 4096)
            {
                prefix[nextCode] = prevCode;
                suffix[nextCode] = firstByte;
                firstChar[nextCode] = firstChar[prevCode];
                nextCode++;

                if (nextCode == (1 << codeSize) && codeSize < 12)
                {
                    codeSize++;
                    codeMask = (1 << codeSize) - 1;
                }
            }

            prevCode = code;
        }

        return output;
    }
}

/// <summary>
/// Inspector customizado do LoadingSpinner com botão rápido para importar o .GIF direto pelo componente.
/// </summary>
[CustomEditor(typeof(LoadingSpinner))]
public class LoadingSpinnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "Dica: Você pode arrastar seu arquivo .GIF para qualquer pasta do projeto (ele será detectado automaticamente) ou clicar no botão abaixo para selecioná-lo direto do computador!",
            MessageType.Info);

        GUI.backgroundColor = new Color(0.25f, 0.85f, 0.45f);
        if (GUILayout.Button("📂 Importar Arquivo .GIF do Eptinho Automaticamente", GUILayout.Height(34)))
        {
            EptinhoGifImporterTool.ImportGifFromMenu();
        }
        GUI.backgroundColor = Color.white;
    }
}
