using UnityEngine;
using UnityEngine.UI;

// Drop this on any active object (e.g. the Player). It builds its own canvas and
// crosshair at runtime, so there is nothing else to set up.
public class Crosshair : MonoBehaviour
{
    [Header("Look")]
    public Color color = Color.white;
    public Color outlineColor = new Color(0f, 0f, 0f, 0.8f);
    public float lineLength = 10f;   // length of each arm in pixels
    public float thickness = 2f;     // thickness of each arm in pixels
    public float gap = 5f;           // empty space between the center and each arm
    public bool centerDot = true;
    public float outline = 1f;       // outline thickness in pixels (0 = none)

    [Header("Behaviour")]
    public bool hideWhenInventoryOpen = true;

    private GameObject root;

    private void Start()
    {
        BuildCrosshair();
    }

    private void Update()
    {
        if (root == null) return;

        bool inventoryOpen = hideWhenInventoryOpen
            && Inventory.Instance != null
            && Inventory.Instance.Container != null
            && Inventory.Instance.Container.activeInHierarchy;

        if (root.activeSelf == inventoryOpen)
            root.SetActive(!inventoryOpen);
    }

    private void OnDestroy()
    {
        if (root != null) Destroy(root.transform.parent.gameObject);
    }

    private void BuildCrosshair()
    {
        // Own overlay canvas so it never depends on your existing UI layout
        GameObject canvasObj = new GameObject("CrosshairCanvas", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root = new GameObject("Crosshair", typeof(RectTransform));
        root.transform.SetParent(canvasObj.transform, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = Vector2.zero;

        float offset = gap + lineLength * 0.5f;

        // Four arms: up, down, left, right
        MakePart("Up", new Vector2(0f, offset), new Vector2(thickness, lineLength));
        MakePart("Down", new Vector2(0f, -offset), new Vector2(thickness, lineLength));
        MakePart("Left", new Vector2(-offset, 0f), new Vector2(lineLength, thickness));
        MakePart("Right", new Vector2(offset, 0f), new Vector2(lineLength, thickness));

        if (centerDot)
            MakePart("Dot", Vector2.zero, new Vector2(thickness, thickness));
    }

    private void MakePart(string partName, Vector2 position, Vector2 size)
    {
        // Outline first (slightly larger, drawn behind), then the colored part on top
        if (outline > 0f)
            MakeImage(partName + "Outline", position, size + Vector2.one * outline * 2f, outlineColor);

        MakeImage(partName, position, size, color);
    }

    private void MakeImage(string objName, Vector2 position, Vector2 size, Color c)
    {
        GameObject go = new GameObject(objName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root.transform, false);

        Image img = go.GetComponent<Image>();
        img.color = c;
        img.raycastTarget = false; // never block UI clicks

        RectTransform rect = img.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}