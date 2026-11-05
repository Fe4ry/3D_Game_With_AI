
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
        else
            Debug.LogError("EscapeZone: WinPanel atanmamış!");
    }

    private void OnTriggerEnter(Collider other)
    {

        // Oyuncu bölgeye girdi mi?
        if (player != null &&
            (other.transform == player ||
             other.transform.IsChildOf(player)))
        {
            playerInside = true;
        }

        // Mahkûm bölgeye girdi mi?
        NPCController foundNPC =
            other.GetComponentInParent<NPCController>();



        if (prisoner != null && foundNPC == prisoner)
        {
            prisonerInside = true;
            Debug.Log("Mahkûm EscapeZone'a girdi!");
        }

        CheckWinCondition();
    }

    private void OnTriggerExit(Collider other)
    {
        // Oyuncu bölgeden çıktı mı?
        if (player != null &&
            (other.transform == player ||
             other.transform.IsChildOf(player)))
        {
            playerInside = false;
        }

        // Mahkûm bölgeden çıktı mı?
        NPCController foundNPC =
            other.GetComponentInParent<NPCController>();

        if (prisoner != null && foundNPC == prisoner)
        {
            prisonerInside = false;
            Debug.Log("Mahkûm EscapeZone'dan çıktı.");
        }
    }

    private void CheckWinCondition()
    {

        if (hasWon ||
            !playerInside ||
            !prisonerInside ||
            prisoner == null ||
            !prisoner.IsPersuaded)
        {
            return;
        }

        hasWon = true;

        if (winPanel != null)
            winPanel.SetActive(true);

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("KAZANILDI!");
    }
}
