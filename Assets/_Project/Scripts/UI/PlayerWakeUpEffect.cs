using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Cria uma vinheta cinematográfica de abrir os olhos (pálpebras) com efeito de blur/visão borrada ao acordar na Base.
/// Totalmente autônomo: cria o Canvas, a vinheta e o volume de blur URP dinamicamente via código.
/// </summary>
public class PlayerWakeUpEffect : MonoBehaviour
{
    public static void TriggerWakeUp(MonoBehaviour runner, Action onComplete, AudioClip optionalAudio = null)
    {
        GameObject effectObj = new GameObject("WakeUpEffect_Controller");
        PlayerWakeUpEffect effect = effectObj.AddComponent<PlayerWakeUpEffect>();
        effect.StartCoroutine(effect.AnimateWakeUp(onComplete, optionalAudio));
    }

    private IEnumerator AnimateWakeUp(Action onComplete, AudioClip optionalAudio)
    {
        // 1. Setup URP Depth of Field (Blur de Câmera da Visão Desfocada)
        Volume blurVolume = null;
        VolumeProfile blurProfile = null;
        try
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                var camData = cam.GetComponent<UniversalAdditionalCameraData>();
                if (camData != null) camData.renderPostProcessing = true;
            }

            GameObject volObj = new GameObject("WakeUp_BlurVolume");
            blurVolume = volObj.AddComponent<Volume>();
            blurVolume.isGlobal = true;
            blurVolume.priority = 9999f;
            blurVolume.weight = 1f;

            blurProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            blurProfile.name = "WakeUp_BlurProfile";
            blurVolume.profile = blurProfile;

            DepthOfField dof = blurProfile.Add<DepthOfField>(true);
            dof.mode.overrideState = true;
            dof.mode.value = DepthOfFieldMode.Gaussian;
            dof.gaussianStart.overrideState = true;
            dof.gaussianStart.value = 0f;
            dof.gaussianEnd.overrideState = true;
            dof.gaussianEnd.value = 0.6f;
            dof.gaussianMaxRadius.overrideState = true;
            dof.gaussianMaxRadius.value = 2.5f;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlayerWakeUpEffect] Volume URP Depth of Field não disponível: " + e.Message);
        }

        // 2. Criar Canvas de Overlay
        GameObject canvasObj = new GameObject("WakeUp_Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();
        CanvasGroup canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = true;

        // 3. Camada de Ofuscamento / Visão Embaçada (Daze Overlay Suave)
        GameObject dazeObj = new GameObject("DazeOverlay");
        dazeObj.transform.SetParent(canvasObj.transform, false);
        Image dazeImg = dazeObj.AddComponent<Image>();
        dazeImg.color = new Color(0.95f, 0.97f, 1f, 0.12f); // Brilho sutil e transparente adaptando à luz
        RectTransform dazeRect = dazeObj.GetComponent<RectTransform>();
        dazeRect.anchorMin = Vector2.zero;
        dazeRect.anchorMax = Vector2.one;
        dazeRect.offsetMin = Vector2.zero;
        dazeRect.offsetMax = Vector2.zero;

        // 4. Pálpebra superior (Top Eyelid)
        GameObject topObj = new GameObject("TopEyelid");
        topObj.transform.SetParent(canvasObj.transform, false);
        Image topImg = topObj.AddComponent<Image>();
        topImg.color = Color.black;
        RectTransform topRect = topObj.GetComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0f, 0.5f);
        topRect.anchorMax = new Vector2(1f, 1f);
        topRect.pivot = new Vector2(0.5f, 1f);
        topRect.offsetMin = Vector2.zero;
        topRect.offsetMax = Vector2.zero;

        // 5. Pálpebra inferior (Bottom Eyelid)
        GameObject bottomObj = new GameObject("BottomEyelid");
        bottomObj.transform.SetParent(canvasObj.transform, false);
        Image bottomImg = bottomObj.AddComponent<Image>();
        bottomImg.color = Color.black;
        RectTransform bottomRect = bottomObj.GetComponent<RectTransform>();
        bottomRect.anchorMin = new Vector2(0f, 0f);
        bottomRect.anchorMax = new Vector2(1f, 0.5f);
        bottomRect.pivot = new Vector2(0.5f, 0f);
        bottomRect.offsetMin = Vector2.zero;
        bottomRect.offsetMax = Vector2.zero;

        if (optionalAudio != null)
        {
            AudioSource audioSource = canvasObj.AddComponent<AudioSource>();
            audioSource.clip = optionalAudio;
            audioSource.volume = 0.7f;
            audioSource.spatialBlend = 0f;
            audioSource.Play();
        }

        void SetEyeOpenAmount(float amount)
        {
            amount = Mathf.Clamp01(amount);
            topRect.anchorMin = new Vector2(0f, Mathf.Lerp(0.5f, 1f, amount));
            bottomRect.anchorMax = new Vector2(1f, Mathf.Lerp(0.5f, 0f, amount));
        }

        // Início: Olhos 100% fechados (tela preta)
        SetEyeOpenAmount(0f);
        yield return new WaitForSeconds(0.4f);

        // --- FASE 1: Primeiro piscar (abre ~35% com visão borrada e fecha rápido) ---
        float elapsed = 0f;
        float dur = 0.45f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 0.35f, elapsed / dur);
            SetEyeOpenAmount(t);
            yield return null;
        }

        // Fecha rapidamente (pestanejar)
        elapsed = 0f;
        dur = 0.25f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0.35f, 0.05f, elapsed / dur);
            SetEyeOpenAmount(t);
            yield return null;
        }

        yield return new WaitForSeconds(0.15f);

        // --- FASE 2: Abertura definitiva + o foco clareia e o blur desaparece ---
        elapsed = 0f;
        dur = 1.2f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / dur);
            float eyeOpen = Mathf.SmoothStep(0.05f, 1f, progress);
            SetEyeOpenAmount(eyeOpen);

            // Suavemente remove o blur e a visão embaçada conforme a vista foca
            float blurFactor = 1f - Mathf.SmoothStep(0f, 1f, progress);
            if (blurVolume != null) blurVolume.weight = blurFactor;
            dazeImg.color = new Color(0.95f, 0.97f, 1f, 0.12f * blurFactor);

            yield return null;
        }

        SetEyeOpenAmount(1f);
        if (blurVolume != null) blurVolume.weight = 0f;

        // Libera os controles do jogador com a visão 100% nítida!
        onComplete?.Invoke();

        // Fade out final do Canvas
        elapsed = 0f;
        dur = 0.3f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = 1f - (elapsed / dur);
            yield return null;
        }

        // Limpeza dos objetos temporários
        if (blurVolume != null)
        {
            Destroy(blurVolume.gameObject);
        }
        if (blurProfile != null)
        {
            Destroy(blurProfile);
        }
        Destroy(canvasObj);
        Destroy(gameObject);
    }
}
