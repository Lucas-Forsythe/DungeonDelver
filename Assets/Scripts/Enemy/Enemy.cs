using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("Loot Drop Settings")]

    [SerializeField]
    private GameObject[] LootPrefabs;

    [SerializeField]
    private float[] LootChances;

    [SerializeField]
    private Vector3 LootSpawnOffset = new Vector3(0f, 0.25f, 0f);

    [Header("Health & Death Settings")]
    [SerializeField] public float MaxHealth = 100f;
    [SerializeField] public Color FlashColor = Color.red;
    [SerializeField] private float FallDuration = 0.5f;
    [SerializeField] public float FlashDuration = 0.15f;
    [SerializeField] public float EmissionIntensity = 2f;
    [SerializeField] public float DeathRotation = 90f;
    [SerializeField] public float TimeTilModelDestruction = 1f;
    [SerializeField] public ParticleSystem DeathParticles;

    private float CurrentHealth;
    private bool isDead;
    private Renderer[] Renderers;
    private Color[] OriginalColors;
    private Coroutine FlashRoutine;
    private Rigidbody rb;

    //AI Settings
    private NavMeshAgent Agent;
    public int CurrentPointIndex = 0;
    public Vector3 CurrentTarget;
    public float PositionThreshold = 0.5f;
    private float IdleTimerCounter;
    private Transform PlayerTransform;
    private bool CanSeePlayer;
    private Vector3 LastKnownPlayerPosition;

    //Assign in the Inspector, or leave empty to auto-fill from the object tagged "PatrolPoint"
    public Transform[] PatrolPoints;

    [Header("AI Settings")]
    [SerializeField] public float IdleTime = 5f;
    [SerializeField] public float AttackDistance = 5f;
    [SerializeField] public float VisionDistance = 10f;
    [SerializeField] public float MinChasingHealth = 80f; //Below this health the enemy flees instead of fighting

    [Header("Flee Settings")]
    [SerializeField] public float FleeChancePercent = 10f; //Chance (0-100) to flee when health first drops below the threshold
    [SerializeField] public float FleeDuration = 5f; //Seconds before the enemy stops fleeing
    [SerializeField] public float FleeDistance = 15f; //How far to run / how far away counts as safe
    [SerializeField] public float FleeSpeedMultiplier = 1.3f;
    [SerializeField] public float FleeRepathInterval = 0.5f;

    private float baseSpeed;
    private float fleeRepathTimer;
    private float fleeTimer;

    //Rolled once, the first time health drops below the threshold
    private bool hasRolledFlee;
    private bool willFlee;

    private bool ShouldFlee => willFlee;

    public enum State
    {
        Idle, Patrolling, Chasing, Attacking, Fleeing
    }
    public State state = State.Idle;

    private void Awake()
    {
        //Set the current health to max health on awake
        CurrentHealth = MaxHealth;

        rb = GetComponent<Rigidbody>();

        //Make the Rigidbody kinematic so physics doesn't fight the NavMeshAgent
        if (rb != null)
            rb.isKinematic = true;

        Agent = GetComponent<NavMeshAgent>();
        if (Agent != null)
            baseSpeed = Agent.speed;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            PlayerTransform = player.transform;
        else
            Debug.LogError($"{name}: no object tagged 'Player' found.", this);

        //Only auto-fill patrol points if none were assigned in the Inspector
        if (PatrolPoints == null || PatrolPoints.Length == 0)
        {
            GameObject PatrolPointParent = GameObject.FindGameObjectWithTag("PatrolPoint");
            if (PatrolPointParent != null)
            {
                PatrolPoints = PatrolPointParent.GetComponentsInChildren<Transform>()
                    .Where(p => p != PatrolPointParent.transform)
                    .ToArray();
            }
        }

        if (PatrolPoints != null && PatrolPoints.Length > 0)
        {
            //Random start so enemies don't all head to the same point
            CurrentPointIndex = Random.Range(0, PatrolPoints.Length);
            CurrentTarget = PatrolPoints[CurrentPointIndex].position;
        }
        else
        {
            Debug.LogWarning($"{name}: no patrol points assigned or found.", this);
        }

        //Grab every renderer on this enemy and its children
        Renderers = GetComponentsInChildren<Renderer>();
        OriginalColors = new Color[Renderers.Length];

        for (int i = 0; i < Renderers.Length; i++)
        {
            OriginalColors[i] = Renderers[i].material.color;
        }
    }

    //Called by the Meele script
    public void Attack(float Amount)
    {
        if (isDead) return;

        CurrentHealth -= Amount;

        //First time below the threshold: roll the dice once to decide if this enemy runs
        if (!hasRolledFlee && CurrentHealth < MinChasingHealth)
        {
            hasRolledFlee = true;
            willFlee = Random.Range(0f, 100f) < FleeChancePercent;
        }

        if (FlashRoutine != null)
            StopCoroutine(FlashRoutine);

        if (CurrentHealth <= 0f)
        {
            Die();
            return;
        }

        //Hurt badly enough? Run, even if the player isn't visible (e.g. hit from behind)
        if (ShouldFlee && state != State.Fleeing)
        {
            StartFleeing();
        }

        FlashRoutine = StartCoroutine(Flash());
    }

    IEnumerator Flash()
    {
        //Turn red + glow
        for (int i = 0; i < Renderers.Length; i++)
        {
            Material mat = Renderers[i].material;
            mat.color = FlashColor;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", FlashColor * EmissionIntensity);
        }

        yield return new WaitForSeconds(FlashDuration);

        //Back to normal
        for (int i = 0; i < Renderers.Length; i++)
        {
            Material mat = Renderers[i].material;
            mat.color = OriginalColors[i];
            mat.SetColor("_EmissionColor", Color.black);
        }

        FlashRoutine = null;
    }

    private void Die()
    {
        isDead = true;

        // Stop the NavMeshAgent
        if (Agent != null)
            Agent.enabled = false;

        // Stop all enemy animations so the enemy cannot stand back up
        Animator[] animators = GetComponentsInChildren<Animator>();

        foreach (Animator anim in animators)
        {
            anim.enabled = false;
        }

        // Stop physics from interfering with the fall
        if (rb != null)
            rb.isKinematic = true;

        // Start the death sequence
        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        Quaternion startRotation = transform.rotation;

        // Rotate backwards around the enemy's local right axis
        Quaternion targetRotation =
            Quaternion.AngleAxis(-DeathRotation, transform.right) * startRotation;

        float elapsed = 0f;

        // Smoothly fall backwards
        while (elapsed < FallDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / FallDuration);

            // Smooth the movement
            t = Mathf.SmoothStep(0f, 1f, t);

            transform.rotation = Quaternion.Slerp(
                startRotation,
                targetRotation,
                t
            );

            yield return null;
        }

        // Make sure the enemy stays down
        transform.rotation = targetRotation;

        // Wait for the remaining death time
        float remainingTime = Mathf.Max(0f, TimeTilModelDestruction - FallDuration);

        yield return new WaitForSeconds(remainingTime);

        // Play death particles
        SpawnDeathParticles();

        // Drop loot
        DropLoot();

        // Destroy enemy
        Destroy(gameObject);
    }

    void BlowUp()
    {
        SpawnDeathParticles();
        Destroy(gameObject);
    }

    void SpawnDeathParticles()
    {
        if (DeathParticles == null) return;

        ParticleSystem instance = Instantiate(DeathParticles, transform.position, Quaternion.identity);
        instance.Play();

        var main = instance.main;
        Destroy(instance.gameObject, main.duration + main.startLifetime.constantMax);
    }

    private void LookForPlayer()
    {
        if (PlayerTransform == null) return;

        Vector3 directionToPlayer = PlayerTransform.position - transform.position;

        if (Physics.Raycast(transform.position, directionToPlayer.normalized, out RaycastHit hit, VisionDistance))
        {
            CanSeePlayer = hit.collider.CompareTag("Player");
        }
        else
        {
            CanSeePlayer = false;
        }

        //Seeing the player must never override fleeing: weak enemies run, healthy ones chase
        if (CanSeePlayer && state != State.Attacking && state != State.Fleeing)
        {
            if (ShouldFlee)
            {
                StartFleeing();
            }
            else
            {
                state = State.Chasing;
            }
        }
    }

    private void Idle()
    {
        Agent.ResetPath();
        IdleTimerCounter -= Time.deltaTime;
        if (IdleTimerCounter <= 0f)
        {
            IdleTimerCounter = IdleTime;
            state = State.Patrolling;
        }
    }

    private void SetNextPatrolTarget()
    {
        CurrentPointIndex = (CurrentPointIndex + 1) % PatrolPoints.Length;
        CurrentTarget = PatrolPoints[CurrentPointIndex].position;
    }

    private void Patrolling()
    {
        //Nothing to patrol to, so just idle instead of throwing errors every frame
        if (PatrolPoints == null || PatrolPoints.Length == 0)
        {
            state = State.Idle;
            return;
        }

        //Ignore height so the pivot offset doesn't stop the enemy from "reaching" the patrol point
        Vector3 FlatOffset = CurrentTarget - transform.position;
        FlatOffset.y = 0f;

        //The agent stops at its Stopping Distance, so the arrival check must be at least that large
        float arriveDistance = Mathf.Max(PositionThreshold, Agent.stoppingDistance + 0.2f);

        if (FlatOffset.magnitude < arriveDistance)
        {
            float chance = Random.Range(0f, 100f);

            if (chance < 10f)
            {
                state = State.Idle;
                return;
            }

            SetNextPatrolTarget();
        }
        else
        {
            Agent.SetDestination(CurrentTarget);

            //If the point can't be fully reached (off the NavMesh, blocked), skip it instead of getting stuck
            if (!Agent.pathPending && Agent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                SetNextPatrolTarget();
            }
        }
    }

    private void Attacking()
    {
        IdleTimerCounter = IdleTime;
        Agent.ResetPath();

        //Too hurt to keep fighting
        if (ShouldFlee)
        {
            StartFleeing();
            return;
        }

        //Attack the player
        if (Vector3.Distance(transform.position, PlayerTransform.position) > AttackDistance || !CanSeePlayer)
        {
            state = State.Chasing;
        }
    }

    private void Chasing()
    {
        IdleTimerCounter = IdleTime;
        Agent.SetDestination(LastKnownPlayerPosition);
        if (ShouldFlee)
        {
            StartFleeing();
        }
        else if (Vector3.Distance(transform.position, PlayerTransform.position) <= AttackDistance && CanSeePlayer)
        {
            state = State.Attacking;
        }
        else if (Vector3.Distance(transform.position, PlayerTransform.position) > VisionDistance)
        {
            state = State.Patrolling;
        }
        else if (Vector3.Distance(transform.position, PlayerTransform.position) < PositionThreshold && !CanSeePlayer)
        {
            state = State.Patrolling;
        }
    }

    private void StartFleeing()
    {
        state = State.Fleeing;
        fleeTimer = FleeDuration;
        fleeRepathTimer = 0f;
    }

    private void Fleeing()
    {
        IdleTimerCounter = IdleTime;

        //Ran for long enough: calm down and stop fleeing (it won't flee again, so it can chase/fight)
        fleeTimer -= Time.deltaTime;
        if (fleeTimer <= 0f)
        {
            willFlee = false;
            Agent.ResetPath();
            state = State.Patrolling;
            return;
        }

        float distToPlayer = Vector3.Distance(transform.position, PlayerTransform.position);

        //Safe: out of sight and far enough away, go back to patrolling
        if (!CanSeePlayer && distToPlayer > FleeDistance)
        {
            state = State.Patrolling;
            return;
        }

        fleeRepathTimer -= Time.deltaTime;
        if (fleeRepathTimer > 0f) return;
        fleeRepathTimer = FleeRepathInterval;

        //Run directly away from the player, trying angled directions if that spot isn't on the NavMesh
        Vector3 away = transform.position - PlayerTransform.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = transform.forward;
        away.Normalize();

        float[] angles = { 0f, 45f, -45f, 90f, -90f };
        foreach (float angle in angles)
        {
            Vector3 candidate = transform.position + Quaternion.Euler(0f, angle, 0f) * away * FleeDistance;
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                Agent.SetDestination(hit.position);
                return;
            }
        }
        //No valid spot found (cornered): keep the current destination
    }

    private void LookAtPlayer()
    {
        if (CanSeePlayer)
        {
            transform.LookAt(new Vector3(PlayerTransform.position.x, transform.position.y, PlayerTransform.position.z));
        }
    }

    private void SetLastKnowPlayerPosition()
    {
        if (CanSeePlayer)
        {
            LastKnownPlayerPosition = PlayerTransform.position;
        }
    }

    private void Update()
    {
        if (isDead || PlayerTransform == null || Agent == null) return;

        LookForPlayer();

        SetLastKnowPlayerPosition();

        switch (state)
        {
            case State.Idle:
                Idle();
                break;

            case State.Attacking:
                Attacking();
                break;

            case State.Patrolling:
                Patrolling();
                break;

            case State.Chasing:
                Chasing();
                break;

            case State.Fleeing:
                Fleeing();
                break;
        }

        //Run faster while fleeing
        Agent.speed = state == State.Fleeing ? baseSpeed * FleeSpeedMultiplier : baseSpeed;

        //Don't turn to face the player while running away
        if (state != State.Fleeing)
            LookAtPlayer();
    }

    private void DropLoot()
    {
        if (LootPrefabs == null || LootChances == null)
            return;

        int itemCount = Mathf.Min(LootPrefabs.Length, LootChances.Length);

        float totalChance = 0f;

        // Calculate the total drop chance.
        for (int i = 0; i < itemCount; i++)
        {
            if (LootPrefabs[i] != null)
            {
                totalChance += Mathf.Max(0f, LootChances[i]);
            }
        }

        if (totalChance <= 0f)
            return;

        // If total chance is below 100, the remaining chance means no drop.
        // If above 100, chances act as relative weights.
        float roll = Random.Range(0f, Mathf.Max(100f, totalChance));

        float currentChance = 0f;

        for (int i = 0; i < itemCount; i++)
        {
            if (LootPrefabs[i] == null)
                continue;

            currentChance += Mathf.Max(0f, LootChances[i]);

            if (roll < currentChance)
            {
                Instantiate(
                    LootPrefabs[i],
                    transform.position + LootSpawnOffset,
                    LootPrefabs[i].transform.rotation
                );

                Debug.Log("Enemy dropped: " + LootPrefabs[i].name);
                return;
            }
        }

        Debug.Log("Enemy dropped no loot.");
    }
}