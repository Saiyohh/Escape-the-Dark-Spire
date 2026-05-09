// EncounterSO.cs (file kept named EncounterData.cs to preserve script GUID for
// existing assets — see the .meta GUID lock).
// -----------------------------------------------------------------------------
// ScriptableObject describing a single fight: pool of possible enemies, count
// range, floor index, and a background. CombatManager rolls enemy count in
// InitializeCombat and spawns from possibleEnemies.
//
// Renamed EncounterData -> EncounterSO during dungeon-side Phase 0 to match
// the Encounter Manager spec (every Sub-Manager pool is List<EncounterSO>).
// MovedFromAttribute keeps existing assets and serialized refs bound.
// -----------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace DarkSpire
{
    [MovedFrom(true, sourceClassName: "EncounterData")]
    [CreateAssetMenu(fileName = "ENC_NewEncounter", menuName = "DarkSpire/Encounter")]
    public class EncounterSO : ScriptableObject
    {
        public string encounterName;
        public EnemyData[] possibleEnemies;
        public int minEnemies = 1;
        public int maxEnemies = 3;
        public int floorNumber = 1;
        public Sprite backgroundSprite;
    }
}
