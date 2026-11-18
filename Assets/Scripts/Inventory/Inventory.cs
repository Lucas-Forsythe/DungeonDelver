using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using PlayerInputComponent = UnityEngine.InputSystem.PlayerInput;

public class Inventory : MonoBehaviour
{
    public static Inventory Instance;

    public ItemScriptableObject Gold;
    public ItemScriptableObject Sword;
    public GameObject hotBarObj;
    public GameObject inventorySlotParent;

    public GameObject Container;

    public Image DragIcon;
    public Transform Hand;
    private GameObject currentequippedItem;

    // What's currently equipped (null if the selected hotbar slot is empty)
    public ItemScriptableObject EquippedItem { get; private set; }
    public GameObject HeldObject => currentequippedItem;

    public float PickupRange = 3f;
    private ItemScriptableObject itemToPickup;
    public Material highlightMaterial;
    private Material originalMaterial;
    private Renderer objectRenderer;

    private Slots draggedSlot;
    private bool isDragging = false;

    private List<Slots> inventorySlots = new List<Slots>();
    private List<Slots> hotBarSlots = new List<Slots>();
    private List<Slots> allSlots = new List<Slots>();
    private int HotBarIndex = 0;
    public float EquipedOpactity = 1f;
    public float UnEquipedOpactity = 0.5f;

    [Header("Audio")]
    public AudioSource sfxSource;   // leave empty to auto-create

    [Header("Input")]
    public PlayerInputComponent playerInput; // leave empty to auto-find
    public string toggleInventoryAction = "ToggleInventory";
    public string interactAction = "Interact";
    public string dropAction = "Drop";
    public string useItemAction = "UseItem";
    public string hotbarNextAction = "HotbarNext";
    public string hotbarPreviousAction = "HotbarPrevious";
    public string hotbarScrollAction = "HotbarScroll";
    public string uiClickAction = "UIClick";
    public string cursorMoveAction = "CursorMove";

    [Header("Gamepad Cursor")]
    public float cursorSpeed = 900f; // pixels per second
    [Tooltip("Optional. Leave empty to auto-create a cursor, or assign your own Image on the canvas.")]
    public RectTransform virtualCursorGraphic;
    [Tooltip("Optional sprite for the auto-created cursor. Leave empty to use the built-in arrow.")]
    public Sprite gamepadCursorSprite;
    public Vector2 gamepadCursorSize = new Vector2(32f, 32f);
    public Color gamepadCursorColor = Color.white;

    private InputAction toggleInventory, interact, drop, useItem;
    private InputAction hotbarNext, hotbarPrevious, hotbarScroll;
    private InputAction uiClick, cursorMove;

    private bool usingGamepadCursor = false;
    private Vector2 virtualCursor;
    private Vector2 pointerPos;
    private Canvas cursorCanvas;
    private Texture2D generatedCursorTexture;

    private void Awake()
    {
        Instance = this;

        inventorySlots.AddRange(inventorySlotParent.GetComponentsInChildren<Slots>());
        hotBarSlots.AddRange(hotBarObj.GetComponentsInChildren<Slots>());

        DragIcon.enabled = false;

        allSlots.AddRange(inventorySlots);
        allSlots.AddRange(hotBarSlots);

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;   // 2D, so it's the same volume wherever you stand
        }
    }

    // Looked up in Start so PlayerInput has finished setting up its actions
    private void Start()
    {
        if (playerInput == null) playerInput = FindFirstObjectByType<PlayerInputComponent>();

        toggleInventory = FindAction(toggleInventoryAction);
        interact = FindAction(interactAction);
        drop = FindAction(dropAction);
        useItem = FindAction(useItemAction);
        hotbarNext = FindAction(hotbarNextAction);
        hotbarPrevious = FindAction(hotbarPreviousAction);
        hotbarScroll = FindAction(hotbarScrollAction);
        uiClick = FindAction(uiClickAction);
        cursorMove = FindAction(cursorMoveAction);

        EnsureCursorGraphic();
        virtualCursorGraphic.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (generatedCursorTexture != null) Destroy(generatedCursorTexture);
    }

    private InputAction FindAction(string actionName)
    {
        if (playerInput == null || playerInput.actions == null) return null;

        InputAction action = playerInput.actions.FindAction(actionName, false);
        if (action == null)
        {
            Debug.LogWarning("Inventory: input action '" + actionName + "' was not found in the Input Actions asset.");
        }
        return action;
    }

    // ---------- Gamepad cursor graphic ----------

    private void EnsureCursorGraphic()
    {
        cursorCanvas = DragIcon.canvas != null ? DragIcon.canvas.rootCanvas : GetComponentInParent<Canvas>();

        if (virtualCursorGraphic == null)
        {
            GameObject go = new GameObject("GamepadCursor", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(cursorCanvas.transform, false);

            Image img = go.GetComponent<Image>();
            img.raycastTarget = false; // never block clicks
            img.color = gamepadCursorColor;
            img.sprite = gamepadCursorSprite != null ? gamepadCursorSprite : CreateArrowSprite();

            virtualCursorGraphic = img.rectTransform;
            virtualCursorGraphic.sizeDelta = gamepadCursorSize;
        }

        // Anchored to the middle of the canvas, tip of the arrow at the pointer position
        virtualCursorGraphic.anchorMin = new Vector2(0.5f, 0.5f);
        virtualCursorGraphic.anchorMax = new Vector2(0.5f, 0.5f);
        virtualCursorGraphic.pivot = new Vector2(0f, 1f);

        if (virtualCursorGraphic.GetComponentInParent<Canvas>() != null)
        {
            cursorCanvas = virtualCursorGraphic.GetComponentInParent<Canvas>().rootCanvas;
        }
    }

    // Draws a simple white arrow with a black outline, no art assets needed
    private Sprite CreateArrowSprite()
    {
        const int size = 32;
        generatedCursorTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        generatedCursorTexture.filterMode = FilterMode.Bilinear;

        // Arrow triangle in pixel coordinates (y = 0 is the bottom)
        Vector2 a = new Vector2(4, 29);   // tip
        Vector2 b = new Vector2(4, 6);
        Vector2 c = new Vector2(21, 18);

        bool[,] fill = new bool[size, size];
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                fill[x, y] = PointInTriangle(new Vector2(x + 0.5f, y + 0.5f), a, b, c);
            }
        }

        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 white = new Color32(255, 255, 255, 255);
        Color32 black = new Color32(0, 0, 0, 255);

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                if (fill[x, y])
                {
                    generatedCursorTexture.SetPixel(x, y, white);
                    continue;
                }

                bool nearFill = false;
                for (int dx = -2; dx <= 2 && !nearFill; dx++)
                {
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                        if (fill[nx, ny]) { nearFill = true; break; }
                    }
                }
                generatedCursorTexture.SetPixel(x, y, nearFill ? black : clear);
            }
        }

        generatedCursorTexture.Apply();

        return Sprite.Create(generatedCursorTexture, new Rect(0, 0, size, size), new Vector2(0f, 1f), 100f);
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b);
        float d2 = Sign(p, b, c);
        float d3 = Sign(p, c, a);
        bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
        bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(hasNeg && hasPos);
    }

    private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    private void PositionCursorGraphic()
    {
        if (virtualCursorGraphic == null || cursorCanvas == null) return;

        RectTransform canvasRect = cursorCanvas.transform as RectTransform;
        Camera cam = cursorCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cursorCanvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, pointerPos, cam, out Vector2 local))
        {
            virtualCursorGraphic.localPosition = local;
        }
    }

    // ---------- Update ----------

    public void Update()
    {
        if (toggleInventory != null && toggleInventory.WasPressedThisFrame()
            && (PlayerHealth.Instance == null || !PlayerHealth.Instance.IsDead))
        {
            // Remember whether the toggle came from a gamepad so the right cursor shows up
            var control = toggleInventory.activeControl;
            if (control != null) usingGamepadCursor = control.device is Gamepad;

            bool open = !Container.activeInHierarchy;
            if (open && usingGamepadCursor)
            {
                virtualCursor = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }
            SetInventoryOpen(open);
        }

        UpdatePointer();

        DetectLookedAt();
        PickUp();

        StartDrag();
        UpdateDragIconPosition();
        EndDrag();

        HandleHotBarSelection();
        HandeleDrop();
        HandleUseItem();
        RefreshEquippedItem();   // after everything that can change the hotbar
        UpdateHotBarOpacity();
    }

    // ---------- Inventory open/close and pointer ----------

    public void SetInventoryOpen(bool open)
    {
        Container.SetActive(open);

        if (!open) CancelDrag();

        if (PlayerMove.Instance != null) PlayerMove.Instance.updatingRotation = !open;
        if (Meele.Instance != null) Meele.Instance.updatingRotation = !open;

        ApplyCursorState(open);
    }

    private void ApplyCursorState(bool open)
    {
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open && !usingGamepadCursor;

        if (virtualCursorGraphic != null)
        {
            bool showGamepadCursor = open && usingGamepadCursor;
            virtualCursorGraphic.gameObject.SetActive(showGamepadCursor);

            // Draw above everything else on the canvas (slots, drag icon)
            if (showGamepadCursor) virtualCursorGraphic.SetAsLastSibling();
        }
    }

    private void UpdatePointer()
    {
        if (!Container.activeInHierarchy) return;

        Mouse mouse = Mouse.current;
        Vector2 stick = cursorMove != null ? cursorMove.ReadValue<Vector2>() : Vector2.zero;

        if (stick.sqrMagnitude > 0.04f)
        {
            if (!usingGamepadCursor)
            {
                usingGamepadCursor = true;
                virtualCursor = mouse != null ? mouse.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                ApplyCursorState(true);
            }

            virtualCursor += stick * cursorSpeed * Time.unscaledDeltaTime;
            virtualCursor.x = Mathf.Clamp(virtualCursor.x, 0f, Screen.width);
            virtualCursor.y = Mathf.Clamp(virtualCursor.y, 0f, Screen.height);
        }
        else if (usingGamepadCursor && mouse != null && mouse.delta.ReadValue().sqrMagnitude > 1f)
        {
            // Player grabbed the mouse again
            usingGamepadCursor = false;
            ApplyCursorState(true);
        }

        if (usingGamepadCursor)
        {
            pointerPos = virtualCursor;
        }
        else
        {
            pointerPos = mouse != null ? mouse.position.ReadValue() : virtualCursor;
        }

        if (usingGamepadCursor)
        {
            PositionCursorGraphic();
        }
    }

    // ---------- Adding items ----------

    public void AddItem(ItemScriptableObject itemToAdd, int amount)
    {
        int remainingAmount = amount;

        foreach (Slots slot in allSlots)
        {
            if (slot.HasItem() && slot.GetItem() == itemToAdd)
            {
                int currentAmount = slot.GetItemCount();
                int maxStackSize = itemToAdd.maxStackSize;

                if (currentAmount < maxStackSize)
                {
                    int spaceLeft = maxStackSize - currentAmount;
                    int amountToAdd = Mathf.Min(spaceLeft, remainingAmount);
                    slot.AddAmount(amountToAdd);
                    remainingAmount -= amountToAdd;
                    if (remainingAmount <= 0)
                        return;
                }
            }
        }

        foreach (Slots slot in allSlots)
        {
            if (!slot.HasItem())
            {
                int amountToPlace = Mathf.Min(itemToAdd.maxStackSize, remainingAmount);
                slot.SetItem(itemToAdd, amountToPlace);
                remainingAmount -= amountToPlace;
                if (remainingAmount <= 0)
                    return;
            }
        }

        if (remainingAmount > 0)
        {
            Debug.LogWarning("Not enough space in inventory to add all items. Remaining amount: " + remainingAmount);
        }
    }

    // ---------- Drag and drop (mouse click or gamepad south button) ----------

    private void StartDrag()
    {
        if (!Container.activeInHierarchy) return;
        if (uiClick == null || !uiClick.WasPressedThisFrame()) return;

        Slots hover = GetHoveredSlot();

        if (hover != null && hover.HasItem())
        {
            draggedSlot = hover;
            isDragging = true;
            DragIcon.enabled = true;
            DragIcon.color = new Color(1, 1, 1, .5f);
            DragIcon.sprite = draggedSlot.GetItem().itemIcon;
        }
    }

    private void EndDrag()
    {
        if (!isDragging) return;
        if (uiClick == null || !uiClick.WasReleasedThisFrame()) return;

        Slots hover = GetHoveredSlot();
        if (hover != null)
        {
            HandleDrop(draggedSlot, hover);
        }

        CancelDrag();
    }

    private void CancelDrag()
    {
        DragIcon.enabled = false;
        draggedSlot = null;
        isDragging = false;
    }

    private void HandleDrop(Slots fromSlot, Slots toSlot)
    {
        if (fromSlot == toSlot)
        {
            return;
        }

        //Stacking logic
        if (toSlot.HasItem() && fromSlot.GetItem() == toSlot.GetItem())
        {
            int max = toSlot.GetItem().maxStackSize;
            int space = max - toSlot.GetItemCount();

            if (space > 0)
            {
                int amountToMove = Mathf.Min(space, fromSlot.GetItemCount());

                toSlot.SetItem(toSlot.GetItem(), toSlot.GetItemCount() + amountToMove);
                fromSlot.SetItem(fromSlot.GetItem(), fromSlot.GetItemCount() - amountToMove);

                if (fromSlot.GetItemCount() <= 0)
                {
                    fromSlot.ClearSlot();
                }
            }
        }

        //Different item logic
        else if (toSlot.HasItem())
        {
            ItemScriptableObject tempItem = toSlot.GetItem();
            int tempAmount = toSlot.GetItemCount();

            toSlot.SetItem(fromSlot.GetItem(), fromSlot.GetItemCount());
            fromSlot.SetItem(tempItem, tempAmount);
            return;
        }

        //Empty slot logic
        else
        {
            toSlot.SetItem(fromSlot.GetItem(), fromSlot.GetItemCount());
            fromSlot.ClearSlot();
        }
    }

    private void UpdateDragIconPosition()
    {
        if (isDragging)
        {
            DragIcon.transform.position = pointerPos;
        }
    }

    private Slots GetHoveredSlot()
    {
        foreach (Slots s in allSlots)
        {
            if (!s.gameObject.activeInHierarchy) continue;

            bool overSlot = IsPointerInsideSlot(s);

            // Mouse users can also rely on the slot's own hover flag
            if (!usingGamepadCursor && s.hoveringOverSlot) overSlot = true;

            if (overSlot) return s;
        }
        return null;
    }

    private bool IsPointerInsideSlot(Slots slot)
    {
        RectTransform rect = slot.transform as RectTransform;
        if (rect == null) return false;

        Canvas canvas = slot.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cam = canvas.worldCamera;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(rect, pointerPos, cam);
    }

    // ---------- World interaction ----------

    private void PickUp()
    {
        if (objectRenderer != null && interact != null && interact.WasPressedThisFrame())
        {
            Item itemData = objectRenderer.GetComponent<Item>();
            if (itemData != null)
            {
                AddItem(itemData.itemData, itemData.itemCount);
                Destroy(itemData.gameObject);
            }
        }
    }

    private void DetectLookedAt()
    {
        if (objectRenderer != null)
        {
            objectRenderer.material = originalMaterial;
            objectRenderer = null;
            originalMaterial = null;
        }

        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, PickupRange))
        {
            Item itemData = hit.collider.GetComponent<Item>();
            if (itemData != null)
            {
                Renderer rend = hit.collider.GetComponent<Renderer>();
                if (rend != null)
                {
                    originalMaterial = rend.material;
                    rend.material = highlightMaterial;
                    objectRenderer = rend;
                }
            }
        }
    }

    // ---------- Hotbar ----------

    private void UpdateHotBarOpacity()
    {
        for (int i = 0; i < hotBarSlots.Count; i++)
        {
            Image slotImage = hotBarSlots[i].GetComponent<Image>();
            if (slotImage != null)
            {
                slotImage.color = (i == HotBarIndex) ? new Color(1f, 1f, 1f, EquipedOpactity) : new Color(1f, 1f, 1f, UnEquipedOpactity);
            }
        }
    }

    private void HandleHotBarSelection()
    {
        int count = hotBarSlots.Count;
        if (count == 0) return;

        int step = 0;

        if (hotbarScroll != null)
        {
            float scroll = hotbarScroll.ReadValue<Vector2>().y;
            if (scroll > 0.01f) step = 1;
            else if (scroll < -0.01f) step = -1;
        }

        if (hotbarNext != null && hotbarNext.WasPressedThisFrame()) step = 1;
        if (hotbarPrevious != null && hotbarPrevious.WasPressedThisFrame()) step = -1;

        if (step != 0)
        {
            HotBarIndex = (HotBarIndex + step + count) % count;
        }
    }

    private void HandeleDrop()
    {
        if (drop == null || !drop.WasPressedThisFrame())
        {
            return;
        }
        Slots EquippedSlot = hotBarSlots[HotBarIndex];

        if (!EquippedSlot.HasItem())
        {
            return;
        }

        ItemScriptableObject itemToDrop = EquippedSlot.GetItem();
        GameObject prefab = itemToDrop.itemPrefab;

        if (prefab == null)
        {
            Debug.LogWarning("Item prefab is not assigned for " + itemToDrop.name);
            return;
        }

        GameObject droppedItem = Instantiate(prefab, Camera.main.transform.position + Camera.main.transform.forward, Quaternion.identity);

        Item item = droppedItem.GetComponent<Item>();
        item.itemData = itemToDrop;
        item.itemCount = EquippedSlot.GetItemCount();

        EquippedSlot.ClearSlot();
    }

    // Only swaps the held object when the equipped item actually changes
    private void RefreshEquippedItem()
    {
        Slots slot = hotBarSlots[HotBarIndex];
        ItemScriptableObject wanted = slot.HasItem() ? slot.GetItem() : null;

        if (wanted == EquippedItem) return;

        if (currentequippedItem != null) Destroy(currentequippedItem);
        currentequippedItem = null;
        EquippedItem = wanted;

        if (wanted == null) return;

        GameObject prefab = wanted.heldItem != null ? wanted.heldItem : wanted.itemPrefab;
        if (prefab != null)
        {
            currentequippedItem = Instantiate(prefab, Hand);
            currentequippedItem.transform.localPosition = wanted.heldPositionOffset;
            currentequippedItem.transform.localRotation = Quaternion.Euler(wanted.heldRotationOffset);
        }
    }

    // Use a consumable (only while the inventory is closed)
    private void HandleUseItem()
    {
        if (Container.activeInHierarchy) return;
        if (useItem == null || !useItem.WasPressedThisFrame()) return;
        if (EquippedItem == null || EquippedItem.itemType != ItemType.Consumable) return;
        if (PlayerHealth.Instance == null || !PlayerHealth.Instance.Heal(EquippedItem.healAmount)) return;

        if (EquippedItem.useSound != null)
            sfxSource.PlayOneShot(EquippedItem.useSound, EquippedItem.useSoundVolume);

        Slots slot = hotBarSlots[HotBarIndex];
        int remaining = slot.GetItemCount() - 1;
        if (remaining <= 0) slot.ClearSlot();
        else slot.SetItem(slot.GetItem(), remaining);
    }
}