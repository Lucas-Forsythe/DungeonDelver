using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInputComponent = UnityEngine.InputSystem.PlayerInput;

public interface IDamageable
{
    void Attack(float Amount);
}

public class Meele : MonoBehaviour
{
    public static Meele Instance;

    [SerializeField] public Transform AttackPoint;
    public LayerMask EnemyLayer;

    [Header("Input")]
    public PlayerInputComponent PlayerInputComponent; // leave empty to auto-find
    public string attackActionName = "Attack";

    private InputAction attackAction;
    private Animator fallbackAnimator;
    private bool canAttack = true;
    public bool updatingRotation;

    private void Awake()
    {
        Instance = this;
        fallbackAnimator = GetComponentInParent<Animator>();
        updatingRotation = true;
    }

    // Looked up in Start so PlayerInputComponent has finished setting up its actions
    private void Start()
    {
        if (PlayerInputComponent == null) PlayerInputComponent = GetComponentInParent<PlayerInputComponent>();
        if (PlayerInputComponent == null) PlayerInputComponent = FindFirstObjectByType<PlayerInputComponent>();

        if (PlayerInputComponent != null && PlayerInputComponent.actions != null)
        {
            attackAction = PlayerInputComponent.actions.FindAction(attackActionName, false);

            // If the action lives in a map that PlayerInput didn't enable, it would never fire
            if (attackAction != null) attackAction.Enable();
        }

        if (attackAction == null)
        {
            Debug.LogWarning("Meele: input action '" + attackActionName + "' not found. Falling back to left mouse / right trigger.");
        }
    }

    private void Update()
    {
        if (!updatingRotation || !canAttack) return;

        ItemScriptableObject weapon = Inventory.Instance.EquippedItem;
        if (weapon == null || weapon.itemType != ItemType.Melee) return;
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead) return;
        if (AttackPressed())
        {
            StartCoroutine(Attack(weapon));
        }
    }

    private bool AttackPressed()
    {
        if (attackAction != null)
        {
            return attackAction.WasPressedThisFrame();
        }

        return (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.rightTrigger.wasPressedThisFrame);
    }

    private Animator GetAnimator()
    {
        GameObject held = Inventory.Instance.HeldObject;
        if (held != null)
        {
            Animator a = held.GetComponentInChildren<Animator>();
            if (a != null) return a;
        }
        return fallbackAnimator;
    }

    IEnumerator Attack(ItemScriptableObject weapon)
    {
        canAttack = false;
        float startTime = Time.time;
        var hitThisSwing = new HashSet<IDamageable>();

        Animator anim = GetAnimator();
        bool animated = false;
        if (anim == null)
        {
            Debug.LogWarning("Meele: no Animator found on the held item or its parents, so no swing animation can play.");
        }
        else
        {
            anim.Play(weapon.attackState, 0, 0f);
            anim.Update(0f); // makes the state info reflect the new state immediately
            animated = anim.GetCurrentAnimatorStateInfo(0).IsName(weapon.attackState);

            if (!animated)
                Debug.LogWarning("Meele: Animator on '" + anim.gameObject.name + "' has no state named '" + weapon.attackState
                    + "' (controller: " + (anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "NONE") + ").");
        }

        if (animated)
        {
            // Scan every frame while the animation is inside the impact window
            while (anim != null)
            {
                if (Inventory.Instance.EquippedItem != weapon) { canAttack = true; yield break; }

                AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);
                if (!info.IsName(weapon.attackState) || info.normalizedTime >= 1f) break;

                if (info.normalizedTime >= weapon.hitWindowStart && info.normalizedTime <= weapon.hitWindowEnd)
                    HitScan(weapon, hitThisSwing);

                yield return null;
            }
        }
        else
        {
            // No usable animation: fall back to a single check after the windup
            yield return new WaitForSeconds(weapon.windUpTime);
            if (Inventory.Instance.EquippedItem != weapon) { canAttack = true; yield break; }
            HitScan(weapon, hitThisSwing);
        }

        // Immediately return to idle after the swing
        anim = GetAnimator();

        if (anim != null && Inventory.Instance.EquippedItem == weapon)
        {
            anim.Play(weapon.idleState, 0, 0f);
        }

        // Wait for the remaining cooldown before allowing another attack
        float remaining = weapon.attackCooldown - (Time.time - startTime);

        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        canAttack = true;
    }

    private void HitScan(ItemScriptableObject weapon, HashSet<IDamageable> hitThisSwing)
    {
        Collider[] hits = Physics.OverlapSphere(AttackPoint.position, weapon.attackRange, EnemyLayer);
        foreach (Collider col in hits)
        {
            IDamageable target = col.GetComponentInParent<IDamageable>();
            if (target != null && hitThisSwing.Add(target))
                target.Attack(weapon.damage);
        }
    }
}