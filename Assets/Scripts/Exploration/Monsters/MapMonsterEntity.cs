using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    // Map-side monster: owns the four-state AI (Patrol / Alert / Chase / Lost),
    // BFS pathing to the next target tile, LoS-gated detection, and the
    // tier-based EncounterManager dispatch on contact.
    //
    // Boss tier is special-cased to never patrol or chase — it sits in the
    // boss room and only triggers combat when the party walks onto its tile.
    //
    // actual scene swap to combat using the EncounterResult returned here.
    public class MapMonsterEntity : MonoBehaviour, IDungeonEntity
    {
        public Vector2Int GridPos { get; private set; }
        public MonsterTier Tier { get; private set; }
        public MonsterAIState State { get; private set; } = MonsterAIState.Patrol;
        public List<Vector2Int> PatrolRoute { get; private set; }

        [Header("Timings (GDD defaults)")]
        [SerializeField] private float patrolMoveInterval = 0.6f;
        [SerializeField] private float chaseMoveInterval  = 0.35f;
        [SerializeField] private float alertDuration      = 0.5f;
        [SerializeField] private float lostDuration       = 3.0f;
        [SerializeField] private float stepLerpDuration   = 0.15f;

        [Header("Detection")]
        [SerializeField] private float detectionRadius = 3.0f;
        [Tooltip("Seconds after spawn during which this monster cannot enter " +
                 "Alert/Chase. Prevents post-combat spawn-camping: when the " +
                 "dungeon reloads with the player at returnPos, monsters reset " +
                 "to their patrol start, and this grace gives the player a beat " +
                 "to move before the AI re-engages.")]
        [SerializeField] private float spawnGraceSeconds = 1.5f;

        // Scene refs
        private GeneratedFloorData floor;
        private PartyToken party;
        private MapEntitySpriteLibrary library;
        private SpriteRenderer sr;
        private AlertIndicator alertIndicator;

        // Runtime state
        private float moveTimer;
        private float alertTimer;
        private float lostTimer;
        private float graceTimer;      // post-spawn grace; can't enter Alert while > 0
        private int patrolIndex;       // index of current/last waypoint
        private bool collisionFired;
        private Coroutine lerpCo;

        // Original spawn record — kept so SceneFlow can mark this exact monster
        // as defeated on Victory (uses spawn.start as the identity key).
        private MonsterSpawn originalSpawn;

        // Initialization ---------------------------------------------------

        public void InitializeMonster(
            MonsterSpawn spawn,
            GeneratedFloorData floor,
            PartyToken party,
            MapEntitySpriteLibrary library,
            SpriteRenderer sr,
            float floorDetectionRadius,
            int alertSortingOrder)
        {
            originalSpawn = spawn;
            Tier = spawn.tier;
            PatrolRoute = spawn.patrolRoute;
            GridPos = spawn.start;

            this.floor = floor;
            this.party = party;
            this.library = library;
            this.sr = sr;
            detectionRadius = floorDetectionRadius;

            // Elites are slightly more attentive per spec (~+0.5 tile).
            if (Tier == MonsterTier.Elite) detectionRadius += 0.5f;

            transform.localPosition = TileToWorld(GridPos);
            DungeonRegistry.Instance?.Register(this);

            // Attach an AlertIndicator child so the "!" pops over the monster.
            if (Tier != MonsterTier.Boss)
            {
                var go = new GameObject("AlertIndicator");
                go.transform.SetParent(transform, false);
                alertIndicator = go.AddComponent<AlertIndicator>();
                alertIndicator.Setup(library, alertSortingOrder);
            }

            patrolIndex = 0;
            moveTimer = patrolMoveInterval;     // small grace before first move
            lostTimer = lostDuration;
            graceTimer = spawnGraceSeconds;     // anti-spawn-camp: ignore party briefly
        }

        private void OnDestroy()
        {
            DungeonRegistry.Instance?.Unregister(this);
        }

        // Update tick ------------------------------------------------------

        private void Update()
        {
            if (floor == null || party == null) return;
            if (graceTimer > 0f) graceTimer -= Time.deltaTime;

            // Boss is static — only collision-checks.
            if (Tier == MonsterTier.Boss)
            {
                CheckCollision();
                return;
            }

            UpdateStateLogic();
            UpdateMovement();
            CheckCollision();
        }

        private void UpdateStateLogic()
        {
            // Spawn grace blocks the patrol→alert transition. Does NOT block
            // an in-progress chase (we'd want a chase to continue after the
            // grace expires, and currently we always reset to Patrol on init,
            // so this branch is the only entry into pursuit anyway).
            if (graceTimer > 0f && State == MonsterAIState.Patrol) return;

            switch (State)
            {
                case MonsterAIState.Patrol:
                    if (CanSeeParty()) EnterAlert();
                    break;

                case MonsterAIState.Alert:
                    alertTimer -= Time.deltaTime;
                    if (alertTimer <= 0f) EnterChase();
                    break;

                case MonsterAIState.Chase:
                    if (CanSeeParty())
                    {
                        lostTimer = lostDuration;
                    }
                    else
                    {
                        lostTimer -= Time.deltaTime;
                        if (lostTimer <= 0f) EnterLost();
                    }
                    break;

                case MonsterAIState.Lost:
                    if (CanSeeParty()) EnterChase();
                    break;
            }
        }

        private void UpdateMovement()
        {
            if (State == MonsterAIState.Alert) return;       // frozen during pop
            if (lerpCo != null) return;                       // mid-step lerp

            float interval = State == MonsterAIState.Chase
                ? chaseMoveInterval
                : patrolMoveInterval;

            moveTimer -= Time.deltaTime;
            if (moveTimer > 0f) return;
            moveTimer = interval;

            Vector2Int? next = State switch
            {
                MonsterAIState.Patrol => NextPatrolStep(),
                MonsterAIState.Chase  => NextChaseStep(),
                MonsterAIState.Lost   => NextLostStep(),
                _ => null,
            };
            if (!next.HasValue) return;
            StepTo(next.Value);
        }

        // State transitions ------------------------------------------------

        private void EnterAlert()
        {
            State = MonsterAIState.Alert;
            alertTimer = alertDuration;
            alertIndicator?.Show(alertDuration);
            DungeonEvents.InvokeFlashMessage("!");
        }

        private void EnterChase()
        {
            State = MonsterAIState.Chase;
            lostTimer = lostDuration;
            alertIndicator?.Hide();
            moveTimer = 0f;     // start chasing immediately
        }

        private void EnterLost()
        {
            State = MonsterAIState.Lost;
            moveTimer = 0f;
        }

        private void EnterPatrol()
        {
            State = MonsterAIState.Patrol;
        }

        // Detection --------------------------------------------------------

        private bool CanSeeParty()
        {
            var p = party.GridPos;
            float dx = p.x - GridPos.x;
            float dy = p.y - GridPos.y;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d > detectionRadius) return false;
            return LineOfSight.HasLoS(floor.tiles, GridPos, p);
        }

        private Vector2Int? NextPatrolStep()
        {
            if (PatrolRoute == null || PatrolRoute.Count == 0) return null;
            if (PatrolRoute.Count == 1) return null;

            // Advance index when we've reached the current target waypoint.
            int target = (patrolIndex + 1) % PatrolRoute.Count;
            if (GridPos == PatrolRoute[target])
            {
                patrolIndex = target;
                target = (patrolIndex + 1) % PatrolRoute.Count;
            }
            return StepTowards(PatrolRoute[target]);
        }

        private Vector2Int? NextChaseStep()
        {
            return StepTowards(party.GridPos);
        }

        private Vector2Int? NextLostStep()
        {
            // Find nearest waypoint (Manhattan).
            int bestDist = int.MaxValue;
            int nearestIdx = 0;
            for (int i = 0; i < PatrolRoute.Count; i++)
            {
                int d = Mathf.Abs(GridPos.x - PatrolRoute[i].x)
                      + Mathf.Abs(GridPos.y - PatrolRoute[i].y);
                if (d < bestDist) { bestDist = d; nearestIdx = i; }
            }

            var nearest = PatrolRoute[nearestIdx];
            if (GridPos == nearest)
            {
                patrolIndex = nearestIdx;
                EnterPatrol();
                return null;
            }
            return StepTowards(nearest);
        }

        // BFS to target, return first step (or null). Treats walls as blocked
        // and ignores the party's tile so chase paths can end on the party.
        private Vector2Int? StepTowards(Vector2Int target)
        {
            if (target == GridPos) return null;
            var path = GridBfs.FindPath(
                floor.tiles, GridPos, target,
                t => t.IsWalkable());
            if (path == null || path.Count < 2) return null;
            return path[1];
        }

        private void StepTo(Vector2Int newTile)
        {
            // Don't share tiles with other monsters. If the destination is
            // occupied, hold position this tick and let the next interval
            // recompute. Cheaper than re-routing pathfinding around live
            // monsters and prevents two monsters stacking on the player's
            // tile (which would race CheckCollision into combat twice).
            if (IsTileOccupiedByOtherMonster(newTile)) return;

            var old = GridPos;
            GridPos = newTile;
            DungeonRegistry.Instance?.Move(this, old, newTile);
            lerpCo = StartCoroutine(LerpStep(newTile));
        }

        private bool IsTileOccupiedByOtherMonster(Vector2Int tile)
        {
            var registry = DungeonRegistry.Instance;
            if (registry == null) return false;
            var list = registry.GetAt(tile);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is MapMonsterEntity m && m != this) return true;
            }
            return false;
        }

        private IEnumerator LerpStep(Vector2Int newTile)
        {
            var startWorld = transform.localPosition;
            var endWorld = TileToWorld(newTile);
            float t = 0f;
            while (t < stepLerpDuration)
            {
                t += Time.deltaTime;
                transform.localPosition = Vector3.Lerp(startWorld, endWorld, Mathf.Clamp01(t / stepLerpDuration));
                yield return null;
            }
            transform.localPosition = endWorld;
            lerpCo = null;
        }

        // Collision -> EM dispatch ---------------------------------------

        private void CheckCollision()
        {
            if (collisionFired) return;
            // If another monster already claimed combat this frame (or last),
            // stand down — only one combat can be in flight at a time. The
            // guard clears in SceneFlow.ReturnFromCombat.
            if (SceneFlow.IsCombatLoadInFlight) return;
            if (party.GridPos != GridPos) return;
            collisionFired = true;

            var em = EncounterManager.Instance;
            EncounterResult result = default;
            if (em != null)
            {
                result = Tier switch
                {
                    MonsterTier.Standard => em.GetMonsterEncounter(),
                    MonsterTier.Elite    => em.GetEliteEncounter(),
                    MonsterTier.Boss     => em.GetBossEncounter(),
                    _ => default,
                };
            }

            string encName = result.encounter != null ? result.encounter.name : "(null)";
            Debug.Log($"[Monster] {Tier} contact at {GridPos} -> slot {result.slotIndex} {encName}");

            if (result.encounter == null)
            {
                // No encounter authored — fall back to the Phase 9 mock so the
                // floor stays testable. Apply override + destroy + log warning.
                Debug.LogWarning("[Monster] No EncounterSO authored for this tier — " +
                                 "using mock destroy. Author a pool to enable combat handoff.");
                if (result.rewardOverride.guaranteedKey) RunContext.keysHeld++;
                if (result.rewardOverride.bonusGold != 0) RunContext.gold += result.rewardOverride.bonusGold;

                var holder = RunStateHolder.Instance;
                if (holder != null)
                {
                    if (Tier == MonsterTier.Boss) holder.bossDefeated = true;
                    else holder.defeatedMonsters.Add(originalSpawn.start);
                }

                DungeonRegistry.Instance?.Unregister(this);
                Destroy(gameObject);
                return;
            }

            // Real handoff: SceneFlow captures floor + party pos into RunStateHolder
            // and loads the combat scene. CombatBootstrap fires the result.
            SceneFlow.LoadCombat(result, originalSpawn, party.GridPos);
        }

        // Helpers ----------------------------------------------------------

        private static Vector3 TileToWorld(Vector2Int t)
            => new Vector3(t.x + 0.5f, t.y + 0.5f, 0f);
    }
}
