using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

[RequireComponent(typeof(NavMeshAgent))]
public class CrabBoss : MonoBehaviour, IDamageable
{
    [Header("Health")]
    public float maxHealth = 300f;
    public float CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0f;

    [Header("Combat")]
    public float detectRange = 25f;
    public float attackRange = 3f;
    public float attackDamage = 20f;
    public float attackWindUp = 0.5f;     // delay before the hit lands
    public float attackCooldown = 2f;
    public float turnSpeed = 6f;
    [Tooltip("Rotate this if the crab model faces sideways (try 90 or -90).")]
    public float modelYawOffset = 0f;

    [Header("UI (optional)")]
    public GameObject bossBarRoot;        // parent object of the bar, starts inactive
    public Slider bossBar;

    [Header("Animation (optional)")]
    public Animator animator;             // leave empty to auto-find in children
    public string speedParam = "Speed";   // float
    public string attackTrigger = "Attack";
    public string dieTrigger = "Die";

    [Header("Death")]
    public float destroyDelay = 3f;
    public GameObject deathEffect;

    private NavMeshAgent agent;
    private bool aggro;
    private float nextAttackTime;
    private bool attacking;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        CurrentHealth = maxHealth;
        if (bossBarRoot != null) bossBarRoot.SetActive(false);
        UpdateBar();
    }

    private void Update()
    {
        if (IsDead) return;

        var playerHealth = PlayerHealth.Instance;
        if (playerHealth == null || playerHealth.IsDead)
        {
            StopMoving();
            return;
        }

        Vector3 toPlayer = playerHealth.transform.position - transform.position;
        toPlayer.y = 0f;                                 // flat distance, ignores height
        float dist = toPlayer.magnitude;

        if (!aggro && dist <= detectRange) StartFight();
        if (!aggro) return;

        if (dist > attackRange && !attacking)
        {
            agent.isStopped = false;
            agent.SetDestination(playerHealth.transform.position);
        }
        else
        {
            agent.isStopped = true;
            FacePlayer(toPlayer);

            if (!attacking && Time.time >= nextAttackTime)
                StartCoroutine(AttackRoutine(playerHealth));
        }

        SetFloat(speedParam, agent.velocity.magnitude);
    }

    private void StartFight()
    {
        aggro = true;
        if (bossBarRoot != null) bossBarRoot.SetActive(true);
    }

    private IEnumerator AttackRoutine(PlayerHealth player)
    {
        attacking = true;
        nextAttackTime = Time.time + attackCooldown;
        SetTrigger(attackTrigger);

        yield return new WaitForSeconds(attackWindUp);

        if (!IsDead && player != null && !player.IsDead)
        {
            Vector3 flat = player.transform.position - transform.position;
            flat.y = 0f;
            if (flat.magnitude <= attackRange + 1f)       // small grace so a step back doesn't dodge it
                player.Attack(attackDamage);
        }

        attacking = false;
    }

    private void FacePlayer(Vector3 toPlayer)
    {
        if (toPlayer.sqrMagnitude < 0.01f) return;
        Quaternion target = Quaternion.LookRotation(toPlayer) * Quaternion.Euler(0f, modelYawOffset, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, turnSpeed * Time.deltaTime);
    }

    // IDamageable, so your sword hits it like any other enemy
    public void Attack(float amount)
    {
        if (IsDead) return;

        if (!aggro) StartFight();                         // getting hit wakes it up
        CurrentHealth = Mathf.Max(CurrentHealth - amount, 0f);
        UpdateBar();

        if (IsDead) Die();
    }

    private void Die()
    {
        StopMoving();
        agent.enabled = false;

        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        SetTrigger(dieTrigger);
        if (bossBarRoot != null) bossBarRoot.SetActive(false);
        if (deathEffect != null) Instantiate(deathEffect, transform.position, Quaternion.identity);

        Destroy(gameObject, destroyDelay);
    }

    private void StopMoving()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
        SetFloat(speedParam, 0f);
    }

    private void UpdateBar()
    {
        if (bossBar == null) return;
        bossBar.maxValue = maxHealth;
        bossBar.value = CurrentHealth;
    }

    // Animator helpers that stay quiet if the parameter doesn't exist yet
    private void SetFloat(string param, float value)
    {
        if (HasParam(param, AnimatorControllerParameterType.Float)) animator.SetFloat(param, value);
    }

    private void SetTrigger(string param)
    {
        if (HasParam(param, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(param);
    }

    private bool HasParam(string param, AnimatorControllerParameterType type)
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(param)) return false;
        foreach (var p in animator.parameters)
            if (p.name == param && p.type == type) return true;
        return false;
    }
}