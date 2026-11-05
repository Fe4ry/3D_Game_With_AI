
using UnityEngine;

public class NPCSensor : MonoBehaviour
{
    [Header("Sensor Settings")]
    [SerializeField] private Transform sensorOrigin;
    [SerializeField] private float detectionDistance = 3f;
    [SerializeField] private LayerMask obstacleLayers = ~0;

    private RaycastHit _hit;

    public bool HasObstacle { get; private set; }
    public float ObstacleDistance { get; private set; }
    public GameObject DetectedObject { get; private set; }

    private void Update()
    {
        DetectObstacle();
    }

    private void DetectObstacle()
    {
        Vector3 origin = sensorOrigin != null
            ? sensorOrigin.position
            : transform.position + Vector3.up * 0.5f;

        Vector3 direction = sensorOrigin != null
    ? sensorOrigin.forward
    : transform.forward;

        HasObstacle = Physics.Raycast(
            origin,
            direction,
            out _hit,
            detectionDistance,
            obstacleLayers,
            QueryTriggerInteraction.Ignore
        );

        if (HasObstacle)
        {
            ObstacleDistance = _hit.distance;
            DetectedObject = _hit.collider.gameObject;
        }
        else
        {
            ObstacleDistance = 0f;
            DetectedObject = null;
        }

        Debug.DrawRay(
            origin,
            direction * detectionDistance,
            HasObstacle ? Color.red : Color.green
        );
    }


    public bool CanDetect(Transform target)
{
    if (target == null)
        return false;

    Vector3 origin = sensorOrigin != null
        ? sensorOrigin.position
        : transform.position + Vector3.up * 0.5f;

    Collider targetCollider = target.GetComponentInChildren<Collider>();

    Vector3 targetPoint = targetCollider != null
        ? targetCollider.bounds.center
        : target.position;

    Vector3 direction = (targetPoint - origin).normalized;

    if (Physics.Raycast(
        origin,
        direction,
        out RaycastHit hit,
        detectionDistance,
        ~0,
        QueryTriggerInteraction.Ignore))
    {
        return hit.transform == target || hit.transform.IsChildOf(target);
    }

    return false;
}
}
