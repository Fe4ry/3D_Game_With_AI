using UnityEngine;
using UnityEngine.AI;

public class NPCFollowPlayer : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float updateInterval = 0.2f;
    [SerializeField, Min(0f)] private float followStoppingDistance = 1.4f;

    private NavMeshAgent agent;
    private float timer;
    private bool isFollowing;
    private bool hasLoggedSetupError;
    private bool hasLoggedDestinationError;
    private bool hasLoggedDestination;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    public bool SetFollowing(bool following)
    {
        if (following && (player == null || agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh))
        {
            Debug.LogWarning(
                "[NPCFollowPlayer] Takip için oyuncu, etkin NavMeshAgent ve NavMesh üzerindeki agent konumu gereklidir.",
                this);
            return false;
        }

        if (following)
        {
            agent.stoppingDistance = Mathf.Max(0f, followStoppingDistance);
            if (!agent.SetDestination(player.position))
            {
                Debug.LogWarning("[NPCFollowPlayer] Oyuncuya giden NavMesh hedefi kabul edilmedi.", this);
                return false;
            }
        }

        bool wasFollowing = isFollowing;
        isFollowing = following;
        timer = 0f;
        hasLoggedDestinationError = false;
        hasLoggedSetupError = false;
        hasLoggedDestination = following;

        if (wasFollowing != following)
        {
            Debug.Log(
                $"[NPCFollowPlayer] Following {(following ? "started" : "stopped")} for '{name}'. " +
                $"Player: {(player != null ? player.name : "unassigned")}; " +
                $"Agent enabled: {(agent != null && agent.isActiveAndEnabled)}; " +
                $"Agent on NavMesh: {(agent != null && agent.isOnNavMesh)}.",
                this);
        }

        if (!following && agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
        }

        return true;
    }

    private void Update()
    {
        if (!isFollowing)
        {
            return;
        }

        if (player == null || agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            if (!hasLoggedSetupError)
            {
                Debug.LogWarning(
                    "[NPCFollowPlayer] Takip için oyuncu, etkin NavMeshAgent ve NavMesh üzerindeki agent konumu gereklidir.",
                    this);
                hasLoggedSetupError = true;
            }

            return;
        }

        hasLoggedSetupError = false;
        timer += Time.deltaTime;
        if (timer < Mathf.Max(0.05f, updateInterval))
        {
            return;
        }

        timer = 0f;
        if (!agent.SetDestination(player.position))
        {
            if (!hasLoggedDestinationError)
            {
                Debug.LogWarning("[NPCFollowPlayer] Oyuncuya giden NavMesh hedefi kabul edilmedi.", this);
                hasLoggedDestinationError = true;
            }

            return;
        }

        if (!hasLoggedDestination)
        {
            Debug.Log(
                $"[NPCFollowPlayer] NavMesh destination accepted for '{name}'; distance: {Vector3.Distance(transform.position, player.position):F2}.",
                this);
            hasLoggedDestination = true;
        }

        hasLoggedDestinationError = false;
    }
}
