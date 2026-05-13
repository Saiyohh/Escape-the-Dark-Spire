using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DarkSpire
{
    public class TargetingArrow : MonoBehaviour
    {
        public static TargetingArrow Instance { get; private set; }

        [Header("Sprites")]
        [Tooltip("Small circle/dot sprite used for each segment of the arrow body.")]
        [SerializeField] private Sprite segmentSprite;

        [Tooltip("Triangle/arrowhead sprite placed at the tip of the arrow.")]
        [SerializeField] private Sprite arrowHeadSprite;

        [Header("Size — Live-Tweakable")]
        [Tooltip("Global multiplier applied on top of every other size below. " +
                 "Drag down if the whole arrow is too big.")]
        [Range(0.1f, 2f)]
        [SerializeField] private float overallScale = 1f;

        [Tooltip("Reference pixel size for each segment in screen space. " +
                 "Combined with overallScale and segmentScaleStart/End to get " +
                 "the actual on-screen size. Live — tweak during Play.")]
        [Range(20f, 500f)]
        [SerializeField] private float segmentPixelSize = 90f;

        [Tooltip("Scale of the first segment (near source). Grows toward tip.")]
        [Range(0.05f, 1.5f)]
        [SerializeField] private float segmentScaleStart = 0.45f;

        [Tooltip("Scale of the last segment (near tip).")]
        [Range(0.1f, 2f)]
        [SerializeField] private float segmentScaleEnd = 0.9f;

        [Tooltip("Arrowhead multiplier. Ends up at segmentPixelSize × overallScale × arrowHeadScale.")]
        [Range(0.1f, 2.5f)]
        [SerializeField] private float arrowHeadScale = 1.2f;

        [Header("Segment Count — EDIT-TIME ONLY")]
        [Tooltip("Number of segments along the curve. Changing this at runtime " +
                 "does NOT rebuild the sprite array — only edit-time + scene-restart.")]
        [SerializeField] private int segmentCount = 19;

        [Header("Curve Settings")]
        [Tooltip("How much the curve arches upward, as a multiplier of source-to-target distance.")]
        [SerializeField] private float archHeight = 0.35f;

        [Tooltip("How much the control point X bends opposite the target direction.")]
        [SerializeField] private float bendAmount = 0.25f;

        [Header("Colors")]
        [SerializeField] private Color defaultColor = Color.white;

        [Tooltip("Arrow color when hovering a valid enemy target.")]
        [SerializeField] private Color enemyHighlightColor = new Color(0.90f, 0.12f, 0.11f); // #E61E1B

        [Tooltip("Arrow color when hovering a valid ally target.")]
        [SerializeField] private Color allyHighlightColor = new Color(0.21f, 0.78f, 0.54f);  // #36C78A

        [Tooltip("Arrow color when hovering the correct faction but out of range. " +
                 "Communicates 'I see the target, I just can't reach it.'")]
        [SerializeField] private Color outOfRangeColor = new Color(0.55f, 0.55f, 0.55f);

        [Header("Highlight Animation")]
        [Tooltip("Speed of the subtle pulse when highlighted.")]
        [SerializeField] private float highlightPulseSpeed = 4f;

        [Tooltip("Minimum brightness multiplier during pulse.")]
        [SerializeField] private float highlightPulseMin = 0.80f;

        [Header("Overlay Canvas")]
        [Tooltip("Sort order for the overlay canvas (above all other canvases).")]
        [SerializeField] private int canvasSortOrder = 1000;

        // Runtime
        private Canvas overlayCanvas;
        private RectTransform canvasRT;
        private Image[] segments;
        private Image arrowHead;
        private RectTransform[] segmentRTs;
        private RectTransform arrowHeadRT;
        private Camera mainCamera;
        private bool isDrawing;
        private Vector2 fromPosition; // world-space origin
        private Color currentColor;
        private bool isHighlighted;

        // ═══════════════════════════════════════════════════
        //  Lifecycle
        // ═══════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            mainCamera = Camera.main;
            CreateOverlayCanvas();
            CreateSprites();
            Hide();
        }

        private void CreateOverlayCanvas()
        {
            var canvasGO = new GameObject("TargetingArrowCanvas");
            canvasGO.transform.SetParent(transform, false);

            overlayCanvas = canvasGO.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = canvasSortOrder;

            // CanvasScaler so segments scale with screen resolution
            // instead of being rendered at raw pixel size (which makes them tiny on high-res displays)
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasRT = canvasGO.GetComponent<RectTransform>();
        }

        private void CreateSprites()
        {
            segments = new Image[segmentCount];
            segmentRTs = new RectTransform[segmentCount];

            for (int i = 0; i < segmentCount; i++)
            {
                var go = new GameObject($"Segment_{i}");
                go.transform.SetParent(overlayCanvas.transform, false);

                var rt = go.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(segmentPixelSize, segmentPixelSize);

                var img = go.AddComponent<Image>();
                img.sprite = segmentSprite;
                img.color = defaultColor;
                img.raycastTarget = false;

                segments[i] = img;
                segmentRTs[i] = rt;
            }

            var headGO = new GameObject("ArrowHead");
            headGO.transform.SetParent(overlayCanvas.transform, false);

            arrowHeadRT = headGO.AddComponent<RectTransform>();
            arrowHeadRT.sizeDelta = new Vector2(segmentPixelSize, segmentPixelSize);

            arrowHead = headGO.AddComponent<Image>();
            arrowHead.sprite = arrowHeadSprite;
            arrowHead.color = defaultColor;
            arrowHead.raycastTarget = false;
        }

        private bool subscribed;

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void Start()
        {
            TrySubscribe();
        }

        private void TrySubscribe()
        {
            if (subscribed || TargetingSystem.Instance == null) return;

            TargetingSystem.Instance.OnTargetingStateChanged += HandleTargetingStateChanged;
            TargetingSystem.Instance.OnTargetHovered += HandleTargetHovered;
            TargetingSystem.Instance.OnTargetUnhovered += HandleTargetUnhovered;
            subscribed = true;
        }

        private void OnDisable()
        {
            if (TargetingSystem.Instance != null)
            {
                TargetingSystem.Instance.OnTargetingStateChanged -= HandleTargetingStateChanged;
                TargetingSystem.Instance.OnTargetHovered -= HandleTargetHovered;
                TargetingSystem.Instance.OnTargetUnhovered -= HandleTargetUnhovered;
            }
            subscribed = false;
        }

        // ═══════════════════════════════════════════════════
        //  Event Handlers
        // ═══════════════════════════════════════════════════

        private void HandleTargetingStateChanged(bool targeting)
        {
            if (targeting)
            {
                // Use the override origin if the triggering action set one;
                // otherwise fall back to the active unit's display position.
                var ts = TargetingSystem.Instance;
                if (ts != null && ts.ArrowOriginOverride != null)
                {
                    fromPosition = ts.ArrowOriginOverride.position;
                }
                else
                {
                    var activeUnit = CombatManager.Instance?.ActiveUnit;
                    if (activeUnit != null)
                    {
                        var display = UnitDisplay.GetDisplay(activeUnit);
                        if (display != null)
                            fromPosition = display.transform.position;
                    }
                }
                Show();
            }
            else
            {
                Hide();
            }
        }

        private void HandleTargetHovered(Unit unit)
        {
            if (!isDrawing || unit == null) return;
            var ts = TargetingSystem.Instance;
            if (ts == null) return;

            if (ts.IsValidTarget(unit))
            {
                // Correct faction + in range → red/green with pulse
                isHighlighted = true;
                currentColor = unit.isPlayerControlled ? allyHighlightColor : enemyHighlightColor;
                ApplyColor(currentColor);
            }
            else if (IsCorrectFactionForMode(unit, ts.CurrentMode, ts.Caster))
            {
                // Correct faction, wrong distance → greyed, no pulse
                isHighlighted = false;
                currentColor = outOfRangeColor;
                ApplyColor(currentColor);
            }
            // else: wrong faction — leave arrow in default color
        }

        private static bool IsCorrectFactionForMode(Unit unit, TargetMode mode, Unit caster)
        {
            if (caster == null) return false;
            bool casterIsPlayer = caster.isPlayerControlled;
            bool unitIsAlly = unit.isPlayerControlled == casterIsPlayer;

            switch (mode)
            {
                case TargetMode.SingleEnemy:
                case TargetMode.AllEnemies:
                case TargetMode.RandomEnemy:
                    return !unitIsAlly;
                case TargetMode.SingleAlly:
                case TargetMode.AllAllies:
                case TargetMode.RandomAlly:
                case TargetMode.WholeParty:
                    return unitIsAlly;
                default:
                    return false;
            }
        }

        private void HandleTargetUnhovered()
        {
            if (!isDrawing) return;

            isHighlighted = false;
            currentColor = defaultColor;
            ApplyColor(currentColor);
        }

        // ═══════════════════════════════════════════════════
        //  Frame Update
        // ═══════════════════════════════════════════════════

        private void Update()
        {
            if (!isDrawing) return;
            if (Mouse.current == null || mainCamera == null) return;

            // Track the origin in real-time if it's a UI element (screen-space)
            var ts = TargetingSystem.Instance;
            if (ts != null && ts.ArrowOriginOverride != null)
            {
                // If the origin is a RectTransform (UI element), convert screen → world
                var rt = ts.ArrowOriginOverride as RectTransform;
                if (rt != null)
                {
                    Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(
                        null, rt.position);
                    fromPosition = mainCamera.ScreenToWorldPoint(
                        new Vector3(screenPos.x, screenPos.y, -mainCamera.transform.position.z));
                }
                else
                {
                    fromPosition = ts.ArrowOriginOverride.position;
                }
            }

            // Convert mouse screen position to world position
            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(
                new Vector3(mouseScreen.x, mouseScreen.y, -mainCamera.transform.position.z));
            Vector2 toPosition = mouseWorld;

            UpdateArrow(fromPosition, toPosition);

            // Subtle pulse when highlighted (same pattern as EndTurnButtonUI glow)
            if (isHighlighted)
            {
                float pulse = Mathf.Lerp(highlightPulseMin, 1f,
                    (Mathf.Sin(Time.time * highlightPulseSpeed) + 1f) * 0.5f);
                Color pulsed = new Color(
                    currentColor.r * pulse,
                    currentColor.g * pulse,
                    currentColor.b * pulse,
                    1f);
                ApplyColor(pulsed);
            }
        }

        // ═══════════════════════════════════════════════════
        //  Bezier Arrow Rendering
        // ═══════════════════════════════════════════════════

        private void UpdateArrow(Vector2 from, Vector2 to)
        {
            Vector2 controlPoint = ComputeControlPoint(from, to);

            // Live-apply size: sizeDelta × overallScale. This is cheap (20 writes)
            // and lets the inspector fields work during Play mode without having
            // to restart the scene.
            float basePx = segmentPixelSize * overallScale;
            Vector2 sizeDelta = new Vector2(basePx, basePx);

            // Place each segment along the bezier curve (world space → screen space)
            for (int i = 0; i < segmentCount; i++)
            {
                float t = (float)(i + 1) / (segmentCount + 1);

                // Scale: grows from start to end (STS2 pattern: 0.28 → 0.42)
                float scale = Mathf.Lerp(segmentScaleStart, segmentScaleEnd, t);
                segmentRTs[i].sizeDelta = sizeDelta;
                segmentRTs[i].localScale = Vector3.one * scale;

                // Position along bezier (world → screen)
                Vector2 pos = BezierCurve(from, to, controlPoint, t);
                Vector3 screenPos = mainCamera.WorldToScreenPoint(
                    new Vector3(pos.x, pos.y, 0f));
                segmentRTs[i].position = new Vector3(screenPos.x, screenPos.y, 0f);

                // Rotation: face toward next bezier sample (tangent approximation)
                float tNext = (float)(i + 2) / (segmentCount + 1);
                tNext = Mathf.Min(tNext, 1f);
                Vector2 nextPos = BezierCurve(from, to, controlPoint, tNext);

                // Use screen-space direction for rotation
                Vector3 nextScreen = mainCamera.WorldToScreenPoint(
                    new Vector3(nextPos.x, nextPos.y, 0f));
                Vector2 dir = new Vector2(nextScreen.x - screenPos.x, nextScreen.y - screenPos.y);
                if (dir.sqrMagnitude > 0.0001f)
                {
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    segmentRTs[i].localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
                }
            }

            // Arrowhead: sits at the end of the curve, pointing along the tangent
            float tLast = (float)segmentCount / (segmentCount + 1);
            Vector2 lastSegPos = BezierCurve(from, to, controlPoint, tLast);
            Vector2 arrowDir = to - lastSegPos;

            Vector3 tipScreen = mainCamera.WorldToScreenPoint(
                new Vector3(to.x, to.y, 0f));
            arrowHeadRT.position = new Vector3(tipScreen.x, tipScreen.y, 0f);
            arrowHeadRT.sizeDelta = sizeDelta;
            arrowHeadRT.localScale = Vector3.one * arrowHeadScale;

            // Use screen-space direction for arrowhead rotation
            Vector3 lastSegScreen = mainCamera.WorldToScreenPoint(
                new Vector3(lastSegPos.x, lastSegPos.y, 0f));
            Vector2 headDir = new Vector2(tipScreen.x - lastSegScreen.x, tipScreen.y - lastSegScreen.y);
            if (headDir.sqrMagnitude > 0.0001f)
            {
                float arrowAngle = Mathf.Atan2(headDir.y, headDir.x) * Mathf.Rad2Deg;
                arrowHeadRT.localRotation = Quaternion.Euler(0f, 0f, arrowAngle - 90f);
            }
        }

        private Vector2 ComputeControlPoint(Vector2 from, Vector2 to)
        {
            Vector2 control;

            // X: offset opposite the target direction (creates the curve bend)
            // STS2: controlPoint.X = From.X - (ArrowHead.X - From.X) * 0.25
            control.x = from.x - (to.x - from.x) * bendAmount;

            // Y: arch upward from the highest point, proportional to distance
            float maxY = Mathf.Max(from.y, to.y);
            float dist = Vector2.Distance(from, to);
            control.y = maxY + dist * archHeight;

            return control;
        }

        private static Vector2 BezierCurve(Vector2 p0, Vector2 p1, Vector2 control, float t)
        {
            float u = 1f - t;
            return u * u * p0 + 2f * u * t * control + t * t * p1;
        }

        // ═══════════════════════════════════════════════════
        //  Show / Hide
        // ═══════════════════════════════════════════════════

        private void Show()
        {
            isDrawing = true;
            isHighlighted = false;
            currentColor = defaultColor;

            for (int i = 0; i < segmentCount; i++)
                segments[i].enabled = true;
            arrowHead.enabled = true;

            ApplyColor(currentColor);
        }

        public void Hide()
        {
            isDrawing = false;
            isHighlighted = false;

            if (segments != null)
            {
                for (int i = 0; i < segmentCount; i++)
                    if (segments[i] != null) segments[i].enabled = false;
            }
            if (arrowHead != null) arrowHead.enabled = false;
        }

        private void ApplyColor(Color color)
        {
            if (segments != null)
            {
                for (int i = 0; i < segmentCount; i++)
                    if (segments[i] != null) segments[i].color = color;
            }
            if (arrowHead != null) arrowHead.color = color;
        }
    }
}
