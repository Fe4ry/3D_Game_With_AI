
using UnityEngine;
using UnityEngine.UI;

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

    private void Start()
    {
        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);
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

        if (interactionPrompt != null)
            interactionPrompt.SetActive(isNearby);

        if (isNearby && Input.GetKeyDown(interactionKey))
        {
            npcController.StartConversation();

            if (interactionPrompt != null)
                interactionPrompt.SetActive(false);
        }
    }
}
