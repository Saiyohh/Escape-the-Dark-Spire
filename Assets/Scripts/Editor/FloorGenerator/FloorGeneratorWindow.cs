#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DarkSpire
{
    // Designer-facing tool for previewing the procedural generator output and
    // pinning specific seeds. Three-panel layout (params / canvas / stats).
    //
    // Implementation note: IMGUI rather than UI Toolkit. The plan named
    // UI Toolkit but the window is a debug+preview tool with no live UX
    // requirements; IMGUI's immediate-mode rendering is a much better fit
    // for a tile grid drawn from a fresh-each-frame data source. UI Toolkit
    // can be revisited if/when this becomes a designer-facing shipped tool.
    public class FloorGeneratorWindow : EditorWindow
    {
        [MenuItem("Tools/DarkSpire/Dungeon/Floor Generator")]
        public static void Open()
        {
            var w = GetWindow<FloorGeneratorWindow>("Floor Generator");
            w.minSize = new Vector2(900, 600);
        }

        // Parameters
        private FloorGenerationConfigSO config;
        private int seed;
        private GeneratedFloorData floor;

        // View state
        private Vector2 canvasScroll;
        private float tilePixels = 16f;
        private bool showCriticalPath = true;
        private bool showPatrolRoutes = true;
        private bool showDeadEnds = true;

        // Cached overlays computed after generation
        private List<Vector2Int> criticalPath;
        private HashSet<Vector2Int> deadEnds;

        // Hover state
        private Vector2Int? hoverTile;

        // ------------------------------------------------------------------
        // Layout
        // ------------------------------------------------------------------

        private void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawLeftPanel(GUILayout.Width(280));
            DrawCenterCanvas();
            DrawRightPanel(GUILayout.Width(240));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            using (new EditorGUI.DisabledScope(config == null))
            {
                if (GUILayout.Button("Generate", EditorStyles.toolbarButton)) Generate();
                if (GUILayout.Button("Re-roll",  EditorStyles.toolbarButton)) Reroll();
                if (GUILayout.Button("Validate", EditorStyles.toolbarButton)) ValidateCurrent();
            }

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(config == null))
            {
                if (GUILayout.Button("Save Config", EditorStyles.toolbarButton)) SaveConfig();
            }
            using (new EditorGUI.DisabledScope(floor == null))
            {
                if (GUILayout.Button("Export as Asset", EditorStyles.toolbarButton)) ExportSnapshot();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawLeftPanel(params GUILayoutOption[] options)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box, options);
            EditorGUILayout.LabelField("Generation Parameters", EditorStyles.boldLabel);

            config = (FloorGenerationConfigSO)EditorGUILayout.ObjectField(
                "Config", config, typeof(FloorGenerationConfigSO), false);

            if (config == null)
            {
                EditorGUILayout.HelpBox(
                    "Drag a FloorGenerationConfigSO here, or create one via\n" +
                    "Assets > Create > DarkSpire > Dungeon > Floor Generation Config.",
                    MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.Space();

            // Inline editor for the config asset so designers can iterate without
            // leaving the window. Same as selecting the asset in the project.
            var serialized = new SerializedObject(config);
            serialized.Update();
            var prop = serialized.GetIterator();
            bool first = true;
            while (prop.NextVisible(first))
            {
                first = false;
                if (prop.name == "m_Script") continue;
                EditorGUILayout.PropertyField(prop, true);
            }
            serialized.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Seed", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            seed = EditorGUILayout.IntField(seed);
            if (GUILayout.Button("Randomize", GUILayout.Width(80)))
                seed = Random.Range(0, int.MaxValue);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Display", EditorStyles.boldLabel);
            tilePixels = EditorGUILayout.Slider("Tile Px", tilePixels, 4f, 32f);
            showCriticalPath = EditorGUILayout.Toggle("Critical Path", showCriticalPath);
            showDeadEnds     = EditorGUILayout.Toggle("Dead Ends",     showDeadEnds);
            showPatrolRoutes = EditorGUILayout.Toggle("Patrol Routes", showPatrolRoutes);

            EditorGUILayout.EndVertical();
        }

        private void DrawCenterCanvas()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true));

            if (floor == null)
            {
                EditorGUILayout.HelpBox(
                    "Hit Generate to render a floor.",
                    MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            float w = floor.gridSize.x * tilePixels;
            float h = floor.gridSize.y * tilePixels;

            canvasScroll = EditorGUILayout.BeginScrollView(canvasScroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            var rect = GUILayoutUtility.GetRect(w, h, GUILayout.Width(w), GUILayout.Height(h));
            DrawFloor(rect);
            HandleHover(rect);

            EditorGUILayout.EndScrollView();

            // Hover tooltip line under the canvas.
            if (hoverTile.HasValue)
            {
                var t = hoverTile.Value;
                EditorGUILayout.LabelField(BuildHoverText(t), EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField(" ", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRightPanel(params GUILayoutOption[] options)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box, options);
            EditorGUILayout.LabelField("Generation Stats", EditorStyles.boldLabel);

            if (floor == null)
            {
                EditorGUILayout.LabelField("(no floor generated)");
                EditorGUILayout.EndVertical();
                return;
            }

            var s = floor.stats;
            EditorGUILayout.LabelField("Seed", floor.seed.ToString());
            EditorGUILayout.LabelField("Grid", $"{floor.gridSize.x} x {floor.gridSize.y}");
            EditorGUILayout.LabelField("Floor / Wall", $"{s.floorTiles} / {s.wallTiles}");
            EditorGUILayout.LabelField("Rooms", s.roomCount.ToString());
            EditorGUILayout.LabelField("Dead Ends", s.deadEndCount.ToString());
            EditorGUILayout.LabelField("Extras Placed", s.extrasPlaced.ToString());
            EditorGUILayout.LabelField("Critical Path", s.criticalPathLength.ToString());
            EditorGUILayout.LabelField("Regen Attempts", s.regenerationAttempts.ToString());
            EditorGUILayout.LabelField("Gen Time", $"{s.generationTimeMs:F1} ms");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Counts", EditorStyles.boldLabel);
            CountSummary();

            EditorGUILayout.EndVertical();
        }

        private void CountSummary()
        {
            int keys = 0, chests = 0, gold = 0, shrines = 0, gates = 0;
            foreach (var e in floor.entities)
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
            foreach (var m in floor.monsterSpawns)
            {
                switch (m.tier)
                {
                    case MonsterTier.Standard: standards++; break;
                    case MonsterTier.Elite:    elites++;    break;
                    case MonsterTier.Boss:     bosses++;    break;
                }
            }
            EditorGUILayout.LabelField("Keys",    keys.ToString());
            EditorGUILayout.LabelField("Chests",  chests.ToString());
            EditorGUILayout.LabelField("Gold",    gold.ToString());
            EditorGUILayout.LabelField("Shrines", shrines.ToString());
            EditorGUILayout.LabelField("Gates",   gates.ToString());
            EditorGUILayout.LabelField("Standards", standards.ToString());
            EditorGUILayout.LabelField("Elites",    elites.ToString());
            EditorGUILayout.LabelField("Bosses",    bosses.ToString());
        }

        // ------------------------------------------------------------------
        // Canvas drawing
        // ------------------------------------------------------------------

        // Visual y-axis flip: GUI rects grow downward, but the GDD uses a
        // bottom-left origin (matches gameplay coordinate system).
        private Rect TileRect(Rect canvas, int x, int y)
        {
            float gx = canvas.x + x * tilePixels;
            float gy = canvas.y + (floor.gridSize.y - 1 - y) * tilePixels;
            return new Rect(gx, gy, tilePixels, tilePixels);
        }

        private static readonly Color WallColor      = new Color(0.13f, 0.13f, 0.15f, 1f);
        private static readonly Color FloorColor     = new Color(0.62f, 0.62f, 0.65f, 1f);
        private static readonly Color StartColor     = new Color(0.27f, 0.53f, 1.00f, 1f);
        private static readonly Color StairwayColor  = new Color(0.63f, 0.50f, 1.00f, 1f);
        private static readonly Color RestColor      = new Color(0.25f, 1.00f, 0.50f, 1f);
        private static readonly Color CritPathColor  = new Color(0.30f, 0.60f, 1.00f, 0.25f);
        private static readonly Color DeadEndColor   = new Color(1.00f, 0.50f, 0.20f, 0.25f);

        private static readonly Color KeyColor       = new Color(1.00f, 0.85f, 0.10f, 1f);
        private static readonly Color ChestColor     = new Color(0.65f, 0.40f, 0.15f, 1f);
        private static readonly Color GoldColor      = new Color(1.00f, 0.75f, 0.00f, 1f);
        private static readonly Color ShrineColor    = new Color(0.30f, 0.95f, 1.00f, 1f);
        private static readonly Color BossGateColor  = new Color(0.90f, 0.20f, 0.20f, 1f);

        private static readonly Color StandardColor  = new Color(0.65f, 0.10f, 0.10f, 1f);
        private static readonly Color EliteColor     = new Color(1.00f, 0.30f, 0.30f, 1f);
        private static readonly Color BossColor      = new Color(0.50f, 0.10f, 0.50f, 1f);

        private void DrawFloor(Rect canvas)
        {
            // 1. Tiles
            for (int x = 0; x < floor.gridSize.x; x++)
            {
                for (int y = 0; y < floor.gridSize.y; y++)
                {
                    var r = TileRect(canvas, x, y);
                    Color c = floor.tiles[x, y] switch
                    {
                        TileType.Wall     => WallColor,
                        TileType.Floor    => FloorColor,
                        TileType.Start    => StartColor,
                        TileType.Stairway => StairwayColor,
                        TileType.Rest     => RestColor,
                        _ => WallColor,
                    };
                    EditorGUI.DrawRect(r, c);
                }
            }

            // 2. Critical path overlay
            if (showCriticalPath && criticalPath != null)
            {
                foreach (var p in criticalPath)
                {
                    EditorGUI.DrawRect(TileRect(canvas, p.x, p.y), CritPathColor);
                }
            }

            // 3. Dead-end overlay
            if (showDeadEnds && deadEnds != null)
            {
                foreach (var p in deadEnds)
                {
                    EditorGUI.DrawRect(TileRect(canvas, p.x, p.y), DeadEndColor);
                }
            }

            // 4. Patrol routes (drawn before entity glyphs so glyphs sit on top)
            if (showPatrolRoutes)
            {
                Handles.BeginGUI();
                foreach (var m in floor.monsterSpawns)
                {
                    if (m.patrolRoute == null || m.patrolRoute.Count < 2) continue;
                    Handles.color = m.tier == MonsterTier.Elite
                        ? new Color(1f, 0.5f, 0.5f, 0.7f)
                        : new Color(0.8f, 0.3f, 0.3f, 0.7f);
                    for (int i = 0; i + 1 < m.patrolRoute.Count; i++)
                    {
                        var a = m.patrolRoute[i];
                        var b = m.patrolRoute[i + 1];
                        var ra = TileRect(canvas, a.x, a.y);
                        var rb = TileRect(canvas, b.x, b.y);
                        Handles.DrawLine(
                            new Vector3(ra.x + ra.width * 0.5f, ra.y + ra.height * 0.5f),
                            new Vector3(rb.x + rb.width * 0.5f, rb.y + rb.height * 0.5f));
                    }
                }
                Handles.EndGUI();
            }

            // 5. Entity glyphs
            foreach (var e in floor.entities)
            {
                Color c = e.kind switch
                {
                    EntityKind.Key      => KeyColor,
                    EntityKind.Chest    => ChestColor,
                    EntityKind.GoldPile => GoldColor,
                    EntityKind.Shrine   => ShrineColor,
                    EntityKind.BossGate => BossGateColor,
                    _ => Color.white,
                };
                DrawGlyph(canvas, e.position, c, 0.55f);
            }

            // 6. Monster glyphs
            foreach (var m in floor.monsterSpawns)
            {
                Color c = m.tier switch
                {
                    MonsterTier.Standard => StandardColor,
                    MonsterTier.Elite    => EliteColor,
                    MonsterTier.Boss     => BossColor,
                    _ => Color.white,
                };
                float scale = m.tier == MonsterTier.Boss ? 0.85f : 0.65f;
                DrawGlyph(canvas, m.start, c, scale);
            }

            // 7. Hover highlight
            if (hoverTile.HasValue)
            {
                var r = TileRect(canvas, hoverTile.Value.x, hoverTile.Value.y);
                var outline = new Color(1f, 1f, 1f, 0.5f);
                EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1), outline);
                EditorGUI.DrawRect(new Rect(r.x, r.y + r.height - 1, r.width, 1), outline);
                EditorGUI.DrawRect(new Rect(r.x, r.y, 1, r.height), outline);
                EditorGUI.DrawRect(new Rect(r.x + r.width - 1, r.y, 1, r.height), outline);
            }
        }

        private void DrawGlyph(Rect canvas, Vector2Int pos, Color color, float scale)
        {
            var r = TileRect(canvas, pos.x, pos.y);
            float pad = (1f - scale) * 0.5f * tilePixels;
            EditorGUI.DrawRect(
                new Rect(r.x + pad, r.y + pad, r.width - pad * 2f, r.height - pad * 2f),
                color);
        }

        private void HandleHover(Rect canvas)
        {
            var ev = Event.current;
            if (ev.type == EventType.Repaint || ev.type == EventType.MouseMove ||
                ev.type == EventType.MouseDrag || ev.type == EventType.MouseUp)
            {
                if (canvas.Contains(ev.mousePosition))
                {
                    int x = Mathf.FloorToInt((ev.mousePosition.x - canvas.x) / tilePixels);
                    int yFlipped = Mathf.FloorToInt((ev.mousePosition.y - canvas.y) / tilePixels);
                    int y = floor.gridSize.y - 1 - yFlipped;
                    if (x >= 0 && y >= 0 && x < floor.gridSize.x && y < floor.gridSize.y)
                    {
                        hoverTile = new Vector2Int(x, y);
                        Repaint();
                    }
                    else
                    {
                        hoverTile = null;
                    }
                }
                else
                {
                    if (hoverTile.HasValue) Repaint();
                    hoverTile = null;
                }
            }
            // Force MouseMove events while window is focused (Unity skips them otherwise)
            wantsMouseMove = true;
        }

        private string BuildHoverText(Vector2Int t)
        {
            string baseLine = $"({t.x},{t.y})  {floor.tiles[t.x, t.y]}";
            foreach (var e in floor.entities)
            {
                if (e.position == t)
                {
                    baseLine += $"  •  {e.kind}";
                    if (e.intPayload != 0) baseLine += $"={e.intPayload}";
                }
            }
            foreach (var m in floor.monsterSpawns)
            {
                if (m.start == t)
                {
                    int wp = m.patrolRoute != null ? m.patrolRoute.Count : 0;
                    baseLine += $"  •  {m.tier} (waypoints: {wp})";
                }
            }
            return baseLine;
        }

        // ------------------------------------------------------------------
        // Actions
        // ------------------------------------------------------------------

        private void Generate()
        {
            floor = DungeonGenerator.Generate(config, seed);
            ComputeOverlays();
            Repaint();
        }

        private void Reroll()
        {
            seed = Random.Range(0, int.MaxValue);
            Generate();
        }

        private void ValidateCurrent()
        {
            if (floor == null) return;
            var reachable = GridBfs.Reachable(
                floor.tiles, floor.startPosition, t => t != TileType.Wall);
            int unreachableEntities = 0;
            foreach (var e in floor.entities)
                if (!reachable.Contains(e.position)) unreachableEntities++;
            int keyCount = 0;
            foreach (var e in floor.entities) if (e.kind == EntityKind.Key) keyCount++;

            string msg = $"Reachable tiles from Start: {reachable.Count}\n" +
                         $"Unreachable entities: {unreachableEntities}\n" +
                         $"Keys placed: {keyCount}\n" +
                         $"Boss Gate reachable: {(reachable.Contains(floor.bossGatePosition) ? "yes" : "no")}";
            EditorUtility.DisplayDialog("Validate Floor", msg, "OK");
        }

        private void SaveConfig()
        {
            if (config == null) return;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log($"[FloorGenerator] Saved config '{config.name}'.");
        }

        private void ExportSnapshot()
        {
            if (floor == null) return;
            string defaultName = $"GFS_{(config != null ? config.name : "Custom")}_seed{floor.seed}.asset";
            string path = EditorUtility.SaveFilePanelInProject(
                "Export Generated Floor",
                defaultName,
                "asset",
                "Save the current floor as a GeneratedFloorSO snapshot.");
            if (string.IsNullOrEmpty(path)) return;

            var snap = GeneratedFloorSO.FromRuntimeData(floor, config != null ? config.name : "Custom");
            AssetDatabase.CreateAsset(snap, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(snap);
            Debug.Log($"[FloorGenerator] Exported snapshot to {path}");
        }

        private void ComputeOverlays()
        {
            if (floor == null)
            {
                criticalPath = null;
                deadEnds = null;
                return;
            }

            criticalPath = GridBfs.FindPath(
                floor.tiles, floor.startPosition, floor.bossGatePosition,
                t => t != TileType.Wall);

            // Recompute dead-end terminals from the final tile grid (the
            // generator's GenContext is gone by now). A dead end is a Floor
            // tile with exactly one Floor neighbour.
            deadEnds = new HashSet<Vector2Int>();
            int w = floor.gridSize.x, h = floor.gridSize.y;
            var dirs = new[]
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1),
            };
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    if (floor.tiles[x, y] == TileType.Wall) continue;
                    int n = 0;
                    foreach (var d in dirs)
                    {
                        int nx = x + d.x, ny = y + d.y;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        if (floor.tiles[nx, ny] != TileType.Wall) n++;
                    }
                    if (n == 1) deadEnds.Add(new Vector2Int(x, y));
                }
        }
    }
}
#endif
