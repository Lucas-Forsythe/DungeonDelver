using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    public static PlayerHealth Instance { get; private set; }

    [Header("Health")]
    public float MaxHealth = 100f;
    public float CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0f;

    [Header("Health UI (optional)")]
    public Slider healthBar;
    public TMP_Text healthText;

    [Header("Death Menu")]
    public GameObject deathPanel;               // starts inactive
    public Button respawnButton;                // also gets focus for gamepad users
    public string mainMenuSceneName = "MainMenu";
    public bool pauseOnDeath = true;
    [Tooltip("Where to respawn. Leave empty to respawn where the scene started.")]
    public Transform respawnPoint;

    [Header("Events (optional)")]
    public UnityEvent onDeath;
    public UnityEvent onRespawn;
    public event Action<float, float> OnHealthChanged;

    private Vector3 startPosition;
    private Quaternion startRotation;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate PlayerHealth found, destroying this one.");
            Destroy(this);
            return;
        }
        Instance = this;

        startPosition = transform.position;
        startRotation = transform.rotation;

        CurrentHealth = MaxHealth;
        if (deathPanel != null) deathPanel.SetActive(false);
        UpdateUI();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            Time.timeScale = 1f; // never leave the game paused after a scene change
        }
    }

    // Returns false if dead or already full, so the item isn't wasted
    public bool Heal(float amount)
    {
        if (IsDead || CurrentHealth >= MaxHealth) return false;

        CurrentHealth = Mathf.Min(CurrentHealth + amount, MaxHealth);
        UpdateUI();
        return true;
    }

    // IDamageable, so enemies can hurt the player the same way you hurt them
    public void Attack(float amount)
    {
        if (IsDead) return;

        CurrentHealth = Mathf.Max(CurrentHealth - amount, 0f);
        UpdateUI();

        if (IsDead) Die();
    }

    private void Die()
    {
        Debug.Log("Player died");

        // Close the inventory first if it was open
        if (Inventory.Instance != null && Inventory.Instance.Container.activeInHierarchy)
            Inventory.Instance.SetInventoryOpen(false);

        if (PlayerMove.Instance != null)
        {
            PlayerMove.Instance.updatingRotation = false;
            PlayerMove.Instance.enabled = false;
        }
        if (Meele.Instance != null) Meele.Instance.updatingRotation = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (deathPanel != null) deathPanel.SetActive(true);
        if (pauseOnDeath) Time.timeScale = 0f;

        // Lets gamepad users navigate the buttons
        if (respawnButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(respawnButton.gameObject);

        onDeath?.Invoke();
    }

    // Hook to the Respawn button
    public void Respawn()
    {
        Time.timeScale = 1f;

        Vector3 pos = respawnPoint != null ? respawnPoint.position : startPosition;
        Quaternion rot = respawnPoint != null ? respawnPoint.rotation : startRotation;

        // CharacterController and Rigidbody both fight direct position changes
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        transform.SetPositionAndRotation(pos, rot);
        Physics.SyncTransforms();

        if (cc != null) cc.enabled = true;

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (PlayerMove.Instance != null)
        {
            PlayerMove.Instance.enabled = true;
            PlayerMove.Instance.updatingRotation = true;
        }
        if (Meele.Instance != null) Meele.Instance.updatingRotation = true;

        if (deathPanel != null) deathPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        CurrentHealth = MaxHealth;
        UpdateUI();

        onRespawn?.Invoke();
    }

    // Hook to the Quit to Menu button
    public void QuitToMenu()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void UpdateUI()
    {
        if (healthBar != null)
        {
            healthBar.maxValue = MaxHealth;
            healthBar.value = CurrentHealth;
        }

        if (healthText != null)
            healthText.text = $"{Mathf.CeilToInt(CurrentHealth)} / {Mathf.CeilToInt(MaxHealth)}";

        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }
}