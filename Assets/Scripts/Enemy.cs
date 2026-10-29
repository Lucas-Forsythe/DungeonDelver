using System.Collections;
using Unity.VisualScripting;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using NUnit.Framework.Constraints;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("Health & Death Settings")]
    [SerializeField] public float MaxHealth = 100f;
    [SerializeField] public Color FlashColor = Color.red;
    [SerializeField] public float FlashDuration = 0.15f;
    [SerializeField] public float EmissionIntensity = 2f;
    [SerializeField] public float DeathRotation = 90f;
    [SerializeField] public float TimeTilModelDestruction = 1f;
    [SerializeField] public ParticleSystem DeathParticles;


    private float CurrentHealth;
    private Renderer[] Renderers;
    private Color[] OriginalColors;
    private Coroutine FlashRoutine;
    private Rigidbody rb;

    //AI Settings
    private NavMeshAgent Agent;
    public int CurrentPointIndex = 0;
    public Vector3 CurrentTarget;
    public float PositionThreshold = 0.5f; //Was 0, which meant the enemy could never "reach" a patrol point
    private float IdleTimerCounter;
    private Transform PlayerTransform;
    private bool CanSeePlayer;
    private Vector3 LastKnownPlayerPosition;
    public Transform[] PatrolPoints;

    [Header("AI Settings")]
    [SerializeField] public float IdleTime = 5f;
    [SerializeField] public float AttackDistance = 5f;
    [SerializeField] public float VisionDistance = 10f;
    [SerializeField] public float MinChasingHealth = 80f;

    public enum State
    {
        Idle, Patrolling, Chasing, Attacking
    }
    public State state = State.Idle;

    private void Awake()
    {
        //Set the current health to max health on awake
        CurrentHealth = MaxHealth;

        rb = GetComponent<Rigidbody>();

        //Make the Rigidbody kinematic so physics doesn't fight the NavMeshAgent (this was pushing the enemy into the floor)
        rb.isKinematic = true;

        Agent = GetComponent<NavMeshAgent>();
        PlayerTransform = GameObject.FindGameObjectWithTag("Player").transform;
        GameObject PatrolPointParent = GameObject.FindGameObjectWithTag("PatrolPoint");
        PatrolPoints = PatrolPointParent.GetComponentsInChildren<Transform>().Where(p => p != PatrolPointParent.transform).ToArray();

        //Start with a real patrol point instead of the world origin (0,0,0)
        if (PatrolPoints.Length > 0)
        {
            CurrentTarget = PatrolPoints[CurrentPointIndex % PatrolPoints.Length].position;
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
        CurrentHealth -= Amount;

        if (FlashRoutine != null)
            StopCoroutine(FlashRoutine);

        if (CurrentHealth <= 0f)
        {
            Die();
            return;
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
        //Stop the AI and the agent so they don't fight the fall rotation
        Agent.enabled = false;
        enabled = false;

        //Let them fall over and rotate to the ground before destroying the model
        rb.freezeRotation = false;
        transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x + DeathRotation, transform.rotation.eulerAngles.y, transform.rotation.eulerAngles.z);
        Invoke("BlowUp", TimeTilModelDestruction);
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
        Vector3 directionToPlayer = PlayerTransform.position - transform.position;

        if (Physics.Raycast(transform.position, directionToPlayer.normalized, out RaycastHit hit, VisionDistance))
        {
            if (hit.collider.CompareTag("Player"))
            {
                CanSeePlayer = true;
            }
            else
            {
                CanSeePlayer = false;
            }
        }
        else
        {
            CanSeePlayer = false;
        }

        if (CanSeePlayer && state != State.Attacking)
        {
            state = State.Chasing;
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

    private void Patrolling()
    {
        //Ignore height so the pivot offset doesn't stop the enemy from "reaching" the patrol point
        Vector3 FlatOffset = CurrentTarget - transform.position;
        FlatOffset.y = 0f;

        if (FlatOffset.magnitude < PositionThreshold)
        {
            float chance = Random.Range(0f, 100f);

            if (chance < 10f)
            {
                state = State.Idle;
                return;
            }

            CurrentPointIndex++;
            CurrentTarget = PatrolPoints[CurrentPointIndex % PatrolPoints.Length].position;
        }

        else
        {
            Agent.SetDestination(CurrentTarget);
        }
    }
    private void Attacking()
    {
        IdleTimerCounter = IdleTime;
        Agent.ResetPath();

        //Attack the player
        if (Vector3.Distance(transform.position, PlayerTransform.position) > AttackDistance || !CanSeePlayer)
        {
            if (CurrentHealth < MinChasingHealth)
            {
                state = State.Patrolling;
            }
            else
            {
                state = State.Chasing;
            }
        }
    }
    private void Chasing()
    {
        IdleTimerCounter = IdleTime;
        Agent.SetDestination(LastKnownPlayerPosition);
        if (CurrentHealth < MinChasingHealth)
        {
            state = State.Patrolling;
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
        LookForPlayer();

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
        }

        LookAtPlayer();
        SetLastKnowPlayerPosition();
    }


}