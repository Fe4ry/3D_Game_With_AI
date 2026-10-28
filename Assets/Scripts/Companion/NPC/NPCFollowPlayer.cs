using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCFollowPlayer : MonoBehaviour
{
    [Header("Takip Hedefi")]
    [SerializeField] private Transform player;

    [Header("Takip Ayarları")]
    [SerializeField, Min(0.05f)]
    private float updateInterval = 0.2f;

    [SerializeField, Min(0.5f)]
    private float stoppingDistance = 1.8f;

    [Header("Animasyon")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField, Min(0.01f)]
    private float animationDampTime = 0.1f;

    private NavMeshAgent agent;
    private float updateTimer;
    private bool isFollowing;

    public bool IsFollowing => isFollowing;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        // Otomatik Animator Bulma
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // Root Motion'ı kod seviyesinde zorunlu olarak kapatıyoruz (Kopmayı önler)
        if (animator != null)
        {
            animator.applyRootMotion = false;
        }

        // NavMeshAgent Temel Ayarları
        agent.stoppingDistance = stoppingDistance;
        agent.updateRotation = true; // Rotasyonu NavMeshAgent yön yönetsin
        agent.isStopped = true;
    }

   
private void Start()
{
    FindPlayerIfNeeded();
    SetFollowing(false);
}


    private void Update()
    {
        if (agent == null)
            return;

        // NavMeshAgent'ın anlık hızını Blend Tree'deki "Speed" parametresine aktar
        if (animator != null && animator.isActiveAndEnabled)
        {
            float movementSpeed = agent.velocity.magnitude;

            animator.SetFloat(
                speedParameter,
                movementSpeed,
                animationDampTime,
                Time.deltaTime
            );
        }

        if (!isFollowing || player == null || !agent.isOnNavMesh)
            return;

        updateTimer += Time.deltaTime;

        if (updateTimer < updateInterval)
            return;

        updateTimer = 0f;

        float distance = Vector3.Distance(transform.position, player.position);

        // Durma mesafesi kontrolü
        if (distance <= stoppingDistance)
        {
            if (!agent.isStopped)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            return;
        }

        // Takip devam ediyor
        if (agent.isStopped)
        {
            agent.isStopped = false;
        }

        agent.SetDestination(player.position);
    }

    public void SetPlayer(Transform target)
    {
        player = target;
    }

    public void SetFollowing(bool follow)
    {
        if (agent == null || !agent.isOnNavMesh)
            return;

        if (follow && player == null)
            FindPlayerIfNeeded();

        if (follow && player == null)
            return;

        isFollowing = follow;
        updateTimer = updateInterval;

        if (follow)
        {
            agent.stoppingDistance = stoppingDistance;
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }
        else
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    private void FindPlayerIfNeeded()
    {
        if (player != null)
            return;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
            player = playerObject.transform;
    }
}