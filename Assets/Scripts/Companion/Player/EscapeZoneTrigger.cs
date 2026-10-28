
using UnityEngine;

public class EscapeZoneTrigger : MonoBehaviour
{
    [Header("Karakter referansları")]
    [SerializeField] private Transform player;
    [SerializeField] private NPCController prisoner;

    [Header("Kazanma ekranı")]
    [SerializeField] private GameObject winPanel;

    private bool playerInside;
    private bool prisonerInside;
    private bool hasWon;

    private void Start()
    {
        if (winPanel != null)
            winPanel.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (player != null &&
            (other.transform == player ||
             other.transform.IsChildOf(player)))
        {
            playerInside = true;
        }

        if (prisoner != null)
        {
            NPCController foundNPC = other.GetComponentInParent<NPCController>();

            if (foundNPC == prisoner)
                prisonerInside = true;
        }

        CheckWinCondition();
    }

    private void OnTriggerExit(Collider other)
    {
        if (player != null &&
            (other.transform == player ||
             other.transform.IsChildOf(player)))
        {
            playerInside = false;
        }

        if (prisoner != null)
        {
            NPCController foundNPC = other.GetComponentInParent<NPCController>();

            if (foundNPC == prisoner)
                prisonerInside = false;
        }
    }

    private void CheckWinCondition()
    {
        if (hasWon || !playerInside || !prisonerInside)
            return;

        if (prisoner == null || !prisoner.IsPersuaded)
            return;

        hasWon = true;

        if (winPanel != null)
            winPanel.SetActive(true);

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("Kazanıldı! Oyuncu ve ikna edilen mahkûm hedef bölgede.");
    }
}
