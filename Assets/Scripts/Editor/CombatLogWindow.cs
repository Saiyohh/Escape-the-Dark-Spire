// CombatLogWindow.cs
// -----------------------------------------------------------------------------
// EditorWindow that captures every CombatEvents.On* fire during Play mode and
// presents them as a filterable, color-coded log. Opens from:
//
//   DarkSpire → Testing → Combat Log Window
//
// Features:
//   • Filter toggles for phase changes, turns, dice rolls, actions, deaths
//   • Live scroll, with auto-scroll-to-end toggle
//   • Clear button
//   • Export current buffer to a timestamped `.md` in <Project>/Logs/
//   • Auto-export when Play mode stops, if "Save on Play End" is ticked
//
// Subscribes lazily — events are only captured while Play mode is active, so
// editor-time UI interaction doesn't pollute the buffer.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    public class CombatLogWindow : EditorWindow
    {
        private enum EntryKind { Phase, Turn, Dice, Action, Death, CombatStart, CombatEnd, ActionState }

        private struct Entry
        {
            public DateTime time;
            public EntryKind kind;
            public string text;
            public Color color;
        }

        private readonly List<Entry> entries = new();
        private Vector2 scroll;
        private bool autoScroll = true;
        private bool saveOnPlayEnd = false;
        private bool subscribed = false;
        private bool showPhase = true;
        private bool showTurn = true;
        private bool showDice = true;
        private bool showAction = true;
        private bool showDeath = true;
        private bool showOther = true;
        private string searchFilter = "";

        [MenuItem("DarkSpire/Testing/Combat Log Window")]
        public static void Open()
        {
            var w = GetWindow<CombatLogWindow>("Combat Log");
            w.minSize = new Vector2(500, 300);
            w.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (EditorApplication.isPlayingOrWillChangePlaymode && Application.isPlaying)
                Subscribe();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Unsubscribe();
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    entries.Clear();
                    Subscribe();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    if (saveOnPlayEnd && entries.Count > 0) ExportMarkdown();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    Unsubscribe();
                    break;
            }
            Repaint();
        }

        private void Subscribe()
        {
            if (subscribed) return;
            CombatEvents.OnCombatStart += HandleCombatStart;
            CombatEvents.OnCombatEnd += HandleCombatEnd;
            CombatEvents.OnPhaseChanged += HandlePhaseChanged;
            CombatEvents.OnTurnStart += HandleTurnStart;
            CombatEvents.OnTurnEnd += HandleTurnEnd;
            CombatEvents.OnUnitTurnStart += HandleUnitTurnStart;
            CombatEvents.OnUnitTurnEnd += HandleUnitTurnEnd;
            CombatEvents.OnDiceRolled += HandleDiceRolled;
            CombatEvents.OnActionResolved += HandleActionResolved;
            CombatEvents.OnEnemyDeath += HandleEnemyDeath;
            CombatEvents.OnActionStateChanged += HandleActionStateChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            CombatEvents.OnCombatStart -= HandleCombatStart;
            CombatEvents.OnCombatEnd -= HandleCombatEnd;
            CombatEvents.OnPhaseChanged -= HandlePhaseChanged;
            CombatEvents.OnTurnStart -= HandleTurnStart;
            CombatEvents.OnTurnEnd -= HandleTurnEnd;
            CombatEvents.OnUnitTurnStart -= HandleUnitTurnStart;
            CombatEvents.OnUnitTurnEnd -= HandleUnitTurnEnd;
            CombatEvents.OnDiceRolled -= HandleDiceRolled;
            CombatEvents.OnActionResolved -= HandleActionResolved;
            CombatEvents.OnEnemyDeath -= HandleEnemyDeath;
            CombatEvents.OnActionStateChanged -= HandleActionStateChanged;
            subscribed = false;
        }

        // -------- event handlers → entry push -----------------------------------

        private void Push(EntryKind kind, string text, Color color)
        {
            entries.Add(new Entry { time = DateTime.Now, kind = kind, text = text, color = color });
            if (entries.Count > 5000) entries.RemoveAt(0); // safety cap
            Repaint();
        }

        private static readonly Color ColPhase  = new(0.60f, 0.82f, 1.00f);
        private static readonly Color ColTurn   = new(0.75f, 0.60f, 1.00f);
        private static readonly Color ColDice   = new(1.00f, 0.86f, 0.30f);
        private static readonly Color ColHit    = new(1.00f, 0.55f, 0.40f);
        private static readonly Color ColHeal   = new(0.40f, 1.00f, 0.55f);
        private static readonly Color ColDeath  = new(1.00f, 0.30f, 0.30f);
        private static readonly Color ColAction = new(0.90f, 0.90f, 0.90f);
        private static readonly Color ColOther  = new(0.70f, 0.70f, 0.70f);

        private void HandleCombatStart() =>
            Push(EntryKind.CombatStart, "⚔ Combat Start", ColPhase);
        private void HandleCombatEnd(bool victory) =>
            Push(EntryKind.CombatEnd, victory ? "🏆 Combat End: VICTORY" : "💀 Combat End: DEFEAT", victory ? ColHeal : ColDeath);
        private void HandlePhaseChanged(CombatPhase phase) =>
            Push(EntryKind.Phase, $"Phase → {phase}", ColPhase);
        private void HandleTurnStart(int turn) =>
            Push(EntryKind.Turn, $"── Turn {turn} begins ──", ColTurn);
        private void HandleTurnEnd(int turn) =>
            Push(EntryKind.Turn, $"── Turn {turn} ends ──", ColTurn);
        private void HandleUnitTurnStart(Unit u) =>
            Push(EntryKind.Turn, $"· {u.unitName} → turn start (HP {u.currentHP}/{u.maxHP})", ColTurn);
        private void HandleUnitTurnEnd(Unit u) =>
            Push(EntryKind.Turn, $"· {u.unitName} → turn end", ColTurn);
        private void HandleDiceRolled(Unit u, int raw, int total, bool hit, bool crit) =>
            Push(EntryKind.Dice, $"🎲 {u.unitName}: raw {raw}, total {total} → " +
                $"{(crit ? "CRIT " : "")}{(hit ? "HIT" : "MISS")}", ColDice);
        private void HandleActionResolved(CombatActionResult r)
        {
            if (r == null) return;
            var sb = new StringBuilder();
            sb.Append($"→ {r.source?.unitName ?? "?"} {r.actionName}");
            if (r.target != null && r.target != r.source) sb.Append($" on {r.target.unitName}");
            if (r.damageDealt > 0) sb.Append($" | dmg {r.damageDealt}");
            if (r.healingDone > 0) sb.Append($" | heal +{r.healingDone}");
            if (r.defenseGained > 0) sb.Append($" | def +{r.defenseGained}");
            if (r.wasDodged) sb.Append(" | DODGED");
            if (r.spSpent > 0) sb.Append($" | SP -{r.spSpent}");
            Push(EntryKind.Action, sb.ToString(),
                r.damageDealt > 0 ? ColHit : (r.healingDone > 0 ? ColHeal : ColAction));
        }
        private void HandleEnemyDeath(Unit u) =>
            Push(EntryKind.Death, $"☠ {u.unitName} defeated", ColDeath);
        private void HandleActionStateChanged(Unit u) =>
            Push(EntryKind.ActionState, $"· {u.unitName} action state changed", ColOther);

        // -------- GUI -----------------------------------------------------------

        private void OnGUI()
        {
            DrawToolbar();
            DrawFilters();
            DrawLog();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label($"Entries: {entries.Count}", EditorStyles.toolbarButton, GUILayout.Width(110));

                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    entries.Clear();

                if (GUILayout.Button("Export .md", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    ExportMarkdown();

                autoScroll = GUILayout.Toggle(autoScroll, "Auto-Scroll", EditorStyles.toolbarButton, GUILayout.Width(90));
                saveOnPlayEnd = GUILayout.Toggle(saveOnPlayEnd, "Save on Play End", EditorStyles.toolbarButton, GUILayout.Width(130));

                GUILayout.FlexibleSpace();
                GUILayout.Label("Search:", GUILayout.Width(50));
                searchFilter = GUILayout.TextField(searchFilter ?? "", EditorStyles.toolbarSearchField, GUILayout.Width(180));
            }
        }

        private void DrawFilters()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                showPhase  = GUILayout.Toggle(showPhase,  "Phase",  "Button", GUILayout.Width(70));
                showTurn   = GUILayout.Toggle(showTurn,   "Turn",   "Button", GUILayout.Width(70));
                showDice   = GUILayout.Toggle(showDice,   "Dice",   "Button", GUILayout.Width(70));
                showAction = GUILayout.Toggle(showAction, "Action", "Button", GUILayout.Width(70));
                showDeath  = GUILayout.Toggle(showDeath,  "Death",  "Button", GUILayout.Width(70));
                showOther  = GUILayout.Toggle(showOther,  "Other",  "Button", GUILayout.Width(70));
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawLog()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var style = new GUIStyle(EditorStyles.label) { richText = false, wordWrap = true };
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (!Include(e.kind)) continue;
                if (!string.IsNullOrEmpty(searchFilter) &&
                    e.text.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                style.normal.textColor = e.color;
                GUILayout.Label($"[{e.time:HH:mm:ss.fff}] {e.text}", style);
            }
            if (autoScroll && Event.current.type == EventType.Repaint)
                scroll.y = float.MaxValue;
            EditorGUILayout.EndScrollView();
        }

        private bool Include(EntryKind kind) => kind switch
        {
            EntryKind.Phase       => showPhase,
            EntryKind.Turn        => showTurn,
            EntryKind.Dice        => showDice,
            EntryKind.Action      => showAction,
            EntryKind.Death       => showDeath,
            EntryKind.CombatStart => showPhase,
            EntryKind.CombatEnd   => showPhase,
            EntryKind.ActionState => showOther,
            _ => showOther,
        };

        // -------- export --------------------------------------------------------

        private void ExportMarkdown()
        {
            if (entries.Count == 0)
            {
                Debug.Log("[CombatLog] Nothing to export.");
                return;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dir = Path.Combine(projectRoot, "Logs");
            Directory.CreateDirectory(dir);

            string filename = $"CombatLog_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.md";
            string path = Path.Combine(dir, filename);

            var sb = new StringBuilder();
            sb.AppendLine($"# Combat Log");
            sb.AppendLine($"_Captured {DateTime.Now:yyyy-MM-dd HH:mm:ss}, {entries.Count} entries_");
            sb.AppendLine();
            sb.AppendLine("| Time | Kind | Event |");
            sb.AppendLine("|---|---|---|");

            foreach (var e in entries)
            {
                string kindStr = e.kind.ToString();
                string text = e.text.Replace("|", "\\|");
                sb.AppendLine($"| {e.time:HH:mm:ss.fff} | {kindStr} | {text} |");
            }

            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[CombatLog] Exported {entries.Count} entries → {path}");
            EditorUtility.RevealInFinder(path);
        }
    }
}
