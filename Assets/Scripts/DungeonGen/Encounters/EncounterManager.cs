using UnityEngine;

namespace DarkSpire
{
    // Persistent, dungeon-scoped service. Routes every Get*Encounter call to
    // the matching Sub-Manager and threads the result through DungeonManager
    // for roadmap-annotation overrides (Phase 8).
    //
    // Lifecycle: instantiated once per run, survives floor changes via
    // DontDestroyOnLoad. Each floor calls SetupForFloor with that floor's
    // FloorEncounterDataSO; sub-managers wipe their state and reload pools.
    //
    // Knowledge boundary: gameplay code calls Get* (mutates state). Only
    // the DungeonManager (and editor debug overlays) calls Peek.
    public class EncounterManager : MonoBehaviour
    {
        public static EncounterManager Instance { get; private set; }

        // Sub-Managers — pure C# objects, no MonoBehaviour.
        private readonly MonsterEncounterSubManager monsters = new();
        private readonly EliteEncounterSubManager elites = new();
        private readonly BossEncounterSubManager bosses = new();
        private readonly TextEncounterSubManager campsites = new(EncounterType.Campsite);
        private readonly TextEncounterSubManager events = new(EncounterType.Event);
        private readonly TextEncounterSubManager shrines = new(EncounterType.Shrine);

        [Tooltip("Optional. Phase 8 wires this to apply roadmap reward overrides; null = no overrides.")]
        [SerializeField] private DungeonManager dungeonManager;

        public void SetDungeonManager(DungeonManager dm) => dungeonManager = dm;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[EM] Duplicate instance — destroying the new one.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // Persist across floor scene loads. Slice runs in one scene so
            // this is precautionary; real multi-floor flow needs it.
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetupForFloor(FloorEncounterDataSO floorData)
        {
            if (floorData == null)
            {
                Debug.LogWarning("[EM] SetupForFloor called with null floorData — sub-managers will be empty.");
            }
            monsters.SetupForFloor(floorData?.monsters);
            elites.SetupForFloor(floorData?.elites);
            bosses.SetupForFloor(floorData?.bosses);
            campsites.SetupForFloor(floorData?.campsites);
            events.SetupForFloor(floorData?.events);
            shrines.SetupForFloor(floorData?.shrines);
        }

        // Dispatch methods --------------------------------------------------

        public EncounterResult GetMonsterEncounter()  => Apply(monsters.GetNext());
        public EncounterResult GetEliteEncounter()    => Apply(elites.GetNext());
        public EncounterResult GetBossEncounter()     => Apply(bosses.GetNext());
        public EncounterResult GetCampsiteEncounter() => Apply(campsites.GetNext());
        public EncounterResult GetEventEncounter()    => Apply(events.GetNext());
        public EncounterResult GetShrineEncounter()   => Apply(shrines.GetNext());

        // DungeonManager-only ----------------------------------------------

        public IEncounterSubManager PeekSub(EncounterType type) => type switch
        {
            EncounterType.Monster  => monsters,
            EncounterType.Elite    => elites,
            EncounterType.Boss     => bosses,
            EncounterType.Campsite => campsites,
            EncounterType.Event    => events,
            EncounterType.Shrine   => shrines,
            _ => null,
        };

        // ------------------------------------------------------------------

        private EncounterResult Apply(EncounterResult result)
        {
            if (dungeonManager != null)
                return dungeonManager.ApplyAnnotations(result);
            return result;
        }
    }
}
