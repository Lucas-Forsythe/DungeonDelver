using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class ControlsDispaly : MonoBehaviour
{
    [SerializeField] InputActionAsset actions;
    [SerializeField] TMP_Text output;

    void OnEnable()
    {
        var sb = new StringBuilder();
        foreach (var map in actions.actionMaps)
        {
            foreach (var action in map.actions)
            {
                sb.AppendLine($"{action.name}: {action.GetBindingDisplayString()}");
            }
        }
        output.text = sb.ToString();
    }
}