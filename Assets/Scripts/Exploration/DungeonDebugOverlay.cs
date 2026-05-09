using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkSpire
{
    // Toggle with F9. Shows the active floor's seed, the EncounterManager
    // sub-manager queue depths, and the DungeonManager's annotation list.
    // Reads exclusively via DungeonManager.PeekRoadmap / PeekSub — gameplay
    // code is never queried directly. Render is IMGUI for editor-debug speed.
    public class DungeonDebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool showOnStart = false;
        [SerializeField] private DungeonBootstrap bootstrap;

        private bool visible;

        private void Awake()
        {
            visible = showOnStart;
            if (bootstrap == null) bootstrap = GetComponentInParent<DungeonBootstrap>();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame) visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible) return;

            const int w = 360, h = 320;
            GUI.Box(new Rect(10, 10, w, h), "");

            GUILayout.BeginArea(new Rect(20, 20, w - 20, h - 20));
            GUILayout.Label("<b>Dungeon Debug</b> (F9 to toggle)",
                new GUIStyle(GUI.skin.label) { richText = true });

            DrawFloor();
            GUILayout.Space(6);
            DrawSubManagers();
            GUILayout.Space(6);
            DrawAnnotations();
            GUILayout.Space(6);
            DrawRunContext();
            GUILayout.EndArea();
        }

        private void DrawFloor()
        {
            var f = bootstrap != null ? bootstrap.ActiveFloor : RunContext.currentFloor;
            if (f == null) { GUILayout.Label("Floor: (none)"); return; }
            GUILayout.Label($"Floor seed={f.seed} size={f.gridSize.x}x{f.gridSize.y} " +
                            $"rooms={f.stats.roomCount} ents={f.entities.Count} mons={f.monsterSpawns.Count}");
        }

        private void DrawSubManagers()
        {
            var dm = DungeonManager.Instance;
            if (dm == null) { GUILayout.Label("DungeonManager: (none)"); return; }
            GUILayout.Label("<b>Sub-Manager queues</b>",
                new GUIStyle(GUI.skin.label) { richText = true });

            DrawQueue(dm, EncounterType.Monster);
            DrawQueue(dm, EncounterType.Elite);
            DrawQueue(dm, EncounterType.Boss);
            DrawQueue(dm, EncounterType.Campsite);
            DrawQueue(dm, EncounterType.Event);
            DrawQueue(dm, EncounterType.Shrine);
        }

        private void DrawQueue(DungeonManager dm, EncounterType type)
        {
            var sub = dm.PeekSub(type);
            if (sub == null) return;
            string state;
            if (sub.TotalAuthoredCount == 0)
            {
                state = "(no pool authored)";
            }
            else if (sub.IsExhausted())
            {
                state = $"reshuffles next ({sub.TotalAuthoredCount} authored)";
            }
            else
            {
                state = $"{sub.RemainingCount()}/{sub.TotalAuthoredCount}";
            }
            GUILayout.Label($"  {type}: {state}");
        }

        private void DrawAnnotations()
        {
            var dm = DungeonManager.Instance;
            var list = dm != null ? dm.PeekRoadmap() : null;
            GUILayout.Label("<b>Roadmap annotations</b>",
                new GUIStyle(GUI.skin.label) { richText = true });
            if (list == null || list.Count == 0)
            {
                GUILayout.Label("  (none)");
                return;
            }
            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                string parts = "";
                if (a.reward.guaranteedKey)        parts += "key ";
                if (a.reward.bonusGold != 0)        parts += $"gold+{a.reward.bonusGold} ";
                if (a.reward.bonusItemDrop != null) parts += $"item:{a.reward.bonusItemDrop.name} ";
                if (parts.Length == 0) parts = "(no override)";
                GUILayout.Label($"  {a.targetType} slot {a.slotIndex} -> {parts}");
            }
        }

        private void DrawRunContext()
        {
            GUILayout.Label("<b>RunContext</b>",
                new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label($"  keys={RunContext.keysHeld}  gold={RunContext.gold}  floor={RunContext.currentFloorIndex}");
        }
    }
}
