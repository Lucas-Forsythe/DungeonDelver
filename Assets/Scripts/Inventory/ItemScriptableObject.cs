using UnityEngine;

public enum ItemType { Generic, Melee, Consumable }

[CreateAssetMenu(fileName = "Item", menuName = "NewItem")]
public class ItemScriptableObject : ScriptableObject
{
    public string itemName;
    public Sprite itemIcon;
    public int maxStackSize = 1;
    public GameObject itemPrefab;   // dropped/world version (has the Item script)
    public GameObject heldItem;     // version shown in the hand (no Item script, collider, or rigidbody)

    public ItemType itemType;

    [Header("Held pose (relative to the Hand)")]
    public Vector3 heldPositionOffset;
    public Vector3 heldRotationOffset;   // euler angles

    [Header("Melee")]
    public float damage = 40f;
    public float attackRange = 0.5f;
    public float windUpTime = 0.3f;
    public float attackCooldown = 1f;
    public string attackState = "Sword_Attack";
    public string idleState = "Sword_Idle";

    [Header("Melee hit window (fraction of the attack animation, 0-1)")]
    [Range(0f, 1f)] public float hitWindowStart = 0.3f;
    [Range(0f, 1f)] public float hitWindowEnd = 0.6f;

    [Header("Consumable")]
    public float healAmount = 25f;
    public AudioClip useSound;
    [Range(0f, 1f)] public float useSoundVolume = 1f;
}