using UnityEngine;

namespace DarkSpire
{
    [AddComponentMenu("DarkSpire/Tools/Combat Debug Harness")]
    public class CombatDebugHarness : MonoBehaviour
    {
        [Header("On-Screen Buttons")]
        [Tooltip("When true, renders a button panel in the Game view during Play. " +
                 "Uncheck to fall back to the Inspector context menu.")]
        public bool showOnScreenButtons = true;

        [Tooltip("Which corner of the Game view the buttons anchor to.")]
        public Anchor anchor = Anchor.BottomRight;

        [Header("Targeting")]
        [Tooltip("When true, Attack/Skill buttons auto-confirm on the first alive " +
                 "enemy (fast testing — no arrow shown). When false, the arrow " +
                 "appears and you click an enemy in the scene to confirm. " +
                 "Leave OFF to exercise the targeting + range-validation flow.")]
        public bool autoConfirmTargeting = false;

        public enum Anchor { BottomRight, BottomLeft, TopRight, TopLeft }

        // ───────── On-screen GUI ─────────
        private GUIStyle buttonStyle;
        private GUIStyle headerStyle;

        private void OnGUI()
        {
            if (!showOnScreenButtons) return;
            if (!Application.isPlaying) return;

            EnsureStyles();

            const float panelW = 220f;
            const float btnH   = 30f;
            const float pad    = 8f;
            const int   rows   = 11; // header + unit-info line + 8 buttons + spacing
            float panelH = rows * btnH + pad * 2;

            Rect panel = GetAnchorRect(panelW, panelH, pad);

            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(panel.x + pad, panel.y + pad,
                                         panel.width - pad * 2, panel.height - pad * 2));

            GUILayout.Label("Combat Debug", headerStyle);
            DrawActiveUnit();

            GUI.enabled = IsPlayerInputReady();
            if (GUILayout.Button("Attack (enemy 0)", buttonStyle, GUILayout.Height(btnH)))
                Attack0();
            if (GUILayout.Button("Skill 0", buttonStyle, GUILayout.Height(btnH)))
                Skill0();
            if (GUILayout.Button("Skill 1", buttonStyle, GUILayout.Height(btnH)))
                Skill1();
            if (GUILayout.Button("Guard", buttonStyle, GUILayout.Height(btnH)))
                Guard();

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("◀ Move Fwd", buttonStyle, GUILayout.Height(btnH)))
                    MoveForward();
                if (GUILayout.Button("Move Back ▶", buttonStyle, GUILayout.Height(btnH)))
                    MoveBackward();
            }
            if (GUILayout.Button("Pass", buttonStyle, GUILayout.Height(btnH)))
                Pass();
            if (GUILayout.Button("End Turn", buttonStyle, GUILayout.Height(btnH)))
                EndTurn();
            GUI.enabled = true;

            GUILayout.EndArea();
        }

        private void DrawActiveUnit()
        {
            var mgr = CombatManager.Instance;
            if (mgr == null) { GUILayout.Label("No CombatManager"); return; }
            var u = mgr.ActiveUnit;
            if (u == null)
            {
                GUILayout.Label($"Phase: {mgr.CurrentPhase}  (no active unit)");
                return;
            }
            GUILayout.Label($"{u.unitName}  R{u.currentRank}  HP {u.currentHP}/{u.maxHP}  SP {u.currentSP}/{u.maxSP}");
        }

        private Rect GetAnchorRect(float w, float h, float pad)
        {
            float x = anchor switch
            {
                Anchor.BottomRight or Anchor.TopRight => Screen.width - w - pad,
                _ => pad,
            };
            float y = anchor switch
            {
                Anchor.BottomLeft or Anchor.BottomRight => Screen.height - h - pad,
                _ => pad,
            };
            return new Rect(x, y, w, h);
        }

        private void EnsureStyles()
        {
            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 13,
                    fixedHeight = 0,
                };
            }
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                };
            }
        }

        private bool IsPlayerInputReady()
        {
            var mgr = CombatManager.Instance;
            return mgr != null
                && mgr.ActiveUnit != null
                && mgr.ActiveUnit.isPlayerControlled;
        }

        // ───────── Commands (shared by IMGUI + context menu) ─────────

        [ContextMenu("1. Attack enemy 0")]
        public void Attack0() => TargetingPick(
            () => CombatManager.Instance?.OnPlayerChooseAttack(null), 0);

        [ContextMenu("2. Use Skill 0 (first equipped)")]
        public void Skill0() => TargetingPick(
            () => CombatManager.Instance?.OnPlayerChooseSkill(0, null), 0);

        [ContextMenu("3. Use Skill 1 (second equipped)")]
        public void Skill1() => TargetingPick(
            () => CombatManager.Instance?.OnPlayerChooseSkill(1, null), 0);

        [ContextMenu("4. Guard")]
        public void Guard() => CombatManager.Instance?.OnPlayerChooseGuard();

        [ContextMenu("5. Move Forward (advance)")]
        public void MoveForward() => CombatManager.Instance?.OnPlayerChooseMove(-1);

        [ContextMenu("6. Move Backward (withdraw)")]
        public void MoveBackward() => CombatManager.Instance?.OnPlayerChooseMove(+1);

        [ContextMenu("7. Pass")]
        public void Pass() => CombatManager.Instance?.OnPlayerChoosePass();

        [ContextMenu("8. End Turn")]
        public void EndTurn() => CombatManager.Instance?.OnPlayerEndTurn();

        private void TargetingPick(System.Action beginAction, int enemyIndex)
        {
            if (CombatManager.Instance == null)
            {
                Debug.LogWarning("[DebugHarness] No CombatManager.Instance — is Play mode active?");
                return;
            }

            beginAction?.Invoke();

            if (!autoConfirmTargeting) return;

            var ts = TargetingSystem.Instance;
            if (ts != null && ts.IsTargeting)
            {
                var enemies = CombatManager.Instance.GetAliveEnemies();
                if (enemyIndex >= 0 && enemyIndex < enemies.Count)
                    ts.SelectTarget(enemies[enemyIndex]);
                else
                    Debug.LogWarning($"[DebugHarness] No alive enemy at index {enemyIndex}.");
            }
        }
    }
}
