using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace DarkSpire
{
    [MovedFrom(true, sourceClassName: "EncounterData")]
    [CreateAssetMenu(fileName = "ENC_NewEncounter", menuName = "DarkSpire/Encounter")]
    public class EncounterSO : ScriptableObject
    {
        public string encounterName;

        [Tooltip("Always-spawned enemies, in order. When this list is populated, " +
                 "every entry is guaranteed to spawn before any pool roll. Leave " +
                 "empty for purely random encounters; combine with possibleEnemies " +
                 "+ min/maxEnemies to add variable extras on top.")]
        public EnemyData[] fixedEnemies;

        public EnemyData[] possibleEnemies;

        [Tooltip("Number of ADDITIONAL enemies rolled from possibleEnemies, on " +
                 "top of fixedEnemies. Set both to 0 for a strictly fixed roster.")]
        public int minEnemies = 1;
        public int maxEnemies = 3;
        public int floorNumber = 1;
        public Sprite backgroundSprite;
    }
}
