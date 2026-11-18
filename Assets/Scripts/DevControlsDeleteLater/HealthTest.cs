using UnityEngine;
using UnityEngine.InputSystem;

public class HealthTest : MonoBehaviour
{
    void Start()
    {
        Debug.Log("HealthTest running on " + gameObject.name);
    }

    void Update()
    {
        if (Keyboard.current == null) { Debug.LogWarning("No keyboard detected"); return; }

        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            Debug.Log("K pressed");
            if (PlayerHealth.Instance == null) { Debug.LogWarning("PlayerHealth.Instance is null"); return; }

            PlayerHealth.Instance.Attack(10f);
            Debug.Log($"Health is now {PlayerHealth.Instance.CurrentHealth}/{PlayerHealth.Instance.MaxHealth}");
        }
    }
}