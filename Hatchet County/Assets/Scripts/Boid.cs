using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Hatchet County - Boid
/// Flocking-based movement using classic Reynolds boid rules.
/// Each frame the boid accumulates weighted steering forces and integrates
/// them into a flat (XZ) velocity, then Slerps the visual rotation to match.
///
/// Steering forces:
///   Separation  -- push away from boids closer than separationRadius
///   Alignment   -- match the average velocity of neighbours
///   Cohesion    -- pull toward the average position of neighbours
///   Avoidance   -- SphereCast forward and reflect off obstacle normals
///   Goal        -- state-specific force (hunt / retreat / roam)
///
/// States (priority order):
///   Retreating  -- health fraction <= retreatHealthFraction; flee from target
///   Hunting     -- target within detectionRange; close to preferredRange then strafe
///   Roaming     -- no target in range; drift between random waypoints
///
/// Neighbour discovery defaults to a per-frame O(n) scan. For large enemy
/// counts, build a BoidManager that partitions boids into a spatial grid and
/// calls SetNeighbours() on each boid instead.
/// </summary>
public class Boid : MonoBehaviour
{
    public enum BoidState { Roaming, Hunting, Retreating }

    [Header("References")]
    [Tooltip("Leave empty to auto-find the player by tag 'Player'.")]
    [SerializeField] private Transform target;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float maxSteerForce = 8f;
    [SerializeField] private float rotationSpeed = 6f;

    [Header("Flocking Radii")]
    [SerializeField] private float separationRadius = 1.5f;
    [SerializeField] private float alignmentRadius = 4f;
    [SerializeField] private float cohesionRadius = 5f;

    [Header("Flocking Weights")]
    [SerializeField] private float separationWeight = 2.5f;
    [SerializeField] private float alignmentWeight = 1f;
    [SerializeField] private float cohesionWeight = 1f;

    [Header("Target Tracking")]
    [Tooltip("Distance at which the boid starts hunting the target.")]
    [SerializeField] private float detectionRange = 12f;
    [Tooltip("Distance the boid tries to maintain from the target (combat spacing).")]
    [SerializeField] private float preferredRange = 2.5f;
    [SerializeField] private float trackingWeight = 3f;

    [Header("Retreat")]
    [Tooltip("Health fraction at which the enemy retreats (0 = never).")]
    [SerializeField] private float retreatHealthFraction = 0.2f;
    [SerializeField] private float retreatWeight = 4f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float avoidanceLookAhead = 2f;
    [SerializeField] private float avoidanceSphereRadius = 0.35f;
    [SerializeField] private float avoidanceWeight = 4f;

    [Header("Roam")]
    [Tooltip("How often (seconds) a new random roam destination is chosen.")]
    [SerializeField] private float roamInterval = 3f;
    [SerializeField] private float roamRadius = 8f;

    public BoidState State { get; private set; } = BoidState.Roaming;

    private Vector3 velocity;
    private Vector3 roamTarget;
    private float roamTimer;
    private List<Boid> neighbours = new List<Boid>();
    private EnemyCombat enemyCombat;

    private void Awake()
    {
        enemyCombat = GetComponent<EnemyCombat>();

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        velocity = transform.forward * moveSpeed;
        roamTarget = PickRoamTarget();
    }

    private void Update()
    {
        UpdateState();
        UpdateNeighbours();

        Vector3 steer = ComputeSteering();
        ApplyVelocity(steer);
        VisualRotation();
    }

    private void UpdateState()
    {
        if (retreatHealthFraction > 0f && enemyCombat != null)
        {
            float healthFrac = (float)enemyCombat.CurrentHealth / enemyCombat.MaxHealth;
            if (healthFrac <= retreatHealthFraction)
            {
                State = BoidState.Retreating;
                return;
            }
        }

        if (target != null && Vector3.Distance(transform.position, target.position) <= detectionRange)
            State = BoidState.Hunting;
        else
            State = BoidState.Roaming;
    }

    private void UpdateNeighbours()
    {
        neighbours.Clear();
        foreach (var b in FindObjectsByType<Boid>(FindObjectsSortMode.None))
        {
            if (b == this) continue;
            if (Vector3.Distance(transform.position, b.transform.position) <= cohesionRadius)
                neighbours.Add(b);
        }
    }

    public void SetNeighbours(List<Boid> list)
    {
        neighbours = list;
    }

    private Vector3 ComputeSteering()
    {
        Vector3 separation = ComputeSeparation() * separationWeight;
        Vector3 alignment = ComputeAlignment() * alignmentWeight;
        Vector3 cohesion = ComputeCohesion() * cohesionWeight;
        Vector3 avoidance = ComputeObstacleAvoidance() * avoidanceWeight;

        Vector3 goal = State switch
        {
            BoidState.Hunting => ComputeTargetSteering() * trackingWeight,
            BoidState.Retreating => ComputeRetreatSteering() * retreatWeight,
            _ => ComputeRoamSteering()
        };

        return separation + alignment + cohesion + avoidance + goal;
    }

    private Vector3 ComputeSeparation()
    {
        Vector3 steer = Vector3.zero;
        int count = 0;

        foreach (var b in neighbours)
        {
            float dist = Vector3.Distance(transform.position, b.transform.position);
            if (dist < separationRadius && dist > 0.001f)
            {
                steer += (transform.position - b.transform.position).normalized / dist;
                count++;
            }
        }

        return count > 0 ? Limit(steer / count, maxSteerForce) : Vector3.zero;
    }

    private Vector3 ComputeAlignment()
    {
        Vector3 avgVel = Vector3.zero;
        int count = 0;

        foreach (var b in neighbours)
        {
            if (Vector3.Distance(transform.position, b.transform.position) < alignmentRadius)
            {
                avgVel += b.velocity;
                count++;
            }
        }

        if (count == 0) return Vector3.zero;
        avgVel /= count;
        return Limit(avgVel - velocity, maxSteerForce);
    }

    private Vector3 ComputeCohesion()
    {
        Vector3 centre = Vector3.zero;
        int count = 0;

        foreach (var b in neighbours)
        {
            if (Vector3.Distance(transform.position, b.transform.position) < cohesionRadius)
            {
                centre += b.transform.position;
                count++;
            }
        }

        if (count == 0) return Vector3.zero;
        centre /= count;
        return Limit((centre - transform.position).normalized * moveSpeed - velocity, maxSteerForce);
    }

    private Vector3 ComputeTargetSteering()
    {
        if (target == null) return Vector3.zero;

        Vector3 toTarget = target.position - transform.position;
        float dist = toTarget.magnitude;

        EnemyCombat combat = GetComponent<EnemyCombat>();
        float stopDist = combat != null ? combat.StrikeRange : preferredRange;

        if (dist <= stopDist)
            return Limit(-velocity, maxSteerForce);

        Vector3 desired = dist > preferredRange
            ? toTarget.normalized * moveSpeed
            : toTarget.normalized * moveSpeed * 0.25f;

        return Limit(desired - velocity, maxSteerForce);
    }

    private Vector3 ComputeRetreatSteering()
    {
        if (target == null) return Vector3.zero;

        Vector3 away = (transform.position - target.position).normalized * moveSpeed;
        return Limit(away - velocity, maxSteerForce);
    }

    private Vector3 ComputeRoamSteering()
    {
        roamTimer -= Time.deltaTime;
        if (roamTimer <= 0f || Vector3.Distance(transform.position, roamTarget) < 0.6f)
        {
            roamTarget = PickRoamTarget();
            roamTimer = roamInterval;
        }

        Vector3 desired = (roamTarget - transform.position).normalized * moveSpeed;
        return Limit(desired - velocity, maxSteerForce);
    }

    private Vector3 PickRoamTarget()
    {
        Vector2 rnd = Random.insideUnitCircle * roamRadius;
        return transform.position + new Vector3(rnd.x, 0f, rnd.y);
    }

    private Vector3 ComputeObstacleAvoidance()
    {
        if (velocity.sqrMagnitude < 0.01f) return Vector3.zero;

        Ray ray = new Ray(transform.position, velocity.normalized);
        if (Physics.SphereCast(ray, avoidanceSphereRadius, out RaycastHit hit,
                               avoidanceLookAhead, obstacleLayer))
        {
            Vector3 avoid = Vector3.Reflect(velocity.normalized, hit.normal);
            return Limit(avoid * moveSpeed - velocity, maxSteerForce);
        }

        return Vector3.zero;
    }

    private void ApplyVelocity(Vector3 steerForce)
    {
        velocity += steerForce * Time.deltaTime;
        velocity = Limit(velocity, moveSpeed);
        velocity.y = 0f;

        transform.position += velocity * Time.deltaTime;
    }

    private void VisualRotation()
    {
        if (velocity.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(velocity.normalized) * Quaternion.Euler(0f, 180f, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                                                rotationSpeed * Time.deltaTime);
    }

    private static Vector3 Limit(Vector3 v, float max)
    {
        return v.sqrMagnitude > max * max ? v.normalized * max : v;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, preferredRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, separationRadius);

        Gizmos.color = Color.white;
        Gizmos.DrawRay(transform.position, velocity);
    }
#endif
}