using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ScreenFader : MonoBehaviour
{
    public Image fadeImage;
    public float fadeDuration = 0.3f;

    void Awake()
    {
        if (fadeImage == null)
        {
            fadeImage = GetComponent<Image>();
        }
    }

    public IEnumerator FadeOut() // Escurecer a tela
    {
        if (fadeImage == null) fadeImage = GetComponent<Image>();
        if (fadeImage == null) yield break;

        if (fadeDuration <= 0f)
        {
            fadeImage.color = new Color(0, 0, 0, 1);
            yield break;
        }

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            fadeImage.color = new Color(0, 0, 0, Mathf.Clamp01(timer / fadeDuration));
            yield return null;
        }
        fadeImage.color = new Color(0, 0, 0, 1); // Garante que fique totalmente preto
    }

    public IEnumerator FadeIn() // Clarear a tela
    {
        if (fadeImage == null) fadeImage = GetComponent<Image>();
        if (fadeImage == null) yield break;

        if (fadeDuration <= 0f)
        {
            fadeImage.color = new Color(0, 0, 0, 0);
            yield break;
        }

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            fadeImage.color = new Color(0, 0, 0, Mathf.Clamp01(1f - (timer / fadeDuration)));
            yield return null;
        }
        fadeImage.color = new Color(0, 0, 0, 0); // Garante que fique totalmente transparente
    }
}