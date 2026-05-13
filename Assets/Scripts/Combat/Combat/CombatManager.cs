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

        public CombatPhase CurrentPhase { get; private set; }
        public int TurnNumber { get; private set; }
        public List<Unit> PlayerUnits { get; private set; } = new();
        public List<Unit> EnemyUnits { get; private set; } = new();
        public Unit ActiveUnit { get; private set; }

        private Queue<Unit> turnOrder = new();
        private bool waitingForPlayerInput;
        private bool combatActive;
        private bool isResolving; // True while dice/damage animation is playing

        private ActionType pendingAction;
        private SkillData pendingSkill;
        private ItemData pendingItem;
        private ItemSource pendingItemSource;
        private int pendingItemIndex;

        public ActionType PendingAction => pendingAction;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            CombatEvents.ClearAll();
        }

        public void InitializeCombat(CharacterData[] party, EncounterSO encounter)
        {
            PlayerUnits.Clear();
            for (int i = 0; i < party.Length && i < playerPositions.Length; i++)
            {
                if (party[i] == null) continue;
                var unit = new Unit(party[i]);
                unit.SetRank(i + 1);
                PlayerUnits.Add(unit);
                SpawnUnitDisplay(unit, playerPositions[i], false);
            }

            EnemyUnits.Clear();
            int fixedCount = encounter.fixedEnemies != null ? encounter.fixedEnemies.Length : 0;
            int extraRoll = (encounter.possibleEnemies != null && encounter.possibleEnemies.Length > 0)
                ? Random.Range(encounter.minEnemies, encounter.maxEnemies + 1)
                : 0;
            int totalEnemies = Mathf.Min(fixedCount + extraRoll, enemyPositions.Length);
            for (int i = 0; i < totalEnemies; i++)
            {
                EnemyData enemyData = i < fixedCount
                    ? encounter.fixedEnemies[i]
                    : encounter.possibleEnemies[Random.Range(0, encounter.possibleEnemies.Length)];
                if (enemyData == null) continue;
                var unit = new Unit(enemyData);
                unit.SetRank(i + 1);
                EnemyUnits.Add(unit);
                SpawnUnitDisplay(unit, enemyPositions[i], true);

                int index = i;
                unit.OnDeath += () => OnUnitDied(unit);
            }

            foreach (var pu in PlayerUnits)
            {
                var captured = pu;
                pu.OnDeath += () => OnUnitDied(captured);
            }

            if (TargetingSystem.Instance != null)
                TargetingSystem.Instance.SetUnitLists(PlayerUnits, EnemyUnits);

            foreach (var enemy in EnemyUnits)
                ApplyStartingConditions(enemy);

            foreach (var enemy in EnemyUnits)
                enemy.ClearConditionalMoveEventState();

            foreach (var enemy in EnemyUnits)
            {
                SetEnemyIntent(enemy);
            }

            TurnNumber = 0;
            combatActive = true;

            CombatEvents.InvokeCombatStart();

            foreach (var u in PlayerUnits) u.conditions.FireCombatStart();
            foreach (var u in EnemyUnits)  u.conditions.FireCombatStart();

            foreach (var u in PlayerUnits)
            {
                if (u.characterData != null && u.characterData.hasOrbSystem
                    && u.characterData.startingOrb != null)
                {
                    OrbManager.Channel(u, u.characterData.startingOrb);
                }
            }

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

                FireOnAllUnits(c => c.ClearByTiming(ClearTiming.RoundStart));
                FireOnAllUnits(c => c.FireRoundStart());
                CombatEvents.InvokeRoundStart(TurnNumber);

                CombatEvents.InvokeTurnStart(TurnNumber);

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

                CurrentPhase = CombatPhase.CleanupPhase;
                CombatEvents.InvokePhaseChanged(CurrentPhase);
                yield return ExecuteCleanupPhase();
                if (!combatActive) yield break;

                FireOnAllUnits(c => c.ClearByTiming(ClearTiming.RoundEnd));
                FireOnAllUnits(c => c.FireRoundEnd());
                CombatEvents.InvokeRoundEnd(TurnNumber);

                CombatEvents.InvokeTurnEnd(TurnNumber);
            }
        }

        private IEnumerator RunCombatIntro()
        {
            yield return new WaitForSeconds(introBarsLeadIn);

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

        private IEnumerator ShowPhaseBanner(string header, string phase)
        {
            if (phaseBanner != null)
                yield return phaseBanner.Show(header, phase);
            else
                yield return new WaitForSeconds(phaseAnnounceDuration);
        }

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

                unit.conditions.FireTurnStart();
                if (!unit.IsAlive)
                {
                    CombatEvents.InvokeUnitTurnStart(unit);
                    unit.conditions.FireTurnEnd();
                    CombatEvents.InvokeUnitTurnEnd(unit);
                    if (CheckCombatEnd()) yield break;
                    continue;
                }

                if (unit.IsStunned)
                {
                    CombatEvents.InvokeUnitTurnStart(unit);
                    unit.conditions.RemoveCondition(ConditionID.Stunned);
                    yield return new WaitForSeconds(0.5f);
                    CombatEvents.InvokeUnitTurnEnd(unit);
                    continue;
                }

                CombatEvents.InvokeUnitTurnStart(unit);

                OrbManager.OnTurnStart(unit);

                waitingForPlayerInput = true;
                yield return new WaitUntil(() => !waitingForPlayerInput);

                unit.conditions.FireTurnEnd();
                CombatEvents.InvokeUnitTurnEnd(unit);

                if (CheckCombatEnd()) yield break;
            }

            ActiveUnit = null;

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

                unit.conditions.FireTurnStart();
                if (!unit.IsAlive)
                {
                    CombatEvents.InvokeUnitTurnStart(unit);
                    unit.conditions.FireTurnEnd();
                    CombatEvents.InvokeUnitTurnEnd(unit);
                    if (CheckCombatEnd()) yield break;
                    continue;
                }

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

                var unitDisplay = UnitDisplay.GetDisplay(unit);
                if (unitDisplay != null && unitDisplay.EnemyWorldHUD != null)
                    unitDisplay.EnemyWorldHUD.PlayIntentPulse();

                yield return new WaitForSeconds(enemyPreActionDelay);

                var move = EnemyAI.DecideMove(unit);
                UnitDisplay targetDisplay = null;

                if (move != null && move.intents != null && move.intents.Length > 0)
                {
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

                    var lungedTargets = new HashSet<Unit>();
                    var deadThisMove = new HashSet<Unit>();
                    bool anyKilled = false;

                    for (int i = 0; i < results.Count; i++)
                    {
                        var r = results[i];
                        if (r == null) continue;
                        if (r.target != null && deadThisMove.Contains(r.target)) continue;

                        if (r.didRoll)
                        {
                            CombatEvents.InvokeDiceRolled(unit, r.rawD20Roll,
                                r.totalAttackRoll, r.didHit, r.wasCrit);
                            yield return new WaitForSeconds(diceRollWaitDuration);
                        }

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
                            rDisplay.SuppressDamageFlash    = false;
                            rDisplay.SuppressDeathAnimation = false;
                            rDisplay.SuppressUIUpdates      = false;
                        }

                        if (enemyAttackerDisplay != null && enemyAttackerDisplay != rDisplay)
                        {
                            enemyAttackerDisplay.SuppressDamageFlash    = false;
                            enemyAttackerDisplay.SuppressDeathAnimation = false;
                            enemyAttackerDisplay.SuppressUIUpdates      = false;
                        }
                        if (r.wasCritMiss && enemyAttackerDisplay != null)
                            enemyAttackerDisplay.PlayDamageFlash();

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

                        if (rDisplay != null && rDisplay.HasPendingDeath)
                        {
                            rDisplay.PlayDeathAnimationImmediate();
                            anyKilled = true;
                            if (r.target != null) deadThisMove.Add(r.target);

                            if (i < results.Count - 1)
                                yield return new WaitForSeconds(effectStaggerDelay);
                            continue;
                        }

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

                    if (anyKilled)
                    {
                        yield return new WaitForSeconds(deathSequenceWait);
                        foreach (var dead in deadThisMove)
                        {
                            if (dead != null)
                            {
                                RankHelper.CompactRanks(GetLineup(dead));
                                break;
                            }
                        }
                    }

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

                unit.ClearConditionalMoveEventState();

                yield return new WaitForSeconds(enemyActionDelay);

                targetDisplay?.SetHighlighted(false);

                unit.conditions.FireTurnEnd();
                CombatEvents.InvokeUnitTurnEnd(unit);

                if (CheckCombatEnd()) yield break;

                if (turnOrder.Count > 0)
                    yield return new WaitForSeconds(enemyTurnGap);
            }

            foreach (var enemy in EnemyUnits)
            {
                if (!enemy.IsAlive) continue;
                SetEnemyIntent(enemy);
            }

            ActiveUnit = null;
        }

        private void SetEnemyIntent(Unit enemy)
        {
            var move = EnemyAI.DecideMove(enemy);
            enemy.currentMove = move;
            enemy.lockedIntentTargets = null;

            if (move != null && move.intents != null)
            {
                var locked = new Unit[move.intents.Length];
                for (int i = 0; i < move.intents.Length; i++)
                {
                    var intent = move.intents[i];
                    locked[i] = intent != null
                        ? EnemyAI.SelectTargetForIntent(enemy, intent, PlayerUnits)
                        : null;
                }
                enemy.lockedIntentTargets = locked;

                CombatEvents.InvokeEnemyMoveSet(enemy, move);
            }
        }

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

                unit.conditions.FireCleanup();

                unit.conditions.TickDurations();
            }

            if (CheckCombatEnd()) yield break;

            yield return new WaitForSeconds(0.3f);
        }

        private void OnUnitDied(Unit unit)
        {
            if (!unit.isPlayerControlled)
                CombatEvents.InvokeEnemyDeath(unit);

            CheckCombatEnd();
        }

        private bool CheckCombatEnd()
        {
            if (!combatActive) return true;

            bool allEnemiesDead = !EnemyUnits.Any(e => e.IsAlive);
            bool allPlayersDead = !PlayerUnits.Any(p => p.IsAlive);

            if (allEnemiesDead)
            {
                combatActive = false;
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

        public void OnPlayerChooseAttack(Transform arrowOrigin = null)
        {
            if (!waitingForPlayerInput || ActiveUnit == null || isResolving) return;
            if (ActiveUnit.hasActedThisTurn) return; // Attack costs an Action

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

            if (TargetingSystem.Instance != null && TargetingSystem.Instance.IsTargeting)
                TargetingSystem.Instance.CancelTargeting();

            var lib = ConditionLibrary.Instance;
            var guardData = lib != null ? lib.Get(ConditionID.Guard) : null;
            var result = SkillResolver.ResolveGuard(ActiveUnit, guardData);
            CombatEvents.InvokeActionResolved(result);

            ActiveUnit.hasActedThisTurn = true;
            CombatEvents.InvokeActionStateChanged(ActiveUnit);
        }

        public void OnPlayerChooseSkill(int skillIndex, Transform arrowOrigin = null)
        {
            if (!waitingForPlayerInput || ActiveUnit == null || isResolving) return;
            if (skillIndex < 0 || skillIndex >= ActiveUnit.equippedSkills.Count) return;

            var skill = ActiveUnit.equippedSkills[skillIndex];
            if (!ActiveUnit.CanUseSkill(skill)) return;

            if (TargetingSystem.Instance != null && TargetingSystem.Instance.IsTargeting)
                TargetingSystem.Instance.CancelTargeting();

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

        public void OnPlayerEndTurn()
        {
            if (!waitingForPlayerInput || ActiveUnit == null || isResolving) return;

            if (TargetingSystem.Instance != null && TargetingSystem.Instance.IsTargeting)
                TargetingSystem.Instance.CancelTargeting();

            waitingForPlayerInput = false;
        }

        public void OnPlayerChooseMove(int direction)
        {
            if (!waitingForPlayerInput || ActiveUnit == null || isResolving) return;
            if (ActiveUnit.hasActedThisTurn) return;

            if (TargetingSystem.Instance != null && TargetingSystem.Instance.IsTargeting)
                TargetingSystem.Instance.CancelTargeting();

            int steps = direction < 0 ? 1 : direction > 0 ? 1 : 0;
            if (steps == 0) return;

            int newRank = direction < 0
                ? RankHelper.Advance(GetLineup(ActiveUnit), ActiveUnit, 1)
                : RankHelper.Withdraw(GetLineup(ActiveUnit), ActiveUnit, 1);

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

        public List<Unit> GetLineup(Unit unit)
            => unit != null && unit.isPlayerControlled ? PlayerUnits : EnemyUnits;

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

        private IEnumerator ResolvePlayerActionCoroutine(List<Unit> targets)
        {
            isResolving = true;

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
                    var targetVictimDisplay = UnitDisplay.GetDisplay(targets[0]);
                    var attackerSelfDisplay = UnitDisplay.GetDisplay(ActiveUnit);
                    if (targetVictimDisplay != null)
                    {
                        targetVictimDisplay.SuppressDamageFlash = true;
                        targetVictimDisplay.SuppressDeathAnimation = true;
                        targetVictimDisplay.SuppressUIUpdates = true;
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

                    if (attackResult.didRoll)
                    {
                        CombatEvents.InvokeDiceRolled(ActiveUnit, attackResult.rawD20Roll,
                            attackResult.totalAttackRoll, attackResult.didHit, attackResult.wasCrit);
                        yield return new WaitForSeconds(diceRollWaitDuration);
                    }

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
                        attackerSelfDisplay.SuppressConditionUI    = false;
                        if (attackResult.wasCritMiss)
                            attackerSelfDisplay.PlayDamageFlash();
                    }

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

                    bool killedTarget = targetVictimDisplay != null && targetVictimDisplay.HasPendingDeath;
                    if (killedTarget)
                    {
                        targetVictimDisplay.PlayDeathAnimationImmediate();
                        yield return new WaitForSeconds(deathSequenceWait);
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

                    if (targetVictimDisplay != null)
                        targetVictimDisplay.SuppressConditionUI = false;

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

                        var lungedTargets = new HashSet<Unit>();

                        bool anyKilled = false;

                        var deadThisSkill = new HashSet<Unit>();

                        for (int i = 0; i < results.Count; i++)
                        {
                            var r = results[i];

                            if (r.target != null && deadThisSkill.Contains(r.target))
                                continue;

                            if (r.didRoll)
                            {
                                CombatEvents.InvokeDiceRolled(ActiveUnit, r.rawD20Roll,
                                    r.totalAttackRoll, r.didHit, r.wasCrit);
                                yield return new WaitForSeconds(diceRollWaitDuration);
                            }

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
                                rDisplay.SuppressDamageFlash    = false;
                                rDisplay.SuppressDeathAnimation = false;
                                rDisplay.SuppressUIUpdates      = false;
                            }

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

                            if (rDisplay != null && rDisplay.HasPendingDeath)
                            {
                                rDisplay.PlayDeathAnimationImmediate();
                                anyKilled = true;
                                if (r.target != null) deadThisSkill.Add(r.target);

                                if (i < results.Count - 1)
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

                            if (i < results.Count - 1)
                                yield return new WaitForSeconds(effectStaggerDelay);
                        }

                        if (anyKilled)
                        {
                            yield return new WaitForSeconds(deathSequenceWait);
                            if (targets != null && targets.Count > 0 && targets[0] != null)
                                RankHelper.CompactRanks(GetLineup(targets[0]));
                        }

                        foreach (var d in suppressedDisplays)
                        {
                            if (d == null) continue;
                            d.SuppressDamageFlash       = false;
                            d.SuppressDeathAnimation    = false;
                            d.SuppressUIUpdates         = false;
                            d.SuppressConditionUI = false;
                        }

                        if (pendingSkill.actionCostType == ActionCostType.FreeAction)
                            ActiveUnit.hasFreeActedThisTurn = true;
                        else
                            ActiveUnit.hasActedThisTurn = true;
                    }
                    break;

                case ActionType.Item:
                    if (pendingItem != null)
                    {
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

                        foreach (var d in suppressedDisplays)
                        {
                            if (d == null) continue;
                            d.SuppressDamageFlash    = false;
                            d.SuppressDeathAnimation = false;
                            d.SuppressUIUpdates      = false;
                            d.SuppressConditionUI    = false;
                        }

                        switch (pendingItem.actionCostType)
                        {
                            case ItemActionCostType.Action:
                                ActiveUnit.hasActedThisTurn = true;
                                break;
                            case ItemActionCostType.FreeAction:
                                ActiveUnit.hasFreeActedThisTurn = true;
                                break;
                            case ItemActionCostType.ZeroCost:
                                break;
                        }

                        if (pendingItem.consumedOnUse)
                        {
                            Inventory.Consume(ActiveUnit, pendingItemSource, pendingItemIndex);
                        }
                    }
                    break;
            }

            foreach (var display in highlightedDisplays)
                display.SetHighlighted(false);

            pendingSkill = null;
            pendingItem  = null;
            CombatEvents.InvokeActionStateChanged(ActiveUnit);
            isResolving = false;
        }

        private void OnTargetingCancelled()
        {
            bool wasSkill = pendingAction == ActionType.Skill;
            bool wasItem  = pendingAction == ActionType.Item;
            pendingSkill = null;
            pendingItem  = null;

            if (wasSkill || wasItem)
            {
                CombatEvents.InvokeSkillTargetingCancelled(ActiveUnit);
            }
            else
            {
                CombatEvents.InvokeActionStateChanged(ActiveUnit);
            }
        }

        public List<Unit> GetAliveEnemies() => EnemyUnits.Where(e => e.IsAlive).ToList();
        public List<Unit> GetAlivePlayerUnits() => PlayerUnits.Where(p => p.IsAlive).ToList();
        public ConditionData GetConditionData(ConditionID id)
        {
            var lib = ConditionLibrary.Instance;
            return lib != null ? lib.Get(id) : null;
        }

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
                float size = Mathf.Lerp(0.55f, 0.25f, (float)i / Mathf.Max(1, positions.Length - 1));
                Gizmos.DrawWireCube(t.position, new Vector3(size, size * 2f, 0f));
                Gizmos.DrawSphere(t.position + Vector3.up * 1.1f, 0.06f);
            }
        }
    }
}
