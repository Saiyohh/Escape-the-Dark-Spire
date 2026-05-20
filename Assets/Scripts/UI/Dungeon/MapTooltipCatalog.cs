// MapTooltipCatalog.cs
// -----------------------------------------------------------------------------
// Single source of truth for the display name + one-line description of every
// dungeon icon. Read by:
//   • MapEntityHoverTrigger — on-hover tooltip per entity / tile.
//   • IconLegendModal       — full list shown by the HUD's "?" button.
//   • FirstRunIntroCard     — the "what these icons mean" snippet.
//
// Authored as code (not a ScriptableObject) so a single edit here propagates
// everywhere without an asset-wiring step.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

namespace DarkSpire
{
    public static class MapTooltipCatalog
    {
        public struct Entry
        {
            public string Name;
            public string Description;
            public Sprite Icon;
        }

        // Icons are resolved lazily off the sprite library so the catalog
        // doesn't have to be re-initialized when the library asset loads.
        private static Sprite Lib(System.Func<MapEntitySpriteLibrary, Sprite> pick)
        {
            var lib = MapEntitySpriteLibrary.Instance;
            return lib != null ? pick(lib) : null;
        }

        public static Entry Key => new Entry
        {
            Name = "Key",
            Description = "Carry these to the boss gate. The gate needs more than one.",
            Icon = Lib(l => l.key),
        };

        public static Entry GoldPile => new Entry
        {
            Name = "Gold Pile",
            Description = "Currency. Picked up on contact — bigger wins ahead.",
            Icon = Lib(l => l.goldPile),
        };

        public static Entry Chest => new Entry
        {
            Name = "Chest",
            Description = "Press E next to a chest to open it.",
            Icon = Lib(l => l.chest),
        };

        public static Entry Shrine => new Entry
        {
            Name = "Shrine",
            Description = "Press E to pray. One-time blessing or curse.",
            Icon = Lib(l => l.shrine),
        };

        public static Entry BossGate => new Entry
        {
            Name = "Boss Gate",
            Description = "Locked. Carry the required keys, then press E to open.",
            Icon = Lib(l => l.GetBossGateSprite(null, open: false)),
        };

        public static Entry Stairway => new Entry
        {
            Name = "Stairway",
            Description = "Floor exit. Defeat the boss first, then step on to escape.",
            Icon = Lib(l => l.stairway),
        };

        public static Entry Rest => new Entry
        {
            Name = "Campsite",
            Description = "Press E to heal and revive the party. One use per camp.",
            Icon = Lib(l => l.campsite),
        };

        public static Entry StandardMonster => new Entry
        {
            Name = "Monster",
            Description = "Walks a patrol. Spots you, alerts (!), then chases — fight on contact.",
            Icon = Lib(l => l.standardMonster),
        };

        public static Entry EliteMonster => new Entry
        {
            Name = "Elite Monster",
            Description = "Tougher patroller with a wider detection range. Worth more.",
            Icon = Lib(l => l.eliteMonster),
        };

        public static Entry BossMonster => new Entry
        {
            Name = "Boss",
            Description = "Sits in the boss room. Defeat it to unlock the stairway exit.",
            Icon = Lib(l => l.boss),
        };

        public static Entry AlertIndicator => new Entry
        {
            Name = "Alerted",
            Description = "A monster spotted you. After a beat it will chase.",
            Icon = Lib(l => l.alertIndicator),
        };

        /// <summary>Iteration order used by the legend modal. BossGate / Boss /
        /// AlertIndicator are intentionally excluded — their current placeholder
        /// art reads poorly at row scale. Re-enable them once authored sprites
        /// land.</summary>
        public static IEnumerable<Entry> AllForLegend()
        {
            yield return Key;
            yield return Stairway;
            yield return GoldPile;
            yield return Chest;
            yield return Shrine;
            yield return Rest;
            yield return StandardMonster;
            yield return EliteMonster;
        }

        public static Entry ForEntityKind(EntityKind kind)
        {
            return kind switch
            {
                EntityKind.Key      => Key,
                EntityKind.GoldPile => GoldPile,
                EntityKind.Chest    => Chest,
                EntityKind.Shrine   => Shrine,
                EntityKind.BossGate => BossGate,
                _ => default,
            };
        }

        public static Entry ForMonsterTier(MonsterTier tier)
        {
            return tier switch
            {
                MonsterTier.Standard => StandardMonster,
                MonsterTier.Elite    => EliteMonster,
                MonsterTier.Boss     => BossMonster,
                _ => default,
            };
        }
    }
}
