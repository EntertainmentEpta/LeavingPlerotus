using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogManager : MonoBehaviour
{
    public static DialogManager Instance { get; private set; }

    [Header("UI Elements")]
    public GameObject dialogPanel;       // O painel escuro no fundo
    public Image portraitImage;          // A imagem do Eptinho
    public TextMeshProUGUI dialogText;   // O texto do diálogo

    [Header("Configurações")]
    public float typeSpeed = 0.02f;      // Velocidade da maquininha de escrever

    private List<string> sentences = new List<string>();
    private int currentIndex = 0;
    private bool isTyping = false;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
            
        dialogPanel.SetActive(false);
    }

    private void Update()
    {
        if (!dialogPanel.activeInHierarchy) return;

        // TECLAS PARA VOLTAR (Seta Esquerda, A ou Backspace)
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Backspace))
        {
            if (currentIndex > 0)
            {
                currentIndex--;
                DisplaySentence();
            }
            return; // Impede que o código de avançar rode junto
        }

        // TECLAS PARA AVANÇAR (Mouse, Espaço, Enter, Seta Direita, D, ou Qualquer tecla padrão de ação)
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || 
            Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.E))
        {
            if (isTyping)
            {
                // Se o texto está aparecendo, o botão preenche a frase toda na hora
                StopAllCoroutines();
                dialogText.text = sentences[currentIndex];
                isTyping = false;
            }
            else
            {
                // Se a frase já terminou, vai para a próxima (ou fecha a tela se for a última)
                currentIndex++;
                if (currentIndex < sentences.Count)
                {
                    DisplaySentence();
                }
                else
                {
                    EndDialog();
                }
            }
        }
    }

    private System.Action onCompleteCallback;

    public void StartDialog(string[] lines, System.Action onComplete = null)
    {
        onCompleteCallback = onComplete;
        Time.timeScale = 0f;
        dialogPanel.SetActive(true);
        sentences.Clear();
        sentences.AddRange(lines);
        
        currentIndex = 0;
        DisplaySentence();
    }

    private void DisplaySentence()
    {
        StopAllCoroutines();
        StartCoroutine(TypeSentence(sentences[currentIndex]));
    }

    private IEnumerator TypeSentence(string sentence)
    {
        isTyping = true;
        dialogText.text = "";
        
        foreach (char letter in sentence.ToCharArray())
        {
            dialogText.text += letter;
            yield return new WaitForSecondsRealtime(typeSpeed);
        }
        
        isTyping = false;
    }

    private void EndDialog()
    {
        dialogPanel.SetActive(false);
        Time.timeScale = 1f;
        
        onCompleteCallback?.Invoke();
        onCompleteCallback = null;
    }
}
