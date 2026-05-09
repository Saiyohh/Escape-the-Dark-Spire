// EnemyDataEditor.cs
// -----------------------------------------------------------------------------
// Custom inspector for EnemyData. Mirrors the section/badge style used by
// SkillDataEditor / CharacterDataEditor:
//
//   [Top summary]  — stat badges (HP/ATK/DEF/SPD/WIL) + move count
//   [Identity]     — name, combat sprite
//   [Display]      — per-enemy display prefab (note: anchor lives on prefab)
//   [Base Stats]   — HP/POW/ATK/DEF/SPD/WIL in a two-column grid
//   [Move Pattern] — list of EnemyMoves, each foldout-boxed with reorder /
//                    delete controls. Inside a move: name + weight + a list of
//                    EnemyIntents. Inside an intent: intentType + a full
//                    SkillEffectData[] panel rendered by EffectsListDrawer
//                    (the same drawer SkillDataEditor uses, so designers
//                    author enemy intents at full skill-effect fidelity).
//   [Turn 1]       — optional move override that forces the first turn.
//   [Meta]         — XP value (foldout).
//
// Each intent is one icon shown over the enemy's head — the intentType picks
// the icon (Attack / Buff / Debuff / Guard / etc.); the behavior comes from
// the effects array. There is no per-intent name field; the move's name is
// the canonical label (used by intent tooltip and combat log).
// -----------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    [CustomEditor(typeof(EnemyData))]
    public class EnemyDataEditor : Editor
    {
        // Identity
        private SerializedProperty enemyNameProp, combatSpriteProp, enemyTypeProp, mapIconProp;

        // Display
        private SerializedProperty displayPrefabProp;

        // Stats
        private SerializedProperty minHPProp, maxHPProp, powProp, atkProp, defProp, spdProp, wilProp;

        // Sprite Black & White filter
        private SerializedProperty bwGrayscaleProp;
        private SerializedProperty bwRedsProp, bwYellowsProp, bwGreensProp, bwCyansProp, bwBluesProp, bwMagentasProp;

        // AI behavior
        private SerializedProperty movePatternModeProp;
        private SerializedProperty movePatternProp;
        private SerializedProperty conditionalMovesProp;
        private SerializedProperty startingConditionsProp;

        // Optional
        private SerializedProperty hasTurn1MoveOverrideProp, turn1MoveOverrideProp;
        private SerializedProperty expValueProp;

        private bool turn1Foldout = false;
        private bool metaFoldout = false;

        // Section header colors
        private static readonly Color IdentityHeader = new(0.78f, 0.45f, 0.40f); // muted red
        private static readonly Color EliteBadge     = new(0.72f, 0.74f, 0.78f); // silver
        private static readonly Color BossBadge      = new(0.78f, 0.25f, 0.30f); // crimson
        private static readonly Color DisplayHeader  = new(0.45f, 0.55f, 0.65f); // steel
        private static readonly Color StatHeader     = new(0.85f, 0.85f, 0.85f); // light grey
        private static readonly Color MovesHeader    = new(0.40f, 0.60f, 0.45f); // green
        private static readonly Color Turn1Header    = new(0.95f, 0.62f, 0.26f); // orange
        private static readonly Color MetaHeader     = new(0.55f, 0.55f, 0.75f); // muted purple

        // Stat badge colors — same vocabulary as CharacterDataEditor.
        private static readonly Color BadgeHP  = new(0.82f, 0.32f, 0.30f);
        private static readonly Color BadgePOW = new(0.85f, 0.50f, 0.25f);
        private static readonly Color BadgeATK = new(0.85f, 0.40f, 0.25f);
        private static readonly Color BadgeDEF = new(0.45f, 0.55f, 0.80f);
        private static readonly Color BadgeSPD = new(0.40f, 0.72f, 0.50f);
        private static readonly Color BadgeWIL = new(0.62f, 0.41f, 0.78f);
        private static readonly Color BadgeXP  = new(0.95f, 0.80f, 0.30f);

        // Intent-type tints — small pill shown next to each intent's header.
        private static Color IntentTint(EnemyIntentType t) => t switch
        {
            EnemyIntentType.Attack  => new Color(0.82f, 0.32f, 0.30f),
            EnemyIntentType.Guard   => new Color(0.45f, 0.55f, 0.80f),
            EnemyIntentType.Buff    => new Color(0.40f, 0.78f, 0.50f),
            EnemyIntentType.Debuff  => new Color(0.62f, 0.41f, 0.78f),
            EnemyIntentType.Skill   => new Color(0.55f, 0.55f, 0.75f),
            EnemyIntentType.Stunned => new Color(0.50f, 0.50f, 0.50f),
            _                       => new Color(0.55f, 0.55f, 0.55f),
        };

        private void OnEnable()
        {
            enemyNameProp            = serializedObject.FindProperty("enemyName");
            combatSpriteProp         = serializedObject.FindProperty("combatSprite");
            enemyTypeProp            = serializedObject.FindProperty("enemyType");
            mapIconProp              = serializedObject.FindProperty("mapIcon");
            displayPrefabProp        = serializedObject.FindProperty("displayPrefab");

            minHPProp = serializedObject.FindProperty("minHP");
            maxHPProp = serializedObject.FindProperty("maxHP");
            powProp   = serializedObject.FindProperty("pow");
            atkProp   = serializedObject.FindProperty("atk");
            defProp   = serializedObject.FindProperty("def");
            spdProp   = serializedObject.FindProperty("spd");
            wilProp   = serializedObject.FindProperty("wil");

            bwGrayscaleProp = serializedObject.FindProperty("bwGrayscale");
            bwRedsProp      = serializedObject.FindProperty("bwReds");
            bwYellowsProp   = serializedObject.FindProperty("bwYellows");
            bwGreensProp    = serializedObject.FindProperty("bwGreens");
            bwCyansProp     = serializedObject.FindProperty("bwCyans");
            bwBluesProp     = serializedObject.FindProperty("bwBlues");
            bwMagentasProp  = serializedObject.FindProperty("bwMagentas");

            movePatternModeProp      = serializedObject.FindProperty("movePatternMode");
            movePatternProp          = serializedObject.FindProperty("movePattern");
            conditionalMovesProp     = serializedObject.FindProperty("conditionalMoves");
            startingConditionsProp   = serializedObject.FindProperty("startingConditions");
            hasTurn1MoveOverrideProp = serializedObject.FindProperty("hasTurn1MoveOverride");
            turn1MoveOverrideProp    = serializedObject.FindProperty("turn1MoveOverride");
            expValueProp             = serializedObject.FindProperty("expValue");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawTopSummary();
            DrawIdentitySection();
            DrawDisplaySection();
            DrawStatsSection();
            DrawSpriteFilterSection();
            DrawStartingConditionsSection();
            DrawMovePatternSection();
            DrawConditionalMovesSection();
            DrawTurn1Section();
            DrawMetaSection();

            serializedObject.ApplyModifiedProperties();
        }

        // ── Top summary ─────────────────────────────────────────────────────
        private void DrawTopSummary()
        {
            var e = (EnemyData)target;
            EditorGUILayout.Space(4);

            // Show "HP min–max" when the range is real, otherwise the single value.
            string hpBadge = (e.minHP > 0 && e.minHP < e.maxHP)
                ? $"HP {e.minHP}–{e.maxHP}"
                : $"HP {e.maxHP}";

            EditorStyleKit.BeginBadgeRow();
            if (e.enemyType == EnemyType.Elite)
                EditorStyleKit.DrawBadge("ELITE", EliteBadge);
            else if (e.enemyType == EnemyType.Boss)
                EditorStyleKit.DrawBadge("BOSS", BossBadge);
            EditorStyleKit.DrawBadge(hpBadge,           BadgeHP);
            EditorStyleKit.DrawBadge($"POW {e.pow}",    BadgePOW);
            EditorStyleKit.DrawBadge($"ATK {e.atk}",    BadgeATK);
            EditorStyleKit.DrawBadge($"DEF {e.def}",    BadgeDEF);
            EditorStyleKit.DrawBadge($"SPD {e.spd}",    BadgeSPD);
            EditorStyleKit.DrawBadge($"WIL {e.wil}",    BadgeWIL);
            if (e.bwGrayscale)
                EditorStyleKit.DrawBadge("B&W", new Color(0.40f, 0.40f, 0.40f));
            EditorStyleKit.EndBadgeRow();

            // Move-summary row: count of moves + total intents, and XP.
            int moveCount = e.movePattern != null ? e.movePattern.Length : 0;
            int intentCount = 0;
            if (e.movePattern != null)
                for (int i = 0; i < e.movePattern.Length; i++)
                    if (e.movePattern[i] != null && e.movePattern[i].intents != null)
                        intentCount += e.movePattern[i].intents.Length;

            EditorStyleKit.BeginBadgeRow();
            EditorStyleKit.DrawBadge($"{moveCount} move{(moveCount == 1 ? "" : "s")}",  MovesHeader);
            EditorStyleKit.DrawBadge($"{intentCount} intent{(intentCount == 1 ? "" : "s")}", MovesHeader);
            if (e.expValue > 0)
                EditorStyleKit.DrawBadge($"XP {e.expValue}", BadgeXP);
            if (e.hasTurn1MoveOverride)
                EditorStyleKit.DrawBadge("Turn 1 override", Turn1Header);
            EditorStyleKit.EndBadgeRow();
        }

        // ── Identity ────────────────────────────────────────────────────────
        private void DrawIdentitySection()
        {
            EditorStyleKit.BeginBoxSection("Identity", IdentityHeader);
            EditorGUILayout.PropertyField(enemyNameProp, new GUIContent("Name"));
            EditorGUILayout.PropertyField(combatSpriteProp, new GUIContent("Combat Sprite"));
            EditorGUILayout.PropertyField(enemyTypeProp, new GUIContent("Type",
                "Encounter tier. Elite = silver badge, Boss = red badge, Normal = no badge."));

            // mapIcon is a Boss-only override — surface it inline so the
            // dependency on Type is obvious; render it disabled (with a hint)
            // for Normal / Elite so designers see the field exists but know
            // why it can't be set.
            var enemyType = (EnemyType)enemyTypeProp.enumValueIndex;
            if (enemyType == EnemyType.Boss)
            {
                EditorGUILayout.PropertyField(mapIconProp, new GUIContent("Map Icon",
                    "Per-boss override sprite for the dungeon map. When null, the " +
                    "map uses MapEntitySpriteLibrary.boss instead."));
                EditorGUILayout.LabelField(
                    "Boss overrides the library icon when this is set; leave null " +
                    "to fall back to the shared boss sprite.",
                    EditorStyles.miniLabel);
            }
            else
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(mapIconProp, new GUIContent("Map Icon",
                        "Only Boss-type enemies use a custom map icon. Normal and " +
                        "Elite always pull from MapEntitySpriteLibrary."));
            }

            EditorStyleKit.EndBoxSection();
        }

        // ── Display ─────────────────────────────────────────────────────────
        private void DrawDisplaySection()
        {
            EditorStyleKit.BeginBoxSection("Display", DisplayHeader);
            EditorGUILayout.PropertyField(displayPrefabProp, new GUIContent("Display Prefab"));
            EditorGUILayout.LabelField(
                "Optional. If null, CombatManager spawns the default UnitDisplay. Per-enemy " +
                "intent / hitbox positioning is authored on the prefab itself " +
                "(enemyIntentAnchorOffset, hitboxOffset / hitboxSize on UnitDisplay), not here.",
                EditorStyles.miniLabel);
            EditorStyleKit.EndBoxSection();
        }

        // ── Base stats ──────────────────────────────────────────────────────
        private void DrawStatsSection()
        {
            EditorStyleKit.BeginBoxSection("Base Stats", StatHeader);

            // HP gets its own row (min..max range). Combat-start HP rolls
            // Random.Range(min, max+1) when min < max; otherwise it's just max.
            DrawHPRangeRow(minHPProp, maxHPProp);
            DrawStatPairRow(powProp, "POW", BadgePOW, atkProp, "ATK", BadgeATK);
            DrawStatPairRow(defProp, "DEF", BadgeDEF, spdProp, "SPD", BadgeSPD);
            DrawSingleStatRow(wilProp, "WIL", BadgeWIL);

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField(
                "Enemies use ATK directly (no weapon) and SPD for initiative — unlike " +
                "players who derive both from DEX. POW adds to attack-effect damage. " +
                "DEF is the to-hit target; WIL gates affliction saves. Set HP min < max " +
                "to give the encounter run-to-run variance; set min = 0 to disable the roll.",
                EditorStyles.miniLabel);
            EditorStyleKit.EndBoxSection();
        }

        /// <summary>One stat field on its own row, half-width — used for WIL
        /// when there's no paired stat alongside it.</summary>
        private static void DrawSingleStatRow(SerializedProperty prop, string label, Color accent)
        {
            float totalWidth = EditorGUIUtility.currentViewWidth - 32f;
            float colWidth = Mathf.Max(120f, (totalWidth - 6f) * 0.5f);
            EditorGUILayout.BeginHorizontal();
            DrawStatField(prop, label, accent, colWidth);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawHPRangeRow(SerializedProperty minProp, SerializedProperty maxProp)
        {
            EditorGUILayout.BeginHorizontal();
            var bar = GUILayoutUtility.GetRect(3, EditorGUIUtility.singleLineHeight,
                GUILayout.Width(3), GUILayout.ExpandHeight(false));
            EditorGUI.DrawRect(bar, BadgeHP);
            GUILayout.Space(4);

            float prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 38f;

            GUILayout.Label("HP", GUILayout.Width(38));
            GUILayout.Label("min", EditorStyles.miniLabel, GUILayout.Width(30));
            minProp.intValue = Mathf.Max(0, EditorGUILayout.IntField(minProp.intValue, GUILayout.Width(60)));
            GUILayout.Label("→", GUILayout.Width(14));
            GUILayout.Label("max", EditorStyles.miniLabel, GUILayout.Width(30));
            maxProp.intValue = Mathf.Max(1, EditorGUILayout.IntField(maxProp.intValue, GUILayout.Width(60)));

            EditorGUIUtility.labelWidth = prevLabelWidth;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawStatPairRow(
            SerializedProperty aProp, string aLabel, Color aColor,
            SerializedProperty bProp, string bLabel, Color bColor)
        {
            const float Gap = 6f;
            float totalWidth = EditorGUIUtility.currentViewWidth - 32f;
            float colWidth = Mathf.Max(120f, (totalWidth - Gap) * 0.5f);

            EditorGUILayout.BeginHorizontal();
            DrawStatField(aProp, aLabel, aColor, colWidth);
            GUILayout.Space(Gap);
            DrawStatField(bProp, bLabel, bColor, colWidth);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawStatField(SerializedProperty prop, string label, Color accent, float width)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Width(width));
            var bar = GUILayoutUtility.GetRect(3, EditorGUIUtility.singleLineHeight,
                GUILayout.Width(3), GUILayout.ExpandHeight(false));
            EditorGUI.DrawRect(bar, accent);
            GUILayout.Space(4);

            float prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 38f;
            EditorGUILayout.PropertyField(prop, new GUIContent(label));
            EditorGUIUtility.labelWidth = prevLabelWidth;

            EditorGUILayout.EndHorizontal();
        }

        // ── Sprite Black & White filter ─────────────────────────────────────
        private static readonly Color BWHeader = new(0.40f, 0.40f, 0.40f);

        private void DrawSpriteFilterSection()
        {
            EditorStyleKit.BeginBoxSection("Sprite Filter — Black & White", BWHeader);

            EditorGUILayout.PropertyField(bwGrayscaleProp,
                new GUIContent("Black & White",
                    "Render the combat sprite through the per-hue grayscale adjust. " +
                    "Designer wires the source material on UnitDisplay.blackAndWhiteMaterial; " +
                    "the runtime clones it per-enemy and pushes the channel weights below."));

            // Live preview — the actual combat sprite with the current weights
            // applied. Mirrors SkillDataEditor's banner preview so designers can
            // tune sliders and see the result without entering Play mode.
            DrawBWSpritePreview();

            if (bwGrayscaleProp.boolValue)
            {
                EditorGUI.indentLevel++;
                DrawBWSlider(bwRedsProp,     "Reds");
                DrawBWSlider(bwYellowsProp,  "Yellows");
                DrawBWSlider(bwGreensProp,   "Greens");
                DrawBWSlider(bwCyansProp,    "Cyans");
                DrawBWSlider(bwBluesProp,    "Blues");
                DrawBWSlider(bwMagentasProp, "Magentas");
                EditorGUI.indentLevel--;

                EditorGUILayout.LabelField(
                    "Channel weights match Photoshop's Black & White adjustment. Push a hue " +
                    "above 1.00 (100%) to brighten that family in the grayscale output, drop " +
                    "to 0 to darken it. Preview is live — the sprite renderer uses the same " +
                    "math at runtime via the cloned material on UnitDisplay.",
                    EditorStyles.miniLabel);
            }
            EditorStyleKit.EndBoxSection();
        }

        private static void DrawBWSlider(SerializedProperty prop, string label)
        {
            if (prop == null) return;
            EditorGUILayout.Slider(prop, 0f, 3f, new GUIContent(label));
        }

        // ── BW sprite preview ───────────────────────────────────────────────

        private static readonly int PropWR_Preview = Shader.PropertyToID("_WR");
        private static readonly int PropWY_Preview = Shader.PropertyToID("_WY");
        private static readonly int PropWG_Preview = Shader.PropertyToID("_WG");
        private static readonly int PropWC_Preview = Shader.PropertyToID("_WC");
        private static readonly int PropWB_Preview = Shader.PropertyToID("_WB");
        private static readonly int PropWM_Preview = Shader.PropertyToID("_WM");

        private static Material _bwPreviewMaterial;

        /// <summary>
        /// Lazy-cached B&amp;W preview material. Tries the sprite-variant shader
        /// first (matches the runtime path) and falls back to the UI variant
        /// when the project hasn't imported the sprite shader yet. Returns
        /// null when neither shader is found — preview falls back to the
        /// raw colored sprite.
        /// </summary>
        private static Material GetBlackAndWhitePreviewMaterial()
        {
            if (_bwPreviewMaterial != null) return _bwPreviewMaterial;

            var shader = Shader.Find("DarkSpire/Sprite/BlackAndWhite");
            if (shader == null) shader = Shader.Find("DarkSpire/UI/BlackAndWhite");
            if (shader == null) return null;

            _bwPreviewMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            return _bwPreviewMaterial;
        }

        private void DrawBWSpritePreview()
        {
            var enemy = (EnemyData)target;
            var sprite = enemy != null ? enemy.combatSprite : null;
            if (sprite == null || sprite.texture == null)
            {
                EditorGUILayout.HelpBox(
                    "No combatSprite assigned — preview hidden. Wire the sprite in the " +
                    "Identity section above to see the filter result.",
                    MessageType.None);
                return;
            }

            EditorGUILayout.Space(4);

            // Allocate a fixed-size preview box; aspect-fit the sprite inside.
            const float previewBox = 200f;
            var boxRect = GUILayoutUtility.GetRect(
                previewBox, previewBox,
                GUILayout.Width(previewBox), GUILayout.Height(previewBox));

            // Background + 1px border, so transparent sprites read against
            // the inspector's variable theme color.
            EditorGUI.DrawRect(boxRect, new Color(0.10f, 0.10f, 0.10f, 1f));
            var border = new Color(0.50f, 0.50f, 0.50f, 0.7f);
            EditorGUI.DrawRect(new Rect(boxRect.x, boxRect.y, boxRect.width, 1), border);
            EditorGUI.DrawRect(new Rect(boxRect.x, boxRect.y + boxRect.height - 1, boxRect.width, 1), border);
            EditorGUI.DrawRect(new Rect(boxRect.x, boxRect.y, 1, boxRect.height), border);
            EditorGUI.DrawRect(new Rect(boxRect.x + boxRect.width - 1, boxRect.y, 1, boxRect.height), border);

            // Compute texture-space UVs (handles atlased sprites correctly).
            var tex = sprite.texture;
            var spriteRect = sprite.rect;
            float texX = spriteRect.x / tex.width;
            float texY = spriteRect.y / tex.height;
            float texW = spriteRect.width / tex.width;
            float texH = spriteRect.height / tex.height;
            var texCoords = new Rect(texX, texY, texW, texH);

            // Aspect-fit the sprite inside the preview box (letterbox / pillarbox).
            float spriteAspect = spriteRect.width / spriteRect.height;
            float drawW = previewBox;
            float drawH = previewBox;
            if (spriteAspect > 1f) drawH = previewBox / spriteAspect;
            else drawW = previewBox * spriteAspect;
            var fitRect = new Rect(
                boxRect.x + (previewBox - drawW) * 0.5f,
                boxRect.y + (previewBox - drawH) * 0.5f,
                drawW, drawH);

            // BW path: push the live slider values into the cached material
            // every repaint so dragging a slider updates the preview live.
            bool wantBW = bwGrayscaleProp != null && bwGrayscaleProp.boolValue;
            var bwMat = wantBW ? GetBlackAndWhitePreviewMaterial() : null;

            if (wantBW && bwMat != null)
            {
                bwMat.SetFloat(PropWR_Preview, bwRedsProp     != null ? bwRedsProp.floatValue     : 0.40f);
                bwMat.SetFloat(PropWY_Preview, bwYellowsProp  != null ? bwYellowsProp.floatValue  : 0.60f);
                bwMat.SetFloat(PropWG_Preview, bwGreensProp   != null ? bwGreensProp.floatValue   : 0.40f);
                bwMat.SetFloat(PropWC_Preview, bwCyansProp    != null ? bwCyansProp.floatValue    : 0.60f);
                bwMat.SetFloat(PropWB_Preview, bwBluesProp    != null ? bwBluesProp.floatValue    : 0.20f);
                bwMat.SetFloat(PropWM_Preview, bwMagentasProp != null ? bwMagentasProp.floatValue : 0.80f);
            }

            // Graphics.DrawTexture only runs during Repaint events. Outside
            // Repaint (Layout pass, etc.) we draw the colored sprite as a
            // placeholder so the layout stays stable.
            if (wantBW && bwMat != null && Event.current.type == EventType.Repaint)
            {
                Graphics.DrawTexture(
                    fitRect, tex, texCoords,
                    leftBorder: 0, rightBorder: 0, topBorder: 0, bottomBorder: 0,
                    color: Color.white, mat: bwMat);
            }
            else
            {
                GUI.DrawTextureWithTexCoords(fitRect, tex, texCoords, alphaBlend: true);
            }
        }

        // ── Starting Conditions ─────────────────────────────────────────────
        private static readonly Color StartingCondHeader = new(0.55f, 0.55f, 0.78f);

        private void DrawStartingConditionsSection()
        {
            EditorStyleKit.DrawColoredSectionHeader("Starting Conditions", StartingCondHeader);
            EditorGUILayout.LabelField(
                "Conditions automatically applied to this enemy at combat start, " +
                "before any OnCombatStart triggers fire. Use to seed signature " +
                "passives — e.g. Byrdonis spawns with Territorial 1.",
                EditorStyles.miniLabel);

            for (int i = 0; i < startingConditionsProp.arraySize; i++)
            {
                var entry = startingConditionsProp.GetArrayElementAtIndex(i);
                if (DrawStartingConditionRow(startingConditionsProp, entry, i)) return;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("＋ Add Starting Condition", GUILayout.Height(20)))
            {
                startingConditionsProp.InsertArrayElementAtIndex(startingConditionsProp.arraySize);
                var fresh = startingConditionsProp.GetArrayElementAtIndex(startingConditionsProp.arraySize - 1);
                var stacksProp = fresh.FindPropertyRelative("stacks");
                if (stacksProp != null) stacksProp.intValue = 1;
            }
            if (startingConditionsProp.arraySize > 0
                && GUILayout.Button("Clear All", GUILayout.Height(20), GUILayout.Width(80)))
            {
                startingConditionsProp.ClearArray();
            }
            EditorGUILayout.EndHorizontal();
        }

        private static bool DrawStartingConditionRow(
            SerializedProperty arrayProp, SerializedProperty entry, int index)
        {
            var idProp = entry.FindPropertyRelative("conditionId");
            var stacksProp = entry.FindPropertyRelative("stacks");
            if (idProp == null || stacksProp == null) return false;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"#{index + 1}", GUILayout.Width(28));
            EditorStyleKit.DrawSortedEnumPopup<ConditionID>(idProp, "");
            GUILayout.Label("×", GUILayout.Width(14));
            stacksProp.intValue = Mathf.Max(1, EditorGUILayout.IntField(stacksProp.intValue, GUILayout.Width(50)));
            if (GUILayout.Button("✕", GUILayout.Width(24)))
            {
                arrayProp.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndHorizontal();
                return true;
            }
            EditorGUILayout.EndHorizontal();
            return false;
        }

        // ── Move Pattern ────────────────────────────────────────────────────
        private void DrawMovePatternSection()
        {
            EditorStyleKit.DrawColoredSectionHeader("Move Pattern", MovesHeader);

            EditorGUILayout.PropertyField(movePatternModeProp,
                new GUIContent("Pattern Mode",
                    "WeightedRandom: rolls each turn against per-move weights. " +
                    "Cycle: iterates moves in order (#1 → #2 → #3 → repeat); " +
                    "weights are ignored."));

            var mode = (EnemyMovePatternMode)movePatternModeProp.enumValueIndex;
            string modeBlurb = mode == EnemyMovePatternMode.Cycle
                ? "Cycle mode: each move plays in order and the pattern loops back at " +
                  "the end. The weight field on each move is ignored — leave it at 1 " +
                  "or use Conditional Moves below for interrupts."
                : "Weighted random: each turn rolls against the weights below. Higher " +
                  "weight = more frequent. A weight of 0 makes a move unpickable.";
            EditorGUILayout.LabelField(modeBlurb, EditorStyles.miniLabel);

            bool isCycle = mode == EnemyMovePatternMode.Cycle;
            for (int i = 0; i < movePatternProp.arraySize; i++)
            {
                if (DrawMove(movePatternProp, movePatternProp.GetArrayElementAtIndex(i), i, isCycle))
                    return; // array mutated (move/delete) — bail out and let the next OnGUI rebuild.
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("＋ Add Move", GUILayout.Height(22)))
            {
                movePatternProp.InsertArrayElementAtIndex(movePatternProp.arraySize);
                InitNewMove(movePatternProp.GetArrayElementAtIndex(movePatternProp.arraySize - 1));
            }
            if (movePatternProp.arraySize > 0
                && GUILayout.Button("Clear All", GUILayout.Height(22), GUILayout.Width(80)))
            {
                movePatternProp.ClearArray();
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// One move foldout. Returns true if the array was mutated (so the
        /// caller bails out and lets the next OnGUI re-enter cleanly).
        /// </summary>
        private static bool DrawMove(SerializedProperty arrayProp, SerializedProperty moveProp,
                                      int index, bool isCycle)
        {
            var nameProp    = moveProp.FindPropertyRelative("name");
            var weightProp  = moveProp.FindPropertyRelative("weight");
            var intentsProp = moveProp.FindPropertyRelative("intents");

            string label = string.IsNullOrEmpty(nameProp.stringValue)
                ? $"Move #{index + 1}"
                : nameProp.stringValue;
            int intentCount = intentsProp != null ? intentsProp.arraySize : 0;

            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = Color.Lerp(Color.white, MovesHeaderTint, 0.25f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUI.backgroundColor = prev;

                const float RowHeight = 18f;
                EditorGUILayout.BeginHorizontal(GUILayout.Height(RowHeight));

                moveProp.isExpanded = EditorGUILayout.Foldout(
                    moveProp.isExpanded, $"#{index + 1}  {label}", toggleOnLabelClick: true);

                GUILayout.FlexibleSpace();

                // Cycle mode shows the step position ("Step 1 of N") in place
                // of the weight chip, since weights are ignored.
                if (isCycle)
                {
                    EditorStyleKit.DrawBadge(
                        $"step {index + 1}/{arrayProp.arraySize}",
                        new Color(0.55f, 0.55f, 0.55f));
                }
                else
                {
                    EditorStyleKit.DrawBadge($"×{Mathf.Max(0, weightProp.intValue)}", MovesHeaderTint);
                }
                EditorStyleKit.DrawBadge($"{intentCount} intent{(intentCount == 1 ? "" : "s")}",
                                         new Color(0.55f, 0.55f, 0.55f));

                GUILayout.Space(4);
                if (GUILayout.Button("▲", GUILayout.Width(24), GUILayout.Height(RowHeight)) && index > 0)
                { arrayProp.MoveArrayElement(index, index - 1); EditorGUILayout.EndHorizontal(); return true; }
                if (GUILayout.Button("▼", GUILayout.Width(24), GUILayout.Height(RowHeight)) && index < arrayProp.arraySize - 1)
                { arrayProp.MoveArrayElement(index, index + 1); EditorGUILayout.EndHorizontal(); return true; }
                if (GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(RowHeight)))
                { arrayProp.DeleteArrayElementAtIndex(index); EditorGUILayout.EndHorizontal(); return true; }

                EditorGUILayout.EndHorizontal();

                if (!moveProp.isExpanded) return false;

                EditorGUILayout.PropertyField(nameProp);
                using (new EditorGUI.DisabledScope(isCycle))
                    EditorGUILayout.PropertyField(weightProp,
                        new GUIContent("Weight",
                            isCycle ? "Ignored in Cycle mode — weights only apply to WeightedRandom."
                                    : "Relative weight for weighted-random selection. " +
                                      "0 makes the move unpickable."));

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Intents", EditorStyles.miniBoldLabel);
                if (DrawIntents(intentsProp)) return true;
            }
            return false;
        }

        private static readonly Color MovesHeaderTint = new(0.40f, 0.60f, 0.45f);

        /// <summary>
        /// Draws every intent inside a move (each in its own boxed foldout)
        /// plus the "+ Add Intent" / "Clear All" controls. Returns true if
        /// the intents array was mutated (caller should bail out).
        /// </summary>
        private static bool DrawIntents(SerializedProperty intentsProp)
        {
            if (intentsProp == null) return false;

            for (int i = 0; i < intentsProp.arraySize; i++)
            {
                if (DrawIntent(intentsProp, intentsProp.GetArrayElementAtIndex(i), i))
                    return true;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("＋ Add Intent", GUILayout.Height(20)))
            {
                intentsProp.InsertArrayElementAtIndex(intentsProp.arraySize);
                InitNewIntent(intentsProp.GetArrayElementAtIndex(intentsProp.arraySize - 1));
            }
            if (intentsProp.arraySize > 0
                && GUILayout.Button("Clear All", GUILayout.Height(20), GUILayout.Width(80)))
            {
                intentsProp.ClearArray();
            }
            EditorGUILayout.EndHorizontal();
            return false;
        }

        private static bool DrawIntent(SerializedProperty arrayProp, SerializedProperty intentProp, int index)
        {
            var typeProp    = intentProp.FindPropertyRelative("intentType");
            var effectsProp = intentProp.FindPropertyRelative("effects");

            var type = (EnemyIntentType)typeProp.enumValueIndex;
            var tint = IntentTint(type);

            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = Color.Lerp(Color.white, tint, 0.30f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUI.backgroundColor = prev;

                const float RowHeight = 18f;
                EditorGUILayout.BeginHorizontal(GUILayout.Height(RowHeight));

                intentProp.isExpanded = EditorGUILayout.Foldout(
                    intentProp.isExpanded, $"Intent #{index + 1}", toggleOnLabelClick: true);

                GUILayout.FlexibleSpace();

                EditorStyleKit.DrawBadge(type.ToString(), tint);
                int effCount = effectsProp != null ? effectsProp.arraySize : 0;
                EditorStyleKit.DrawBadge($"{effCount} effect{(effCount == 1 ? "" : "s")}",
                                         new Color(0.55f, 0.55f, 0.55f));

                GUILayout.Space(4);
                if (GUILayout.Button("▲", GUILayout.Width(24), GUILayout.Height(RowHeight)) && index > 0)
                { arrayProp.MoveArrayElement(index, index - 1); EditorGUILayout.EndHorizontal(); return true; }
                if (GUILayout.Button("▼", GUILayout.Width(24), GUILayout.Height(RowHeight)) && index < arrayProp.arraySize - 1)
                { arrayProp.MoveArrayElement(index, index + 1); EditorGUILayout.EndHorizontal(); return true; }
                if (GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(RowHeight)))
                { arrayProp.DeleteArrayElementAtIndex(index); EditorGUILayout.EndHorizontal(); return true; }

                EditorGUILayout.EndHorizontal();

                if (!intentProp.isExpanded) return false;

                EditorGUILayout.PropertyField(typeProp, new GUIContent("Intent Type",
                    "Picks the icon shown over the enemy's head for this intent."));

                // Range — paired min/max so designers see the window inline.
                var rangeMinProp = intentProp.FindPropertyRelative("rangeMin");
                var rangeMaxProp = intentProp.FindPropertyRelative("rangeMax");
                if (rangeMinProp != null && rangeMaxProp != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Range", GUILayout.Width(EditorGUIUtility.labelWidth - 2f));
                    GUILayout.Label("min", EditorStyles.miniLabel, GUILayout.Width(28));
                    rangeMinProp.intValue = Mathf.Max(1, EditorGUILayout.IntField(rangeMinProp.intValue, GUILayout.Width(50)));
                    GUILayout.Label("→", GUILayout.Width(14));
                    GUILayout.Label("max", EditorStyles.miniLabel, GUILayout.Width(28));
                    rangeMaxProp.intValue = Mathf.Max(rangeMinProp.intValue, EditorGUILayout.IntField(rangeMaxProp.intValue, GUILayout.Width(50)));
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }

                // Targeting preference — only meaningful for offensive intents
                // (Attack / Debuff). Self-buffing intents ignore it; we still
                // expose the field so designers can author it consistently.
                var prefProp = intentProp.FindPropertyRelative("targetPreference");
                if (prefProp != null)
                {
                    EditorGUILayout.PropertyField(prefProp, new GUIContent("Prefers",
                        "How this intent picks among players in range. Random = uniform; " +
                        "the others express AI personality (LowestHP for finishers, " +
                        "LowestWIL for afflicters, HasCondition for opportunists)."));

                    var pref = (EnemyTargetPreference)prefProp.enumValueIndex;
                    if (pref == EnemyTargetPreference.HasCondition
                     || pref == EnemyTargetPreference.LacksCondition)
                    {
                        var condProp = intentProp.FindPropertyRelative("preferredCondition");
                        if (condProp != null)
                            EditorStyleKit.DrawSortedEnumPopup<ConditionID>(condProp, "Condition");
                    }
                }

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Effects", EditorStyles.miniBoldLabel);
                EffectsListDrawer.Draw(effectsProp);
            }
            return false;
        }

        // ── Conditional Moves ───────────────────────────────────────────────
        private static readonly Color CondMovesHeader = new(0.78f, 0.55f, 0.30f);

        private void DrawConditionalMovesSection()
        {
            EditorStyleKit.DrawColoredSectionHeader("Conditional Moves (overrides)", CondMovesHeader);
            EditorGUILayout.LabelField(
                "Reactive overrides that interrupt the move pattern when their trigger " +
                "fires. Listed in priority order — first match wins. Common uses: boss " +
                "phase shifts (HPBelowPercent), counter-moves on shield-strip " +
                "(OnConditionRemoved=Shields), scripted opening turns (OnTurnNumber).",
                EditorStyles.miniLabel);

            for (int i = 0; i < conditionalMovesProp.arraySize; i++)
            {
                if (DrawConditionalMove(conditionalMovesProp,
                                         conditionalMovesProp.GetArrayElementAtIndex(i), i))
                    return;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("＋ Add Conditional Move", GUILayout.Height(22)))
            {
                conditionalMovesProp.InsertArrayElementAtIndex(conditionalMovesProp.arraySize);
                InitNewConditionalMove(conditionalMovesProp.GetArrayElementAtIndex(
                    conditionalMovesProp.arraySize - 1));
            }
            if (conditionalMovesProp.arraySize > 0
                && GUILayout.Button("Clear All", GUILayout.Height(22), GUILayout.Width(80)))
            {
                conditionalMovesProp.ClearArray();
            }
            EditorGUILayout.EndHorizontal();
        }

        private static bool DrawConditionalMove(
            SerializedProperty arrayProp, SerializedProperty cmProp, int index)
        {
            var labelProp   = cmProp.FindPropertyRelative("label");
            var triggerProp = cmProp.FindPropertyRelative("trigger");
            var percentProp = cmProp.FindPropertyRelative("percent");
            var turnProp    = cmProp.FindPropertyRelative("turnNumber");
            var condIdProp  = cmProp.FindPropertyRelative("conditionId");
            var stacksProp  = cmProp.FindPropertyRelative("stacks");
            var moveProp    = cmProp.FindPropertyRelative("move");
            var onceProp    = cmProp.FindPropertyRelative("oncePerCombat");

            var trigger = (EnemyConditionalTrigger)triggerProp.enumValueIndex;
            string headerLabel = string.IsNullOrEmpty(labelProp.stringValue)
                ? $"Conditional #{index + 1}  ·  {trigger}"
                : $"#{index + 1}  {labelProp.stringValue}  ·  {trigger}";

            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = Color.Lerp(Color.white, CondMovesHeader, 0.25f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUI.backgroundColor = prev;

                const float RowHeight = 18f;
                EditorGUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
                cmProp.isExpanded = EditorGUILayout.Foldout(cmProp.isExpanded,
                    headerLabel, toggleOnLabelClick: true);
                GUILayout.FlexibleSpace();
                if (onceProp.boolValue)
                    EditorStyleKit.DrawBadge("once", new Color(0.55f, 0.55f, 0.55f));

                if (GUILayout.Button("▲", GUILayout.Width(24), GUILayout.Height(RowHeight)) && index > 0)
                { arrayProp.MoveArrayElement(index, index - 1); EditorGUILayout.EndHorizontal(); return true; }
                if (GUILayout.Button("▼", GUILayout.Width(24), GUILayout.Height(RowHeight)) && index < arrayProp.arraySize - 1)
                { arrayProp.MoveArrayElement(index, index + 1); EditorGUILayout.EndHorizontal(); return true; }
                if (GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(RowHeight)))
                { arrayProp.DeleteArrayElementAtIndex(index); EditorGUILayout.EndHorizontal(); return true; }
                EditorGUILayout.EndHorizontal();

                if (!cmProp.isExpanded) return false;

                EditorGUILayout.PropertyField(labelProp, new GUIContent("Label",
                    "Designer-friendly identifier shown in the section header. " +
                    "No runtime meaning."));
                EditorGUILayout.PropertyField(triggerProp, new GUIContent("Trigger"));

                // Trigger-specific parameter UI.
                switch (trigger)
                {
                    case EnemyConditionalTrigger.HPBelowPercent:
                    case EnemyConditionalTrigger.HPAbovePercent:
                        EditorGUILayout.Slider(percentProp, 0f, 1f, new GUIContent("HP %",
                            "Threshold as a fraction of maxHP. 0.5 = 50%."));
                        break;

                    case EnemyConditionalTrigger.OnTurnNumber:
                        EditorGUILayout.PropertyField(turnProp, new GUIContent("Turn #",
                            "Combat turn number this fires on. 1 = first round."));
                        break;

                    case EnemyConditionalTrigger.OnConditionApplied:
                    case EnemyConditionalTrigger.OnConditionRemoved:
                        EditorStyleKit.DrawSortedEnumPopup<ConditionID>(condIdProp, "Condition");
                        break;

                    case EnemyConditionalTrigger.OnConditionAtStacks:
                        EditorStyleKit.DrawSortedEnumPopup<ConditionID>(condIdProp, "Condition");
                        EditorGUILayout.PropertyField(stacksProp, new GUIContent("Min Stacks"));
                        break;
                }

                EditorGUILayout.PropertyField(onceProp, new GUIContent("Once per Combat",
                    "When true, the trigger latches after firing once and won't fire " +
                    "again this combat. When false, fires every turn the condition holds."));

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Override Move", EditorStyles.miniBoldLabel);
                DrawEmbeddedMove(moveProp);
            }
            return false;
        }

        /// <summary>
        /// Render an EnemyMove inline (no array reorder / delete controls) —
        /// used by the conditional-move and turn-1-override sections where the
        /// move lives as a single embedded field.
        /// </summary>
        private static void DrawEmbeddedMove(SerializedProperty moveProp)
        {
            if (moveProp == null) return;
            var nameProp    = moveProp.FindPropertyRelative("name");
            var weightProp  = moveProp.FindPropertyRelative("weight");
            var intentsProp = moveProp.FindPropertyRelative("intents");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(nameProp);
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(weightProp);
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Intents", EditorStyles.miniBoldLabel);
                DrawIntents(intentsProp);
            }
        }

        private static void InitNewConditionalMove(SerializedProperty cmProp)
        {
            var labelProp   = cmProp.FindPropertyRelative("label");
            var triggerProp = cmProp.FindPropertyRelative("trigger");
            var percentProp = cmProp.FindPropertyRelative("percent");
            var turnProp    = cmProp.FindPropertyRelative("turnNumber");
            var stacksProp  = cmProp.FindPropertyRelative("stacks");
            var onceProp    = cmProp.FindPropertyRelative("oncePerCombat");
            var moveProp    = cmProp.FindPropertyRelative("move");

            if (labelProp != null)   labelProp.stringValue   = "Phase Shift";
            if (triggerProp != null) triggerProp.enumValueIndex = (int)EnemyConditionalTrigger.HPBelowPercent;
            if (percentProp != null) percentProp.floatValue  = 0.5f;
            if (turnProp != null)    turnProp.intValue       = 1;
            if (stacksProp != null)  stacksProp.intValue     = 1;
            if (onceProp != null)    onceProp.boolValue      = true;

            if (moveProp != null)
            {
                var moveName = moveProp.FindPropertyRelative("name");
                var moveWeight = moveProp.FindPropertyRelative("weight");
                var moveIntents = moveProp.FindPropertyRelative("intents");
                if (moveName != null)   moveName.stringValue = "Override Move";
                if (moveWeight != null) moveWeight.intValue  = 1;
                if (moveIntents != null) moveIntents.ClearArray();
            }
            cmProp.isExpanded = true;
        }

        // ── Turn 1 override ─────────────────────────────────────────────────
        private void DrawTurn1Section()
        {
            EditorStyleKit.DrawColoredSectionHeader("Turn 1 Override (optional)", Turn1Header);

            EditorGUILayout.PropertyField(hasTurn1MoveOverrideProp,
                new GUIContent("Force Turn 1 Move",
                    "When true, this enemy will use the move below on its first turn " +
                    "instead of rolling the move pattern."));

            if (!hasTurn1MoveOverrideProp.boolValue) return;

            turn1Foldout = EditorGUILayout.Foldout(turn1Foldout, "Turn 1 Move", toggleOnLabelClick: true);
            if (!turn1Foldout) return;

            // Render the override as a single-move panel (no array controls).
            var nameProp    = turn1MoveOverrideProp.FindPropertyRelative("name");
            var weightProp  = turn1MoveOverrideProp.FindPropertyRelative("weight");
            var intentsProp = turn1MoveOverrideProp.FindPropertyRelative("intents");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(nameProp);
                // Weight is irrelevant for the override (only one), but keep
                // the field exposed so a designer can later promote the
                // override into the move pattern by copy-paste.
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(weightProp);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Intents", EditorStyles.miniBoldLabel);
                DrawIntents(intentsProp);
            }
        }

        // ── Meta ────────────────────────────────────────────────────────────
        private void DrawMetaSection()
        {
            metaFoldout = EditorGUILayout.Foldout(metaFoldout, "Meta", toggleOnLabelClick: true);
            if (!metaFoldout) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(expValueProp,
                    new GUIContent("XP Value", "XP awarded to the party when this enemy dies."));
            }
        }

        // ── Defaults for newly added entries ────────────────────────────────
        private static void InitNewMove(SerializedProperty moveProp)
        {
            var nameProp   = moveProp.FindPropertyRelative("name");
            var weightProp = moveProp.FindPropertyRelative("weight");
            var intentsProp = moveProp.FindPropertyRelative("intents");

            if (nameProp != null)    nameProp.stringValue = "New Move";
            if (weightProp != null)  weightProp.intValue  = 1;
            if (intentsProp != null) intentsProp.ClearArray();
            // Always start with one intent so a fresh move shows authoring
            // affordance immediately instead of an empty intents list.
            if (intentsProp != null)
            {
                intentsProp.InsertArrayElementAtIndex(0);
                InitNewIntent(intentsProp.GetArrayElementAtIndex(0));
            }
            moveProp.isExpanded = true;
        }

        private static void InitNewIntent(SerializedProperty intentProp)
        {
            var typeProp    = intentProp.FindPropertyRelative("intentType");
            var effectsProp = intentProp.FindPropertyRelative("effects");
            if (typeProp != null) typeProp.enumValueIndex = (int)EnemyIntentType.Attack;
            if (effectsProp != null) effectsProp.ClearArray();
            intentProp.isExpanded = true;
        }
    }
}
