using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "DarkSpire/Weapon")]
    public class WeaponData : ScriptableObject
    {
        public string weaponName;
        [TextArea] public string description;
        public Sprite icon;

        [Header("Combat Stats")]
        public int baseDamage;
        public int attackBonus;
        public int hitCount = 1;
        public int critThreshold = 20;
        public TargetMode targetMode = TargetMode.SingleEnemy;

        [Header("Range (distance units)")]
        [Tooltip("Minimum distance this weapon can reach. 1 = melee-only.")]
        [Min(1)] public int rangeMin = 1;
        [Tooltip("Maximum distance this weapon can reach. Melee weapons " +
                 "typically use 1-2; bows 3-6.")]
        [Min(1)] public int rangeMax = 2;

        [Header("On-Hit Effect")]
        [Tooltip("If true, successful hits roll onHitConditionChance to apply " +
                 "onHitConditionID at onHitConditionStacks stacks.")]
        public bool hasOnHit;
        public ConditionID onHitConditionID;
        public int onHitConditionStacks = 1;
        [Range(0f, 1f)] public float onHitConditionChance = 1f;

        [Header("On-Miss Effect")]
        public bool hasOnMiss;
        public ConditionID onMissConditionID;
        public int onMissConditionStacks = 1;

        [Header("Orb Strike (Defect-style weapons)")]
        [Tooltip("When true, every Attack with this weapon also channels a " +
                 "random Tier 1 orb (Lightning or Frost, 50/50) after the hit " +
                 "resolves. The Defect's WPN_OrbBeam uses this; every other " +
                 "weapon leaves it false.")]
        public bool channelOrbOnAttack;

        [Header("Meta")]
        public Alignment alignment;
        public Rarity rarity;
        public WeaponData upgradedVersion;
        public int maxUpgradeTier = 1;
        public int shopCost;

        public List<DescriptionToken> BuildDescriptionTokens()
        {
            return DescriptionTokenizer.BuildWeaponTokens(this);
        }
    }
}
