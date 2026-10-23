
using UnityEngine;

public class NPCInteraction : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private Transform player;
    [SerializeField] private NPCController npcController;
    [SerializeField] private GameObject interactionPrompt;

    [Header("Ayarlar")]
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;

    private bool isNearby;
    private bool conversationStarted;

    private void Start()
    {
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }
        else
        {
            Debug.LogWarning(
                "InteractionPrompt atanmamış! Inspector'dan E ile konuşma yazısını bağla.",
                this
            );
        }
    }

    private void Update()
    {
        if (player == null || npcController == null)
            return;

        float distance = Vector3.Distance(
            player.position,
            transform.position
        );

        isNearby = distance <= interactionDistance;

        // NPC'ye yaklaşınca E ile konuş yazısını göster.
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(
                isNearby && !conversationStarted
            );
        }

        // E tuşuyla konuşmayı başlat.
        if (isNearby &&
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
