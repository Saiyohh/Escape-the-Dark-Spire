using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkSpire
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class UnitDisplay : MonoBehaviour
    {
        private static Dictionary<Unit, UnitDisplay> registry = new();
        public static UnitDisplay GetDisplay(Unit unit) =>
            registry.TryGetValue(unit, out var display) ? display : null;

        [Header("References")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("Source material for the per-hue Black & White adjust applied to " +
                 "this unit's sprite when EnemyData.bwGrayscale is true. Author a " +
                 "material from DarkSpire/Sprite/BlackAndWhite (the SpriteRenderer " +
                 "variant — NOT the UI variant) and drop it here. The runtime " +
                 "clones it per-unit so each enemy's six channel weights don't " +
                 "bleed into the shared asset. Leave null to disable BW.")]
        [SerializeField] private Material blackAndWhiteMaterial;
        private Material runtimeBWMaterial;
        private static readonly int PropWR_BW = Shader.PropertyToID("_WR");
        private static readonly int PropWY_BW = Shader.PropertyToID("_WY");
        private static readonly int PropWG_BW = Shader.PropertyToID("_WG");
        private static readonly int PropWC_BW = Shader.PropertyToID("_WC");
        private static readonly int PropWB_BW = Shader.PropertyToID("_WB");
        private static readonly int PropWM_BW = Shader.PropertyToID("_WM");

        [Tooltip("Sibling BoxCollider2D that defines the targeting hitbox. " +
                 "Authored directly on the prefab — tweak size/offset on the " +
                 "collider and this script follows. Found via GetComponent if " +
                 "left null.")]
        [SerializeField] private BoxCollider2D boxCollider;

        [Tooltip("Child Transform that marks where the turn indicator should " +
                 "sit above this unit. Convention: create an empty child " +
                 "named 'TurnIndicatorAnchor' and position it at the top of " +
                 "the hitbox, above the head. Falls back to the hitbox's " +
                 "top-center if left null.")]
        [SerializeField] private Transform turnIndicatorAnchor;

        [Header("Corner Bracket Highlight")]
        [Tooltip("Bracket/corner sprite used at each corner of the hitbox.\n" +
                 "Base orientation = top-left. Flipped for other corners.")]
        [SerializeField] private Sprite cornerBracketSprite;

        [Tooltip("Color tint for the corner bracket sprites when highlighted.")]
        [SerializeField] private Color highlightColor = new Color(1f, 1f, 1f, 1f);

        [Tooltip("How much to inset the brackets from the hitbox edges (world units).")]
        [SerializeField] private float cornerInset = 0.05f;

        [Tooltip("Scale of each corner bracket sprite.")]
        [SerializeField] private float cornerScale = 0.5f;

        [Tooltip("Sorting layer for corner bracket sprites. Must match a layer " +
                 "in Edit → Project Settings → Tags and Layers → Sorting Layers.")]
        [SerializeField] private string cornerSortingLayer = "Combat HUD";

        [Tooltip("Sorting order for corner bracket sprites (above unit sprite).")]
        [SerializeField] private int cornerSortingOrder = 90;

        [Header("Sprite Sorting")]
        [Tooltip("Sorting layer assigned to this unit's main SpriteRenderer. " +
                 "Front-most ranks render above behind ranks within this layer; " +
                 "the active acting unit renders above everyone via a temporary " +
                 "boost.")]
        [SerializeField] private string unitsSortingLayer = "Units";
        [Tooltip("How much each rank step shifts the sorting order. Larger = " +
                 "more headroom between ranks (room for additional layered " +
                 "sprites without overlap).")]
        [SerializeField] private int rankOrderStep = 10;
        [Tooltip("Order added on top of the rank-based base while this unit is " +
                 "the active actor. Big enough to clear every other unit on " +
                 "the layer (default 100 = jumps over ~10 ranks of headroom).")]
        [SerializeField] private int activeUnitOrderBoost = 100;
        private int currentBaseOrder;
        private bool isActiveOrderBoosted;

        [Header("Animation Settings")]
        [SerializeField] private float attackLungeDistance = 0.5f;
        [SerializeField] private float attackLungeDuration = 0.30f;

        [Tooltip("Distance the unit is knocked back when taking damage.")]
        [SerializeField] private float hurtKnockbackDistance = 0.25f;
        [SerializeField] private float hurtKnockbackDuration = 0.12f;
        [SerializeField] private float hurtKnockbackReturnDuration = 0.20f;

        [Tooltip("Shake used for debuff application (lighter than old damage shake).")]
        [SerializeField] private float debuffShakeDuration = 0.3f;
        [SerializeField] private float debuffShakeIntensity = 0.05f;

        [SerializeField] private float deathFadeDuration = 1.0f;
        [Tooltip("Distance the dead unit's sprite drifts downward as it fades " +
                 "out. World units. Tweak to taste — applied uniformly to every " +
                 "unit so the death timing reads as a single global motion.")]
        [SerializeField] private float deathDropDistance = 1.0f;

        public float DeathFadeDuration => deathFadeDuration;

        [Header("Flash Colors")]
        [SerializeField] private Color normalColor = Color.white;

        [Header("Inactive Dim")]
        [Tooltip("When another unit is taking its turn, this unit's sprite tints " +
                 "to dimmedColor instead of normalColor — focuses attention on " +
                 "whoever's active. Damage/heal flash resets honor the current " +
                 "dim state, so flashes still play correctly mid-dim.")]
        [SerializeField] private bool dimWhenInactive = true;
        [SerializeField] private Color dimmedColor = new Color(0.45f, 0.45f, 0.45f, 1f);

        private bool isDimmed;
        private Color BaseColor => (isDimmed && dimWhenInactive) ? dimmedColor : normalColor;

        [Header("Speech Bubble")]
        [Tooltip("Empty child Transform marking where the action-refusal speech " +
                 "bubble spawns (e.g. 'No SP', 'No action'). The bubble prefab " +
                 "uses bottom-left pivot, so position this where the tail should " +
                 "attach — typically just above the unit's head, on the side " +
                 "facing the screen interior.")]
        [SerializeField] private Transform speechBubbleAnchor;

        [Tooltip("Speech bubble prefab (Canvas + SpeechBubbleController). " +
                 "Spawned as a child of speechBubbleAnchor so it follows the " +
                 "unit through lunge / knockback / move animations.")]
        [SerializeField] private GameObject speechBubblePrefab;

        private SpeechBubbleController activeSpeechBubble;

        [Header("World HUD (Player Only)")]
        [SerializeField] private GameObject unitWorldHUDPrefab;
        [SerializeField] private Vector3 worldHUDOffset = new Vector3(0, -1.2f, 0);

        [Header("World HUD (Enemy Only)")]
        [SerializeField] private GameObject enemyWorldHUDPrefab;

        [Tooltip("World-space offset from transform origin for the BELOW region " +
                 "(HP bar, DEF, conditions).\nPositioned at the enemy's feet.")]
        [SerializeField] private Vector3 enemyWorldHUDOffset = new Vector3(0, -1.0f, 0);

        [Tooltip("World-space offset from transform origin for the INTENT region " +
                 "(icons above the enemy's head).\nSet per-prefab so intents sit just above each enemy.")]
        [SerializeField] private Vector3 enemyIntentAnchorOffset = new Vector3(0, 1.5f, 0);

        public Unit LinkedUnit { get; private set; }

        public Vector2 TurnIndicatorOffset
        {
            get
            {
                if (turnIndicatorAnchor != null)
                    return (Vector2)turnIndicatorAnchor.localPosition;

                if (boxCollider != null)
                    return new Vector2(boxCollider.offset.x,
                                       boxCollider.offset.y + boxCollider.size.y * 0.5f);

                return new Vector2(0f, 2f); // legacy default
            }
        }

        public Vector2 HitboxSize => boxCollider != null ? boxCollider.size : Vector2.one;

        public Vector2 HitboxOffset => boxCollider != null ? boxCollider.offset : Vector2.zero;

        public Vector3 FloaterAnchorWorld
        {
            get
            {
                if (boxCollider != null)
                {
                    float topY = boxCollider.offset.y + boxCollider.size.y * 0.5f;
                    return transform.position + new Vector3(0f, topY, 0f);
                }
                return LinkedUnit != null && !LinkedUnit.isPlayerControlled
                    ? transform.position + enemyIntentAnchorOffset
                    : transform.position + (Vector3)TurnIndicatorOffset;
            }
        }

        public void ShowSpeechBubble(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (speechBubblePrefab == null || speechBubbleAnchor == null) return;

            var sharedCanvas = CombatUIManager.WorldCanvas;
            if (sharedCanvas == null)
            {
                Debug.LogWarning(
                    "[UnitDisplay] ShowSpeechBubble called but " +
                    "CombatUIManager.WorldCanvas isn't available — bubble " +
                    "prefab requires a Canvas ancestor to render. Make sure " +
                    "CombatUIManager is initialized before player input.", this);
                return;
            }

            if (activeSpeechBubble != null)
                Destroy(activeSpeechBubble.gameObject);

            var go = Instantiate(speechBubblePrefab, sharedCanvas.transform, worldPositionStays: false);
            go.transform.position = speechBubbleAnchor.position;

            activeSpeechBubble = go.GetComponent<SpeechBubbleController>();
            if (activeSpeechBubble != null)
                activeSpeechBubble.Show(message);
            else
                Debug.LogWarning(
                    "[UnitDisplay] speechBubblePrefab is missing the " +
                    "SpeechBubbleController component on its root.", this);
        }

        private Vector3 originalPosition;
        private bool isDead;
        private UnitWorldHUD worldHUD;
        private EnemyWorldHUD enemyWorldHUD;

        public bool SuppressDamageFlash { get; set; }

        public bool SuppressDeathAnimation { get; set; }

        public bool HasPendingDeath { get; private set; }

        private bool suppressConditionUI;
        public bool SuppressConditionUI
        {
            get => suppressConditionUI;
            set
            {
                bool wasSuppressed = suppressConditionUI;
                suppressConditionUI = value;
                if (wasSuppressed && !value) OnConditionUISuppressionLifted?.Invoke();
            }
        }
        public event System.Action OnConditionUISuppressionLifted;

        private bool suppressUIUpdates;
        public bool SuppressUIUpdates
        {
            get => suppressUIUpdates;
            set
            {
                bool wasSuppressed = suppressUIUpdates;
                suppressUIUpdates = value;
                if (wasSuppressed && !value) OnUISuppressionLifted?.Invoke();
            }
        }
        public event System.Action OnUISuppressionLifted;

        private SpriteRenderer[] cornerBrackets;
        private Vector3[] cornerRestPositions; // cached default positions
        private bool isHighlighted;
        private Coroutine highlightAnim;

        public UnitWorldHUD WorldHUD => worldHUD;

        public EnemyWorldHUD EnemyWorldHUD => enemyWorldHUD;

        private Transform animRoot;

        private void Awake()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

            if (spriteRenderer == null)
            {
                Debug.LogError(
                    "[UnitDisplay] No SpriteRenderer found in children. " +
                    "Author the unit's SpriteRenderer on a child GameObject " +
                    "of this prefab (e.g. 'SpriteRoot' or 'Body') and either " +
                    "wire it into UnitDisplay.spriteRenderer or rely on the " +
                    "auto GetComponentInChildren lookup.", this);
            }
            else if (spriteRenderer.transform == transform)
            {
                Debug.LogError(
                    "[UnitDisplay] SpriteRenderer is on the prefab ROOT, " +
                    "but it must live on a CHILD GameObject so attack lunge / " +
                    "knockback / death-drop animations only move the sprite " +
                    "and leave HUDs, orb tray, aura, and turn indicator at " +
                    "the rest pose. Move the SpriteRenderer to a child " +
                    "GameObject (e.g. 'SpriteRoot') and re-wire the reference.",
                    this);
            }

            animRoot = spriteRenderer != null ? spriteRenderer.transform : transform;

            if (boxCollider == null)
                boxCollider = GetComponent<BoxCollider2D>();

            if (boxCollider != null)
                boxCollider.isTrigger = true;

            CreateCornerBrackets();
        }

        private void CreateCornerBrackets()
        {
            if (cornerBracketSprite == null) return;

            cornerBrackets = new SpriteRenderer[4];
            string[] names = { "Bracket_TL", "Bracket_TR", "Bracket_BL", "Bracket_BR" };
            bool[] flipX = { false, true, false, true };
            bool[] flipY = { false, false, true, true };

            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject(names[i]);
                go.transform.SetParent(transform);
                go.transform.localScale = Vector3.one * cornerScale;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = cornerBracketSprite;
                sr.color = highlightColor;
                sr.flipX = flipX[i];
                sr.flipY = flipY[i];
                sr.sortingLayerName = cornerSortingLayer;
                sr.sortingOrder = cornerSortingOrder;
                sr.enabled = false;

                cornerBrackets[i] = sr;
            }

            PositionCornerBrackets();
        }

        private static readonly Vector3[] cornerDirections =
        {
            new Vector3(-1, 1, 0).normalized,   // TL
            new Vector3( 1, 1, 0).normalized,   // TR
            new Vector3(-1,-1, 0).normalized,    // BL
            new Vector3( 1,-1, 0).normalized,    // BR
        };

        private void PositionCornerBrackets()
        {
            if (cornerBrackets == null) return;

            var size   = HitboxSize;
            var offset = HitboxOffset;
            float halfW = size.x * 0.5f - cornerInset;
            float halfH = size.y * 0.5f - cornerInset;
            float cx = offset.x;
            float cy = offset.y;

            cornerRestPositions = new Vector3[]
            {
                new Vector3(cx - halfW, cy + halfH, 0f),  // top-left
                new Vector3(cx + halfW, cy + halfH, 0f),  // top-right
                new Vector3(cx - halfW, cy - halfH, 0f),  // bottom-left
                new Vector3(cx + halfW, cy - halfH, 0f),  // bottom-right
            };

            for (int i = 0; i < 4; i++)
            {
                if (cornerBrackets[i] != null)
                    cornerBrackets[i].transform.localPosition = cornerRestPositions[i];
            }
        }

        public void Initialize(Unit unit)
        {
            LinkedUnit = unit;
            originalPosition = transform.position;
            registry[unit] = this;

            if (unit.combatSprite != null)
                spriteRenderer.sprite = unit.combatSprite;

            ApplyBlackAndWhiteFilter(unit);

            ApplyRankSorting(unit.currentRank);

            unit.OnDamageTaken += _ => { if (!SuppressDamageFlash) PlayDamageFlash(); };
            unit.OnHealReceived += _ => PlayHealFlash();
            unit.OnDeath += PlayDeathAnimation;
            unit.OnRankChanged += HandleRankChanged;

            CombatEvents.OnUnitTurnStart += HandleAnyUnitTurnStart;
            CombatEvents.OnUnitTurnEnd   += HandleAnyUnitTurnEnd;
            CombatEvents.OnCombatEnd     += HandleCombatEndUndim;

            Transform hudParent = (CombatUIManager.WorldCanvas != null)
                ? CombatUIManager.WorldCanvas.transform
                : transform;

            if (unit.isPlayerControlled && unitWorldHUDPrefab != null)
            {
                var hudGO = Instantiate(unitWorldHUDPrefab, hudParent);
                AttachWorldFollow(hudGO, worldHUDOffset, unit);
                worldHUD = hudGO.GetComponent<UnitWorldHUD>();
                worldHUD?.Initialize(unit);
            }

            if (!unit.isPlayerControlled && enemyWorldHUDPrefab != null)
            {
                var hudGO = Instantiate(enemyWorldHUDPrefab, hudParent);
                AttachWorldFollow(hudGO, enemyWorldHUDOffset, unit);

                enemyWorldHUD = hudGO.GetComponent<EnemyWorldHUD>();

                float intentRelativeY = enemyIntentAnchorOffset.y - enemyWorldHUDOffset.y;
                enemyWorldHUD?.Initialize(unit, intentRelativeY);
            }
        }

        private void AttachWorldFollow(GameObject hudGO, Vector3 offset, Unit unit)
        {
            if (hudGO == null) return;
            var follow = hudGO.GetComponent<WorldFollow>();
            if (follow == null) follow = hudGO.AddComponent<WorldFollow>();
            follow.Bind(transform, offset, unit);
        }

        private void OnDestroy()
        {
            if (LinkedUnit != null)
                registry.Remove(LinkedUnit);

            CombatEvents.OnUnitTurnStart -= HandleAnyUnitTurnStart;
            CombatEvents.OnUnitTurnEnd   -= HandleAnyUnitTurnEnd;
            CombatEvents.OnCombatEnd     -= HandleCombatEndUndim;
        }

        private void HandleAnyUnitTurnStart(Unit activeUnit)
        {
            if (LinkedUnit == null || !LinkedUnit.IsAlive) return;
            if (activeUnit == null) { SetDimmed(false); return; }

            bool sameSide  = LinkedUnit.isPlayerControlled == activeUnit.isPlayerControlled;
            bool isActive  = LinkedUnit == activeUnit;
            SetDimmed(sameSide && !isActive);

            if (isActive) SetActiveSortingBoost(true);
        }

        private void HandleAnyUnitTurnEnd(Unit endingUnit)
        {
            if (LinkedUnit == null) return;
            if (endingUnit != null && LinkedUnit == endingUnit)
                SetActiveSortingBoost(false);
        }

        private void HandleCombatEndUndim(bool _)
        {
            SetDimmed(false);
            SetActiveSortingBoost(false);
        }

        private void SetDimmed(bool dim)
        {
            if (isDimmed == dim) return;
            isDimmed = dim;
            if (spriteRenderer != null) spriteRenderer.color = BaseColor;
        }

        private static UnitDisplay currentlyHovered;

        private void Update()
        {
            if (!ShouldRunHoverCheck()) return;

            var mouse = Mouse.current;
            var cam = Camera.main;
            if (mouse == null || cam == null) return;

            Vector2 mouseScreen = mouse.position.ReadValue();
            Vector3 mouseWorld = cam.ScreenToWorldPoint(
                new Vector3(mouseScreen.x, mouseScreen.y, -cam.transform.position.z));

            var hit = Physics2D.OverlapPoint(mouseWorld);

            UnitDisplay hitDisplay = null;
            if (hit != null)
                hitDisplay = hit.GetComponent<UnitDisplay>();

            if (hitDisplay != currentlyHovered)
            {
                if (currentlyHovered != null)
                    currentlyHovered.HandleMouseExit();

                currentlyHovered = hitDisplay;

                if (currentlyHovered != null)
                    currentlyHovered.HandleMouseEnter();
            }

            if (mouse.leftButton.wasPressedThisFrame && currentlyHovered != null)
                currentlyHovered.HandleMouseClick();
        }

        private bool ShouldRunHoverCheck()
        {
            foreach (var kvp in registry)
            {
                if (kvp.Value != null && kvp.Value.gameObject.activeInHierarchy)
                    return kvp.Value == this;
            }
            return false;
        }

        private void HandleMouseClick()
        {
            if (isDead) return;
            if (TargetingSystem.Instance != null && TargetingSystem.Instance.IsTargeting)
            {
                TargetingSystem.Instance.SelectTarget(LinkedUnit);
            }
        }

        private void HandleMouseEnter()
        {
            if (isDead) return;

            TargetingSystem.Instance?.NotifyTargetHovered(LinkedUnit);

            if (TargetingSystem.Instance != null && TargetingSystem.Instance.IsValidTarget(LinkedUnit))
                SetHighlighted(true);

            if (worldHUD != null) worldHUD.OnUnitHoverEnter();
            if (enemyWorldHUD != null) enemyWorldHUD.OnUnitHoverEnter();
        }

        private void HandleMouseExit()
        {
            if (isDead) return;
            SetHighlighted(false);
            TargetingSystem.Instance?.NotifyTargetUnhovered();

            if (worldHUD != null) worldHUD.OnUnitHoverExit();
            if (enemyWorldHUD != null) enemyWorldHUD.OnUnitHoverExit();
        }

        [System.NonSerialized] private ChanceBox registeredChanceBox;

        public void RegisterChanceBox(ChanceBox box)
        {
            registeredChanceBox = box;
        }

        [Header("Bracket Animation")]
        [Tooltip("How far brackets start outside their rest position (world units).")]
        [SerializeField] private float bracketExpandDistance = 0.15f;

        [Tooltip("How far brackets overshoot inward past rest position (world units).")]
        [SerializeField] private float bracketOvershoot = 0.02f;

        [Tooltip("Duration of the bracket appear animation.")]
        [SerializeField] private float bracketShowDuration = 0.15f;

        [Tooltip("Duration of the bracket disappear animation.")]
        [SerializeField] private float bracketHideDuration = 0.10f;

        public void SetHighlighted(bool on)
        {
            if (isHighlighted == on) return;
            isHighlighted = on;

            if (cornerBrackets == null || cornerRestPositions == null) return;

            if (highlightAnim != null)
                StopCoroutine(highlightAnim);

            highlightAnim = StartCoroutine(on
                ? BracketShowCoroutine()
                : BracketHideCoroutine());
        }

        private IEnumerator BracketShowCoroutine()
        {
            for (int i = 0; i < 4; i++)
            {
                if (cornerBrackets[i] == null) continue;
                cornerBrackets[i].enabled = true;
                cornerBrackets[i].transform.localPosition =
                    cornerRestPositions[i] + cornerDirections[i] * bracketExpandDistance;
            }

            float elapsed = 0f;
            while (elapsed < bracketShowDuration)
            {
                float t = elapsed / bracketShowDuration;
                float curved = 1f - Mathf.Pow(1f - t, 3f);
                float overshootT;
                if (t < 0.6f)
                {
                    overshootT = Mathf.Lerp(bracketExpandDistance, 0f, curved / 0.85f);
                }
                else if (t < 0.85f)
                {
                    float subT = (t - 0.6f) / 0.25f;
                    overshootT = Mathf.Lerp(0f, -bracketOvershoot, subT);
                }
                else
                {
                    float subT = (t - 0.85f) / 0.15f;
                    overshootT = Mathf.Lerp(-bracketOvershoot, 0f, subT);
                }

                for (int i = 0; i < 4; i++)
                {
                    if (cornerBrackets[i] == null) continue;
                    cornerBrackets[i].transform.localPosition =
                        cornerRestPositions[i] + cornerDirections[i] * overshootT;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            for (int i = 0; i < 4; i++)
            {
                if (cornerBrackets[i] != null)
                    cornerBrackets[i].transform.localPosition = cornerRestPositions[i];
            }
            highlightAnim = null;
        }

        private IEnumerator BracketHideCoroutine()
        {
            float elapsed = 0f;
            while (elapsed < bracketHideDuration)
            {
                float t = elapsed / bracketHideDuration;
                float curved = t * t; // ease-in
                float offset = Mathf.Lerp(0f, bracketExpandDistance, curved);

                for (int i = 0; i < 4; i++)
                {
                    if (cornerBrackets[i] == null) continue;
                    cornerBrackets[i].transform.localPosition =
                        cornerRestPositions[i] + cornerDirections[i] * offset;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            for (int i = 0; i < 4; i++)
            {
                if (cornerBrackets[i] != null)
                {
                    cornerBrackets[i].enabled = false;
                    cornerBrackets[i].transform.localPosition = cornerRestPositions[i];
                }
            }
            highlightAnim = null;
        }

        public void PlayDamageFlash()
        {
            if (isDead) return;
            StartCoroutine(DamageFlashCoroutine());
        }

        private IEnumerator DamageFlashCoroutine()
        {
            float knockDir = LinkedUnit != null && LinkedUnit.isPlayerControlled ? -1f : 1f;
            Vector3 knockOffset = new Vector3(knockDir * hurtKnockbackDistance, 0f, 0f);

            float elapsed = 0f;
            while (elapsed < hurtKnockbackDuration)
            {
                float t = elapsed / hurtKnockbackDuration;
                float eased = 1f - (1f - t) * (1f - t); // ease-out quad
                animRoot.localPosition = Vector3.Lerp(Vector3.zero, knockOffset, eased);
                elapsed += Time.deltaTime;
                yield return null;
            }
            animRoot.localPosition = knockOffset;

            spriteRenderer.color = Color.red;

            elapsed = 0f;
            while (elapsed < hurtKnockbackReturnDuration)
            {
                float t = elapsed / hurtKnockbackReturnDuration;
                float eased = t * t; // ease-in quad
                animRoot.localPosition = Vector3.Lerp(knockOffset, Vector3.zero, eased);
                elapsed += Time.deltaTime;
                yield return null;
            }
            animRoot.localPosition = Vector3.zero;

            yield return new WaitForSeconds(0.1f);
            spriteRenderer.color = BaseColor;
        }

        public void PlayHealFlash()
        {
            if (isDead) return;
            StartCoroutine(HealFlashCoroutine());
        }

        private IEnumerator HealFlashCoroutine()
        {
            spriteRenderer.color = Color.green;
            yield return new WaitForSeconds(0.4f);
            spriteRenderer.color = BaseColor;
        }

        public void PlayAttackAnimation(Vector3 targetDirection)
        {
            if (isDead) return;
            StartCoroutine(AttackLungeCoroutine(targetDirection));
        }

        private IEnumerator AttackLungeCoroutine(Vector3 direction)
        {
            Vector3 lungeOffset = direction.normalized * attackLungeDistance;

            float elapsed = 0f;
            while (elapsed < attackLungeDuration)
            {
                float t = elapsed / attackLungeDuration;
                float eased = 1f - (1f - t) * (1f - t); // ease-out quad
                animRoot.localPosition = Vector3.Lerp(Vector3.zero, lungeOffset, eased);
                elapsed += Time.deltaTime;
                yield return null;
            }
            animRoot.localPosition = lungeOffset;

            float returnDuration = attackLungeDuration * 0.8f;
            elapsed = 0f;
            while (elapsed < returnDuration)
            {
                float t = elapsed / returnDuration;
                float eased = t * t; // ease-in quad
                animRoot.localPosition = Vector3.Lerp(lungeOffset, Vector3.zero, eased);
                elapsed += Time.deltaTime;
                yield return null;
            }
            animRoot.localPosition = Vector3.zero;
        }

        public void PlayDebuffShake()
        {
            if (isDead) return;
            StartCoroutine(DebuffShakeCoroutine());
        }

        private IEnumerator DebuffShakeCoroutine()
        {
            float elapsed = 0f;
            while (elapsed < debuffShakeDuration)
            {
                float x = Random.Range(-debuffShakeIntensity, debuffShakeIntensity);
                float y = Random.Range(-debuffShakeIntensity, debuffShakeIntensity);
                animRoot.localPosition = new Vector3(x, y, 0f);
                elapsed += Time.deltaTime;
                yield return null;
            }
            animRoot.localPosition = Vector3.zero;
        }

        public void PlayDeathAnimation()
        {
            if (SuppressDeathAnimation)
            {
                HasPendingDeath = true;
                return;
            }

            PlayDeathAnimationImmediate();
        }

        [Header("Rank Slide Animation")]
        [Tooltip("How long the unit takes to slide to its new rank position.")]
        [SerializeField] private float rankSlideDuration = 0.35f;

        private Coroutine rankSlide;

        private void HandleRankChanged(int oldRank, int newRank)
        {
            if (isDead || LinkedUnit == null || CombatManager.Instance == null) return;
            var target = CombatManager.Instance.GetSpawnPositionForRank(
                LinkedUnit.isPlayerControlled, newRank);
            if (target == null) return;

            ApplyRankSorting(newRank);

            if (rankSlide != null) StopCoroutine(rankSlide);
            rankSlide = StartCoroutine(SlideToCoroutine(target.position));
        }

        private void ApplyRankSorting(int rank)
        {
            if (spriteRenderer == null) return;
            if (!string.IsNullOrEmpty(unitsSortingLayer))
                spriteRenderer.sortingLayerName = unitsSortingLayer;

            currentBaseOrder = -(Mathf.Max(RankHelper.MinRank, rank) - RankHelper.MinRank) * rankOrderStep;
            spriteRenderer.sortingOrder = currentBaseOrder
                + (isActiveOrderBoosted ? activeUnitOrderBoost : 0);
        }

        private void SetActiveSortingBoost(bool boosted)
        {
            if (isActiveOrderBoosted == boosted) return;
            isActiveOrderBoosted = boosted;
            if (spriteRenderer == null) return;
            spriteRenderer.sortingOrder = currentBaseOrder
                + (isActiveOrderBoosted ? activeUnitOrderBoost : 0);
        }

        private Vector3 introTargetPosition;
        private bool hasIntroPrep;
        private Coroutine introSlideCo;

        public void PrepareForIntro(float offsetX)
        {
            introTargetPosition = transform.position;
            transform.position = new Vector3(
                introTargetPosition.x + offsetX,
                introTargetPosition.y,
                introTargetPosition.z);
            originalPosition = introTargetPosition;
            hasIntroPrep = true;
        }

        public Coroutine PlayIntroSlide(float duration, float startDelay)
        {
            if (!hasIntroPrep) return null;
            if (introSlideCo != null) StopCoroutine(introSlideCo);
            introSlideCo = StartCoroutine(IntroSlideCoroutine(duration, startDelay));
            return introSlideCo;
        }

        private IEnumerator IntroSlideCoroutine(float duration, float startDelay)
        {
            if (startDelay > 0f)
            {
                float d = 0f;
                while (d < startDelay) { d += Time.deltaTime; yield return null; }
            }

            Vector3 from = transform.position;
            Vector3 to = introTargetPosition;
            float dur = Mathf.Max(0.0001f, duration);
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                float eased = 1f - Mathf.Pow(1f - k, 3f);
                transform.position = Vector3.LerpUnclamped(from, to, eased);
                yield return null;
            }
            transform.position = to;
            introSlideCo = null;
            hasIntroPrep = false;
        }

        private IEnumerator SlideToCoroutine(Vector3 destination)
        {
            Vector3 start = transform.position;
            float elapsed = 0f;
            while (elapsed < rankSlideDuration)
            {
                float t = elapsed / rankSlideDuration;
                float eased = t < 0.5f
                    ? 2f * t * t
                    : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
                transform.position = Vector3.Lerp(start, destination, eased);
                elapsed += Time.deltaTime;
                yield return null;
            }
            transform.position = destination;
            originalPosition = destination; // lunge/knockback animations anchor off this
            rankSlide = null;
        }

        public void PlayDeathAnimationImmediate()
        {
            HasPendingDeath = false;
            isDead = true;
            SetHighlighted(false);
            if (worldHUD != null)      worldHUD.Hide();
            if (enemyWorldHUD != null) enemyWorldHUD.Hide();
            StartCoroutine(DeathCoroutine());
        }

        private IEnumerator DeathCoroutine()
        {
            float elapsed = 0;
            Color startColor = spriteRenderer.color;
            Vector3 startLocal = animRoot.localPosition;

            while (elapsed < deathFadeDuration)
            {
                float t = elapsed / deathFadeDuration;
                spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);
                animRoot.localPosition = startLocal + Vector3.down * (t * deathDropDistance);
                elapsed += Time.deltaTime;
                yield return null;
            }

            gameObject.SetActive(false);
        }

        private static bool warnedMissingBWMaterial = false;

        private void ApplyBlackAndWhiteFilter(Unit unit)
        {
            if (spriteRenderer == null || unit == null) return;

            var data = unit.enemyData;
            if (data == null || !data.bwGrayscale)
            {
                if (runtimeBWMaterial != null)
                    spriteRenderer.material = null;
                return;
            }

            if (blackAndWhiteMaterial == null)
            {
                if (!warnedMissingBWMaterial)
                {
                    warnedMissingBWMaterial = true;
                    Debug.LogWarning(
                        $"[UnitDisplay] '{unit.unitName}' has bwGrayscale=true but " +
                        "no blackAndWhiteMaterial wired on the UnitDisplay prefab. " +
                        "Create a material from shader 'DarkSpire/Sprite/BlackAndWhite' " +
                        "and assign it to the blackAndWhiteMaterial slot on every " +
                        "UnitDisplay prefab. The UI variant ('DarkSpire/UI/BlackAndWhite') " +
                        "won't render on a SpriteRenderer.",
                        this);
                }
                return;
            }

            if (runtimeBWMaterial == null)
                runtimeBWMaterial = new Material(blackAndWhiteMaterial)
                { hideFlags = HideFlags.DontSave };

            runtimeBWMaterial.SetFloat(PropWR_BW, data.bwReds);
            runtimeBWMaterial.SetFloat(PropWY_BW, data.bwYellows);
            runtimeBWMaterial.SetFloat(PropWG_BW, data.bwGreens);
            runtimeBWMaterial.SetFloat(PropWC_BW, data.bwCyans);
            runtimeBWMaterial.SetFloat(PropWB_BW, data.bwBlues);
            runtimeBWMaterial.SetFloat(PropWM_BW, data.bwMagentas);

            spriteRenderer.material = runtimeBWMaterial;
        }
    }
}
