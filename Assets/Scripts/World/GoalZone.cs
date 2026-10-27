
using UnityEngine;

public class GameWinZone : MonoBehaviour
{
    [Header("Karakterler")]
    [SerializeField] private Transform player;
    [SerializeField] private NPCFollowPlayer prisoner;

    [Header("Kazanma")]
    [SerializeField] private GameObject winPanel;

    private bool gameWon;

    private void Start()
    {
        if (winPanel != null)
            winPanel.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        CheckWinCondition();
    }

    private void OnTriggerStay(Collider other)
    {
        CheckWinCondition();
    }

    private void CheckWinCondition()
    {
        if (gameWon || player == null || prisoner == null)
            return;

        if (!prisoner.IsFollowing)
            return;

        if (!IsInsideZone(player))
            return;

        if (!IsInsideZone(prisoner.transform))
            return;

        gameWon = true;

        if (winPanel != null)
            winPanel.SetActive(true);

        Debug.Log("OYUN KAZANILDI!");
    }

    private bool IsInsideZone(Transform target)
    {
        Collider zone = GetComponent<Collider>();

        if (zone == null)
            return false;

        Collider[] colliders =
            target.GetComponentsInChildren<Collider>();

        foreach (Collider targetCollider in colliders)
        {
            if (zone.bounds.Intersects(targetCollider.bounds))
                return true;
        }

        return false;
    }
}
