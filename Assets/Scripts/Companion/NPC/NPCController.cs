
using System.Collections;
using UnityEngine;

public class NPCController : MonoBehaviour
{
    [Header("Ollama")]
    [SerializeField] private OllamaService ollamaService;

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
            Debug.LogError("OllamaService Inspector'da atanmamış!", this);
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


    private IEnumerator SendPlayerMessage(string playerMessage)
    {
        isWaitingForResponse = true;

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
                Debug.Log(
                    $"NPC: {decision.dialogue} | " +
                    $"Karar: {decision.action} | " +
                    $"İkna: {decision.persuaded}",
                    this
                );
            },
            error =>
            {
                Debug.LogError(error, this);
            }
        );

        isWaitingForResponse = false;
    }
}
