
using System.Collections;
using UnityEngine;

public class NPCController : MonoBehaviour
{
    [Header("Ollama")]
    [SerializeField] private OllamaService ollamaService;

    [Header("Diyalog")]
    [SerializeField] private DialogueUI dialogueUI;

    [Header("NPC Kişiliği")]
    [TextArea(3, 6)]
    [SerializeField] private string personality =
        "Sen bir mahkumsun. Geçmişine ve kişiliğine uygun davran.";

    private bool isWaitingForResponse;

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

    private IEnumerator SendPlayerMessage(string playerMessage)
    {
        isWaitingForResponse = true;

        // İstek gönderilmeden önce konuşma balonunu göster.
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

                Debug.Log(
                    $"NPC: {decision.dialogue} | Karar: {decision.action} | İkna: {decision.persuaded}",
                    this
                );
            },
            error =>
            {
                Debug.LogError(error, this);
                ShowError("Ollama hatası:\n" + error);
            }
        );

        // İstek tamamlandı; artık tekrar konuşulabilir.
        isWaitingForResponse = false;
    }

    private void ShowError(string message)
    {
        Debug.LogError(message, this);

        if (dialogueUI != null)
            dialogueUI.ShowError(message);
    }
}
