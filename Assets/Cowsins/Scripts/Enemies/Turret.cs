using UnityEngine;

namespace cowsins
{
    public class Turret : MonoBehaviour
    {
        public enum TargetType
        {
            Player,
            Enemies
        }

        public enum RangeShape
        {
            Sphere,
            Cube,
            Cone
        }

        [SerializeField, Title("Target Settings"), Tooltip("Choose whether this turret targets the Player or Enemies.")]
        private TargetType targetType = TargetType.Player;

        [SerializeField] private bool displayGizmos = true;
        [SerializeField] private Animator animator;
        [SerializeField, Tooltip("The part of the turret that rotates.")] private Transform turretHead;

        [SerializeField, Title("Range Shape"), Tooltip("Select the detection range shape.")]
        private RangeShape rangeShape = RangeShape.Sphere;

        [SerializeField, Tooltip("Distance within the player is detectable (Sphere mode).")]
        private float detectionRadius = 10f;

        [SerializeField, Tooltip("Bounds of the cube detection area.")]
        private Vector3 cubeBounds = new Vector3(10f, 10f, 10f);

        [SerializeField, Tooltip("Maximum distance of cone detection.")]
        private float coneRange = 15f;
        [SerializeField, Tooltip("Angle of the cone in degrees."), Range(0f, 180f)]
        private float coneAngle = 60f;
        [SerializeField, Tooltip("If true, cone uses a fixed direction. If false, cone rotates with turret head.")]
        private bool coneStaticDirection = false;
        [SerializeField, Tooltip("Horizontal rotation angle (0-360 degrees)."), Range(0f, 360f)]
        private float coneHorizontalAngle = 0f;
        [SerializeField, Tooltip("Vertical rotation angle (-90 to 90 degrees)."), Range(-90f, 90f)]
        private float coneVerticalAngle = 0f;

        [SerializeField, Tooltip("Enable vertical movement."), Title("Basic Settings")]
        private bool allowVerticalMovement = false;
        [SerializeField, Tooltip("Speed of rotation interpolation.")] private float lerpSpeed = 5f;
        [SerializeField, Tooltip("Check for line of sight to target. If blocked, turret won't track.")]
        private bool requireLineOfSight = false;
        [SerializeField, Tooltip("Layers that block line of sight.")]
        private LayerMask obstacleLayers;

        [SerializeField, Title("Projectile Settings")] private GameObject projectilePrefab;
        [SerializeField, Min(0)] private float projectileSpeed, projectileDamage, projectileDuration;
        [SerializeField] private Transform firePoint;
        [SerializeField] private GameObject muzzleFlash;
        [SerializeField, Tooltip("Shots per second."), Title("Shooting")] private float fireRate = 2f;

        [SerializeField, Title("Pool Settings")] private int projectilePoolSize;

        private bool canShoot = false;
        private float fireCooldown = 0f;
        private Transform target;

        Vector3 targetDirection;
        Quaternion targetRotation;

        private void Start()
        {
            InitializeTarget();
            PoolManager.Instance.RegisterPool(projectilePrefab, projectilePoolSize);
            PoolManager.Instance.RegisterPool(muzzleFlash, projectilePoolSize);
        }

        private void Update()
        {
            UpdateTarget();
            if (target == null) return;

            Vector3 targetDir = target.position - transform.position;
            if (!allowVerticalMovement) targetDir.y = 0f;

            if (IsTargetInRange(target.position) && HasLineOfSight(target.position))
            {
                AimAtTarget(targetDir);
                Shoot(targetDir);
            }
            else
            {
                canShoot = false;
                fireCooldown = fireRate;
            }
        }

        private void InitializeTarget()
        {
            if (targetType == TargetType.Player)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null) target = player.transform;
            }
        }

        private void UpdateTarget()
        {
            if (targetType == TargetType.Enemies)
            {
                FindClosestEnemy();
            }
        }

        private bool IsTargetInRange(Vector3 targetPosition)
        {
            switch (rangeShape)
            {
                case RangeShape.Sphere:
                    return IsInSphereRange(targetPosition);

                case RangeShape.Cube:
                    return IsInCubeRange(targetPosition);

                case RangeShape.Cone:
                    return IsInConeRange(targetPosition);

                default:
                    return false;
            }
        }

        private bool IsInSphereRange(Vector3 targetPosition)
        {
            float distance = Vector3.Distance(transform.position, targetPosition);
            return distance <= detectionRadius;
        }

        private bool IsInCubeRange(Vector3 targetPosition)
        {
            Vector3 localPos = transform.InverseTransformPoint(targetPosition);
            return Mathf.Abs(localPos.x) <= cubeBounds.x / 2f &&
                   Mathf.Abs(localPos.y) <= cubeBounds.y / 2f &&
                   Mathf.Abs(localPos.z) <= cubeBounds.z / 2f;
        }

        private bool IsInConeRange(Vector3 targetPosition)
        {
            Vector3 directionToTarget = targetPosition - transform.position;
            float distance = directionToTarget.magnitude;

            if (distance > coneRange) return false;

            Vector3 forward = GetConeForwardDirection();
            float angle = Vector3.Angle(forward, directionToTarget);

            return angle <= coneAngle / 2f;
        }

        private Vector3 GetConeForwardDirection()
        {
            if (coneStaticDirection)
            {
                Quaternion rotation = Quaternion.Euler(coneVerticalAngle, coneHorizontalAngle, 0f);
                return transform.TransformDirection(rotation * Vector3.forward);
            }
            else
            {
                return turretHead != null ? turretHead.forward : transform.forward;
            }
        }

        private bool HasLineOfSight(Vector3 targetPosition)
        {
            if (!requireLineOfSight) return true;

            Vector3 direction = targetPosition - transform.position;
            float distance = direction.magnitude;

            if (Physics.Raycast(transform.position, direction.normalized, out RaycastHit hit, distance, obstacleLayers))
            {
                // line of sight blocked
                return false;
            }

            return true;
        }

        private void AimAtTarget(Vector3 direction)
        {
            canShoot = true;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            turretHead.rotation = Quaternion.RotateTowards(turretHead.rotation, targetRotation, lerpSpeed * 100f * Time.deltaTime);
        }

        private void Shoot(Vector3 direction)
        {
            fireCooldown -= Time.deltaTime;
            if (!canShoot || fireCooldown > 0) return;

            fireCooldown = fireRate;
            animator?.SetTrigger("Fire");
            PoolManager.Instance.GetFromPool(muzzleFlash, firePoint.position, firePoint.rotation);

            var projGO = PoolManager.Instance.GetFromPool(projectilePrefab, firePoint.position, Quaternion.identity);
            var projectile = projGO.GetComponent<TurretProjectile>();
            projectile.Initialize(targetType, direction.normalized, projectileDamage, projectileSpeed, projectileDuration, obstacleLayers, ReturnProjectileToPool);
        }

        private void ReturnProjectileToPool(TurretProjectile proj)
        {
            proj.destroyEvent.RemoveAllListeners();
            PoolManager.Instance?.ReturnToPool(proj.gameObject, projectilePrefab);
        }

        private void FindClosestEnemy()
        {
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
            float minDistance = Mathf.Infinity;
            target = null;

            foreach (var e in enemies)
            {
                if (e == this.gameObject) continue;

                EnemyHealth enemyHealth = e.GetComponent<EnemyHealth>();
                if (enemyHealth == null) continue;

                // Skip dead enemies
                if (enemyHealth.IsDead) continue;

                // Check if enemy is in range before calculating distance
                if (!IsTargetInRange(e.transform.position)) continue;

                // Check line of sight
                if (!HasLineOfSight(e.transform.position)) continue;

                float dist = Vector3.Distance(transform.position, e.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    target = e.transform;
                }
            }
        }

        // Draw Gizmos
        private void OnDrawGizmosSelected()
        {
            if (!displayGizmos) return;

            Gizmos.color = Color.yellow;

            switch (rangeShape)
            {
                case RangeShape.Sphere:
                    DrawSphereGizmo();
                    break;

                case RangeShape.Cube:
                    DrawCubeGizmo();
                    break;

                case RangeShape.Cone:
                    DrawConeGizmo();
                    break;
            }
        }

        private void DrawSphereGizmo()
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, Vector3.one);
            Gizmos.DrawWireSphere(Vector3.zero, detectionRadius);
        }

        private void DrawCubeGizmo()
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, cubeBounds);
        }

        private void DrawConeGizmo()
        {
            Vector3 forward;

            if (coneStaticDirection)
            {
                Quaternion rotation = Quaternion.Euler(coneVerticalAngle, coneHorizontalAngle, 0f);
                forward = transform.TransformDirection(rotation * Vector3.forward);
            }
            else
            {
                forward = turretHead != null ? turretHead.forward : transform.forward;
            }

            Vector3 origin = transform.position;

            // Calculate cone lines
            int segments = 16;
            float halfAngle = coneAngle / 2f;

            Vector3 perpendicular = Vector3.Cross(forward, Vector3.up);
            if (perpendicular.sqrMagnitude < 0.001f)
            {
                perpendicular = Vector3.Cross(forward, Vector3.right);
            }
            perpendicular.Normalize();

            Vector3[] directions = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float angle = (i / (float)segments) * 360f;

                Vector3 rotated = Quaternion.AngleAxis(angle, forward) * perpendicular;
                Vector3 direction = Quaternion.AngleAxis(halfAngle, rotated) * forward;
                directions[i] = direction;
            }

            // Draw cone circle
            for (int i = 0; i < segments; i++)
            {
                Gizmos.DrawLine(origin + directions[i] * coneRange,
                              origin + directions[i + 1] * coneRange);
            }

            // Draw lines from origin to cone edge
            for (int i = 0; i <= segments; i += segments / 4)
            {
                Gizmos.DrawLine(origin, origin + directions[i] * coneRange);
            }

            Gizmos.DrawLine(origin, origin + forward * coneRange);
        }
    }
}