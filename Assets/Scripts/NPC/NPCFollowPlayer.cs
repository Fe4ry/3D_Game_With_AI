
using UnityEngine;
using UnityEngine.AI;

public class NPCFollowPlayer : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float updateInterval = 0.2f;

    private NavMeshAgent agent;
    private float timer;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

private void Update()
{Debug.LogWarning("UPDATE TEST 123");
Debug.LogWarning(
    $"Agent konumu: {agent.transform.position}, " +
    $"OnNavMesh: {agent.isOnNavMesh}"
);
    if (player == null)
    {
        Debug.LogWarning("PLAYER NULL");
        return;
    }
Debug.LogWarning("UPDATE TEST 123");
    if (agent == null)
    {
        Debug.LogWarning("AGENT NULL");
        return;
    }

    if (!agent.isOnNavMesh)
    {
        Debug.LogWarning("AGENT NAVMESH ÜZERİNDE DEĞİL");
        return;
    }

    timer += Time.deltaTime;
Debug.LogWarning("UPDATE TEST 123");
    if (timer >= updateInterval)
    {
        Debug.Log($"Oyuncu hedef konumu: {player.position}");
Debug.Log($"Player: {player.name} | Konum: {player.position}");
        bool accepted = agent.SetDestination(player.position);
        Debug.Log(
            $"Accepted: {accepted}, " +
            $"OnNavMesh: {agent.isOnNavMesh}, " +
            $"PathPending: {agent.pathPending}, " +
            $"HasPath: {agent.hasPath}, " +
            $"PathStatus: {agent.pathStatus}, " +
            $"Remaining: {agent.remainingDistance}, " +
            $"Speed: {agent.velocity.magnitude}"
        );

        timer = 0f;
    }
}
    
}

