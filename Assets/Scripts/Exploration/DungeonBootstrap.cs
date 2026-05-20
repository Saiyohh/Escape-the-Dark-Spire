using UnityEngine;

namespace DarkSpire
{
    // Scene entry point for DungeonFloor.unity. Resolves the floor source
    // (RunContext snapshot > RunContext config > inspector fallback config),
    // runs the generator, hands the result to FloorRenderer + EntitySpawner,
    // spawns the party token at the Start tile, and wires camera follow +
    // grid movement input. Phase 5+ adds fog, monster AI, interactable wiring.
    public class DungeonBootstrap : MonoBehaviour
    {
        [Header("Floor Source (used when RunContext is unset)")]
        [SerializeField] private FloorGenerationConfigSO fallbackConfig;
        [SerializeField] private GeneratedFloorSO fallbackSnapshot;
        [SerializeField] private int fallbackSeed;
        [SerializeField] private bool fallbackRandomSeed = true;

        [Header("Sprite Library")]
        [Tooltip("Optional override. Leave null to use the singleton " +
                 "(MapEntitySpriteLibrary.Instance) loaded via the project's " +
                 "Preloaded Assets list. Wire a different asset here only if " +
                 "this scene needs a custom set (e.g. a debug/test floor).")]
        [SerializeField] private MapEntitySpriteLibrary spriteLibrary;

        [Header("Renderers (auto-created if null)")]
        [SerializeField] private FloorRenderer floorRenderer;
        [SerializeField] private EntitySpawner entitySpawner;

        [Header("Party")]
        [SerializeField] private PartyToken partyToken;
        [SerializeField] private GridMovement gridMovement;

        [Header("Fog of War")]
        [SerializeField] private FogOfWar fogOfWar;
        [Tooltip("Used when a snapshot has no source config (fallback sight radius).")]
        [SerializeField] private float snapshotSightRadius = 5f;
        [Tooltip("Used when a snapshot has no source config (fallback monster detection radius).")]
        [SerializeField] private float snapshotDetectionRadius = 3f;

        [Header("Interaction")]
        [SerializeField] private DungeonRegistry registry;
        [SerializeField] private DungeonInteractor interactor;
        [SerializeField] private DungeonWalkoverDispatcher walkoverDispatcher;

        [Header("Encounter Manager")]
        [Tooltip("Optional. If left null, the bootstrap will reuse the existing EM " +
                 "singleton or create a new one on this GameObject.")]
        [SerializeField] private EncounterManager encounterManager;
        [SerializeField] private DungeonManager dungeonManager;

        [Header("Debug")]
        [SerializeField] private bool createDebugOverlay = true;
        [SerializeField] private DungeonDebugOverlay debugOverlay;

        [Header("Camera")]
        [SerializeField] private Camera worldCamera;
        [SerializeField] private float cameraInitialPadding = 1.5f;
        [Tooltip("If true, camera locks to a tight follow on the party token after build. " +
                 "If false, the initial whole-floor framing is preserved (debug view).")]
        [SerializeField] private bool followParty = true;
        [Tooltip("Orthographic size used during follow. Smaller = tighter zoom on the party.")]
        [SerializeField] private float followOrthographicSize = 7f;
        [SerializeField] private CameraFollow cameraFollow;

        public GeneratedFloorData ActiveFloor { get; private set; }
        public PartyToken PartyToken => partyToken;

        // Tracked separately from RunContext so snapshot-driven runs (where
        // RunContext.currentFloorConfig may be null) can still report the
        // sight/detection radii used for this floor.
        private FloorGenerationConfigSO resolvedConfig;

        private void Start()
        {
            // Run-tier UI host: pause menu, pickup toasts, future inventory etc.
            // First call instantiates the MenuCanvas prefab and DontDestroyOnLoads
            // it; subsequent calls (e.g. from CombatBootstrap) return the existing
            // instance. MainMenuController.Start tears it down on return-to-menu.
            MenuCanvasController.GetOrCreate();

            // Run-scoped feedback widgets that the dungeon needs available.
            // Each is a singleton with a runtime fallback Canvas, so this is
            // safe whether or not the user has authored Resources prefabs.
            FlashMessageController.GetOrCreate();
            RestMenu.GetOrCreate();
            MapHoverTooltip.GetOrCreate();
            IconLegendModal.GetOrCreate();

            EnsureRenderers();
            EnsureRegistry();
            BuildFloor();
            BindDungeonUI();

            // First-time-user intro card. Self-gates on PlayerPrefs so it
            // shows once per install. Triggered last so the dungeon is fully
            // built and visible behind the modal.
            FirstRunIntroCard.ShowIfFirstRun();
        }

        // Scene-scoped HUD + Minimap. MUST be authored in DungeonFloor.unity.
        // Bootstrap only finds them — it does not create them. Use the
        // scaffolder menu items the first time:
        //   Tools > DarkSpire > Scenes > Scaffold FloorHUD into open scene
        //   Tools > DarkSpire > Scenes > Scaffold Minimap into open scene
        [Header("Dungeon UI (auto-found in scene)")]
        [SerializeField] private FloorHUD floorHUD;
        [SerializeField] private Minimap minimap;

        private void BindDungeonUI()
        {
            if (floorHUD == null)
                floorHUD = FindAnyObjectByType<FloorHUD>(FindObjectsInactive.Include);
            if (minimap == null)
                minimap = FindAnyObjectByType<Minimap>(FindObjectsInactive.Include);

            if (floorHUD == null)
                Debug.LogWarning(
                    "[DungeonBootstrap] No FloorHUD in scene. Scaffold one via " +
                    "Tools > DarkSpire > Scenes > Scaffold FloorHUD into open scene.");

            if (minimap == null)
                Debug.LogWarning(
                    "[DungeonBootstrap] No Minimap in scene. Scaffold one via " +
                    "Tools > DarkSpire > Scenes > Scaffold Minimap into open scene.");
            else if (ActiveFloor != null)
                minimap.Bind(ActiveFloor, fogOfWar, partyToken);
        }

        // Run-timer increment moved to FloorHUD.Update so the wall-clock keeps
        // ticking during combat (FloorHUD persists across scenes; DungeonBootstrap
        // doesn't).

        private void OnDestroy()
        {
            DungeonEvents.ClearAll();
        }

        private void EnsureRegistry()
        {
            if (registry != null) return;
            registry = GetComponentInChildren<DungeonRegistry>();
            if (registry != null) return;
            var go = new GameObject("DungeonRegistry");
            go.transform.SetParent(transform, false);
            registry = go.AddComponent<DungeonRegistry>();
        }

        private void EnsureRenderers()
        {
            // Resolve sprite library: explicit override wins, otherwise pull
            // the singleton. Bootstrap is the single place we resolve so
            // every consumer downstream gets the same instance.
            var resolvedLibrary = MapEntitySpriteLibrary.ResolveOrSingleton(spriteLibrary);

            if (floorRenderer == null)
            {
                var go = new GameObject("FloorRenderer");
                go.transform.SetParent(transform, false);
                floorRenderer = go.AddComponent<FloorRenderer>();
            }
            floorRenderer.SetSpriteLibrary(resolvedLibrary);

            if (entitySpawner == null)
            {
                var go = new GameObject("EntitySpawner");
                go.transform.SetParent(transform, false);
                entitySpawner = go.AddComponent<EntitySpawner>();
            }
            entitySpawner.SetSpriteLibrary(resolvedLibrary);

            if (worldCamera == null) worldCamera = Camera.main;
        }

        private void BuildFloor()
        {
            GeneratedFloorData floor = ResolveFloor();
            if (floor == null)
            {
                Debug.LogError("[DungeonBootstrap] Failed to resolve a floor — see warnings above.");
                return;
            }

            ActiveFloor = floor;
            RunContext.currentFloor = floor;

            ConfigureEncounterManager(floor);            // EM ready before any encounter trigger
            floorRenderer.Render(floor);
            entitySpawner.SpawnEntities(floor);          // non-monster entities self-register
            SpawnParty(floor);                           // token created, NOT placed
            SpawnMonsters(floor);                        // needs the party token as their target
            ConfigureInteraction(floor);                 // interactor + walkover hooks
            ConfigureFog(floor);                         // fog hooks, then calls Place
            ConfigureCamera(floor);

            Debug.Log($"[DungeonBootstrap] Rendered floor seed={floor.seed} " +
                      $"size={floor.gridSize.x}x{floor.gridSize.y} " +
                      $"rooms={floor.stats.roomCount} entities={floor.entities.Count} " +
                      $"monsters={floor.monsterSpawns.Count}");
        }

        private GeneratedFloorData ResolveFloor()
        {
            // 1. RESUME path — coming back from combat. Reuse the saved floor
            //    so the layout and untouched entities stay identical.
            var holder = RunStateHolder.Instance;
            if (holder != null && holder.resumeMode && holder.savedFloor != null)
            {
                Debug.Log("[DungeonBootstrap] Resuming from RunStateHolder (post-combat return).");
                resolvedConfig = RunContext.currentFloorConfig ?? fallbackConfig;
                return holder.savedFloor;
            }

            // 2. Fresh floor — snapshot or config + seed.
            var snapshot = RunContext.snapshotOverride ?? fallbackSnapshot;
            if (snapshot != null)
            {
                resolvedConfig = null;
                return snapshot.ToRuntimeData();
            }

            var config = RunContext.currentFloorConfig ?? fallbackConfig;
            if (config == null)
            {
                Debug.LogError("[DungeonBootstrap] No FloorGenerationConfigSO set " +
                               "(neither RunContext.currentFloorConfig nor fallbackConfig).");
                return null;
            }
            resolvedConfig = config;

            int seed = RunContext.seed;
            if (RunContext.currentFloorConfig == null)
            {
                seed = fallbackRandomSeed ? Random.Range(0, int.MaxValue) : fallbackSeed;
            }

            return DungeonGenerator.Generate(config, seed);
        }

        private void SpawnParty(GeneratedFloorData floor)
        {
            if (partyToken == null)
            {
                var go = new GameObject("PartyToken");
                go.transform.SetParent(transform, false);
                partyToken = go.AddComponent<PartyToken>();
            }
            partyToken.Bind(floor);

            // Place AFTER fog binding so the initial OnPlaced reveals the
            // start area. ConfigureFog runs in BuildFloor between SpawnParty
            // and ConfigureCamera, but we need fog wired before Place fires.
            // Solution: don't Place here — ConfigureFog calls Place itself.

            if (gridMovement == null)
            {
                var go = new GameObject("GridMovement");
                go.transform.SetParent(transform, false);
                gridMovement = go.AddComponent<GridMovement>();
            }
            gridMovement.SetToken(partyToken);
        }

        private void ConfigureEncounterManager(GeneratedFloorData floor)
        {
            // Encounter Manager — MUST be created at scene root so its Awake
            // can DontDestroyOnLoad itself (the Awake guard is gated on
            // transform.parent == null). If parented under DungeonBootstrap,
            // the EM gets destroyed alongside the dungeon scene on every
            // combat transition, the post-combat dungeon load creates a fresh
            // empty EM, and SetupForFloor is skipped on resume — so every Sub-
            // Manager returns a null encounter and combat falls through to the
            // mock-destroy path. Root-level creation lets DDOL fire.
            if (encounterManager == null) encounterManager = EncounterManager.Instance;
            if (encounterManager == null)
            {
                var go = new GameObject("EncounterManager");
                // Intentionally unparented — see comment above.
                encounterManager = go.AddComponent<EncounterManager>();
            }

            // Dungeon Manager — same DDOL contract as EM. Author at scene root.
            if (dungeonManager == null) dungeonManager = DungeonManager.Instance;
            if (dungeonManager == null)
            {
                var go = new GameObject("DungeonManager");
                dungeonManager = go.AddComponent<DungeonManager>();
            }

            encounterManager.SetDungeonManager(dungeonManager);

            // Only reset Sub-Manager state on a FRESH floor entry. On resume
            // from combat we keep slot indices, seen pools and queue order so
            // roadmap annotations (e.g. "1st and 3rd Standard fight drop key")
            // survive the round-trip.
            bool isResume = RunStateHolder.Instance != null && RunStateHolder.Instance.resumeMode;
            if (!isResume)
            {
                dungeonManager.SetupForFloor(floor.encounterPool != null ? floor.encounterPool.annotations : null);
                encounterManager.SetupForFloor(floor.encounterPool);
            }

            if (floor.encounterPool == null && !isResume)
            {
                Debug.LogWarning("[DungeonBootstrap] floor.encounterPool is null — " +
                                 "set FLOOR_*.encounterPool to a FloorEncounterDataSO.");
            }

            if (createDebugOverlay && debugOverlay == null)
            {
                var go = new GameObject("DungeonDebugOverlay");
                go.transform.SetParent(transform, false);
                debugOverlay = go.AddComponent<DungeonDebugOverlay>();
            }
        }

        private void SpawnMonsters(GeneratedFloorData floor)
        {
            float detection = resolvedConfig != null
                ? resolvedConfig.enemyDetectionRadius
                : snapshotDetectionRadius;
            entitySpawner.SpawnMonsters(floor, partyToken, detection);
        }

        private void ConfigureInteraction(GeneratedFloorData floor)
        {
            if (interactor == null)
            {
                var go = new GameObject("DungeonInteractor");
                go.transform.SetParent(transform, false);
                interactor = go.AddComponent<DungeonInteractor>();
            }
            if (walkoverDispatcher == null)
            {
                var go = new GameObject("DungeonWalkoverDispatcher");
                go.transform.SetParent(transform, false);
                walkoverDispatcher = go.AddComponent<DungeonWalkoverDispatcher>();
            }
            walkoverDispatcher.Bind(partyToken, registry, floor);
            // Hand the renderer ref so the dispatcher can destroy the campsite
            // overlay after a rest is consumed.
            walkoverDispatcher.SetFloorRenderer(floorRenderer);

            // Interactor needs walkoverDispatcher for tile-based interactions
            // (rest tile at party's own position triggered via E).
            interactor.Bind(partyToken, registry, walkoverDispatcher);
        }

        private void ConfigureFog(GeneratedFloorData floor)
        {
            if (fogOfWar == null)
            {
                var go = new GameObject("FogOfWar");
                go.transform.SetParent(transform, false);
                fogOfWar = go.AddComponent<FogOfWar>();
            }

            float sight = resolvedConfig != null
                ? resolvedConfig.sightRadius
                : snapshotSightRadius;

            fogOfWar.Bind(floor, sight);

            if (partyToken != null)
            {
                // Subscribe before placing so the initial reveal fires.
                partyToken.OnPlaced += pos => fogOfWar.UpdateForPartyAt(pos);
                partyToken.OnMoved  += (_, to) => fogOfWar.UpdateForPartyAt(to);

                // Resume path: place where we left off and clear the resume flag.
                var holder = RunStateHolder.Instance;
                Vector2Int placeAt = floor.startPosition;
                if (holder != null && holder.resumeMode)
                {
                    placeAt = holder.returnPos;
                    holder.resumeMode = false;
                    Debug.Log($"[DungeonBootstrap] Resumed party at {placeAt}. " +
                              $"Defeated monsters: {holder.defeatedMonsters.Count}, " +
                              $"removed entities: {holder.removedEntities.Count}.");
                }
                partyToken.Place(placeAt);
            }
        }

        private void ConfigureCamera(GeneratedFloorData floor)
        {
            if (worldCamera == null) return;
            worldCamera.orthographic = true;

            if (followParty && partyToken != null)
            {
                worldCamera.orthographicSize = followOrthographicSize;
                if (cameraFollow == null)
                {
                    cameraFollow = worldCamera.GetComponent<CameraFollow>();
                    if (cameraFollow == null) cameraFollow = worldCamera.gameObject.AddComponent<CameraFollow>();
                }
                cameraFollow.SetTarget(partyToken.transform);
                cameraFollow.SetFloor(floor);

                // Snap camera to the party on the first frame so we don't see
                // a follow-lerp from the previous position.
                var p = partyToken.transform.position;
                var z = worldCamera.transform.position.z;
                worldCamera.transform.position = new Vector3(p.x, p.y, z != 0f ? z : -10f);
            }
            else
            {
                FrameWholeFloor(floor);
            }
        }

        private void FrameWholeFloor(GeneratedFloorData floor)
        {
            float w = floor.gridSize.x;
            float h = floor.gridSize.y;
            float aspect = worldCamera.aspect <= 0f ? 16f / 9f : worldCamera.aspect;
            float halfH = h * 0.5f + cameraInitialPadding;
            float halfW = (w * 0.5f + cameraInitialPadding) / aspect;
            worldCamera.orthographicSize = Mathf.Max(halfH, halfW);

            var pos = worldCamera.transform.position;
            worldCamera.transform.position = new Vector3(w * 0.5f, h * 0.5f, pos.z != 0f ? pos.z : -10f);
        }
    }
}
