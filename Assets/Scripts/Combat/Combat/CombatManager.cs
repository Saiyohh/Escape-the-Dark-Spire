// CombatManager.cs
// -----------------------------------------------------------------------------
// Singleton. Drives the entire combat scene via a CombatLoop coroutine.
//
// Phase sequence (looped each round until Victory/Defeat):
//   CombatStart
//     → PlayerInitiativeRoll → PlayerPhase
//     → EnemyInitiativeRoll  → EnemyPhase
//     → CleanupPhase
//
// PlayerPhase iterates the turn queue and WaitUntil !waitingForPlayerInput —
// the End Turn button is the only way to advance. EnemyPhase auto-runs each
// enemy with pulse/lunge/dice animations sequenced against suppress flags on
// UnitDisplay so damage flashes + deaths line up with the dice + lunge visuals.
// CleanupPhase ticks Poison/Regen and decrements condition durations.
//
// Everything external reads combat state via CombatEvents (CombatManager is
// the exclusive caller of InvokeX). Units + TargetingSystem are wired through
// properties; UI HUDs spawn from CharacterData/EnemyData display prefabs.
//
// Pass 2 work done: Poison tick now fires at each unit's TurnStart (GDD spec);
// temporaryDefense replaced by Shields condition; Free Action vocabulary.
// Cooldowns have been stripped — SP is the sole gate.
// -----------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DarkSpire
{
    public class CombatManager : MonoBehaviour
    {
        public static CombatManager Instance { get; private set; }

        [Header("Scene References")]
        [SerializeField] private Transform[] playerPositions;
        [SerializeField] private Transform[] enemyPositions;

        [Header("Prefabs")]
        [Tooltip("Default UnitDisplay prefab used when a CharacterData/EnemyData " +
                 "doesn't have its own displayPrefab assigned.")]
        [SerializeField] private GameObject defaultUnitDisplayPrefab;

        [Header("Death Sequence Pacing")]
        [Tooltip("Seconds to wait after a killing blow before resolving the rest " +
                 "of the action. Covers the world-HUD fade-out + sprite drop+fade " +
                 "so the player can read the death and the ranks compact visibly " +
                 "before combat continues (or ends). Should be ≥ UnitDisplay's " +
                 "deathFadeDuration.")]
        [SerializeField] private float deathSequenceWait = 1.2f;

        [Header("Orb Phase-End Pacing")]
        [Tooltip("Seconds between each orb's Passive trigger at phase end. " +
                 "Gives the player time to see each orb resolve and the per-orb " +
                 "VFX play. 0.45 ≈ comfortable read time without dragging the " +
                 "phase out.")]
        [SerializeField] private float orbPassiveInterval = 0.45f;

        [Header("Timing")]
        [SerializeField] private float phaseAnnounceDuration = 1.2f;
        [SerializeField] private float enemyActionDelay = 0.8f;

        [Tooltip("Pause between one enemy finishing its turn and the next enemy starting. " +
                 "Gives the player time to read what happened.")]
        [SerializeField] private float enemyTurnGap = 0.6f;

        [Tooltip("Delay after the intent pulse before the enemy actually acts. " +
                 "Lets the player see which enemy is about to move.")]
        [SerializeField] private float enemyPreActionDelay = 0.8f;

        [Header("Resolution Timing")]
        [Tooltip("Delay after dice roll event before showing damage/result. " +
                 "Should match the spin + number + result reveal of DiceRollUI.")]
        [SerializeField] private float diceRollWaitDuration = 1.2f;

        [Tooltip("Delay between each effect when a skill has multiple results.")]
        [SerializeField] private float effectStaggerDelay = 0.3f;

        [Tooltip("Time to wait for the attacker's lunge animation (forward + return) " +
                 "plus a beat before showing the outcome. Lunge ≈ 0.54s, pause ≈ 0.46s.")]
        [SerializeField] private float attackAnimationWait = 1.0f;

        [Header("Skill Playback Micro-Steps")]
        [Tooltip("Beat between the damage flash and the damage number popup.")]
        [SerializeField] private float postFlashDelay = 0.15f;
        [Tooltip("Beat between the damage number popup and the next playback step. " +
                 "Lets the HP bar tween register before conditions or further effects.")]
        [SerializeField] private float postDamageDelay = 0.35f;
        [Tooltip("Beat before the first condition floater of an effect spawns.")]
        [SerializeField] private float preConditionDelay = 0.25f;
        [Tooltip("Beat between each successive condition floater within an effect.")]
        [SerializeField] private float conditionFloaterStagger = 0.40f;

        [Header("Combat Intro Sequence")]
        [Tooltip("Phase-change banner. Optional — if null, phase changes fall " +
                 "back to a blank phaseAnnounceDuration wait.")]
        [SerializeField] private PhaseBannerUI phaseBanner;

        [Tooltip("Off-screen X offset (world units) used as each unit's intro " +
                 "starting position. Players slide from -offset (left edge), " +
                 "enemies from +offset (right edge). Bigger = travels further.")]
        [SerializeField] private float introSlideOffsetX = 12f;

        [Tooltip("Per-unit slide duration. Each rank uses the same duration; " +
                 "stagger between ranks is added on top via introRankStagger.")]
        [SerializeField] private float introSlideDuration = 0.5f;

        [Tooltip("Stagger between rank slide-ins. Front rank starts first, " +
                 "each subsequent rank delays by this amount. Tight stagger " +
                 "≈ \"all hit their spot at roughly the same time\".")]
        [SerializeField] private float introRankStagger = 0.05f;

        [Tooltip("Time after CombatStart fires before the unit slides begin — " +
                 "lets the top + bottom bars start moving first.")]
        [SerializeField] private float introBarsLeadIn = 0.15f;

        [Tooltip("Time after the last unit lands before HUDs start fading in.")]
        [SerializeField] private float introHudLeadIn = 0.05f;

        [Tooltip("HUD fade-in duration.")]
        [SerializeField] private float introHudFadeDuration = 0.30f;

        [Tooltip("Time after HUDs finish fading before the first phase banner.")]
        [SerializeField] private float introBannerLeadIn = 0.10f;

        // Runtime state
        public CombatPhase CurrentPhase { get; private set; }
        public int TurnNumber { get; private set; }
        public List<Unit> PlayerUnits { get; private set; } = new();
        public List<Unit> EnemyUnits { get; private set; } = new();
        public Unit ActiveUnit { get; private set; }

        private Queue<Unit> turnOrder = new();
        private bool waitingForPlayerInput;
        private bool combatActive;
        private bool isResolving; // True while dice/damage animation is playing

        // Pending action (waiting for targeting)
        private ActionType pendingAction;
        private SkillData pendingSkill;
        // Item-action pendings — parallel to pendingSkill. Item, source, and
        // index together identify which slot (party bag or owner pouch) was
        // chosen so Inventory.Consume can decrement the correct list after
        // the action resolves.
        private ItemData pendingItem;
        private ItemSource pendingItemSource;
        private int pendingItemIndex;

        /// <summary>Exposes pending action type so CombatUIManager can decide what info to show during targeting.</summary>
        public ActionType PendingAction => pendingAction;

        // Condition lookup is owned by ConditionLibrary.Instance — CombatManager
        // no longer caches an array. GetConditionData() below is a thin wrapper
        // kept for call-site convenience.

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            CombatEvents.ClearAll();
        }

        /// <summary>Initialize combat with party and encounter data.</summary>
        public void InitializeCombat(CharacterData[] party, EncounterSO encounter)
        {
            // Condition data is pulled live from ConditionLibrary.Instance.
            // No per-combat lookup to build — the library is the single source
            // of truth and is shared across every system.

            // Create player units. Spawn order = rank order — party[0] is rank 1
            // (front-most), party[3] is rank 4 (back). playerPositions[i] should
            // be authored in the scene so P1 sits closest to the midline.
            PlayerUnits.Clear();
            for (int i = 0; i < party.Length && i < playerPositions.Length; i++)
            {
                if (party[i] == null) continue;
                var unit = new Unit(party[i]);
                unit.SetRank(i + 1);
                PlayerUnits.Add(unit);
                SpawnUnitDisplay(unit, playerPositions[i], false);
            }

            // Create enemy units (same rank convention on the enemy side).
            EnemyUnits.Clear();
            int enemyCount = Random.Range(encounter.minEnemies, encounter.maxEnemies + 1);
            enemyCount = Mathf.Min(enemyCount, enemyPositions.Length);
            for (int i = 0; i < enemyCount; i++)
            {
                var enemyData = encounter.possibleEnemies[Random.Range(0, encounter.possibleEnemies.Length)];
                var unit = new Unit(enemyData);
                unit.SetRank(i + 1);
                EnemyUnits.Add(unit);
                SpawnUnitDisplay(unit, enemyPositions[i], true);

                // Subscribe to death
                int index = i;
                unit.OnDeath += () => OnUnitDied(unit);
            }

            // Subscribe player deaths
            foreach (var pu in PlayerUnits)
            {
                var captured = pu;
                pu.OnDeath += () => OnUnitDied(captured);
            }

            // Setup targeting system
            if (TargetingSystem.Instance != null)
                TargetingSystem.Instance.SetUnitLists(PlayerUnits, EnemyUnits);

            // Seed enemies with their authored starting conditions BEFORE
            // SetEnemyIntent runs (so move-pickers see the condition state) and
            // before FireCombatStart — gives signature passives like Byrdonis's
            // Territorial a chance to land before any combat-start triggers
            // fire. Uses the standard ApplyCondition path so floaters / icons
            // still spawn (the combat scene is fully booted by this point).
            foreach (var enemy in EnemyUnits)
                ApplyStartingConditions(enemy);

            // Clear the conditional-move event sets so the very first intent
            // pick doesn't latch onto self-applied starting conditions —
            // designers expect "OnConditionApplied" triggers to fire when a
            // PLAYER lands the condition mid-combat, not when the enemy itself
            // seeded with it on spawn.
            foreach (var enemy in EnemyUnits)
                enemy.ClearConditionalMoveEventState();

            // Set initial intents for enemies
            foreach (var enemy in EnemyUnits)
            {
                SetEnemyIntent(enemy);
            }

            TurnNumber = 0;
            combatActive = true;

            CombatEvents.InvokeCombatStart();

            // Fire OnCombatStart triggers on every unit (Stone Armor grants Plating,
            // Plague Flask applies Poison, Ring of the Snake restores SP, etc.)
            foreach (var u in PlayerUnits) u.conditions.FireCombatStart();
            foreach (var u in EnemyUnits)  u.conditions.FireCombatStart();

            // Orb-bearer combat-start hook: Cracked Core for the Defect channels
            // its starting orb. Driven by CharacterData.startingOrb so any future
            // orb-bearer character can opt in by populating that field.
            foreach (var u in PlayerUnits)
            {
                if (u.characterData != null && u.characterData.hasOrbSystem
                    && u.characterData.startingOrb != null)
                {
                    OrbManager.Channel(u, u.characterData.startingOrb);
                }
            }

            // Star-bearer combat-start hook: Divine Right for the Regent grants
            // the unit's CharacterData.startingStars on combat start. Stars are
            // a per-combat resource — initialize fresh each fight.
            foreach (var u in PlayerUnits)
            {
                if (u.characterData == null) continue;
                u.currentStars = 0; // reset before granting (covers re-init)
                if (u.characterData.startingStars > 0)
                    u.GainStars(u.characterData.startingStars);
            }

            StartCoroutine(CombatLoop());
        }

        private void SpawnUnitDisplay(Unit unit, Transform position, bool isEnemy)
        {
            // Use per-unit prefab from CharacterData/EnemyData, fall back to default
            GameObject prefab = null;
            if (unit.isPlayerControlled && unit.characterData != null)
                prefab = unit.characterData.displayPrefab;
            else if (!unit.isPlayerControlled && unit.enemyData != null)
                prefab = unit.enemyData.displayPrefab;

            if (prefab == null)
                prefab = defaultUnitDisplayPrefab;

            if (prefab == null) return;

            var go = Instantiate(prefab, position.position, Quaternion.identity);
            var display = go.GetComponent<UnitDisplay>();
            if (display != null)
            {
                display.Initialize(unit);
                // Stage the display + its HUD off-screen / invisible for the
                // intro sequence. RunCombatIntro slides everything in once
                // the bars have started moving.
                float offset = isEnemy ? introSlideOffsetX : -introSlideOffsetX;
                display.PrepareForIntro(offset);
                display.WorldHUD?.PrepareForIntro();
                display.EnemyWorldHUD?.PrepareForIntro();
            }
        }

        private IEnumerator CombatLoop()
        {
            yield return RunCombatIntro();

            while (combatActive)
            {
                TurnNumber++;

                // ── ROUND START ────────────────────────────────────────────
                // Clear RoundStart-timed conditions on every unit BEFORE any
                // turn begins. This is what makes Shields given to an ally
                // mid-round actually protect them — the clear fires here, not
                // at the start of their individual turn.
                FireOnAllUnits(c => c.ClearByTiming(ClearTiming.RoundStart));
                FireOnAllUnits(c => c.FireRoundStart());
                CombatEvents.InvokeRoundStart(TurnNumber);

                CombatEvents.InvokeTurnStart(TurnNumber);

                // ── PLAYER PHASE ───────────────────────────────────────────
                CurrentPhase = CombatPhase.PlayerInitiativeRoll;
                CombatEvents.InvokePhaseChanged(CurrentPhase);
                RollInitiative(PlayerUnits);

                CurrentPhase = CombatPhase.PlayerPhase;
                CombatEvents.InvokePhaseChanged(CurrentPhase);
                yield return ShowPhaseBanner($"Round {TurnNumber}", "Player Phase");

                FireOnAllUnits(c => c.FirePlayerPhaseStart());
                CombatEvents.InvokePlayerPhaseStart();

                yield return ExecutePlayerPhase();
                if (!combatActive) yield break;

                FireOnAllUnits(c => c.FirePlayerPhaseEnd());
                CombatEvents.InvokePlayerPhaseEnd();

                // ── ENEMY PHASE ────────────────────────────────────────────
                CurrentPhase = CombatPhase.EnemyInitiativeRoll;
                CombatEvents.InvokePhaseChanged(CurrentPhase);
                RollInitiative(EnemyUnits);

                CurrentPhase = CombatPhase.EnemyPhase;
                CombatEvents.InvokePhaseChanged(CurrentPhase);
                yield return ShowPhaseBanner($"Round {TurnNumber}", "Enemy Phase");

                FireOnAllUnits(c => c.FireEnemyPhaseStart());
                CombatEvents.InvokeEnemyPhaseStart();

                yield return ExecuteEnemyPhase();
                if (!combatActive) yield break;

                FireOnAllUnits(c => c.FireEnemyPhaseEnd());
                CombatEvents.InvokeEnemyPhaseEnd();

                // ── CLEANUP ────────────────────────────────────────────────
                CurrentPhase = CombatPhase.CleanupPhase;
                CombatEvents.InvokePhaseChanged(CurrentPhase);
                yield return ExecuteCleanupPhase();
                if (!combatActive) yield break;

                // ── ROUND END ──────────────────────────────────────────────
                FireOnAllUnits(c => c.ClearByTiming(ClearTiming.RoundEnd));
                FireOnAllUnits(c => c.FireRoundEnd());
                CombatEvents.InvokeRoundEnd(TurnNumber);

                CombatEvents.InvokeTurnEnd(TurnNumber);
            }
        }

        /// <summary>
        /// Combat-start choreography: bars are already sliding (their own
        /// OnCombatStart subscription fires synchronously with InvokeCombatStart).
        /// We pause briefly so the bars are visibly in motion, then slide units
        /// in front-rank-first, fade in HUDs, and let the phase-banner machinery
        /// take over for the first "Round 1: Player Phase" announcement.
        /// </summary>
        private IEnumerator RunCombatIntro()
        {
            // 1. Wait for the bars to be visibly mid-slide.
            yield return new WaitForSeconds(introBarsLeadIn);

            // 2. Slide units in. Front rank starts first; each subsequent rank
            // delays by introRankStagger so the formation reads as a wave that
            // converges toward the center together.
            int maxRank = 0;
            for (int i = 0; i < PlayerUnits.Count; i++)
            {
                var d = UnitDisplay.GetDisplay(PlayerUnits[i]);
                if (d == null) continue;
                int r = PlayerUnits[i].currentRank;
                float delay = (r - RankHelper.MinRank) * introRankStagger;
                d.PlayIntroSlide(introSlideDuration, delay);
                if (r > maxRank) maxRank = r;
            }
            for (int i = 0; i < EnemyUnits.Count; i++)
            {
                var d = UnitDisplay.GetDisplay(EnemyUnits[i]);
                if (d == null) continue;
                int r = EnemyUnits[i].currentRank;
                float delay = (r - RankHelper.MinRank) * introRankStagger;
                d.PlayIntroSlide(introSlideDuration, delay);
                if (r > maxRank) maxRank = r;
            }

            float totalSlideTime = (maxRank - RankHelper.MinRank) * introRankStagger
                                 + introSlideDuration;
            yield return new WaitForSeconds(totalSlideTime);

            // 3. Fade in every HUD overlay together.
            yield return new WaitForSeconds(introHudLeadIn);
            for (int i = 0; i < PlayerUnits.Count; i++)
            {
                var d = UnitDisplay.GetDisplay(PlayerUnits[i]);
                d?.WorldHUD?.FadeIn(introHudFadeDuration);
                d?.EnemyWorldHUD?.FadeIn(introHudFadeDuration);
            }
            for (int i = 0; i < EnemyUnits.Count; i++)
            {
                var d = UnitDisplay.GetDisplay(EnemyUnits[i]);
                d?.WorldHUD?.FadeIn(introHudFadeDuration);
                d?.EnemyWorldHUD?.FadeIn(introHudFadeDuration);
            }
            yield return new WaitForSeconds(introHudFadeDuration + introBannerLeadIn);
        }

        /// <summary>
        /// Show the configured PhaseBannerUI with the given header (e.g.
        /// "Round 1") + phase (e.g. "Player Phase") and yield until it
        /// finishes its in/hold/out cycle. Falls back to a blank
        /// phaseAnnounceDuration wait when no banner is wired.
        /// </summary>
        private IEnumerator ShowPhaseBanner(string header, string phase)
        {
            if (phaseBanner != null)
                yield return phaseBanner.Show(header, phase);
            else
                yield return new WaitForSeconds(phaseAnnounceDuration);
        }

        /// <summary>Run the given action on every alive unit's ConditionManager.</summary>
        private void FireOnAllUnits(System.Action<ConditionManager> action)
        {
            for (int i = 0; i < PlayerUnits.Count; i++)
                if (PlayerUnits[i] != null) action(PlayerUnits[i].conditions);
            for (int i = 0; i < EnemyUnits.Count; i++)
                if (EnemyUnits[i] != null) action(EnemyUnits[i].conditions);
        }

        private void RollInitiative(List<Unit> units)
        {
            turnOrder.Clear();
            var sorted = units.Where(u => u.IsAlive)
                .Select(u =>
                {
                    u.initiativeRoll = DiceRoller.RollInitiative(u.baseSPD);
                    return u;
                })
                .OrderByDescending(u => u.initiativeRoll)
                .ToList();

            foreach (var u in sorted)
                turnOrder.Enqueue(u);
        }

        private IEnumerator ExecutePlayerPhase()
        {
            while (turnOrder.Count > 0)
            {
                var unit = turnOrder.Dequeue();
                if (!unit.IsAlive) continue;

                ActiveUnit = unit;
                unit.ResetTurnState();
                unit.conditions.ClearTurnStartConditions();

                // Fire TurnStart triggers — Poison damage, Plating guard grant,
                // and any custom TurnStart reactions run via their conditions.
                unit.conditions.FireTurnStart();
                if (!unit.IsAlive)
                {
                    CombatEvents.InvokeUnitTurnStart(unit);
                    unit.conditions.FireTurnEnd();
                    CombatEvents.InvokeUnitTurnEnd(unit);
                    if (CheckCombatEnd()) yield break;
                    continue;
                }

                // Check stunned
                if (unit.IsStunned)
                {
                    CombatEvents.InvokeUnitTurnStart(unit);
                    unit.conditions.RemoveCondition(ConditionID.Stunned);
                    yield return new WaitForSeconds(0.5f);
                    CombatEvents.InvokeUnitTurnEnd(unit);
                    continue;
                }

                CombatEvents.InvokeUnitTurnStart(unit);

                // Defect orb start-of-turn sweep: orbs flagged
                // passiveAtTurnStart (Plasma) fire their Passive before the
                // player picks an action. Gated internally by hasOrbSystem.
                OrbManager.OnTurnStart(unit);

                // Wait for player input
                waitingForPlayerInput = true;
                yield return new WaitUntil(() => !waitingForPlayerInput);

                unit.conditions.FireTurnEnd();
                CombatEvents.InvokeUnitTurnEnd(unit);

                if (CheckCombatEnd()) yield break;
            }

            ActiveUnit = null;

            // Defect orb end-of-phase sweep — phase-scoped (after every player
            // has acted) rather than per-turn. Coroutine variant yields between
            // orbs so the player can see each passive resolve. OrbManager
            // internally gates on hasOrbSystem, so this is a no-op for any
            // non-Defect player unit.
            foreach (var pu in PlayerUnits)
                yield return StartCoroutine(OrbManager.OnPhaseEndCoroutine(pu, orbPassiveInterval));
        }

        private IEnumerator ExecuteEnemyPhase()
        {
            while (turnOrder.Count > 0)
            {
                var unit = turnOrder.Dequeue();
                if (!unit.IsAlive) continue;

                ActiveUnit = unit;
                unit.ResetTurnState();
                unit.conditions.ClearTurnStartConditions();

                // Fire TurnStart triggers for the enemy (same as player phase)
                unit.conditions.FireTurnStart();
                if (!unit.IsAlive)
                {
                    CombatEvents.InvokeUnitTurnStart(unit);
                    unit.conditions.FireTurnEnd();
                    CombatEvents.InvokeUnitTurnEnd(unit);
                    if (CheckCombatEnd()) yield break;
                    continue;
                }

                // Check stunned
                if (unit.IsStunned)
                {
                    CombatEvents.InvokeUnitTurnStart(unit);
                    unit.conditions.RemoveCondition(ConditionID.Stunned);
                    yield return new WaitForSeconds(0.5f);
                    unit.conditions.FireTurnEnd();
                    CombatEvents.InvokeUnitTurnEnd(unit);
                    continue;
                }

                CombatEvents.InvokeUnitTurnStart(unit);

                // Play intent pulse animation to telegraph the enemy's action
                var unitDisplay = UnitDisplay.GetDisplay(unit);
                if (unitDisplay != null && unitDisplay.EnemyWorldHUD != null)
                    unitDisplay.EnemyWorldHUD.PlayIntentPulse();

                // Wait so the player can see which enemy is acting and what it intends
                yield return new WaitForSeconds(enemyPreActionDelay);

                // Decide and execute the move (multi-intent).
                var move = EnemyAI.DecideMove(unit);
                UnitDisplay targetDisplay = null;

                if (move != null && move.intents != null && move.intents.Length > 0)
                {
                    // Does this move attack any player? Used to decide whether
                    // to muzzle player-side flash/death/UI updates upfront.
                    bool hasAttackIntent = false;
                    for (int ii = 0; ii < move.intents.Length; ii++)
                    {
                        if (move.intents[ii] != null
                            && move.intents[ii].intentType == EnemyIntentType.Attack)
                        {
                            hasAttackIntent = true;
                            break;
                        }
                    }

                    // Suppress flash / death / HP / condition UI on every
                    // unit the move can touch so we can replay through the
                    // ordered playback contract below. Suppress the attacker
                    // too — crit-miss self-damage flashes here as well.
                    var suppressedDisplays = new List<UnitDisplay>();
                    if (hasAttackIntent)
                    {
                        foreach (var pu in PlayerUnits)
                        {
                            var d = UnitDisplay.GetDisplay(pu);
                            if (d != null)
                            {
                                d.SuppressDamageFlash    = true;
                                d.SuppressDeathAnimation = true;
                                d.SuppressUIUpdates      = true;
                                d.SuppressConditionUI    = true;
                                suppressedDisplays.Add(d);
                            }
                        }
                    }
                    var enemyAttackerDisplay = UnitDisplay.GetDisplay(unit);
                    if (enemyAttackerDisplay != null && !suppressedDisplays.Contains(enemyAttackerDisplay))
                    {
                        enemyAttackerDisplay.SuppressDamageFlash    = true;
                        enemyAttackerDisplay.SuppressDeathAnimation = true;
                        enemyAttackerDisplay.SuppressUIUpdates      = true;
                        enemyAttackerDisplay.SuppressConditionUI    = true;
                        suppressedDisplays.Add(enemyAttackerDisplay);
                    }
                    // Self-targeting buff/debuff intents may apply conditions
                    // to allies — preempt by suppressing every enemy display
                    // too. Cheap; nothing else listens to these flags.
                    foreach (var eu in EnemyUnits)
                    {
                        var d = UnitDisplay.GetDisplay(eu);
                        if (d != null && !suppressedDisplays.Contains(d))
                        {
                            d.SuppressDamageFlash    = true;
                            d.SuppressDeathAnimation = true;
                            d.SuppressUIUpdates      = true;
                            d.SuppressConditionUI    = true;
                            suppressedDisplays.Add(d);
                        }
                    }

                    var results = EnemyAI.ExecuteMove(unit, move, PlayerUnits, EnemyUnits);

                    // Highlight: prefer the first non-self target, fall back
                    // to the acting enemy if the move only self-targets.
                    Unit highlightTarget = null;
                    for (int ii = 0; ii < results.Count; ii++)
                    {
                        if (results[ii] != null && results[ii].target != null
                            && results[ii].target != unit)
                        {
                            highlightTarget = results[ii].target;
                            break;
                        }
                    }
                    if (highlightTarget == null) highlightTarget = unit;

                    targetDisplay = UnitDisplay.GetDisplay(highlightTarget);
                    targetDisplay?.SetHighlighted(true);

                    // Track lunged targets so multi-effect / multi-intent
                    // moves don't lunge twice at the same player.
                    var lungedTargets = new HashSet<Unit>();
                    var deadThisMove = new HashSet<Unit>();
                    bool anyKilled = false;

                    for (int i = 0; i < results.Count; i++)
                    {
                        var r = results[i];
                        if (r == null) continue;
                        if (r.target != null && deadThisMove.Contains(r.target)) continue;

                        // Dice roll feedback (per Attack effect that rolled).
                        if (r.didRoll)
                        {
                            CombatEvents.InvokeDiceRolled(unit, r.rawD20Roll,
                                r.totalAttackRoll, r.didHit, r.wasCrit);
                            yield return new WaitForSeconds(diceRollWaitDuration);
                        }

                        // 1. Lunge — only when the result targets a unit on
                        // the opposite side (skips Self/AllAllies effects
                        // where target is the enemy or another enemy).
                        bool isOffensiveTarget = r.target != null
                                              && r.target.isPlayerControlled != unit.isPlayerControlled;
                        if (isOffensiveTarget && !lungedTargets.Contains(r.target))
                        {
                            lungedTargets.Add(r.target);
                            var attackerD = UnitDisplay.GetDisplay(unit);
                            var victimD   = UnitDisplay.GetDisplay(r.target);
                            if (attackerD != null && victimD != null)
                            {
                                Vector3 dir = victimD.transform.position - attackerD.transform.position;
                                attackerD.PlayAttackAnimation(dir);
                                yield return new WaitForSeconds(attackAnimationWait);
                            }
                        }

                        var rDisplay = UnitDisplay.GetDisplay(r.target);
                        if (rDisplay != null)
                        {
                            // Lift HP / flash on this target. Condition UI
                            // stays suppressed until the condition step below
                            // so the icon paints with its floater.
                            rDisplay.SuppressDamageFlash    = false;
                            rDisplay.SuppressDeathAnimation = false;
                            rDisplay.SuppressUIUpdates      = false;
                        }

                        // Crit-miss self-damage flashes on the enemy after
                        // the lunge. Lift the attacker's flash flag once,
                        // then replay if needed.
                        if (enemyAttackerDisplay != null && enemyAttackerDisplay != rDisplay)
                        {
                            enemyAttackerDisplay.SuppressDamageFlash    = false;
                            enemyAttackerDisplay.SuppressDeathAnimation = false;
                            enemyAttackerDisplay.SuppressUIUpdates      = false;
                        }
                        if (r.wasCritMiss && enemyAttackerDisplay != null)
                            enemyAttackerDisplay.PlayDamageFlash();

                        // 2 + 3 + 4. Damage flash → damage number → HP tween.
                        if (r.didHit && r.damageDealt > 0 && rDisplay != null)
                        {
                            rDisplay.PlayDamageFlash();
                            yield return new WaitForSeconds(postFlashDelay);

                            CombatEvents.InvokeActionResolved(r);
                            if (r.target != null && r.target.isPlayerControlled)
                                CombatEvents.InvokePlayerHit(r.target, r.damageDealt);
                            yield return new WaitForSeconds(postDamageDelay);
                        }
                        else
                        {
                            CombatEvents.InvokeActionResolved(r);
                        }

                        // 5. Death short-circuit per target.
                        if (rDisplay != null && rDisplay.HasPendingDeath)
                        {
                            rDisplay.PlayDeathAnimationImmediate();
                            anyKilled = true;
                            if (r.target != null) deadThisMove.Add(r.target);

                            if (i < results.Count - 1)
                                yield return new WaitForSeconds(effectStaggerDelay);
                            continue;
                        }

                        // 6 + 7. Condition floaters + icon catch-up.
                        if (r.conditionsApplied != null && r.conditionsApplied.Count > 0
                            && r.target != null)
                        {
                            yield return new WaitForSeconds(preConditionDelay);
                            if (rDisplay != null) rDisplay.SuppressConditionUI = false;
                            for (int c = 0; c < r.conditionsApplied.Count; c++)
                            {
                                var (id, stacks) = r.conditionsApplied[c];
                                FloatingNumberManager.Instance?.SpawnConditionApplied(r.target, id, stacks);
                                yield return new WaitForSeconds(conditionFloaterStagger);
                            }
                        }

                        if (i < results.Count - 1)
                            yield return new WaitForSeconds(effectStaggerDelay);
                    }

                    // Hold once for the death sequence + rank compact when
                    // any kill landed this move.
                    if (anyKilled)
                    {
                        yield return new WaitForSeconds(deathSequenceWait);
                        // Compact whichever side took losses. We pick the
                        // first dead target's lineup; multi-side kills are
                        // exotic enough we can revisit if it ever comes up.
                        foreach (var dead in deadThisMove)
                        {
                            if (dead != null)
                            {
                                RankHelper.CompactRanks(GetLineup(dead));
                                break;
                            }
                        }
                    }

                    // Final sweep — clear any flag still up (e.g. on a
                    // target that never reached the condition step, or on
                    // an attacker that didn't enter the per-result loop).
                    foreach (var d in suppressedDisplays)
                    {
                        if (d == null) continue;
                        d.SuppressDamageFlash    = false;
                        d.SuppressDeathAnimation = false;
                        d.SuppressUIUpdates      = false;
                        d.SuppressConditionUI    = false;
                    }
                }

                unit.hasActedThisTurn = true;

                // Clear the conditional-move event tracking now that the
                // enemy has acted — OnConditionApplied / OnConditionRemoved
                // triggers fire once per gap between actions, not once per
                // event in the gap.
                unit.ClearConditionalMoveEventState();

                yield return new WaitForSeconds(enemyActionDelay);

                // Unhighlight target after the action + delay resolves
                targetDisplay?.SetHighlighted(false);

                unit.conditions.FireTurnEnd();
                CombatEvents.InvokeUnitTurnEnd(unit);

                if (CheckCombatEnd()) yield break;

                // Pause between enemies so the player can read the outcome
                if (turnOrder.Count > 0)
                    yield return new WaitForSeconds(enemyTurnGap);
            }

            // Set new intents for next turn
            foreach (var enemy in EnemyUnits)
            {
                if (!enemy.IsAlive) continue;
                SetEnemyIntent(enemy);
            }

            ActiveUnit = null;
        }

        /// <summary>
        /// Pick the next move for an enemy and broadcast it so the HUD can
        /// render the intent icons + tooltip. Called at combat start and at
        /// the end of every enemy phase. Routes through EnemyAI.DecideMove
        /// (turn-1 + conditional overrides considered) so the telegraphed
        /// intent matches what will actually execute.
        /// </summary>
        private void SetEnemyIntent(Unit enemy)
        {
            var move = EnemyAI.DecideMove(enemy);
            if (move != null)
                CombatEvents.InvokeEnemyMoveSet(enemy, move);
        }

        /// <summary>
        /// Apply EnemyData.startingConditions to a freshly spawned enemy.
        /// Runs once at combat start; the standard ApplyCondition path fires
        /// OnConditionApplied (so the HUD icon + floater paint normally).
        /// </summary>
        private static void ApplyStartingConditions(Unit enemy)
        {
            if (enemy == null || enemy.enemyData == null) return;
            var seeds = enemy.enemyData.startingConditions;
            if (seeds == null || seeds.Length == 0) return;

            var lib = ConditionLibrary.Instance;
            if (lib == null) return;

            for (int i = 0; i < seeds.Length; i++)
            {
                var seed = seeds[i];
                if (seed == null || seed.stacks <= 0) continue;
                var data = lib.Get(seed.conditionId);
                if (data == null)
                {
                    Debug.LogWarning(
                        $"[CombatManager] '{enemy.unitName}' starting condition " +
                        $"'{seed.conditionId}' has no entry in ConditionLibrary — " +
                        "create the SO or refresh the library.", enemy.enemyData);
                    continue;
                }
                enemy.conditions.ApplyCondition(data, seed.stacks, null);
            }
        }

        private IEnumerator ExecuteCleanupPhase()
        {
            var allUnits = new List<Unit>();
            allUnits.AddRange(PlayerUnits);
            allUnits.AddRange(EnemyUnits);

            foreach (var unit in allUnits)
            {
                if (!unit.IsAlive) continue;

                // Fire OnCleanup triggers (Regen heals here; future cleanup-timed
                // conditions plug in the same way).
                unit.conditions.FireCleanup();

                // Tick duration-based conditions
                unit.conditions.TickDurations();
            }

            if (CheckCombatEnd()) yield break;

            yield return new WaitForSeconds(0.3f);
        }

        private void OnUnitDied(Unit unit)
        {
            if (!unit.isPlayerControlled)
                CombatEvents.InvokeEnemyDeath(unit);

            // End combat the moment the killing blow lands rather than waiting
            // for the next turn boundary. Idempotent: subsequent CheckCombatEnd
            // calls (from turn-end coroutines) early-return once combat is over.
            CheckCombatEnd();
        }

        private bool CheckCombatEnd()
        {
            // Already-ended latch — keeps multiple call sites safe and lets
            // running coroutines yield-break on their next CheckCombatEnd.
            if (!combatActive) return true;

            bool allEnemiesDead = !EnemyUnits.Any(e => e.IsAlive);
            bool allPlayersDead = !PlayerUnits.Any(p => p.IsAlive);

            if (allEnemiesDead)
            {
                combatActive = false;
                // Unblock any phase coroutine that's waiting on player input
                // so it can yield-break cleanly instead of stalling.
                waitingForPlayerInput = false;
                CurrentPhase = CombatPhase.Victory;
                CombatEvents.InvokePhaseChanged(CurrentPhase);
                FireCombatEndOnAllUnits();
                CombatEvents.InvokeCombatEnd(true);
                return true;
            }

            if (allPlayersDead)
            {
                combatActive = false;
                waitingForPlayerInput = false;
                CurrentPhase = CombatPhase.Defeat;
                CombatEvents.InvokePhaseChanged(CurrentPhase);
                FireCombatEndOnAllUnits();
                CombatEvents.InvokeCombatEnd(false);
                return true;
            }

            return false;
        }

        private void FireCombatEndOnAllUnits()
        {
            foreach (var u in PlayerUnits) u.conditions.FireCombatEnd();
            foreach (var u in EnemyUnits)  u.conditions.FireCombatEnd();
        }

        // --- Player Action Handlers (called by UI) ---

        /// <param name="arrowOrigin">Optional Transform the targeting arrow originates from
        /// (e.g. the attack button). Pass null to use the unit's position.</param>
        public void OnPlayerChooseAttack(Transform arrowOrigin = null)
        {
            if (!waitingForPlayerInput || ActiveUnit == null || isResolving) return;
            if (ActiveUnit.hasActedThisTurn) return; // Attack costs an Action

            // Cancel any active targeting from a previous action selection (hides arrow)
            if (TargetingSystem.Instance != null && TargetingSystem.Instance.IsTargeting)
                TargetingSystem.Instance.CancelTargeting();

            pendingAction = ActionType.Attack;

            int rMin = ActiveUnit.equippedWeapon != null ? ActiveUnit.equippedWeapon.rangeMin : 1;
            int rMax = ActiveUnit.equippedWeapon != null ? ActiveUnit.equippedWeapon.rangeMax : 2;
            TargetingSystem.Instance.BeginTargeting(
                ActiveUnit.equippedWeapon?.targetMode ?? TargetMode.SingleEnemy,
                ActiveUnit,
                OnTargetsConfirmed,
                OnTargetingCancelled,
                arrowOrigin,
                rMin, rMax);
        }

        public void OnPlayerChooseGuard()
        {
            if (!waitingForPlayerInput || ActiveUnit == null || isResolving) return;
            if (ActiveUnit.hasActedThisTurn) return; // Guard costs an Action

            // Cancel any active targeting from a previous action selection (hides arrow)
            if (TargetingSystem.Instance != null && TargetingSystem.Instance.IsTargeting)
                TargetingSystem.Instance.CancelTargeting();

            var lib = ConditionLibrary.Instance;
            var guardData = lib != null ? lib.Get(ConditionID.Guard) : null;
            var result = SkillResolver.ResolveGuard(ActiveUnit, guardData);
            CombatEvents.InvokeActionResolved(result);

            ActiveUnit.hasActedThisTurn = true;
            CombatEvents.InvokeActionStateChanged(ActiveUnit);
        }

        /// <param name="arrowOrigin">Optional Transform the targeting arrow originates from
        /// (e.g. the skill card). Pass null to use the unit's position.</param>
        public void OnPlayerChooseSkill(int skillIndex, Transform arrowOrigin = null)
        {
            if (!waitingForPlayerInput || ActiveUnit == null || isResolving) return;
            if (skillIndex < 0 || skillIndex >= ActiveUnit.equippedSkills.Count) return;

            var skill = ActiveUnit.equippedSkills[skillIndex];
            if (!ActiveUnit.CanUseSkill(skill)) return;

            // Cancel any active targeting from a previous action selection (hides arrow)
            if (TargetingSystem.Instance != null && TargetingSystem.Instance.IsTargeting)
                TargetingSystem.Instance.CancelTargeting();

            // Check if the required action type is still available
            bool costAvailable = skill.actionCostType == ActionCostType.FreeAction
                ? ActiveUnit.HasFreeActionAvailable
                : ActiveUnit.HasActionAvailable;
            if (!costAvailable) return;

            pendingAction = ActionType.Skill;
            pendingSkill = skill;

            TargetingSystem.Instance.BeginTargeting(
                skill.primaryTargetMode,
                ActiveUnit,
                OnTargetsConfirmed,
                OnTargetingCancelled,
                arrowOrigin,
                skill.rangeMin, skill.rangeMax,
                skill.targetPickCount);
        }

        /// <summary>
        /// Player picked an item from the Items submenu. Mirrors
        /// OnPlayerChooseSkill but routes through the item refusal gate
        /// (Immobilized + ItemActionCostType-aware) and remembers the
        /// (source, index) pair so Inventory.Consume can decrement the right
        /// list after resolution.
        /// </summary>
        /// <param name="source">Party bag (shared) or Pouch (active unit's pouch).</param>
        /// <param name="index">Index into the source list at the moment of click.</param>
        /// <param name="arrowOrigin">Optional Transform the targeting arrow draws from.</param>
        public void OnPlayerChooseItem(ItemSource source, int index, Transform arrowOrigin = null)
        {
            if (!waitingForPlayerInput || ActiveUnit == null || isResolving) return;

            var inst = Inventory.Resolve(ActiveUnit, source, index);
            if (inst == null) return;
            var item = inst.Resolve();
            if (item == null)
            {
                Debug.LogWarning(
                    $"[CombatManager] OnPlayerChooseItem: ItemInstance with " +
                    $"id={inst.itemID} couldn't resolve to ItemData via " +
                    $"ItemLibrary. Asset missing or library not refreshed.");
                return;
            }

            if (!ActiveUnit.CanUseItem(item)) return;

            // Cancel any active targeting from a previous selection (hides arrow)
            if (TargetingSystem.Instance != null && TargetingSystem.Instance.IsTargeting)
                TargetingSystem.Instance.CancelTargeting();

            pendingAction      = ActionType.Item;
            pendingItem        = item;
            pendingItemSource  = source;
            pendingItemIndex   = index;

            TargetingSystem.Instance.BeginTargeting(
                item.primaryTargetMode,
                ActiveUnit,
                OnTargetsConfirmed,
                OnTargetingCancelled,
                arrowOrigin,
                rangeMin: 0, rangeMax: 0,
                item.targetPickCount);
        }

        /// <summary>
        /// End the current player's turn. This is the ONLY way to advance past
        /// the waitingForPlayerInput block. Called by EndTurnButtonUI.
        /// </summary>
        public void OnPlayerEndTurn()
        {
            if (!waitingForPlayerInput || ActiveUnit == null || isResolving) return;

            // Cancel any active targeting (hides arrow)
            if (TargetingSystem.Instance != null && TargetingSystem.Instance.IsTargeting)
                TargetingSystem.Instance.CancelTargeting();

            waitingForPlayerInput = false;
        }

        /// <summary>
        /// Move action: shift the active player by 1 rank. direction = -1 advances
        /// (toward front), +1 withdraws (toward back). Full-turn cost — consumes
        /// the unit's Action. Intervening allies shift by 1 to make room.
        /// </summary>
        public void OnPlayerChooseMove(int direction)
        {
            if (!waitingForPlayerInput || ActiveUnit == null || isResolving) return;
            if (ActiveUnit.hasActedThisTurn) return;

            // Cancel any active targeting
            if (TargetingSystem.Instance != null && TargetingSystem.Instance.IsTargeting)
                TargetingSystem.Instance.CancelTargeting();

            int steps = direction < 0 ? 1 : direction > 0 ? 1 : 0;
            if (steps == 0) return;

            int newRank = direction < 0
                ? RankHelper.Advance(GetLineup(ActiveUnit), ActiveUnit, 1)
                : RankHelper.Withdraw(GetLineup(ActiveUnit), ActiveUnit, 1);

            // Fire an ActionResolved so the log picks it up
            var result = new CombatActionResult
            {
                source = ActiveUnit,
                target = ActiveUnit,
                actionType = ActionType.Move,
                actionName = direction < 0 ? "Advance" : "Withdraw",
            };
            CombatEvents.InvokeActionResolved(result);

            ActiveUnit.hasActedThisTurn = true;
            CombatEvents.InvokeActionStateChanged(ActiveUnit);
        }

        /// <summary>
        /// Pass action: consume the Action without doing anything. Free Action
        /// remains available. Equivalent to skipping a beat mid-round.
        /// </summary>
        public void OnPlayerChoosePass()
        {
            if (!waitingForPlayerInput || ActiveUnit == null || isResolving) return;
            if (ActiveUnit.hasActedThisTurn) return;

            if (TargetingSystem.Instance != null && TargetingSystem.Instance.IsTargeting)
                TargetingSystem.Instance.CancelTargeting();

            var result = new CombatActionResult
            {
                source = ActiveUnit,
                target = ActiveUnit,
                actionType = ActionType.Pass,
                actionName = "Pass",
            };
            CombatEvents.InvokeActionResolved(result);

            ActiveUnit.hasActedThisTurn = true;
            CombatEvents.InvokeActionStateChanged(ActiveUnit);
        }

        /// <summary>
        /// Flee action: abandon combat. Stubbed for Pass 2.B — fires CombatEnd
        /// with victory=false and phase=Defeat. When the exploration layer is
        /// wired up this should transition to a distinct Fled phase and put the
        /// party back at the floor's start tile instead of a full game-over.
        /// </summary>
        public void OnPlayerChooseFlee()
        {
            if (!waitingForPlayerInput || ActiveUnit == null || isResolving) return;
            UnityEngine.Debug.Log("[CombatManager] Flee requested — ending combat " +
                "(Defeat phase as a stub; replace with Fled phase later).");
            combatActive = false;
            CurrentPhase = CombatPhase.Defeat;
            CombatEvents.InvokePhaseChanged(CurrentPhase);
            CombatEvents.InvokeCombatEnd(false);
            waitingForPlayerInput = false;
        }

        /// <summary>
        /// Returns the side-lineup list for the given unit. Used by the rank
        /// system when a unit moves — only their own side shifts.
        /// </summary>
        public List<Unit> GetLineup(Unit unit)
            => unit != null && unit.isPlayerControlled ? PlayerUnits : EnemyUnits;

        /// <summary>
        /// Returns the world-space Transform for the given rank on the given side.
        /// UnitDisplay uses this to lerp to its new position when OnRankChanged fires.
        /// </summary>
        public Transform GetSpawnPositionForRank(bool isPlayer, int rank)
        {
            var arr = isPlayer ? playerPositions : enemyPositions;
            int idx = Mathf.Clamp(rank - 1, 0, arr.Length - 1);
            return arr.Length > 0 ? arr[idx] : null;
        }

        private void OnTargetsConfirmed(List<Unit> targets)
        {
            if (ActiveUnit == null) return;
            StartCoroutine(ResolvePlayerActionCoroutine(targets));
        }

        /// <summary>
        /// Coroutine that resolves the player's action with proper timing:
        /// dice roll animation plays → wait → THEN show damage/effects → update UI.
        /// Blocks further input until resolution completes.
        /// </summary>
        private IEnumerator ResolvePlayerActionCoroutine(List<Unit> targets)
        {
            isResolving = true;

            // Highlight all targets with corner brackets (covers auto-target with 1 enemy)
            var highlightedDisplays = new List<UnitDisplay>();
            foreach (var t in targets)
            {
                var display = UnitDisplay.GetDisplay(t);
                if (display != null)
                {
                    display.SetHighlighted(true);
                    highlightedDisplays.Add(display);
                }
            }

            switch (pendingAction)
            {
                case ActionType.Attack:
                    // Suppress damage flash and death on BOTH the target and
                    // the attacker so neither plays before dice + lunge.
                    // Crit-miss self-damage on the attacker would otherwise
                    // flash the player red before the animation runs.
                    var targetVictimDisplay = UnitDisplay.GetDisplay(targets[0]);
                    var attackerSelfDisplay = UnitDisplay.GetDisplay(ActiveUnit);
                    if (targetVictimDisplay != null)
                    {
                        targetVictimDisplay.SuppressDamageFlash = true;
                        targetVictimDisplay.SuppressDeathAnimation = true;
                        // SuppressUIUpdates also defers HP / DEF / condition
                        // refreshes on the world HUD so the bar doesn't move
                        // before the lunge plays.
                        targetVictimDisplay.SuppressUIUpdates = true;
                        // SuppressConditionUI muzzles the auto event-
                        // driven popup for weapon on-hit conditions; replayed
                        // after damage feedback below.
                        targetVictimDisplay.SuppressConditionUI = true;
                    }
                    if (attackerSelfDisplay != null)
                    {
                        attackerSelfDisplay.SuppressDamageFlash = true;
                        attackerSelfDisplay.SuppressDeathAnimation = true;
                        attackerSelfDisplay.SuppressUIUpdates = true;
                        attackerSelfDisplay.SuppressConditionUI = true;
                    }

                    var attackResult = SkillResolver.ResolveWeaponAttack(ActiveUnit, targets[0]);

                    // 1) Dice roll animation → wait for roll to finish
                    if (attackResult.didRoll)
                    {
                        CombatEvents.InvokeDiceRolled(ActiveUnit, attackResult.rawD20Roll,
                            attackResult.totalAttackRoll, attackResult.didHit, attackResult.wasCrit);
                        yield return new WaitForSeconds(diceRollWaitDuration);
                    }

                    // 2) Play attacker's lunge animation toward the target
                    {
                        var attackerDisplay = UnitDisplay.GetDisplay(ActiveUnit);
                        var targetDisplay = UnitDisplay.GetDisplay(targets[0]);
                        if (attackerDisplay != null && targetDisplay != null)
                        {
                            Vector3 dir = targetDisplay.transform.position - attackerDisplay.transform.position;
                            attackerDisplay.PlayAttackAnimation(dir);
                            yield return new WaitForSeconds(attackAnimationWait);
                        }
                    }

                    // 3) Lift HP / flash suppression so the bar tween + flash
                    // paint with this hit. Condition UI stays suppressed
                    // until the condition step below.
                    if (targetVictimDisplay != null)
                    {
                        targetVictimDisplay.SuppressDamageFlash    = false;
                        targetVictimDisplay.SuppressDeathAnimation = false;
                        targetVictimDisplay.SuppressUIUpdates      = false;
                    }
                    if (attackerSelfDisplay != null)
                    {
                        attackerSelfDisplay.SuppressDamageFlash    = false;
                        attackerSelfDisplay.SuppressDeathAnimation = false;
                        attackerSelfDisplay.SuppressUIUpdates      = false;
                        // Caster condition UI lifts immediately (no per-effect
                        // playback for caster-side UI in a basic attack).
                        attackerSelfDisplay.SuppressConditionUI    = false;
                        if (attackResult.wasCritMiss)
                            attackerSelfDisplay.PlayDamageFlash();
                    }

                    // 4) Damage flash → damage number → HP tween, with beats.
                    if (attackResult.didHit && attackResult.damageDealt > 0 && targetVictimDisplay != null)
                    {
                        targetVictimDisplay.PlayDamageFlash();
                        yield return new WaitForSeconds(postFlashDelay);

                        CombatEvents.InvokeActionResolved(attackResult);
                        CombatEvents.InvokePlayerAttack(attackResult);
                        yield return new WaitForSeconds(postDamageDelay);
                    }
                    else
                    {
                        CombatEvents.InvokeActionResolved(attackResult);
                        CombatEvents.InvokePlayerAttack(attackResult);
                    }

                    // 5) Death short-circuit, else 6) condition floaters.
                    bool killedTarget = targetVictimDisplay != null && targetVictimDisplay.HasPendingDeath;
                    if (killedTarget)
                    {
                        targetVictimDisplay.PlayDeathAnimationImmediate();
                        // Wait for the HUD fade + sprite drop to read before
                        // continuing. CheckCombatEnd in the outer loop fires
                        // after this yield, so combat-end (and the return
                        // delay) only kick in after the death is on screen.
                        yield return new WaitForSeconds(deathSequenceWait);
                        // Compact ranks: front-most rank stays at MinRank,
                        // survivors slide forward to fill the gap.
                        if (targets[0] != null) RankHelper.CompactRanks(GetLineup(targets[0]));
                    }
                    else if (attackResult.conditionsApplied != null
                             && attackResult.conditionsApplied.Count > 0
                             && attackResult.target != null)
                    {
                        yield return new WaitForSeconds(preConditionDelay);
                        if (targetVictimDisplay != null)
                            targetVictimDisplay.SuppressConditionUI = false;
                        for (int c = 0; c < attackResult.conditionsApplied.Count; c++)
                        {
                            var (id, stacks) = attackResult.conditionsApplied[c];
                            FloatingNumberManager.Instance?.SpawnConditionApplied(attackResult.target, id, stacks);
                            yield return new WaitForSeconds(conditionFloaterStagger);
                        }
                    }

                    // Fallback unsuppress so the flag never stays stuck if
                    // this attack didn't enter the condition-floater branch
                    // (no on-hit condition, kill, etc.).
                    if (targetVictimDisplay != null)
                        targetVictimDisplay.SuppressConditionUI = false;

                    // Orb-channel-on-attack (Defect's WPN_OrbBeam). Auto-picks
                    // a random Tier 1 orb (Lightning or Frost, 50/50) and
                    // channels it. Fires regardless of whether the weapon hit
                    // landed — the orb engine must keep running.
                    if (ActiveUnit.equippedWeapon != null
                        && ActiveUnit.equippedWeapon.channelOrbOnAttack)
                    {
                        var orbLib = OrbLibrary.Instance;
                        if (orbLib != null)
                        {
                            var orb = orbLib.GetRandomByTier(1);
                            if (orb != null) OrbManager.Channel(ActiveUnit, orb);
                        }
                    }

                    ActiveUnit.hasActedThisTurn = true;
                    break;

                case ActionType.Skill:
                    if (pendingSkill != null)
                    {
                        // Suppress damage flash and death on all targets AND
                        // on the caster — crit-miss self-damage on the caster
                        // would otherwise flash before the animation runs.
                        // SuppressConditionUI muzzles the auto event-
                        // driven condition popups that fire synchronously
                        // inside SkillResolver; the per-result loop replays
                        // them at the right beat.
                        var suppressedDisplays = new List<UnitDisplay>();
                        foreach (var t in targets)
                        {
                            var d = UnitDisplay.GetDisplay(t);
                            if (d != null)
                            {
                                d.SuppressDamageFlash = true;
                                d.SuppressDeathAnimation = true;
                                d.SuppressUIUpdates = true;
                                d.SuppressConditionUI = true;
                                suppressedDisplays.Add(d);
                            }
                        }
                        var skillCasterDisplay = UnitDisplay.GetDisplay(ActiveUnit);
                        if (skillCasterDisplay != null && !suppressedDisplays.Contains(skillCasterDisplay))
                        {
                            skillCasterDisplay.SuppressDamageFlash = true;
                            skillCasterDisplay.SuppressDeathAnimation = true;
                            skillCasterDisplay.SuppressUIUpdates = true;
                            skillCasterDisplay.SuppressConditionUI = true;
                            suppressedDisplays.Add(skillCasterDisplay);
                        }

                        var results = SkillResolver.ResolveSkill(
                            ActiveUnit, pendingSkill, targets,
                            PlayerUnits, EnemyUnits, null);

                        // Track targets we've already lunged toward so multi-effect
                        // skills (e.g. "Deal 8 damage AND apply Vulnerable") don't
                        // cause the attacker to lunge twice at the same target.
                        var lungedTargets = new HashSet<Unit>();

                        // Track whether ANY target died during the skill so we
                        // can hold for the death sequence + compact ranks once
                        // at the end (a multi-kill skill shouldn't stack the
                        // wait per-kill).
                        bool anyKilled = false;

                        // Once a target dies mid-skill we skip any later
                        // results that still address it. SkillResolver
                        // already filters dead targets at resolve time, but
                        // pre-built results from earlier effects can still
                        // list them — short-circuit visually here.
                        var deadThisSkill = new HashSet<Unit>();

                        for (int i = 0; i < results.Count; i++)
                        {
                            var r = results[i];

                            if (r.target != null && deadThisSkill.Contains(r.target))
                                continue;

                            // Dice roll + wait (for skills that roll to hit)
                            if (r.didRoll)
                            {
                                CombatEvents.InvokeDiceRolled(ActiveUnit, r.rawD20Roll,
                                    r.totalAttackRoll, r.didHit, r.wasCrit);
                                yield return new WaitForSeconds(diceRollWaitDuration);
                            }

                            // 1. Lunge — only once per unique target.
                            if (r.target != null && !lungedTargets.Contains(r.target))
                            {
                                lungedTargets.Add(r.target);
                                var attackerDisplay = UnitDisplay.GetDisplay(ActiveUnit);
                                var targetDisplay = UnitDisplay.GetDisplay(r.target);
                                if (attackerDisplay != null && targetDisplay != null)
                                {
                                    Vector3 dir = targetDisplay.transform.position - attackerDisplay.transform.position;
                                    attackerDisplay.PlayAttackAnimation(dir);
                                    yield return new WaitForSeconds(attackAnimationWait);
                                }
                            }

                            var rDisplay = UnitDisplay.GetDisplay(r.target);
                            if (rDisplay != null)
                            {
                                // Lift HP / flash suppression so the bar
                                // tween + damage flash paint with this
                                // result. Condition UI stays suppressed
                                // until the condition step below — the icon
                                // should appear with its floater, not with
                                // the HP drop.
                                rDisplay.SuppressDamageFlash    = false;
                                rDisplay.SuppressDeathAnimation = false;
                                rDisplay.SuppressUIUpdates      = false;
                            }

                            // 2 + 3 + 4. Damage flash → damage number → HP tween.
                            if (r.didHit && r.damageDealt > 0 && rDisplay != null)
                            {
                                rDisplay.PlayDamageFlash();
                                yield return new WaitForSeconds(postFlashDelay);

                                CombatEvents.InvokeActionResolved(r);
                                CombatEvents.InvokePlayerSkillUsed(r);
                                yield return new WaitForSeconds(postDamageDelay);
                            }
                            else
                            {
                                CombatEvents.InvokeActionResolved(r);
                                CombatEvents.InvokePlayerSkillUsed(r);
                            }

                            // 5. Death short-circuit.
                            if (rDisplay != null && rDisplay.HasPendingDeath)
                            {
                                rDisplay.PlayDeathAnimationImmediate();
                                anyKilled = true;
                                if (r.target != null) deadThisSkill.Add(r.target);

                                if (i < results.Count - 1)
                                    yield return new WaitForSeconds(effectStaggerDelay);
                                continue;
                            }

                            // 6 + 7. Condition floaters + icon catch-up.
                            // Lifting SuppressConditionUI here fires
                            // OnConditionUISuppressionLifted, which triggers
                            // the HUD's RebuildConditions in the same beat
                            // as the first condition floater spawn.
                            if (r.conditionsApplied != null && r.conditionsApplied.Count > 0)
                            {
                                yield return new WaitForSeconds(preConditionDelay);
                                if (rDisplay != null) rDisplay.SuppressConditionUI = false;
                                for (int c = 0; c < r.conditionsApplied.Count; c++)
                                {
                                    var (id, stacks) = r.conditionsApplied[c];
                                    FloatingNumberManager.Instance?.SpawnConditionApplied(r.target, id, stacks);
                                    yield return new WaitForSeconds(conditionFloaterStagger);
                                }
                            }

                            if (i < results.Count - 1)
                                yield return new WaitForSeconds(effectStaggerDelay);
                        }

                        // If the skill killed at least one target, hold for the
                        // death sequence (HUD fade + sprite drop) and compact
                        // ranks before continuing. One wait covers all kills
                        // from a multi-target skill — they drop simultaneously.
                        if (anyKilled)
                        {
                            yield return new WaitForSeconds(deathSequenceWait);
                            if (targets != null && targets.Count > 0 && targets[0] != null)
                                RankHelper.CompactRanks(GetLineup(targets[0]));
                        }

                        // Final sweep — any display still in the suppressed
                        // set (most importantly the caster, which the per-
                        // result loop above doesn't visit unless the skill
                        // self-targets) gets cleared here. Lifting
                        // SuppressUIUpdates triggers the world HUD's
                        // catch-up refresh.
                        foreach (var d in suppressedDisplays)
                        {
                            if (d == null) continue;
                            d.SuppressDamageFlash       = false;
                            d.SuppressDeathAnimation    = false;
                            d.SuppressUIUpdates         = false;
                            d.SuppressConditionUI = false;
                        }

                        // Consume the matching action type
                        if (pendingSkill.actionCostType == ActionCostType.FreeAction)
                            ActiveUnit.hasFreeActedThisTurn = true;
                        else
                            ActiveUnit.hasActedThisTurn = true;
                    }
                    break;

                case ActionType.Item:
                    if (pendingItem != null)
                    {
                        // Same display-suppression setup as the Skill case —
                        // the item playback loop reuses the skill playback
                        // structure (lunge/flash/floaters), so it needs the
                        // same suppressed-display contract.
                        var suppressedDisplays = new List<UnitDisplay>();
                        foreach (var t in targets)
                        {
                            var d = UnitDisplay.GetDisplay(t);
                            if (d != null)
                            {
                                d.SuppressDamageFlash = true;
                                d.SuppressDeathAnimation = true;
                                d.SuppressUIUpdates = true;
                                d.SuppressConditionUI = true;
                                suppressedDisplays.Add(d);
                            }
                        }
                        var itemUserDisplay = UnitDisplay.GetDisplay(ActiveUnit);
                        if (itemUserDisplay != null && !suppressedDisplays.Contains(itemUserDisplay))
                        {
                            itemUserDisplay.SuppressDamageFlash = true;
                            itemUserDisplay.SuppressDeathAnimation = true;
                            itemUserDisplay.SuppressUIUpdates = true;
                            itemUserDisplay.SuppressConditionUI = true;
                            suppressedDisplays.Add(itemUserDisplay);
                        }

                        var itemResults = SkillResolver.ResolveItem(
                            ActiveUnit, pendingItem, targets,
                            PlayerUnits, EnemyUnits);

                        var lungedTargets = new HashSet<Unit>();
                        bool anyKilled = false;
                        var deadThisItem = new HashSet<Unit>();

                        for (int i = 0; i < itemResults.Count; i++)
                        {
                            var r = itemResults[i];
                            if (r.target != null && deadThisItem.Contains(r.target)) continue;

                            if (r.didRoll)
                            {
                                CombatEvents.InvokeDiceRolled(ActiveUnit, r.rawD20Roll,
                                    r.totalAttackRoll, r.didHit, r.wasCrit);
                                yield return new WaitForSeconds(diceRollWaitDuration);
                            }

                            if (r.target != null && !lungedTargets.Contains(r.target))
                            {
                                lungedTargets.Add(r.target);
                                var userDisplay = UnitDisplay.GetDisplay(ActiveUnit);
                                var targetDisplay = UnitDisplay.GetDisplay(r.target);
                                if (userDisplay != null && targetDisplay != null
                                    && r.target != ActiveUnit)
                                {
                                    Vector3 dir = targetDisplay.transform.position - userDisplay.transform.position;
                                    userDisplay.PlayAttackAnimation(dir);
                                    yield return new WaitForSeconds(attackAnimationWait);
                                }
                            }

                            var rDisplay = UnitDisplay.GetDisplay(r.target);
                            if (rDisplay != null)
                            {
                                rDisplay.SuppressDamageFlash    = false;
                                rDisplay.SuppressDeathAnimation = false;
                                rDisplay.SuppressUIUpdates      = false;
                            }

                            if (r.didHit && r.damageDealt > 0 && rDisplay != null)
                            {
                                rDisplay.PlayDamageFlash();
                                yield return new WaitForSeconds(postFlashDelay);
                                CombatEvents.InvokeActionResolved(r);
                                yield return new WaitForSeconds(postDamageDelay);
                            }
                            else
                            {
                                CombatEvents.InvokeActionResolved(r);
                            }

                            if (rDisplay != null && rDisplay.HasPendingDeath)
                            {
                                rDisplay.PlayDeathAnimationImmediate();
                                anyKilled = true;
                                if (r.target != null) deadThisItem.Add(r.target);

                                if (i < itemResults.Count - 1)
                                    yield return new WaitForSeconds(effectStaggerDelay);
                                continue;
                            }

                            if (r.conditionsApplied != null && r.conditionsApplied.Count > 0)
                            {
                                yield return new WaitForSeconds(preConditionDelay);
                                if (rDisplay != null) rDisplay.SuppressConditionUI = false;
                                for (int c = 0; c < r.conditionsApplied.Count; c++)
                                {
                                    var (id, stacks) = r.conditionsApplied[c];
                                    FloatingNumberManager.Instance?.SpawnConditionApplied(r.target, id, stacks);
                                    yield return new WaitForSeconds(conditionFloaterStagger);
                                }
                            }

                            if (i < itemResults.Count - 1)
                                yield return new WaitForSeconds(effectStaggerDelay);
                        }

                        if (anyKilled)
                        {
                            yield return new WaitForSeconds(deathSequenceWait);
                            if (targets != null && targets.Count > 0 && targets[0] != null)
                                RankHelper.CompactRanks(GetLineup(targets[0]));
                        }

                        // Final suppression sweep — same as Skill case.
                        foreach (var d in suppressedDisplays)
                        {
                            if (d == null) continue;
                            d.SuppressDamageFlash    = false;
                            d.SuppressDeathAnimation = false;
                            d.SuppressUIUpdates      = false;
                            d.SuppressConditionUI    = false;
                        }

                        // Action-cost consumption — ZeroCost bypasses both
                        // gates, FreeAction sets hasFreeActedThisTurn,
                        // Action sets hasActedThisTurn.
                        switch (pendingItem.actionCostType)
                        {
                            case ItemActionCostType.Action:
                                ActiveUnit.hasActedThisTurn = true;
                                break;
                            case ItemActionCostType.FreeAction:
                                ActiveUnit.hasFreeActedThisTurn = true;
                                break;
                            case ItemActionCostType.ZeroCost:
                                // No turn-economy state change.
                                break;
                        }

                        // Consume one charge from the source slot.
                        if (pendingItem.consumedOnUse)
                        {
                            Inventory.Consume(ActiveUnit, pendingItemSource, pendingItemIndex);
                        }
                    }
                    break;
            }

            // Unhighlight all targets after action resolves
            foreach (var display in highlightedDisplays)
                display.SetHighlighted(false);

            pendingSkill = null;
            pendingItem  = null;
            CombatEvents.InvokeActionStateChanged(ActiveUnit);
            isResolving = false;
            // Turn does NOT end here — player must click End Turn
        }

        private void OnTargetingCancelled()
        {
            // Was the player targeting from the skill or item submenu?
            bool wasSkill = pendingAction == ActionType.Skill;
            bool wasItem  = pendingAction == ActionType.Item;
            pendingSkill = null;
            pendingItem  = null;

            if (wasSkill || wasItem)
            {
                // Stay in the submenu — just repopulate to un-grey the selected card.
                // The submenu's HandleTargetingEnded already clears the drawn card state.
                // Reuses the SkillTargetingCancelled event for both — submenus
                // listen to it to close themselves; ItemSubmenuUI subscribes to
                // the same event.
                CombatEvents.InvokeSkillTargetingCancelled(ActiveUnit);
            }
            else
            {
                // Non-skill targeting (e.g. attack) — restore the ActionPanel
                CombatEvents.InvokeActionStateChanged(ActiveUnit);
            }
        }

        // --- Queries ---
        public List<Unit> GetAliveEnemies() => EnemyUnits.Where(e => e.IsAlive).ToList();
        public List<Unit> GetAlivePlayerUnits() => PlayerUnits.Where(p => p.IsAlive).ToList();
        /// <summary>Thin wrapper around ConditionLibrary.Instance.Get — kept for
        /// call-site convenience. Returns null if the library doesn't have
        /// the ID registered.</summary>
        public ConditionData GetConditionData(ConditionID id)
        {
            var lib = ConditionLibrary.Instance;
            return lib != null ? lib.Get(id) : null;
        }

        // --- Editor Gizmos ---
        // Visualizes the rank layout in the Scene view so designers can tell at
        // a glance which Transforms correspond to rank 1 (front, touching the
        // midline) vs rank 4 (back). Front-rank markers are drawn larger.
        private void OnDrawGizmos()
        {
            DrawRankGizmos(playerPositions, new Color(0.30f, 0.55f, 1.00f, 0.85f));
            DrawRankGizmos(enemyPositions,  new Color(1.00f, 0.30f, 0.30f, 0.85f));
        }

        private static void DrawRankGizmos(Transform[] positions, Color color)
        {
            if (positions == null) return;
            for (int i = 0; i < positions.Length; i++)
            {
                var t = positions[i];
                if (t == null) continue;
                Gizmos.color = color;
                // Front rank (index 0) is the biggest; back rank the smallest.
                float size = Mathf.Lerp(0.55f, 0.25f, (float)i / Mathf.Max(1, positions.Length - 1));
                Gizmos.DrawWireCube(t.position, new Vector3(size, size * 2f, 0f));
                Gizmos.DrawSphere(t.position + Vector3.up * 1.1f, 0.06f);
#if UNITY_EDITOR
                UnityEditor.Handles.color = color;
                UnityEditor.Handles.Label(t.position + Vector3.up * 1.25f, $"R{i + 1}");
#endif
            }
        }
    }
}
