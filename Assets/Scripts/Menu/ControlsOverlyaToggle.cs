using UnityEngine;

public class ControlsOverlyaToggle : MonoBehaviour
{
    [SerializeField] GameObject overlay;

    public void Open() => overlay.SetActive(true);
    public void Close() => overlay.SetActive(false);
}
