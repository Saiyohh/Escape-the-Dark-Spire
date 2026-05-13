using UnityEngine;

namespace DarkSpire
{
    // Spawns map-side entity and monster representations from a
    // GeneratedFloorData. Phase 3 ships with placeholder sprite glyphs (same
    // white-sprite + tint fallback as FloorRenderer); Phase 6 swaps in real
    // prefabs (KeyEntity / ChestEntity / RestTileEntity / ...) and Phase 9
    // swaps in MapMonsterEntity prefabs.
    public class EntitySpawner : MonoBehaviour
    {
        [Tooltip("Optional override. Leave null to use the singleton " +
                 "MapEntitySpriteLibrary.Instance.")]
        [SerializeField] private MapEntitySpriteLibrary spriteLibrary;
        [SerializeField] private int entitySortingOrder = 5;
        [SerializeField] private int monsterSortingOrder = 6;
        [SerializeField] private int alertIndicatorSortingOrder = 8;

        private Transform entityParent;
        private Transform monsterParent;
        private Sprite whiteSprite;

        public void SetSpriteLibrary(MapEntitySpriteLibrary lib) => spriteLibrary = lib;

        private MapEntitySpriteLibrary Library =>
            MapEntitySpriteLibrary.ResolveOrSingleton(spriteLibrary);

        // run AFTER the party token exists (monsters need a target).
        public void SpawnEntities(GeneratedFloorData floor)
        {
            EnsureWhiteSprite();
            ResetParents();

            var holder = RunStateHolder.Instance;
            int kept = 0, skipped = 0;
            foreach (var e in floor.entities)
            {
                if (holder != null && holder.IsRemoved(e.position))
                {
                    skipped++;
                    continue;
                }
                // Boss is dead AND the gate sits on the staircase tile —
                // skip the gate so the stairway tile underneath is naked
                // and the player can step onto it to descend.
                if (e.kind == EntityKind.BossGate && holder != null && holder.bossDefeated)
                {
                    skipped++;
                    continue;
                }
                SpawnEntity(e);
                kept++;
            }

            // Diagnostic: makes it obvious whether the resume path is filtering
            // already-collected pickups (the gold-pile / key respawn check).
            if (holder != null)
            {
                Debug.Log($"[EntitySpawner] Spawned {kept} entities, skipped " +
                          $"{skipped} (RunStateHolder.removedEntities count: {holder.removedEntities.Count}).");
            }
        }

        public void SpawnMonsters(GeneratedFloorData floor, PartyToken party, float detectionRadius)
        {
            EnsureWhiteSprite();
            EnsureMonsterParent();
            var holder = RunStateHolder.Instance;
            foreach (var m in floor.monsterSpawns)
            {
                if (holder != null)
                {
                    if (m.tier == MonsterTier.Boss && holder.bossDefeated) continue;
                    if (m.tier != MonsterTier.Boss && holder.IsMonsterDefeated(m.start)) continue;
                }
                SpawnMonster(m, floor, party, detectionRadius);
            }
        }

        // Back-compat shim — kept so old call sites don't break, but it now
        // only spawns entities (monsters need the party).
        public void Spawn(GeneratedFloorData floor) => SpawnEntities(floor);

        public void Clear()
        {
            if (entityParent  != null) DestroyImmediateOrPlay(entityParent.gameObject);
            if (monsterParent != null) DestroyImmediateOrPlay(monsterParent.gameObject);
            entityParent = null;
            monsterParent = null;
        }

        private void EnsureWhiteSprite()
        {
            if (whiteSprite != null) return;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
            tex.filterMode = FilterMode.Point;
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            whiteSprite = Sprite.Create(
                tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), pixelsPerUnit: 1f);
            whiteSprite.name = "RuntimeWhiteEntity";
        }

        private void ResetParents()
        {
            if (entityParent  != null) DestroyImmediateOrPlay(entityParent.gameObject);
            if (monsterParent != null) DestroyImmediateOrPlay(monsterParent.gameObject);
            var ge = new GameObject("Entities");
            ge.transform.SetParent(transform, false);
            entityParent = ge.transform;
            EnsureMonsterParent();
        }

        private void EnsureMonsterParent()
        {
            if (monsterParent != null) return;
            var gm = new GameObject("Monsters");
            gm.transform.SetParent(transform, false);
            monsterParent = gm.transform;
        }

        private void SpawnEntity(EntityPlacement e)
        {
            var go = new GameObject($"E_{e.kind}_{e.position.x}_{e.position.y}");
            go.transform.SetParent(entityParent, false);
            go.transform.localPosition = new Vector3(e.position.x + 0.5f, e.position.y + 0.5f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = entitySortingOrder;

            Sprite authored = null;
            var lib = Library;
            if (lib != null)
            {
                authored = e.kind switch
                {
                    EntityKind.Key      => lib.key,
                    EntityKind.Chest    => lib.chest,
                    EntityKind.GoldPile => lib.goldPile,
                    EntityKind.Shrine   => lib.shrine,
                    // strPayload carries the cardinal facing ("N"/"S"/"E"/"W")
                    // written by PlaceBossGate. Resolver falls back to the
                    // non-directional sprite when a slot is unassigned.
                    EntityKind.BossGate => lib.GetBossGateSprite(e.strPayload, open: false),
                    _ => null,
                };
            }

            if (authored != null) { sr.sprite = authored; sr.color = Color.white; }
            else
            {
                sr.sprite = whiteSprite;
                sr.color = e.kind switch
                {
                    EntityKind.Key      => new Color(1.00f, 0.85f, 0.10f),
                    EntityKind.Chest    => new Color(0.65f, 0.40f, 0.15f),
                    EntityKind.GoldPile => new Color(1.00f, 0.75f, 0.00f),
                    EntityKind.Shrine   => new Color(0.30f, 0.95f, 1.00f),
                    EntityKind.BossGate => new Color(0.90f, 0.20f, 0.20f),
                    _ => Color.magenta,
                };
                go.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
            }

            // Attach the gameplay component matching the kind. Each component
            // self-registers with DungeonRegistry.Instance in Initialize.
            MapEntityBase entity = e.kind switch
            {
                EntityKind.Key      => go.AddComponent<KeyEntity>(),
                EntityKind.Chest    => go.AddComponent<ChestEntity>(),
                EntityKind.GoldPile => go.AddComponent<GoldPileEntity>(),
                EntityKind.Shrine   => go.AddComponent<ShrineEntity>(),
                EntityKind.BossGate => go.AddComponent<BossGateEntity>(),
                _ => null,
            };
            entity?.Initialize(e, sr, Library);
        }

        private void SpawnMonster(MonsterSpawn m, GeneratedFloorData floor, PartyToken party, float detectionRadius)
        {
            var go = new GameObject($"M_{m.tier}_{m.start.x}_{m.start.y}");
            go.transform.SetParent(monsterParent, false);
            go.transform.localPosition = new Vector3(m.start.x + 0.5f, m.start.y + 0.5f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = monsterSortingOrder;

            Sprite authored = null;
            var lib = Library;
            if (lib != null)
            {
                authored = m.tier switch
                {
                    MonsterTier.Standard => lib.standardMonster,
                    MonsterTier.Elite    => lib.eliteMonster,
                    // Boss spawns can override the library icon by setting
                    // EnemyData.mapIcon on the boss enemy in this floor's
                    // encounter pool. Peek the boss encounter (no consume)
                    // and pull the override; fall back to the library sprite
                    // when no override is wired.
                    MonsterTier.Boss     => ResolveBossMapIcon() ?? lib.boss,
                    _ => null,
                };
            }

            if (authored != null) { sr.sprite = authored; sr.color = Color.white; }
            else
            {
                sr.sprite = whiteSprite;
                sr.color = m.tier switch
                {
                    MonsterTier.Standard => new Color(0.65f, 0.10f, 0.10f),
                    MonsterTier.Elite    => new Color(1.00f, 0.30f, 0.30f),
                    MonsterTier.Boss     => new Color(0.50f, 0.10f, 0.50f),
                    _ => Color.magenta,
                };
                float scale = m.tier == MonsterTier.Boss ? 0.85f : 0.65f;
                go.transform.localScale = new Vector3(scale, scale, 1f);
            }

            var monster = go.AddComponent<MapMonsterEntity>();
            monster.InitializeMonster(m, floor, party, Library, sr, detectionRadius, alertIndicatorSortingOrder);
        }

        private static Sprite ResolveBossMapIcon()
        {
            var em = EncounterManager.Instance;
            if (em == null) return null;

            var sub = em.PeekSub(EncounterType.Boss);
            var peeked = sub?.Peek(0);
            var encounter = peeked?.encounter;
            if (encounter == null || encounter.possibleEnemies == null) return null;

            // Prefer the icon from a boss-flagged enemy first.
            for (int i = 0; i < encounter.possibleEnemies.Length; i++)
            {
                var e = encounter.possibleEnemies[i];
                if (e == null) continue;
                if (e.enemyType == EnemyType.Boss && e.mapIcon != null) return e.mapIcon;
            }

            // Fall back to the first authored mapIcon in the pool — covers
            // single-enemy boss encounters where the EnemyType wasn't flagged
            // explicitly.
            for (int i = 0; i < encounter.possibleEnemies.Length; i++)
            {
                var e = encounter.possibleEnemies[i];
                if (e?.mapIcon != null) return e.mapIcon;
            }

            return null;
        }

        private static void DestroyImmediateOrPlay(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }
}
