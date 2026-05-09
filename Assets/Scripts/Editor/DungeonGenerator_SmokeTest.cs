#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DarkSpire
{
    // Editor menu: Tools > DarkSpire > Dungeon > Smoke Test (Floor 1)
    // Runs the procedural generator against the first FloorGenerationConfigSO
    // it finds in the project (preferring one named FLOOR_1) and prints
    // generation stats + a small ASCII preview to the console.
    public static class DungeonGenerator_SmokeTest
    {
        [MenuItem("Tools/DarkSpire/Dungeon/Smoke Test (random seed)")]
        public static void RunRandomSeed()
        {
            Run(seed: Random.Range(0, int.MaxValue));
        }

        [MenuItem("Tools/DarkSpire/Dungeon/Smoke Test (seed 0)")]
        public static void RunSeedZero()
        {
            Run(seed: 0);
        }

        private static void Run(int seed)
        {
            var config = FindFloorConfig();
            if (config == null)
            {
                Debug.LogError("[SmokeTest] No FloorGenerationConfigSO found in the project. " +
                               "Right-click in Project: Create > DarkSpire > Dungeon > Floor Generation Config.");
                return;
            }

            Debug.Log($"[SmokeTest] Generating with config '{config.name}', seed {seed} ...");
            var floor = DungeonGenerator.Generate(config, seed);
            if (floor == null)
            {
                Debug.LogError("[SmokeTest] Generator returned null — see warnings above.");
                return;
            }

            Debug.Log(SummariseFloor(floor));
            Debug.Log(AsciiPreview(floor));
        }

        private static FloorGenerationConfigSO FindFloorConfig()
        {
            var guids = AssetDatabase.FindAssets("t:FloorGenerationConfigSO");
            FloorGenerationConfigSO fallback = null;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<FloorGenerationConfigSO>(path);
                if (so == null) continue;
                if (so.name.Contains("FLOOR_1") || so.name == "FLOOR_1") return so;
                fallback ??= so;
            }
            return fallback;
        }

        private static string SummariseFloor(GeneratedFloorData f)
        {
            int keys = 0, chests = 0, gold = 0, shrines = 0, gates = 0;
            foreach (var e in f.entities)
            {
                switch (e.kind)
                {
                    case EntityKind.Key:      keys++;    break;
                    case EntityKind.Chest:    chests++;  break;
                    case EntityKind.GoldPile: gold++;    break;
                    case EntityKind.Shrine:   shrines++; break;
                    case EntityKind.BossGate: gates++;   break;
                }
            }
            int standards = 0, elites = 0, bosses = 0;
            foreach (var m in f.monsterSpawns)
            {
                switch (m.tier)
                {
                    case MonsterTier.Standard: standards++; break;
                    case MonsterTier.Elite:    elites++;    break;
                    case MonsterTier.Boss:     bosses++;    break;
                }
            }
            return $"[SmokeTest] OK seed={f.seed}  size={f.gridSize.x}x{f.gridSize.y}  " +
                   $"floor/wall={f.stats.floorTiles}/{f.stats.wallTiles}  " +
                   $"rooms={f.stats.roomCount}  deadEnds={f.stats.deadEndCount}  " +
                   $"extras={f.stats.extrasPlaced}  critPath={f.stats.criticalPathLength}  " +
                   $"genMs={f.stats.generationTimeMs:F1}  regenAttempts={f.stats.regenerationAttempts}\n" +
                   $"          entities: K{keys} C{chests} G{gold} Sh{shrines} BG{gates}\n" +
                   $"          monsters: S{standards} E{elites} B{bosses}\n" +
                   $"          start={f.startPosition} bossGate={f.bossGatePosition} stair={f.stairwayPosition}";
        }

        private static string AsciiPreview(GeneratedFloorData f)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[SmokeTest] ASCII preview (origin bottom-left):");
            for (int y = f.gridSize.y - 1; y >= 0; y--)
            {
                for (int x = 0; x < f.gridSize.x; x++)
                {
                    char c = f.tiles[x, y] switch
                    {
                        TileType.Wall     => '#',
                        TileType.Floor    => '.',
                        TileType.Start    => 'S',
                        TileType.Stairway => '>',
                        TileType.Rest     => 'R',
                        _ => '?',
                    };
                    var p = new Vector2Int(x, y);
                    foreach (var e in f.entities)
                    {
                        if (e.position != p) continue;
                        c = e.kind switch
                        {
                            EntityKind.Key      => 'k',
                            EntityKind.Chest    => 'c',
                            EntityKind.GoldPile => '$',
                            EntityKind.Shrine   => 'h',
                            EntityKind.BossGate => 'G',
                            _ => c,
                        };
                        break;
                    }
                    foreach (var m in f.monsterSpawns)
                    {
                        if (m.start != p) continue;
                        c = m.tier switch
                        {
                            MonsterTier.Standard => 'm',
                            MonsterTier.Elite    => 'M',
                            MonsterTier.Boss     => 'B',
                            _ => c,
                        };
                        break;
                    }
                    sb.Append(c);
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
#endif
