// TooltipController.cs
// -----------------------------------------------------------------------------
// Singleton pool/registry for the tooltip system. Owns the tooltip prefab
// that gets instantiated each time a TooltipSpawnPoint asks for a fresh
// view, the parent transform under which pooled instances live, and the
// global timing defaults that triggers fall back to.
//
// The controller no longer tracks ownership or anchor placement — that
// moved to TooltipSpawnPoint, which manages a per-anchor stack of visible
// tooltips. Each container in combat (orb tray, condition row, skill info
// panel) hosts its own spawn point and routes triggers within it through
// that point.
//
// Authoring shape — drop on a GameObject under the combat canvas (or any
// scene root). Set the prefab + the tooltips parent (a RectTransform under
// the same combat canvas, last sibling so the children draw on top).
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public class TooltipController : MonoBehaviour
    {
        public static TooltipController Instance { get; private set; }

        [Header("Pool")]
        [Tooltip("Tooltip prefab — the GameObject with a TooltipView " +
                 "component on its root and the panel structure (header / " +
                 "body / passive-evoke) underneath. Instantiated whenever a " +
                 "spawn point needs a new tooltip and the pool is empty.")]
        [SerializeField] private GameObject viewPrefab;

        [Tooltip("Parent for every pooled tooltip instance. Should be a " +
                 "GameObject with a RectTransform, sitting under the combat " +
                 "canvas as the last sibling so tooltips render above other " +
                 "UI. If left null, falls back to this controller's transform.")]
        [SerializeField] private GameObject tooltipsParent;

        [Header("Timing defaults")]
        [Tooltip("Default seconds between hover-enter and the tooltip " +
                 "appearing. Each trigger can override per-instance.")]
        [Min(0f)] public float defaultShowDelay = 0.3f;

        [Tooltip("Default seconds between hover-exit and the tooltip " +
                 "disappearing. The grace lets the cursor sweep between " +
                 "adjacent icons without flicker.")]
        [Min(0f)] public float defaultHideGrace = 0.05f;

        [Header("Diagnostics")]
        [Tooltip("When on, the tooltip system logs every pipeline step " +
                 "(trigger fire → spawn point resolved → pool allocate → " +
                 "content set → layout). Flip on to debug a tooltip that " +
                 "isn't appearing; flip off when done so the console isn't " +
                 "spammed in normal play.")]
        public bool verboseLogging;

        /// <summary>True if any TooltipController in the scene has verbose
        /// logging on. Used by every system file via a single check so we
        /// can sprinkle log statements without a static dependency.</summary>
        public static bool Verbose =>
            Instance != null && Instance.verboseLogging;

        public RectTransform TooltipsParent =>
            tooltipsParent != null
                ? tooltipsParent.transform as RectTransform
                : (transform as RectTransform);

        // Free instances waiting to be reused.
        private readonly Stack<TooltipView> pool = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Get a tooltip view (from the pool, or instantiated fresh).
        /// The view is parented under <paramref name="parent"/> if non-null,
        /// otherwise under <see cref="TooltipsParent"/>. Caller is responsible
        /// for calling <see cref="Release"/> when finished.</summary>
        public TooltipView Allocate(Transform parent = null)
        {
            Transform target = parent != null ? parent : TooltipsParent;

            if (target == null)
            {
                if (verboseLogging)
                    Debug.LogWarning("[Tooltip] Allocate aborted — TooltipsParent is null. " +
                                     "Assign a RectTransform under your combat canvas " +
                                     "to TooltipController.tooltipsParent.", this);
                return null;
            }

            // Pop until we find a non-null entry — pool entries can become
            // null if scene reload destroyed pooled views without notifying.
            TooltipView v = null;
            while (pool.Count > 0 && v == null)
                v = pool.Pop();

            bool fromPool = v != null;

            if (v == null)
            {
                if (viewPrefab == null)
                {
                    if (verboseLogging)
                        Debug.LogWarning("[Tooltip] Allocate aborted — viewPrefab is null. " +
                                         "Assign a tooltip prefab GameObject to " +
                                         "TooltipController.viewPrefab.", this);
                    return null;
                }
                // Instantiate as a GameObject, then resolve the TooltipView
                // on the spawned instance's root. The script must live on
                // the root — that's the panel's RectTransform and the entity
                // we activate / parent / pool.
                var instance = Instantiate(viewPrefab, target);
                v = instance.GetComponent<TooltipView>();
                if (v == null)
                {
                    Debug.LogError("[Tooltip] viewPrefab root has no TooltipView component. " +
                                   "The TooltipView script must be on the prefab's root " +
                                   "GameObject — same one with the panel's RectTransform " +
                                   "and Image.", this);
                    Destroy(instance);
                    return null;
                }
            }
            else
            {
                v.transform.SetParent(target, worldPositionStays: false);
            }

            // Force-active. Two reasons: (1) if the prefab was saved with
            // activeSelf=false somehow, Instantiate preserves that and the
            // panel never renders; (2) Release set it inactive before
            // pooling, so reuse must reactivate.
            v.gameObject.SetActive(true);

            if (verboseLogging)
            {
                string parentName = v.transform.parent != null
                    ? v.transform.parent.name : "<scene root>";
                Debug.Log($"[Tooltip] Allocate {(fromPool ? "(pooled)" : "(instantiated)")} " +
                          $"name='{v.gameObject.name}' parent='{parentName}' " +
                          $"entityID={v.gameObject.GetEntityId()} " +
                          $"activeSelf={v.gameObject.activeSelf} " +
                          $"activeInHierarchy={v.gameObject.activeInHierarchy}", v);
            }

            return v;
        }

        /// <summary>Return a tooltip view to the pool. Caller should drop its
        /// reference after calling this.</summary>
        public void Release(TooltipView view)
        {
            if (view == null) return;
            view.gameObject.SetActive(false);
            if (TooltipsParent != null)
                view.transform.SetParent(TooltipsParent, worldPositionStays: false);
            pool.Push(view);

            if (verboseLogging)
                Debug.Log($"[Tooltip] Release → pool (size now {pool.Count})", view);
        }

        // ─── Diagnostic ──────────────────────────────────────────────────────

        /// <summary>
        /// Editor-only test spawner. Right-click this component in the
        /// Inspector during Play mode → "Test Spawn Tooltip" to allocate a
        /// tooltip with placeholder content and place it at screen center.
        /// Bypasses the trigger / spawn point pipeline entirely so you can
        /// verify the prefab + parent + pool chain in isolation. The
        /// spawned tooltip is NOT released — it stays visible until you
        /// stop Play mode or call ClearTestSpawn.
        /// </summary>
        [ContextMenu("Test Spawn Tooltip")]
        public void TestSpawnTooltip()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Tooltip] Test spawn requires Play mode.", this);
                return;
            }
            var v = Allocate();
            if (v == null) return;

            v.SetContent(TooltipContent.ForStatic(
                name: "Test Tooltip",
                icon: null,
                description: "If you can read this, the prefab + parent " +
                             "chain works. Problem is upstream (trigger / " +
                             "spawn point / content)."));

            // Place at screen center.
            v.SetScreenPosition(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            Debug.Log($"[Tooltip] Test-spawned tooltip at screen center. " +
                      $"Inspect '{v.gameObject.name}' under '{(v.transform.parent != null ? v.transform.parent.name : "<scene root>")}' " +
                      $"in the hierarchy.", v);
        }

        /// <summary>Hide & pool every tooltip the test spawner created.</summary>
        [ContextMenu("Clear Test Spawn")]
        public void ClearTestSpawn()
        {
            if (TooltipsParent == null) return;
            for (int i = TooltipsParent.childCount - 1; i >= 0; i--)
            {
                var child = TooltipsParent.GetChild(i);
                var v = child.GetComponent<TooltipView>();
                if (v != null && v.gameObject.activeSelf) Release(v);
            }
        }
    }
}
