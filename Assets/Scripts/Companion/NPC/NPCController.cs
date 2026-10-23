using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class NPCController : MonoBehaviour
{
    [Header("Ollama")]
    [SerializeField] private OllamaService ollamaService;

    [Header("Diyalog")]
    [SerializeField] private DialogueUI dialogueUI;

    [Header("Diyalog Seçim Ekranı")]
    [SerializeField] private GameObject dialogueChoicePanel;
    [SerializeField] private Text choiceNPCMessageText;
    [SerializeField] private Button helpChoiceButton;
    [SerializeField] private Button ignoreChoiceButton;
    [SerializeField] private Button insultChoiceButton;

    [Header("NPC Kişiliği")]
    [TextArea(3, 6)]
    [SerializeField] private string personality =
        "Sen bir mahkumsun. Geçmişine ve kişiliğine uygun davran.";

    private bool isWaitingForResponse;

    private void Awake()
    {
        SetChoicePanelVisible(false);

        if (helpChoiceButton != null)
            helpChoiceButton.onClick.AddListener(OnHelpSelected);

        if (ignoreChoiceButton != null)
            ignoreChoiceButton.onClick.AddListener(OnIgnoreSelected);

        if (insultChoiceButton != null)
            insultChoiceButton.onClick.AddListener(OnInsultSelected);
    }

    private void OnDestroy()
    {
        if (helpChoiceButton != null)
            helpChoiceButton.onClick.RemoveListener(OnHelpSelected);

        if (ignoreChoiceButton != null)
            ignoreChoiceButton.onClick.RemoveListener(OnIgnoreSelected);

        if (insultChoiceButton != null)
            insultChoiceButton.onClick.RemoveListener(OnInsultSelected);
    }

    public void Talk(string playerMessage)
    {
        if (isWaitingForResponse)
            return;

        if (ollamaService == null)
        {
            ShowError("OllamaService Inspector'da atanmamış!");
            return;
        }

        StartCoroutine(SendPlayerMessage(playerMessage));
    }

    public void AskForHelp()
    {
        Talk("Bana yardım et. Benimle gelmeni istiyorum.");
    }

    public void SayHello()
    {
        Talk("Merhaba. Seninle konuşmak istiyorum.");
    }

    public void StartConversation()
    {
        Talk("Merhaba. Seninle konuşmak istiyorum.");
    }

    private void OnHelpSelected()
    {
        Talk("Sana yardım etmek istiyorum. Benimle gelmeni kabul ediyor musun?");
    }

    private void OnIgnoreSelected()
    {
        Talk("Seninle ilgilenmiyorum. Kendi başının çaresine bak.");
    }

    private void OnInsultSelected()
    {
        Talk("Sen işe yaramaz ve zavallı birisin.");
    }

    private IEnumerator SendPlayerMessage(string playerMessage)
    {
        isWaitingForResponse = true;

        if (dialogueUI != null)
            dialogueUI.ShowThinking();

        string prompt =
            personality +
            "\nOyuncunun söylediği: " + playerMessage +
            "\nYalnızca şu JSON biçiminde cevap ver:" +
            "\n{\"action\":\"FOLLOW veya STAY\"," +
            "\"dialogue\":\"NPC'nin cevabı\"," +
            "\"persuaded\":true veya false}" +
            "\nFOLLOW yalnızca NPC ikna olduysa kullanılmalı. " +
            "Aksi durumda STAY kullan. " +
            "dialogue alanında yalnızca NPC'nin söyleyeceği cümle olsun.";

        yield return ollamaService.GetDecision(
            prompt,
            decision =>
            {
                if (dialogueUI != null)
                    dialogueUI.ShowDialogue(decision.dialogue);

                if (choiceNPCMessageText != null)
                    choiceNPCMessageText.text = decision.dialogue;

                SetChoicePanelVisible(true);

                Debug.Log(
                    $"NPC: {decision.dialogue} | " +
                    $"Karar: {decision.action} | " +
                    $"İkna: {decision.persuaded}",
                    this
                );
            },
            error =>
            {
                ShowError("Ollama hatası:\n" + error);
            }
        );

        isWaitingForResponse = false;
    }

    private void SetChoicePanelVisible(bool visible)
    {
        if (dialogueChoicePanel != null)
        {
            dialogueChoicePanel.SetActive(visible);
        }
        else
        {
            Debug.LogWarning(
                "DialogueChoicePanel Inspector'da atanmamış!",
                this
            );
        }

        Cursor.visible = visible;
        Cursor.lockState = visible
            ? CursorLockMode.None
            : CursorLockMode.Locked;
    }

    private void ShowError(string message)
    {
        Debug.LogError(message, this);

        if (dialogueUI != null)
            dialogueUI.ShowError(message);
    }
}
