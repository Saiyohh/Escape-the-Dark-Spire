// CharacterDataEditor.cs
// -----------------------------------------------------------------------------
// Custom inspector for CharacterData. Matches the color-coded section style
// used by SkillDataEditor / WeaponDataEditor / ConditionDataEditor:
//
//   [Top summary]      — alignment + stat badges (HP, SP, POW, DEX, DEF, WIL)
//                        and the 5th-pool flags that are enabled
//   [Identity]         — name, title, description, portraits, combat sprite.
//                        Header uses DrawCharacterSectionHeader so the
//                        character's own signature + highlight colors drive
//                        the strip.
//   [Base Stats]       — HP/SP/POW/DEX/DEF/WIL in a two-column grid
//   [Palette]          — signature + highlight color fields side-by-side,
//                        with a live preview swatch showing how they read
//                        against each other. A "Reset to Alignment" button
//                        re-seeds both from CharacterPalette defaults.
//   [Display]          — displayPrefab
//   [Starter Loadout]  — startingWeapon, startingSkills
//   [5th Pool]         — toggle row for orb/spellbook/potion/stance flags
//   [Progression]      — startingLevel, hpPerLevel (foldout)
// -----------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    [CustomEditor(typeof(CharacterData))]
    public class CharacterDataEditor : Editor
    {
        // Identity
        private SerializedProperty characterNameProp, descriptionProp;
        private SerializedProperty headIconProp, combatSpriteProp;
        private SerializedProperty alignmentProp;

        // Stats
        private SerializedProperty maxHPProp, maxSPProp, powProp, dexProp, defProp, wilProp;

        // Display
        private SerializedProperty displayPrefabProp;
        private SerializedProperty partyTrayScaleProp, partyTrayOffsetProp;

        // Palette
        private SerializedProperty signatureColorProp, highlightColorProp;

        // Loadout
        private SerializedProperty startingWeaponProp, startingSkillsProp;

        // 5th pool
        private SerializedProperty hasOrbSystemProp, hasSpellbookProp, hasPotionSackProp, hasStanceSystemProp;

        // Orb subsystem hooks (only relevant when hasOrbSystem is true)
        private SerializedProperty startingOrbProp;

        // Star subsystem hook (Regent — Divine Right combat-start grant)
        private SerializedProperty startingStarsProp;

        // Progression
        private SerializedProperty startingLevelProp, hpPerLevelProp;

        // Action Refusal Voice Lines
        private SerializedProperty refusalNoActionProp, refusalNoFreeActionProp,
            refusalNotEnoughSPProp, refusalNotEnoughStarsProp, refusalImmobilizedProp;

        private bool progressionFoldout = false;
        private bool voiceLinesFoldout  = false;

        // Neutral section colors (used when the accent shouldn't be the character's own color)
        private static readonly Color StatHeader    = new(0.85f, 0.85f, 0.85f); // light grey
        private static readonly Color DisplayHeader = new(0.45f, 0.55f, 0.65f); // steel
        private static readonly Color PaletteHeader = new(0.55f, 0.40f, 0.78f); // purple
        private static readonly Color LoadoutHeader = new(0.55f, 0.70f, 0.40f); // olive green
        private static readonly Color FlagsHeader   = new(0.95f, 0.62f, 0.26f); // orange

        // Stat badge colors — share vocabulary with the rest of the inspectors.
        private static readonly Color BadgeHP  = new(0.82f, 0.32f, 0.30f);
        private static readonly Color BadgeSP  = new(0.30f, 0.50f, 0.80f);
        private static readonly Color BadgePOW = new(0.85f, 0.50f, 0.25f);
        private static readonly Color BadgeDEX = new(0.40f, 0.72f, 0.50f);
        private static readonly Color BadgeDEF = new(0.45f, 0.55f, 0.80f);
        private static readonly Color BadgeWIL = new(0.62f, 0.41f, 0.78f);

        private void OnEnable()
        {
            characterNameProp  = serializedObject.FindProperty("characterName");
            descriptionProp    = serializedObject.FindProperty("description");
            headIconProp       = serializedObject.FindProperty("headIcon");
            combatSpriteProp   = serializedObject.FindProperty("combatSprite");
            alignmentProp      = serializedObject.FindProperty("alignment");

            maxHPProp          = serializedObject.FindProperty("maxHP");
            maxSPProp          = serializedObject.FindProperty("maxSP");
            powProp            = serializedObject.FindProperty("pow");
            dexProp            = serializedObject.FindProperty("dex");
            defProp            = serializedObject.FindProperty("def");
            wilProp            = serializedObject.FindProperty("wil");

            displayPrefabProp     = serializedObject.FindProperty("displayPrefab");
            partyTrayScaleProp    = serializedObject.FindProperty("partyTrayPortraitScale");
            partyTrayOffsetProp   = serializedObject.FindProperty("partyTrayPortraitOffset");

            signatureColorProp = serializedObject.FindProperty("signatureColor");
            highlightColorProp = serializedObject.FindProperty("highlightColor");

            startingWeaponProp = serializedObject.FindProperty("startingWeapon");
            startingSkillsProp = serializedObject.FindProperty("startingSkills");

            hasOrbSystemProp   = serializedObject.FindProperty("hasOrbSystem");
            hasSpellbookProp   = serializedObject.FindProperty("hasSpellbook");
            hasPotionSackProp  = serializedObject.FindProperty("hasPotionSack");
            hasStanceSystemProp = serializedObject.FindProperty("hasStanceSystem");
            startingOrbProp    = serializedObject.FindProperty("startingOrb");
            startingStarsProp  = serializedObject.FindProperty("startingStars");

            startingLevelProp  = serializedObject.FindProperty("startingLevel");
            hpPerLevelProp     = serializedObject.FindProperty("hpPerLevel");

            refusalNoActionProp       = serializedObject.FindProperty("refusalLine_NoAction");
            refusalNoFreeActionProp   = serializedObject.FindProperty("refusalLine_NoFreeAction");
            refusalNotEnoughSPProp    = serializedObject.FindProperty("refusalLine_NotEnoughSP");
            refusalNotEnoughStarsProp = serializedObject.FindProperty("refusalLine_NotEnoughStars");
            refusalImmobilizedProp    = serializedObject.FindProperty("refusalLine_Immobilized");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawTopSummary();
            DrawIdentitySection();
            DrawStatsSection();
            DrawPaletteSection();
            DrawDisplaySection();
            DrawPartyTrayPreviewSection();
            DrawLoadoutSection();
            DrawFifthPoolSection();
            DrawVoiceLinesSection();
            DrawProgressionSection();

            serializedObject.ApplyModifiedProperties();
        }

        // ── Top summary ──────────────────────────────────────────────────────
        private void DrawTopSummary()
        {
            var c = (CharacterData)target;

            EditorGUILayout.Space(4);

            // Row 1: identity-style badges (alignment + stat totals)
            EditorStyleKit.BeginBadgeRow();
            EditorStyleKit.DrawBadge(c.alignment.ToString(),
                EditorStyleKit.CharacterSignature(c.alignment));
            EditorStyleKit.DrawBadge($"HP {c.maxHP}",  BadgeHP);
            EditorStyleKit.DrawBadge($"SP {c.maxSP}",  BadgeSP);
            EditorStyleKit.DrawBadge($"POW {c.pow}",   BadgePOW);
            EditorStyleKit.DrawBadge($"DEX {c.dex}",   BadgeDEX);
            EditorStyleKit.DrawBadge($"DEF {c.def}",   BadgeDEF);
            EditorStyleKit.DrawBadge($"WIL {c.wil}",   BadgeWIL);
            EditorStyleKit.EndBadgeRow();

            // Row 2: 5th-pool flags actually enabled (skip if all off)
            if (c.hasOrbSystem || c.hasSpellbook || c.hasPotionSack || c.hasStanceSystem)
            {
                EditorStyleKit.BeginBadgeRow();
                if (c.hasOrbSystem)    EditorStyleKit.DrawBadge("Orbs",      new Color(0.35f, 0.78f, 0.92f));
                if (c.hasSpellbook)    EditorStyleKit.DrawBadge("Spellbook", new Color(0.62f, 0.41f, 0.78f));
                if (c.hasPotionSack)   EditorStyleKit.DrawBadge("Potions",   new Color(0.40f, 0.78f, 0.50f));
                if (c.hasStanceSystem) EditorStyleKit.DrawBadge("Stances",   new Color(0.95f, 0.62f, 0.26f));
                EditorStyleKit.EndBadgeRow();
            }
        }

        // ── Identity ─────────────────────────────────────────────────────────
        private void DrawIdentitySection()
        {
            var c = (CharacterData)target;
            // Pass the target's live colors directly — going through the
            // alignment → CharacterLibrary lookup would resolve to whichever
            // character is registered for this alignment (possibly a different
            // SO), and wouldn't reflect unsaved color edits on the current target.
            EditorStyleKit.BeginCharacterBoxSection("Identity", c.signatureColor, c.highlightColor);
            EditorGUILayout.PropertyField(alignmentProp);
            EditorGUILayout.PropertyField(characterNameProp, new GUIContent("Name"));
            EditorGUILayout.PropertyField(descriptionProp);

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Art", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(headIconProp,     new GUIContent("Head Icon"));
            EditorGUILayout.PropertyField(combatSpriteProp, new GUIContent("Combat Sprite"));
            EditorStyleKit.EndBoxSection();
        }

        // ── Base stats ───────────────────────────────────────────────────────
        private void DrawStatsSection()
        {
            EditorStyleKit.BeginBoxSection("Base Stats", StatHeader);

            // Two-column grid: HP/SP, POW/DEX, DEF/WIL
            DrawStatPairRow(maxHPProp, "HP",  BadgeHP, maxSPProp,  "SP",  BadgeSP);
            DrawStatPairRow(powProp,   "POW", BadgePOW, dexProp,    "DEX", BadgeDEX);
            DrawStatPairRow(defProp,   "DEF", BadgeDEF, wilProp,    "WIL", BadgeWIL);

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField(
                "Players use DEX for both attack rolls and initiative. DEF is the hit target; " +
                "enemies need D20 + attack bonus ≥ DEF. WIL gates affliction saves.",
                EditorStyles.miniLabel);
            EditorStyleKit.EndBoxSection();
        }

        /// <summary>
        /// Draws two stat fields on the same line with equal column widths.
        /// The row's total usable width is split 50/50 (minus a small gap),
        /// so HP/SP, POW/DEX, DEF/WIL all line up vertically regardless of
        /// how Unity sizes individual IntFields.
        /// </summary>
        private static void DrawStatPairRow(
            SerializedProperty aProp, string aLabel, Color aColor,
            SerializedProperty bProp, string bLabel, Color bColor)
        {
            const float Gap = 6f;
            // `currentViewWidth` is the inspector's pixel width; subtract the
            // indent + scrollbar fudge so fields don't overflow.
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

            // 3px color bar
            var bar = GUILayoutUtility.GetRect(3, EditorGUIUtility.singleLineHeight,
                GUILayout.Width(3), GUILayout.ExpandHeight(false));
            EditorGUI.DrawRect(bar, accent);
            GUILayout.Space(4);

            // Narrow the label so the int field gets consistent space on both halves
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 38f;
            EditorGUILayout.PropertyField(prop, new GUIContent(label));
            EditorGUIUtility.labelWidth = previousLabelWidth;

            EditorGUILayout.EndHorizontal();
        }

        // ── Palette ──────────────────────────────────────────────────────────
        private void DrawPaletteSection()
        {
            EditorStyleKit.BeginBoxSection("Palette", PaletteHeader);

            var c = (CharacterData)target;

            // Side-by-side color fields with live preview swatches. Uses
            // ColorField directly (not PropertyField) so the [Header("Palette")]
            // attribute on signatureColor doesn't render an extra label on the
            // left column and misalign the swatches.
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();
            EditorGUI.BeginChangeCheck();
            var newSig = EditorGUILayout.ColorField(
                new GUIContent("Signature"), signatureColorProp.colorValue,
                showEyedropper: true, showAlpha: false, hdr: false);
            if (EditorGUI.EndChangeCheck()) signatureColorProp.colorValue = newSig;
            DrawSwatch(c.signatureColor, "Aa",
                EditorStyleKit.IdealTextFor(c.signatureColor), 36);
            EditorGUILayout.EndVertical();

            GUILayout.Space(6);

            EditorGUILayout.BeginVertical();
            EditorGUI.BeginChangeCheck();
            var newHi = EditorGUILayout.ColorField(
                new GUIContent("Highlight"), highlightColorProp.colorValue,
                showEyedropper: true, showAlpha: false, hdr: false);
            if (EditorGUI.EndChangeCheck()) highlightColorProp.colorValue = newHi;
            DrawSwatch(c.highlightColor, "Aa",
                EditorStyleKit.IdealTextFor(c.highlightColor), 36);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            // Combined preview: signature bar + highlight strip, like a section header
            EditorGUILayout.Space(4);
            DrawCombinedPreview(c.signatureColor, c.highlightColor, c.characterName);

            EditorGUILayout.Space(2);
            if (GUILayout.Button("Reset to Alignment Defaults", GUILayout.Height(20)))
            {
                Undo.RecordObject(c, "Reset Character Palette");
                c.signatureColor = CharacterPalette.DefaultSignature(c.alignment);
                c.highlightColor = CharacterPalette.DefaultHighlight(c.alignment);
                EditorUtility.SetDirty(c);
                serializedObject.Update();
            }

            EditorGUILayout.LabelField(
                "Signature = strong surfaces (badges, name plates, callout backgrounds). " +
                "Highlight = soft surfaces (card fills, selection glows, row tints).",
                EditorStyles.miniLabel);
            EditorStyleKit.EndBoxSection();
        }

        private static void DrawSwatch(Color bg, string label, Color textColor, float height)
        {
            var rect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.label,
                GUILayout.Height(height), GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, bg);

            // Thin dark outline for legibility
            var edge = new Color(0f, 0f, 0f, 0.28f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), edge);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height - 1, rect.width, 1), edge);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), edge);
            EditorGUI.DrawRect(new Rect(rect.x + rect.width - 1, rect.y, 1, rect.height), edge);

            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                normal = { textColor = textColor },
            };
            GUI.Label(rect, label, style);
        }

        /// <summary>
        /// Small preview strip that mimics the DrawCharacterSectionHeader look:
        /// signature left bar + highlight strip tint + sample text.
        /// </summary>
        private static void DrawCombinedPreview(Color signature, Color highlight, string sampleText)
        {
            var rect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.boldLabel,
                GUILayout.Height(28), GUILayout.ExpandWidth(true));

            // Signature bar
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 5, rect.height), signature);

            // Highlight strip tint
            var tint = new Color(highlight.r, highlight.g, highlight.b, 0.35f);
            EditorGUI.DrawRect(new Rect(rect.x + 5, rect.y, rect.width - 5, rect.height), tint);

            // Sample text
            var textStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
            };
            var textRect = new Rect(rect.x + 12, rect.y, rect.width - 14, rect.height);
            GUI.Label(textRect, string.IsNullOrEmpty(sampleText) ? "Sample Header" : sampleText, textStyle);

            // Inline badge-on-signature sample
            var badgeWidth = 64f;
            var badgeRect = new Rect(rect.x + rect.width - badgeWidth - 6, rect.y + 5, badgeWidth, rect.height - 10);
            EditorGUI.DrawRect(badgeRect, signature);
            var badgeStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = EditorStyleKit.IdealTextFor(signature) },
            };
            GUI.Label(badgeRect, "BADGE", badgeStyle);

            // Bottom hairline
            var line = new Rect(rect.x, rect.y + rect.height - 1, rect.width, 1);
            EditorGUI.DrawRect(line, new Color(0.5f, 0.5f, 0.5f, 0.4f));
        }

        // ── Display ──────────────────────────────────────────────────────────
        private void DrawDisplaySection()
        {
            EditorStyleKit.BeginBoxSection("Display", DisplayHeader);
            EditorGUILayout.PropertyField(displayPrefabProp);
            if (displayPrefabProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "Empty — CombatManager will spawn its default UnitDisplay prefab.",
                    MessageType.None);
            }
            EditorStyleKit.EndBoxSection();
        }

        // ── Party Tray Preview ───────────────────────────────────────────────
        // Two views, top to bottom:
        //
        //   1. Context: full sprite at fitted scale, with collapsed + expanded
        //      mask rectangles outlined on top so you can see WHERE on the
        //      sprite each mask state crops.
        //   2. Mask result: side-by-side collapsed (140×80) and expanded
        //      (140×130) cells rendered at runtime-actual pixel sizes,
        //      showing exactly what the pill will display.
        //
        // Reference geometry: at runtime the Portrait Image's RectTransform is
        // 342×342 (authored on the prefab) and gets multiplied by
        // partyTrayPortraitScale via localScale. The mask shows a 140×height
        // window of that scaled rect, translated by partyTrayPortraitOffset.
        private static readonly Color PartyTrayHeader   = new(0.30f, 0.62f, 0.78f); // teal
        private static readonly Color PartyTrayBgDark   = new(0.10f, 0.10f, 0.10f);
        private static readonly Color PartyTrayDimVeil  = new(0f, 0f, 0f, 0.45f);
        private static readonly Color PartyTrayBoxColExp= new(1f, 0.85f, 0.30f, 1f); // gold (expanded)
        private static readonly Color PartyTrayBoxColCol= new(0.95f, 0.95f, 0.95f, 1f); // white (collapsed)
        private static readonly Color PartyTrayEdge     = new(0.85f, 0.85f, 0.85f, 0.55f);
        private const float PartyTrayCollapsedW = 140f;
        private const float PartyTrayCollapsedH = 80f;
        private const float PartyTrayExpandedW  = 140f;
        private const float PartyTrayExpandedH  = 130f;
        // Authored Portrait Image RectTransform size on the prefab. The runtime
        // applies localScale = partyTrayPortraitScale on top of this — so the
        // effective portrait rect inside the mask is (342 × scale) on each side.
        private const float PartyTrayPortraitRectSize = 342f;
        private const float PartyTrayContextMaxDim    = 240f;
        private const float PartyTrayOffsetClamp      = 500f;

        private void DrawPartyTrayPreviewSection()
        {
            var c = (CharacterData)target;

            EditorStyleKit.BeginBoxSection("Party Tray Preview", PartyTrayHeader);

            EditorGUILayout.Slider(partyTrayScaleProp, 2f, 6f, new GUIContent("Portrait Scale"));

            Vector2 offset = partyTrayOffsetProp.vector2Value;
            EditorGUI.BeginChangeCheck();
            float newX = EditorGUILayout.Slider(new GUIContent("Offset X"),
                offset.x, -PartyTrayOffsetClamp, PartyTrayOffsetClamp);
            float newY = EditorGUILayout.Slider(new GUIContent("Offset Y"),
                offset.y, -PartyTrayOffsetClamp, PartyTrayOffsetClamp);
            if (EditorGUI.EndChangeCheck())
                partyTrayOffsetProp.vector2Value = new Vector2(newX, newY);

            EditorGUILayout.Space(4);
            DrawPartyTrayContextView(c);
            EditorGUILayout.Space(8);
            DrawPartyTrayMaskResultsRow(c);

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Reset Scale & Offset", GUILayout.Height(20)))
            {
                Undo.RecordObject(c, "Reset Party Tray Portrait");
                c.partyTrayPortraitScale  = 3f;
                c.partyTrayPortraitOffset = Vector2.zero;
                EditorUtility.SetDirty(c);
                serializedObject.Update();
            }

            EditorGUILayout.LabelField(
                "Top: full sprite with mask outlines (white = collapsed 140×80, " +
                "gold = expanded 140×130). Bottom: actual mask cells at runtime size.",
                EditorStyles.miniLabel);

            EditorStyleKit.EndBoxSection();
        }

        // ── Context view: full sprite + mask outlines ─────────────────────
        private static void DrawPartyTrayContextView(CharacterData c)
        {
            var sprite = c != null ? c.combatSprite : null;

            if (sprite == null || sprite.texture == null
                || sprite.rect.width <= 0f || sprite.rect.height <= 0f)
            {
                var ph = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.label,
                    GUILayout.Height(80f), GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(ph, PartyTrayBgDark);
                var phStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.7f, 0.7f, 0.7f) },
                };
                GUI.Label(ph, "Assign a Combat Sprite to enable preview", phStyle);
                return;
            }

            float spriteW = sprite.rect.width;
            float spriteH = sprite.rect.height;
            float runtimeScale = Mathf.Max(0.01f, c.partyTrayPortraitScale);

            float ctxScale = Mathf.Min(
                PartyTrayContextMaxDim / spriteW,
                PartyTrayContextMaxDim / spriteH);
            ctxScale = Mathf.Min(ctxScale, 4f);
            float dispW = spriteW * ctxScale;
            float dispH = spriteH * ctxScale;

            var rowRect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.label,
                GUILayout.Height(dispH + 8f), GUILayout.ExpandWidth(true));

            var spriteRect = new Rect(
                rowRect.x + (rowRect.width - dispW) * 0.5f,
                rowRect.y + 4f,
                dispW, dispH);

            var bg = new Rect(spriteRect.x - 3f, spriteRect.y - 3f,
                              spriteRect.width + 6f, spriteRect.height + 6f);
            EditorGUI.DrawRect(bg, PartyTrayBgDark);

            var tex = sprite.texture;
            var tc  = new Rect(
                sprite.rect.x / tex.width,
                sprite.rect.y / tex.height,
                sprite.rect.width  / tex.width,
                sprite.rect.height / tex.height);
            GUI.DrawTextureWithTexCoords(spriteRect, tex, tc, alphaBlend: true);

            // Map: in portrait-rect space the mask covers (140/scale × h/scale)
            // and is centered at (171 - offset.x/scale, 171 + offset.y/scale)
            // (GUI y is down-positive; UI offset.y is up-positive — hence the +).
            // displayPerPortrait converts portrait-rect pixels into context-view
            // display pixels assuming the displayed sprite spans the 342×342 rect.
            float displayPerPortraitX = dispW / PartyTrayPortraitRectSize;
            float displayPerPortraitY = dispH / PartyTrayPortraitRectSize;

            float colBoxW = (PartyTrayCollapsedW / runtimeScale) * displayPerPortraitX;
            float colBoxH = (PartyTrayCollapsedH / runtimeScale) * displayPerPortraitY;
            float expBoxW = (PartyTrayExpandedW  / runtimeScale) * displayPerPortraitX;
            float expBoxH = (PartyTrayExpandedH  / runtimeScale) * displayPerPortraitY;

            float cx = spriteRect.x + spriteRect.width  * 0.5f
                       - (c.partyTrayPortraitOffset.x / runtimeScale) * displayPerPortraitX;
            float cy = spriteRect.y + spriteRect.height * 0.5f
                       + (c.partyTrayPortraitOffset.y / runtimeScale) * displayPerPortraitY;

            var expBox = new Rect(cx - expBoxW * 0.5f, cy - expBoxH * 0.5f, expBoxW, expBoxH);
            var colBox = new Rect(cx - colBoxW * 0.5f, cy - colBoxH * 0.5f, colBoxW, colBoxH);

            DrawDimVeil(spriteRect, expBox);
            DrawRectOutline(expBox, PartyTrayBoxColExp, 2f);
            DrawRectOutline(colBox, PartyTrayBoxColCol, 1f);
            DrawRectOutline(spriteRect, PartyTrayEdge, 1f);
        }

        // ── Mask results: side-by-side cells at runtime size ──────────────
        private static void DrawPartyTrayMaskResultsRow(CharacterData c)
        {
            const float labelHeight = 16f;
            const float gap = 14f;
            float rowHeight = PartyTrayExpandedH + labelHeight + 4f;

            var rowRect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.label,
                GUILayout.Height(rowHeight), GUILayout.ExpandWidth(true));

            float pairW = PartyTrayCollapsedW + gap + PartyTrayExpandedW;
            float startX = rowRect.x + (rowRect.width - pairW) * 0.5f;

            // Baseline-align: collapsed sits lower so both share a bottom edge.
            var collapsedMask = new Rect(
                startX,
                rowRect.y + (PartyTrayExpandedH - PartyTrayCollapsedH),
                PartyTrayCollapsedW, PartyTrayCollapsedH);
            DrawTrayMaskResult(collapsedMask, c);

            var expandedMask = new Rect(
                startX + PartyTrayCollapsedW + gap,
                rowRect.y,
                PartyTrayExpandedW, PartyTrayExpandedH);
            DrawTrayMaskResult(expandedMask, c);

            var labelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
            };
            GUI.Label(new Rect(collapsedMask.x, rowRect.y + PartyTrayExpandedH + 2f,
                               PartyTrayCollapsedW, labelHeight),
                      "Collapsed (140×80)", labelStyle);
            GUI.Label(new Rect(expandedMask.x, rowRect.y + PartyTrayExpandedH + 2f,
                               PartyTrayExpandedW, labelHeight),
                      "Expanded (140×130)", labelStyle);
        }

        private static void DrawTrayMaskResult(Rect maskRect, CharacterData c)
        {
            EditorGUI.DrawRect(maskRect, PartyTrayBgDark);

            var sprite = c != null ? c.combatSprite : null;
            if (sprite != null && sprite.texture != null
                && sprite.rect.width > 0f && sprite.rect.height > 0f)
            {
                float scale = Mathf.Max(0.01f, c.partyTrayPortraitScale);
                float drawW = PartyTrayPortraitRectSize * scale;
                float drawH = PartyTrayPortraitRectSize * scale;

                // Mirror runtime: portrait centered in mask, then translated by
                // offset. UI offset.y up-positive → GUI y down-positive (flip).
                float drawXLocal = (maskRect.width  - drawW) * 0.5f + c.partyTrayPortraitOffset.x;
                float drawYLocal = (maskRect.height - drawH) * 0.5f - c.partyTrayPortraitOffset.y;

                var tex = sprite.texture;
                var tc  = new Rect(
                    sprite.rect.x / tex.width,
                    sprite.rect.y / tex.height,
                    sprite.rect.width  / tex.width,
                    sprite.rect.height / tex.height);

                GUI.BeginClip(maskRect);
                GUI.DrawTextureWithTexCoords(
                    new Rect(drawXLocal, drawYLocal, drawW, drawH),
                    tex, tc, alphaBlend: true);
                GUI.EndClip();
            }

            DrawRectOutline(maskRect, PartyTrayEdge, 1f);
        }

        // Draw four rects to dim the area outside `inner` within `outer`.
        private static void DrawDimVeil(Rect outer, Rect inner)
        {
            float ix0 = Mathf.Max(outer.x, inner.x);
            float iy0 = Mathf.Max(outer.y, inner.y);
            float ix1 = Mathf.Min(outer.x + outer.width,  inner.x + inner.width);
            float iy1 = Mathf.Min(outer.y + outer.height, inner.y + inner.height);

            if (iy0 > outer.y)
                EditorGUI.DrawRect(new Rect(outer.x, outer.y, outer.width, iy0 - outer.y), PartyTrayDimVeil);
            if (iy1 < outer.y + outer.height)
                EditorGUI.DrawRect(new Rect(outer.x, iy1, outer.width, outer.y + outer.height - iy1), PartyTrayDimVeil);
            if (ix0 > outer.x)
                EditorGUI.DrawRect(new Rect(outer.x, iy0, ix0 - outer.x, iy1 - iy0), PartyTrayDimVeil);
            if (ix1 < outer.x + outer.width)
                EditorGUI.DrawRect(new Rect(ix1, iy0, outer.x + outer.width - ix1, iy1 - iy0), PartyTrayDimVeil);
        }

        private static void DrawRectOutline(Rect r, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, thickness), color);
            EditorGUI.DrawRect(new Rect(r.x, r.y + r.height - thickness, r.width, thickness), color);
            EditorGUI.DrawRect(new Rect(r.x, r.y, thickness, r.height), color);
            EditorGUI.DrawRect(new Rect(r.x + r.width - thickness, r.y, thickness, r.height), color);
        }

        // ── Starter Loadout ──────────────────────────────────────────────────
        private void DrawLoadoutSection()
        {
            EditorStyleKit.BeginBoxSection("Starter Loadout", LoadoutHeader);
            EditorGUILayout.PropertyField(startingWeaponProp);
            EditorGUILayout.PropertyField(startingSkillsProp, new GUIContent("Starting Skills (2)"), true);

            if (startingSkillsProp.arraySize != 2)
            {
                EditorGUILayout.HelpBox(
                    $"Expected 2 starting skills, currently {startingSkillsProp.arraySize}.",
                    MessageType.Warning);
            }
            EditorStyleKit.EndBoxSection();
        }

        // ── 5th Pool Flags ───────────────────────────────────────────────────
        private void DrawFifthPoolSection()
        {
            EditorStyleKit.BeginBoxSection("5th Pool Systems", FlagsHeader);
            EditorGUILayout.LabelField(
                "Character-specific resource subsystems layered on top of the core loop.",
                EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            DrawFlagToggle(hasOrbSystemProp,   "Orbs",      new Color(0.35f, 0.78f, 0.92f));
            DrawFlagToggle(hasSpellbookProp,   "Spellbook", new Color(0.62f, 0.41f, 0.78f));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            DrawFlagToggle(hasPotionSackProp,  "Potions",   new Color(0.40f, 0.78f, 0.50f));
            DrawFlagToggle(hasStanceSystemProp, "Stances",  new Color(0.95f, 0.62f, 0.26f));
            EditorGUILayout.EndHorizontal();

            // ── Per-system combat-start hooks ──────────────────────────────
            // Only surface the relevant fields when their subsystem is active,
            // so non-orb characters don't see an Orb Data slot, etc.

            if (hasOrbSystemProp != null && hasOrbSystemProp.boolValue)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Orb subsystem", EditorStyles.miniBoldLabel);
                if (startingOrbProp != null)
                {
                    EditorGUILayout.PropertyField(startingOrbProp,
                        new GUIContent("Starting Orb",
                            "Channeled at combat start. Defect's Cracked Core core aspect = Lightning."));
                    if (startingOrbProp.objectReferenceValue == null)
                    {
                        EditorGUILayout.HelpBox(
                            "No starting orb set — the orb tray will spawn empty. Set this to " +
                            "Orb_Lightning to match the canonical Cracked Core core aspect.",
                            MessageType.None);
                    }
                }
            }

            // Stars: surfaced whenever startingStars > 0 OR the field exists,
            // so the Regent's Divine Right grant is always editable. There's
            // no dedicated bool flag for the Star pool.
            if (startingStarsProp != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Star subsystem", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(startingStarsProp,
                    new GUIContent("Starting Stars",
                        "Stars granted at combat start. Regent's Divine Right = 3. " +
                        "Leave at 0 for non-Star characters."));
            }

            EditorStyleKit.EndBoxSection();
        }

        private static void DrawFlagToggle(SerializedProperty prop, string label, Color onColor)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.MinWidth(120));
            var bar = GUILayoutUtility.GetRect(4, EditorGUIUtility.singleLineHeight,
                GUILayout.Width(4), GUILayout.ExpandHeight(false));
            EditorGUI.DrawRect(bar, prop.boolValue ? onColor : new Color(0.35f, 0.35f, 0.35f));
            GUILayout.Space(4);
            prop.boolValue = EditorGUILayout.ToggleLeft(label, prop.boolValue);
            EditorGUILayout.EndHorizontal();
        }

        // ── Action Refusal Voice Lines (foldout) ─────────────────────────────
        // Per-character speech lines + writer prompts for the action-refusal
        // bubble. Each entry has a `text` field (what the character says) and
        // a `writerPrompt` field (authoring guidance the user can hand to a
        // writer for iteration). Foldout starts closed to keep the inspector
        // tidy — these are infrequently edited once authored.
        private void DrawVoiceLinesSection()
        {
            EditorGUILayout.Space(6);
            voiceLinesFoldout = EditorGUILayout.Foldout(
                voiceLinesFoldout, "Action Refusal Voice Lines", true);
            if (!voiceLinesFoldout) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox(
                "Per-character speech bubble lines shown when the player " +
                "tries an action they can't take. Leave a line blank to " +
                "fall back to the generic version.",
                MessageType.Info);

            DrawRefusalLineEntry(refusalNoActionProp,       "No Action",
                "Triggered when the player has already used their Action this turn (Attack, Guard, Skill, Move).");
            DrawRefusalLineEntry(refusalNoFreeActionProp,   "No Free Action",
                "Triggered when the player tries a free-action skill but has already used their free action this turn.");
            DrawRefusalLineEntry(refusalNotEnoughSPProp,    "Not Enough SP",
                "Triggered when the player tries a skill whose SP cost exceeds their current SP.");

            // Stars line is only relevant for characters who actually use the
            // Star resource. startingStars > 0 is the canonical "has Stars
            // pool" gate (matches StarsUI.FindStarBearer and the rest of the
            // codebase — there's no separate hasStarSystem flag). Hides the
            // entry entirely on non-Star characters so it doesn't clutter
            // their inspector with an unauthorable line.
            bool hasStarsResource = startingStarsProp != null && startingStarsProp.intValue > 0;
            if (hasStarsResource)
            {
                DrawRefusalLineEntry(refusalNotEnoughStarsProp, "Not Enough Stars",
                    "Triggered when a skill's star cost exceeds the unit's current stars.");
            }


            DrawRefusalLineEntry(refusalImmobilizedProp,    "Immobilized",
                "Triggered when any condition with preventsAction (e.g. Stunned) is active. Overrides every other refusal — the unit can't act at all.");

            EditorGUI.indentLevel--;
        }

        private static void DrawRefusalLineEntry(SerializedProperty lineProp, string label, string contextHint)
        {
            if (lineProp == null) return;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(contextHint, EditorStyles.miniLabel);

            EditorGUILayout.PropertyField(lineProp,
                new GUIContent("Line",
                    "The line this character speaks when this refusal " +
                    "fires. Leave blank to use the generic fallback."));
        }

        // ── Progression (foldout) ────────────────────────────────────────────
        private void DrawProgressionSection()
        {
            EditorGUILayout.Space(6);
            progressionFoldout = EditorGUILayout.Foldout(progressionFoldout, "Progression", true);
            if (progressionFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(startingLevelProp);
                EditorGUILayout.PropertyField(hpPerLevelProp);
                EditorGUI.indentLevel--;
            }
        }
    }
}
