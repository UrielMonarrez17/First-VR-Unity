using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Manages the UI Poke Components Interface for VR.
/// Allows changing Shape (Cube, Sphere, Cylinder), Color (Red, Green, Blue),
/// Size (Small, Medium, Large), and Rotation around X, Y, Z axes.
/// Resets object with ResetButton, and accepts changes / confirms answer with PokeButton.
/// </summary>
public class ObjectModifierManager : MonoBehaviour
{
    public enum ShapeType { Cube, Sphere, Cylinder }
    public enum ColorType { Red, Green, Blue }
    public enum SizeType { Small, Medium, Large }

    [Header("Target Objects")]
    [Tooltip("The object being modified (e.g. 'Change cube').")]
    public GameObject targetObject;

    [Tooltip("The reference/goal target to copy (e.g. 'Target').")]
    public GameObject referenceTarget;

    [Header("Physical 3D Poke Buttons on Table")]
    [Tooltip("Physical 3D PokeButton for accepting changes / confirming final answer.")]
    public XRBaseInteractable pokeButton;

    [Tooltip("Physical 3D ResetButton for resetting object to original state.")]
    public XRBaseInteractable resetButton;

    [Header("UI Shape Buttons")]
    public Button cubeButton;
    public Button sphereButton;
    public Button cylinderButton;

    [Header("UI Color Controls")]
    public TMP_InputField colorInputField;
    public Button redButton;
    public Button greenButton;
    public Button blueButton;

    [Header("UI Size Controls")]
    public TMP_InputField sizeInputSmall;
    public TMP_InputField sizeInputMedium;
    public TMP_InputField sizeInputLarge;
    public Button smallButton;
    public Button mediumButton;
    public Button largeButton;

    [Header("UI Rotation Controls")]
    public TMP_InputField rotationInputX;
    public TMP_InputField rotationInputY;
    public TMP_InputField rotationInputZ;
    public Button rotateXButton;
    public Button rotateYButton;
    public Button rotateZButton;

    [Header("Feedback Displays")]
    public TMP_Text statusText;
    public TMP_Text titleText;

    [Header("Custom Meshes (Optional, auto-generated if null)")]
    public Mesh cubeMesh;
    public Mesh sphereMesh;
    public Mesh cylinderMesh;

    [Header("Settings")]
    public float smallScaleMultiplier = 0.58f;
    public float mediumScaleMultiplier = 1.0f;
    public float largeScaleMultiplier = 1.5f;
    public float rotationStepDegrees = 45f;

    // Predefined colors
    public static readonly Color ColorRed = new Color(0.92f, 0.22f, 0.22f, 1f);
    public static readonly Color ColorGreen = new Color(0.22f, 0.82f, 0.28f, 1f);
    public static readonly Color ColorBlue = new Color(0.22f, 0.48f, 0.95f, 1f);

    // Initial state of targetObject
    private ShapeType m_InitialShape = ShapeType.Cube;
    private Color m_InitialColor = Color.white;
    private Vector3 m_InitialScale;
    private Vector3 m_InitialPosition;
    private Quaternion m_InitialRotation;

    // Current state
    private ShapeType m_CurrentShape = ShapeType.Cube;
    private ColorType m_CurrentColorType = ColorType.Red;
    private Color m_CurrentColor = Color.white;
    private SizeType m_CurrentSizeType = SizeType.Medium;
    private Vector3 m_CurrentRotationAngles = Vector3.zero;

    // Cached components on targetObject
    private MeshFilter m_TargetMeshFilter;
    private MeshRenderer m_TargetMeshRenderer;
    private Rigidbody m_TargetRigidbody;
    private XRGrabInteractable m_TargetGrabInteractable;

    private BoxCollider m_BoxCollider;
    private SphereCollider m_SphereCollider;
    private CapsuleCollider m_CapsuleCollider;

    // Static cached meshes across sessions
    private static Mesh s_DefaultCubeMesh;
    private static Mesh s_DefaultSphereMesh;
    private static Mesh s_DefaultCylinderMesh;

    // Helper component for detecting poke clicks on UI elements
    public class UIPokeTrigger : MonoBehaviour, IPointerClickHandler
    {
        public Action onClick;
        public void OnPointerClick(PointerEventData eventData)
        {
            onClick?.Invoke();
        }
    }

    private void Awake()
    {
        AutoDiscoverReferences();
        InitializePrimitives();
    }

    private void Start()
    {
        InitializeTargetObject();
        SetupUIListeners();
        SetupPokeButtonListeners();
        UpdateUIDisplays();
        ShowStatus("Ready: Modify object or copy target.");
    }

    private void Update()
    {
#if UNITY_EDITOR
        HandleEditorKeyboardInput();
#endif
    }

    #region Initialization & Discovery

    private void AutoDiscoverReferences()
    {
        // 1. Target Object to modify ("Change cube" or "Cube")
        if (targetObject == null)
        {
            targetObject = GameObject.Find("Change cube");
            if (targetObject == null) targetObject = GameObject.Find("Cube");
        }

        // 2. Reference Target ("Target" or "Cube (1)")
        if (referenceTarget == null)
        {
            referenceTarget = GameObject.Find("Target");
            if (referenceTarget == null) referenceTarget = GameObject.Find("Cube (1)");
        }

        // 3. Physical PokeButton (Confirm)
        if (pokeButton == null)
        {
            GameObject pbGo = GameObject.Find("PokeButton");
            if (pbGo != null) pokeButton = pbGo.GetComponent<XRBaseInteractable>();
        }

        // 4. Physical ResetButton (Reset)
        if (resetButton == null)
        {
            GameObject rbGo = GameObject.Find("ResetButton");
            if (rbGo != null) resetButton = rbGo.GetComponent<XRBaseInteractable>();
        }

        // 5. Canvas UI Elements
        Transform canvasTrans = transform;
        if (canvasTrans.name != "UI Poke Components")
        {
            GameObject canvasGo = GameObject.Find("UI Poke Components");
            if (canvasGo != null) canvasTrans = canvasGo.transform;
        }

        Transform elementsTrans = canvasTrans.Find("Elements");
        if (elementsTrans != null)
        {
            // Shape buttons (Icon Button (2) = Cube, Icon Button (1) = Sphere, Icon Button = Cylinder)
            if (cubeButton == null)
            {
                Transform t = elementsTrans.Find("Icon Button (2)");
                if (t != null) cubeButton = t.GetComponent<Button>();
            }
            if (sphereButton == null)
            {
                Transform t = elementsTrans.Find("Icon Button (1)");
                if (t != null) sphereButton = t.GetComponent<Button>();
            }
            if (cylinderButton == null)
            {
                Transform t = elementsTrans.Find("Icon Button");
                if (t != null) cylinderButton = t.GetComponent<Button>();
            }

            // Size Inputs
            Transform sizeInputsTrans = elementsTrans.Find("Size Inputs");
            if (sizeInputsTrans != null)
            {
                Transform sx = sizeInputsTrans.Find("Size Input X");
                if (sx != null)
                {
                    if (sizeInputSmall == null) sizeInputSmall = sx.GetComponent<TMP_InputField>();
                    if (smallButton == null) smallButton = sx.GetComponent<Button>() ?? sx.gameObject.AddComponent<Button>();
                }
                Transform sy = sizeInputsTrans.Find("Size Input Y");
                if (sy != null)
                {
                    if (sizeInputMedium == null) sizeInputMedium = sy.GetComponent<TMP_InputField>();
                    if (mediumButton == null) mediumButton = sy.GetComponent<Button>() ?? sy.gameObject.AddComponent<Button>();
                }
                Transform sz = sizeInputsTrans.Find("Size Input Z");
                if (sz != null)
                {
                    if (sizeInputLarge == null) sizeInputLarge = sz.GetComponent<TMP_InputField>();
                    if (largeButton == null) largeButton = sz.GetComponent<Button>() ?? sz.gameObject.AddComponent<Button>();
                }

                // Update size axis labels from x, y, z to S, M, L
                Transform lx = sizeInputsTrans.Find("x");
                if (lx != null) SetTextIfExists(lx, "S");
                Transform ly = sizeInputsTrans.Find("y");
                if (ly != null) SetTextIfExists(ly, "M");
                Transform lz = sizeInputsTrans.Find("z");
                if (lz != null) SetTextIfExists(lz, "L");
            }

            // Rotation Inputs
            Transform rotInputsTrans = elementsTrans.Find("Rotation Inputs");
            if (rotInputsTrans != null)
            {
                Transform rx = rotInputsTrans.Find("Size Input X");
                if (rx != null)
                {
                    if (rotationInputX == null) rotationInputX = rx.GetComponent<TMP_InputField>();
                    if (rotateXButton == null) rotateXButton = rx.GetComponent<Button>() ?? rx.gameObject.AddComponent<Button>();
                }
                Transform ry = rotInputsTrans.Find("Size Input Y");
                if (ry != null)
                {
                    if (rotationInputY == null) rotationInputY = ry.GetComponent<TMP_InputField>();
                    if (rotateYButton == null) rotateYButton = ry.GetComponent<Button>() ?? ry.gameObject.AddComponent<Button>();
                }
                Transform rz = rotInputsTrans.Find("Size Input Z");
                if (rz != null)
                {
                    if (rotationInputZ == null) rotationInputZ = rz.GetComponent<TMP_InputField>();
                    if (rotateZButton == null) rotateZButton = rz.GetComponent<Button>() ?? rz.gameObject.AddComponent<Button>();
                }
            }

            // Color Input
            Transform colorTrans = elementsTrans.Find("Color Input");
            if (colorTrans != null && colorInputField == null)
            {
                colorInputField = colorTrans.GetComponent<TMP_InputField>();
            }
        }

        // Status Text (e.g. Change Title above object or in canvas)
        if (statusText == null)
        {
            Transform changeTitle = canvasTrans.Find("Change Title");
            if (changeTitle != null) statusText = changeTitle.GetComponent<TMP_Text>();
        }
        if (titleText == null)
        {
            Transform t = canvasTrans.Find("Title");
            if (t != null) titleText = t.GetComponent<TMP_Text>();
        }
    }

    private void SetTextIfExists(Transform t, string newText)
    {
        TMP_Text tmp = t.GetComponent<TMP_Text>();
        if (tmp != null) tmp.text = newText;
        else
        {
            Text uText = t.GetComponent<Text>();
            if (uText != null) uText.text = newText;
        }
    }

    private void InitializePrimitives()
    {
        if (cubeMesh == null)
        {
            if (s_DefaultCubeMesh == null) s_DefaultCubeMesh = ExtractPrimitiveMesh(PrimitiveType.Cube);
            cubeMesh = s_DefaultCubeMesh;
        }
        if (sphereMesh == null)
        {
            if (s_DefaultSphereMesh == null) s_DefaultSphereMesh = ExtractPrimitiveMesh(PrimitiveType.Sphere);
            sphereMesh = s_DefaultSphereMesh;
        }
        if (cylinderMesh == null)
        {
            if (s_DefaultCylinderMesh == null) s_DefaultCylinderMesh = ExtractPrimitiveMesh(PrimitiveType.Cylinder);
            cylinderMesh = s_DefaultCylinderMesh;
        }
    }

    private Mesh ExtractPrimitiveMesh(PrimitiveType type)
    {
        GameObject temp = GameObject.CreatePrimitive(type);
        Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(temp);
        return mesh;
    }

    private void InitializeTargetObject()
    {
        if (targetObject == null)
        {
            Debug.LogError("[ObjectModifierManager] Target Object is null! Cannot initialize.");
            return;
        }

        m_TargetMeshFilter = targetObject.GetComponent<MeshFilter>();
        m_TargetMeshRenderer = targetObject.GetComponent<MeshRenderer>();
        m_TargetRigidbody = targetObject.GetComponent<Rigidbody>();
        m_TargetGrabInteractable = targetObject.GetComponent<XRGrabInteractable>();

        // Cache initial transforms
        m_InitialPosition = targetObject.transform.position;
        m_InitialRotation = targetObject.transform.rotation;
        m_InitialScale = targetObject.transform.localScale;

        // Instantiate material so modifications don't alter shared assets
        if (m_TargetMeshRenderer != null && m_TargetMeshRenderer.material != null)
        {
            m_TargetMeshRenderer.material = new Material(m_TargetMeshRenderer.material);
            m_InitialColor = m_TargetMeshRenderer.material.color;
        }

        // Setup colliders for shape swapping
        m_BoxCollider = targetObject.GetComponent<BoxCollider>();
        if (m_BoxCollider == null) m_BoxCollider = targetObject.AddComponent<BoxCollider>();

        m_SphereCollider = targetObject.GetComponent<SphereCollider>();
        if (m_SphereCollider == null) m_SphereCollider = targetObject.AddComponent<SphereCollider>();
        m_SphereCollider.enabled = false;

        m_CapsuleCollider = targetObject.GetComponent<CapsuleCollider>();
        if (m_CapsuleCollider == null) m_CapsuleCollider = targetObject.AddComponent<CapsuleCollider>();
        m_CapsuleCollider.enabled = false;

        // Reset tracking state
        m_CurrentShape = ShapeType.Cube;
        m_CurrentColor = m_InitialColor;
        m_CurrentSizeType = SizeType.Medium;
        m_CurrentRotationAngles = Vector3.zero;

        ApplyColliderForShape(m_CurrentShape);
    }

    #endregion

    #region UI & Event Listeners

    private void SetupUIListeners()
    {
        // Shape Buttons
        if (cubeButton != null)
        {
            cubeButton.onClick.RemoveAllListeners();
            cubeButton.onClick.AddListener(() => SetShape(ShapeType.Cube));
            AttachPokeTrigger(cubeButton.gameObject, () => SetShape(ShapeType.Cube));
            SetButtonLabel(cubeButton, "Cube");
        }
        if (sphereButton != null)
        {
            sphereButton.onClick.RemoveAllListeners();
            sphereButton.onClick.AddListener(() => SetShape(ShapeType.Sphere));
            AttachPokeTrigger(sphereButton.gameObject, () => SetShape(ShapeType.Sphere));
            SetButtonLabel(sphereButton, "Sphere");
        }
        if (cylinderButton != null)
        {
            cylinderButton.onClick.RemoveAllListeners();
            cylinderButton.onClick.AddListener(() => SetShape(ShapeType.Cylinder));
            AttachPokeTrigger(cylinderButton.gameObject, () => SetShape(ShapeType.Cylinder));
            SetButtonLabel(cylinderButton, "Cylinder");
        }

        // Color Input Field
        if (colorInputField != null)
        {
            colorInputField.onEndEdit.RemoveAllListeners();
            colorInputField.onEndEdit.AddListener(OnColorInputEndEdit);

            // Poke on color field cycles Red -> Green -> Blue
            AttachPokeTrigger(colorInputField.gameObject, CycleColor);
        }

        // Dedicated Color Buttons (if created or assigned)
        if (redButton != null)
        {
            redButton.onClick.RemoveAllListeners();
            redButton.onClick.AddListener(() => SetColor(ColorType.Red));
            AttachPokeTrigger(redButton.gameObject, () => SetColor(ColorType.Red));
        }
        if (greenButton != null)
        {
            greenButton.onClick.RemoveAllListeners();
            greenButton.onClick.AddListener(() => SetColor(ColorType.Green));
            AttachPokeTrigger(greenButton.gameObject, () => SetColor(ColorType.Green));
        }
        if (blueButton != null)
        {
            blueButton.onClick.RemoveAllListeners();
            blueButton.onClick.AddListener(() => SetColor(ColorType.Blue));
            AttachPokeTrigger(blueButton.gameObject, () => SetColor(ColorType.Blue));
        }

        // Size Inputs / Buttons
        if (smallButton != null)
        {
            smallButton.onClick.RemoveAllListeners();
            smallButton.onClick.AddListener(() => SetSize(SizeType.Small));
            AttachPokeTrigger(smallButton.gameObject, () => SetSize(SizeType.Small));
        }
        if (sizeInputSmall != null)
        {
            sizeInputSmall.onEndEdit.RemoveAllListeners();
            sizeInputSmall.onEndEdit.AddListener((s) => SetSize(SizeType.Small));
            AttachPokeTrigger(sizeInputSmall.gameObject, () => SetSize(SizeType.Small));
        }

        if (mediumButton != null)
        {
            mediumButton.onClick.RemoveAllListeners();
            mediumButton.onClick.AddListener(() => SetSize(SizeType.Medium));
            AttachPokeTrigger(mediumButton.gameObject, () => SetSize(SizeType.Medium));
        }
        if (sizeInputMedium != null)
        {
            sizeInputMedium.onEndEdit.RemoveAllListeners();
            sizeInputMedium.onEndEdit.AddListener((s) => SetSize(SizeType.Medium));
            AttachPokeTrigger(sizeInputMedium.gameObject, () => SetSize(SizeType.Medium));
        }

        if (largeButton != null)
        {
            largeButton.onClick.RemoveAllListeners();
            largeButton.onClick.AddListener(() => SetSize(SizeType.Large));
            AttachPokeTrigger(largeButton.gameObject, () => SetSize(SizeType.Large));
        }
        if (sizeInputLarge != null)
        {
            sizeInputLarge.onEndEdit.RemoveAllListeners();
            sizeInputLarge.onEndEdit.AddListener((s) => SetSize(SizeType.Large));
            AttachPokeTrigger(sizeInputLarge.gameObject, () => SetSize(SizeType.Large));
        }

        // Rotation Inputs / Buttons
        if (rotateXButton != null)
        {
            rotateXButton.onClick.RemoveAllListeners();
            rotateXButton.onClick.AddListener(() => RotateAxis(Vector3.right));
            AttachPokeTrigger(rotateXButton.gameObject, () => RotateAxis(Vector3.right));
        }
        if (rotationInputX != null)
        {
            rotationInputX.onEndEdit.RemoveAllListeners();
            rotationInputX.onEndEdit.AddListener((val) => SetRotationAngle(0, val));
            AttachPokeTrigger(rotationInputX.gameObject, () => RotateAxis(Vector3.right));
        }

        if (rotateYButton != null)
        {
            rotateYButton.onClick.RemoveAllListeners();
            rotateYButton.onClick.AddListener(() => RotateAxis(Vector3.up));
            AttachPokeTrigger(rotateYButton.gameObject, () => RotateAxis(Vector3.up));
        }
        if (rotationInputY != null)
        {
            rotationInputY.onEndEdit.RemoveAllListeners();
            rotationInputY.onEndEdit.AddListener((val) => SetRotationAngle(1, val));
            AttachPokeTrigger(rotationInputY.gameObject, () => RotateAxis(Vector3.up));
        }

        if (rotateZButton != null)
        {
            rotateZButton.onClick.RemoveAllListeners();
            rotateZButton.onClick.AddListener(() => RotateAxis(Vector3.forward));
            AttachPokeTrigger(rotateZButton.gameObject, () => RotateAxis(Vector3.forward));
        }
        if (rotationInputZ != null)
        {
            rotationInputZ.onEndEdit.RemoveAllListeners();
            rotationInputZ.onEndEdit.AddListener((val) => SetRotationAngle(2, val));
            AttachPokeTrigger(rotationInputZ.gameObject, () => RotateAxis(Vector3.forward));
        }
    }

    private void AttachPokeTrigger(GameObject go, Action action)
    {
        if (go == null) return;
        UIPokeTrigger trigger = go.GetComponent<UIPokeTrigger>();
        if (trigger == null) trigger = go.AddComponent<UIPokeTrigger>();
        trigger.onClick = action;
    }

    private void SetupPokeButtonListeners()
    {
        // PokeButton (Accept changes / Confirm answer)
        if (pokeButton != null)
        {
            pokeButton.selectEntered.RemoveAllListeners();
            pokeButton.selectEntered.AddListener((args) => ConfirmFinalAnswer());
        }

        // ResetButton (Reset object)
        if (resetButton != null)
        {
            resetButton.selectEntered.RemoveAllListeners();
            resetButton.selectEntered.AddListener((args) => ResetObject());
        }
    }

    private void SetButtonLabel(Button btn, string label)
    {
        if (btn == null) return;
        TMP_Text tmp = btn.GetComponentInChildren<TMP_Text>();
        if (tmp == null)
        {
            GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(btn.transform, false);
            tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 18;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            RectTransform rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }
        tmp.text = label;
    }

    #endregion

    #region Shape Modification

    public void SetShape(ShapeType shape)
    {
        if (targetObject == null || m_TargetMeshFilter == null) return;

        m_CurrentShape = shape;

        switch (shape)
        {
            case ShapeType.Cube:
                m_TargetMeshFilter.sharedMesh = cubeMesh;
                break;
            case ShapeType.Sphere:
                m_TargetMeshFilter.sharedMesh = sphereMesh;
                break;
            case ShapeType.Cylinder:
                m_TargetMeshFilter.sharedMesh = cylinderMesh;
                break;
        }

        ApplyColliderForShape(shape);
        UpdateUIDisplays();
        ShowStatus($"Shape set to: {shape}");
    }

    private void ApplyColliderForShape(ShapeType shape)
    {
        if (m_BoxCollider == null || m_SphereCollider == null || m_CapsuleCollider == null) return;

        m_BoxCollider.enabled = (shape == ShapeType.Cube);
        m_SphereCollider.enabled = (shape == ShapeType.Sphere);
        m_CapsuleCollider.enabled = (shape == ShapeType.Cylinder);

        Collider activeCollider = (shape == ShapeType.Cube) ? (Collider)m_BoxCollider :
                                  (shape == ShapeType.Sphere) ? (Collider)m_SphereCollider :
                                  (Collider)m_CapsuleCollider;

        if (m_TargetGrabInteractable != null)
        {
            m_TargetGrabInteractable.colliders.Clear();
            m_TargetGrabInteractable.colliders.Add(activeCollider);
        }
    }

    #endregion

    #region Color Modification

    public void SetColor(ColorType colorType)
    {
        m_CurrentColorType = colorType;
        switch (colorType)
        {
            case ColorType.Red:
                ApplyColor(ColorRed);
                break;
            case ColorType.Green:
                ApplyColor(ColorGreen);
                break;
            case ColorType.Blue:
                ApplyColor(ColorBlue);
                break;
        }

        UpdateUIDisplays();
        ShowStatus($"Color set to: {colorType}");
    }

    public void CycleColor()
    {
        ColorType nextColor = (ColorType)(((int)m_CurrentColorType + 1) % 3);
        SetColor(nextColor);
    }

    public void SetColorByName(string colorName)
    {
        if (string.IsNullOrWhiteSpace(colorName)) return;
        string lower = colorName.Trim().ToLowerInvariant();

        if (lower.Contains("red") || lower.Contains("rojo") || lower == "r")
        {
            SetColor(ColorType.Red);
        }
        else if (lower.Contains("green") || lower.Contains("verde") || lower == "g")
        {
            SetColor(ColorType.Green);
        }
        else if (lower.Contains("blue") || lower.Contains("azul") || lower == "b")
        {
            SetColor(ColorType.Blue);
        }
        else
        {
            if (ColorUtility.TryParseHtmlString(colorName, out Color parsedColor))
            {
                ApplyColor(parsedColor);
                ShowStatus($"Custom color: {colorName}");
            }
            else
            {
                ShowStatus($"Unknown color '{colorName}'. Try Red, Green, or Blue.");
            }
        }
    }

    private void ApplyColor(Color c)
    {
        m_CurrentColor = c;
        if (m_TargetMeshRenderer != null && m_TargetMeshRenderer.material != null)
        {
            m_TargetMeshRenderer.material.color = c;
            m_TargetMeshRenderer.material.SetColor("_BaseColor", c);
        }

        // Also tint the Color Input field background to match
        if (colorInputField != null)
        {
            Image bg = colorInputField.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = new Color(c.r, c.g, c.b, 0.45f);
            }
        }
    }

    private void OnColorInputEndEdit(string text)
    {
        SetColorByName(text);
    }

    #endregion

    #region Size Modification

    public void SetSize(SizeType sizeType)
    {
        if (targetObject == null) return;

        m_CurrentSizeType = sizeType;
        float mult = mediumScaleMultiplier;
        switch (sizeType)
        {
            case SizeType.Small:
                mult = smallScaleMultiplier;
                break;
            case SizeType.Medium:
                mult = mediumScaleMultiplier;
                break;
            case SizeType.Large:
                mult = largeScaleMultiplier;
                break;
        }

        targetObject.transform.localScale = m_InitialScale * mult;
        UpdateUIDisplays();
        ShowStatus($"Size set to: {sizeType}");
    }

    public void SetSizeByName(string sizeName)
    {
        if (string.IsNullOrWhiteSpace(sizeName)) return;
        string lower = sizeName.Trim().ToLowerInvariant();

        if (lower.Contains("small") || lower.Contains("peque") || lower == "s")
        {
            SetSize(SizeType.Small);
        }
        else if (lower.Contains("large") || lower.Contains("grande") || lower == "l")
        {
            SetSize(SizeType.Large);
        }
        else
        {
            SetSize(SizeType.Medium);
        }
    }

    #endregion

    #region Rotation Modification

    public void RotateAxis(Vector3 axis)
    {
        if (targetObject == null) return;

        if (axis == Vector3.right)
        {
            m_CurrentRotationAngles.x = (m_CurrentRotationAngles.x + rotationStepDegrees) % 360f;
        }
        else if (axis == Vector3.up)
        {
            m_CurrentRotationAngles.y = (m_CurrentRotationAngles.y + rotationStepDegrees) % 360f;
        }
        else if (axis == Vector3.forward)
        {
            m_CurrentRotationAngles.z = (m_CurrentRotationAngles.z + rotationStepDegrees) % 360f;
        }

        ApplyCurrentRotation();
        UpdateUIDisplays();
        ShowStatus($"Rotated {axis}: ({m_CurrentRotationAngles.x:0}°, {m_CurrentRotationAngles.y:0}°, {m_CurrentRotationAngles.z:0}°)");
    }

    public void RotateX() => RotateAxis(Vector3.right);
    public void RotateY() => RotateAxis(Vector3.up);
    public void RotateZ() => RotateAxis(Vector3.forward);

    private void SetRotationAngle(int axisIndex, string angleStr)
    {
        if (float.TryParse(angleStr.Replace("°", "").Trim(), out float angle))
        {
            angle = (angle % 360f + 360f) % 360f;
            if (axisIndex == 0) m_CurrentRotationAngles.x = angle;
            else if (axisIndex == 1) m_CurrentRotationAngles.y = angle;
            else if (axisIndex == 2) m_CurrentRotationAngles.z = angle;

            ApplyCurrentRotation();
            UpdateUIDisplays();
            ShowStatus($"Rotation set to: ({m_CurrentRotationAngles.x:0}°, {m_CurrentRotationAngles.y:0}°, {m_CurrentRotationAngles.z:0}°)");
        }
    }

    private void ApplyCurrentRotation()
    {
        if (targetObject == null) return;
        targetObject.transform.rotation = m_InitialRotation * Quaternion.Euler(m_CurrentRotationAngles);
    }

    #endregion

    #region Reset & Confirm (Poke Buttons)

    /// <summary>
    /// Resets the object to its original state (shape, color, size, rotation, position).
    /// Triggered by the physical ResetButton or UI reset.
    /// </summary>
    public void ResetObject()
    {
        if (targetObject == null) return;

        // Reset Rigidbody velocity
        if (m_TargetRigidbody != null)
        {
            m_TargetRigidbody.linearVelocity = Vector3.zero;
            m_TargetRigidbody.angularVelocity = Vector3.zero;
        }

        // Reset Transform
        targetObject.transform.position = m_InitialPosition;
        targetObject.transform.rotation = m_InitialRotation;
        targetObject.transform.localScale = m_InitialScale;

        // Reset States
        m_CurrentShape = m_InitialShape;
        m_CurrentColor = m_InitialColor;
        m_CurrentSizeType = SizeType.Medium;
        m_CurrentRotationAngles = Vector3.zero;

        // Apply Shape
        if (m_TargetMeshFilter != null)
        {
            m_TargetMeshFilter.sharedMesh = cubeMesh;
        }
        ApplyColliderForShape(ShapeType.Cube);

        // Apply Color
        if (m_TargetMeshRenderer != null && m_TargetMeshRenderer.material != null)
        {
            m_TargetMeshRenderer.material.color = m_InitialColor;
            m_TargetMeshRenderer.material.SetColor("_BaseColor", m_InitialColor);
        }

        UpdateUIDisplays();
        ShowStatus("<color=#FF9800>Object reset to original state!</color>");
        Debug.Log("[ObjectModifierManager] Object reset to original state.");
    }

    /// <summary>
    /// Confirms the final answer / accepts the changes.
    /// Triggered by the physical PokeButton.
    /// </summary>
    public void ConfirmFinalAnswer()
    {
        Debug.Log("[ObjectModifierManager] PokeButton pressed: Confirming final answer...");

        bool hasReference = (referenceTarget != null);
        if (!hasReference)
        {
            ShowStatus("<color=#4CAF50>Changes Accepted! Final Answer Confirmed!</color>");
            return;
        }

        // Compare against referenceTarget
        bool shapeMatch = CheckShapeMatch(referenceTarget);
        bool colorMatch = CheckColorMatch(referenceTarget);
        bool sizeMatch = CheckSizeMatch(referenceTarget);
        bool rotationMatch = CheckRotationMatch(referenceTarget);

        bool allMatch = shapeMatch && colorMatch && sizeMatch && rotationMatch;

        if (allMatch)
        {
            ShowStatus("<color=#4CAF50>Changes Accepted! CORRECT: Perfect Match with Target! \u2714</color>");
            Debug.Log("[ObjectModifierManager] Final answer confirmed: PERFECT MATCH!");
        }
        else
        {
            List<string> diffs = new List<string>();
            if (!shapeMatch) diffs.Add("Shape");
            if (!colorMatch) diffs.Add("Color");
            if (!sizeMatch) diffs.Add("Size");
            if (!rotationMatch) diffs.Add("Rotation");

            string diffStr = string.Join(", ", diffs);
            ShowStatus($"<color=#FFC107>Changes Accepted! Differences: {diffStr}</color>");
            Debug.Log($"[ObjectModifierManager] Final answer confirmed. Differences: {diffStr}");
        }
    }

    #endregion

    #region Comparison Helpers

    private bool CheckShapeMatch(GameObject refObj)
    {
        MeshFilter mf = refObj.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null || m_TargetMeshFilter == null) return true;

        string refName = mf.sharedMesh.name.ToLowerInvariant();
        if (refName.Contains("cube") || refName.Contains("box"))
            return m_CurrentShape == ShapeType.Cube;
        if (refName.Contains("sphere"))
            return m_CurrentShape == ShapeType.Sphere;
        if (refName.Contains("cylinder"))
            return m_CurrentShape == ShapeType.Cylinder;

        return true;
    }

    private bool CheckColorMatch(GameObject refObj)
    {
        MeshRenderer mr = refObj.GetComponent<MeshRenderer>();
        if (mr == null || mr.sharedMaterial == null) return true;

        Color refColor = mr.sharedMaterial.color;
        float dist = Vector4.Distance((Vector4)refColor, (Vector4)m_CurrentColor);
        return dist < 0.35f;
    }

    private bool CheckSizeMatch(GameObject refObj)
    {
        float refScaleMag = refObj.transform.localScale.magnitude;
        float currentScaleMag = targetObject.transform.localScale.magnitude;
        float ratio = currentScaleMag / Mathf.Max(0.001f, refScaleMag);
        return Mathf.Abs(ratio - 1f) < 0.25f;
    }

    private bool CheckRotationMatch(GameObject refObj)
    {
        if (m_CurrentShape == ShapeType.Sphere) return true;

        float angle = Quaternion.Angle(targetObject.transform.rotation, refObj.transform.rotation);

        if (m_CurrentShape == ShapeType.Cylinder)
        {
            Vector3 myUp = targetObject.transform.up;
            Vector3 refUp = refObj.transform.up;
            return Vector3.Angle(myUp, refUp) < 15f;
        }

        if (m_CurrentShape == ShapeType.Cube)
        {
            return (angle % 90f < 15f || angle % 90f > 75f);
        }

        return angle < 20f;
    }

    #endregion

    #region UI Presentation & Feedback

    private void UpdateUIDisplays()
    {
        // Update Color Input field text
        if (colorInputField != null)
        {
            colorInputField.text = m_CurrentColorType.ToString();
        }

        // Update Size Inputs
        if (sizeInputSmall != null) sizeInputSmall.text = (m_CurrentSizeType == SizeType.Small) ? "[Small]" : "Small";
        if (sizeInputMedium != null) sizeInputMedium.text = (m_CurrentSizeType == SizeType.Medium) ? "[Med]" : "Med";
        if (sizeInputLarge != null) sizeInputLarge.text = (m_CurrentSizeType == SizeType.Large) ? "[Large]" : "Large";

        // Update Rotation Inputs
        if (rotationInputX != null) rotationInputX.text = $"{m_CurrentRotationAngles.x:0}°";
        if (rotationInputY != null) rotationInputY.text = $"{m_CurrentRotationAngles.y:0}°";
        if (rotationInputZ != null) rotationInputZ.text = $"{m_CurrentRotationAngles.z:0}°";

        // Highlight active shape button
        HighlightButton(cubeButton, m_CurrentShape == ShapeType.Cube);
        HighlightButton(sphereButton, m_CurrentShape == ShapeType.Sphere);
        HighlightButton(cylinderButton, m_CurrentShape == ShapeType.Cylinder);

        // Highlight active size button
        HighlightButton(smallButton, m_CurrentSizeType == SizeType.Small);
        HighlightButton(mediumButton, m_CurrentSizeType == SizeType.Medium);
        HighlightButton(largeButton, m_CurrentSizeType == SizeType.Large);
    }

    private void HighlightButton(Button btn, bool active)
    {
        if (btn == null) return;
        var colors = btn.colors;
        colors.normalColor = active ? new Color(0.2f, 0.7f, 1f, 1f) : Color.white;
        colors.selectedColor = colors.normalColor;
        btn.colors = colors;
    }

    private void ShowStatus(string msg)
    {
        if (statusText != null)
        {
            statusText.text = msg;
        }
        else
        {
            Debug.Log($"[Status] {msg}");
        }
    }

    #endregion

    #region Editor Testing

#if UNITY_EDITOR
    private void HandleEditorKeyboardInput()
    {
        // Shapes: 1 (Cube), 2 (Sphere), 3 (Cylinder)
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetShape(ShapeType.Cube);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetShape(ShapeType.Sphere);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetShape(ShapeType.Cylinder);

        // Color cycle: C
        if (Input.GetKeyDown(KeyCode.C)) CycleColor();

        // Size cycle: S
        if (Input.GetKeyDown(KeyCode.S))
        {
            SizeType nextSize = (SizeType)(((int)m_CurrentSizeType + 1) % 3);
            SetSize(nextSize);
        }

        // Rotation: X, Y, Z
        if (Input.GetKeyDown(KeyCode.X)) RotateX();
        if (Input.GetKeyDown(KeyCode.Y)) RotateY();
        if (Input.GetKeyDown(KeyCode.Z)) RotateZ();

        // Reset: R
        if (Input.GetKeyDown(KeyCode.R)) ResetObject();

        // Confirm: Space or Return
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) ConfirmFinalAnswer();
    }
#endif

    #endregion
}
