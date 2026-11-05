
using UnityEngine;

public class NPCInteraction : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private Transform player;
    [SerializeField] private NPCController npcController;
    [SerializeField] private NPCSensor npcSensor;
    [SerializeField] private GameObject interactionPrompt;

    [Header("Ayarlar")]
    [SerializeField] private KeyCode interactionKey = KeyCode.E;

    private bool isDetected;
    private bool conversationStarted;

    private void Start()
    {
        if (player == null)
            Debug.LogError("NPCInteraction: Player atanmamış!", this);

        if (npcController == null)
            Debug.LogError("NPCInteraction: NPCController atanmamış!", this);

        if (npcSensor == null)
            Debug.LogError("NPCInteraction: NPCSensor atanmamış!", this);

        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);
        else
            Debug.LogWarning("NPCInteraction: InteractionPrompt atanmamış!", this);
    }

    private void Update()
    {
        if (player == null || npcController == null || npcSensor == null)
            return;

        isDetected = npcSensor.CanDetect(player);
        Debug.Log($"NPCInteraction: Player detected with Raycast System: {isDetected}");

        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(
                isDetected && !conversationStarted
            );
        }

        if (isDetected &&
            !conversationStarted &&
            Input.GetKeyDown(interactionKey))
        {
            conversationStarted = true;

            if (interactionPrompt != null)
                interactionPrompt.SetActive(false);

            npcController.StartConversation();
        }
    }
}
