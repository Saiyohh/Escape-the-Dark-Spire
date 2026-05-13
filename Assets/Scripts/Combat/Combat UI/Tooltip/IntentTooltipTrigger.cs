using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DarkSpire
{
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("DarkSpire/Tooltip/Intent Tooltip Trigger")]
    public class IntentTooltipTrigger : UITooltipTrigger
    {
        private IntentIconUI iconUI;

        private static readonly List<Unit> _targetScratch = new();
        private static readonly HashSet<Unit> _seen = new();
        private static readonly StringBuilder _bodyBuilder = new();

        protected override void Awake()
        {
            base.Awake();
            iconUI = GetComponent<IntentIconUI>();
        }

        protected override bool BuildContent(out TooltipContent content)
        {
            content = default;
            if (iconUI == null) return false;

            var intent = iconUI.Intent;
            var source = iconUI.Source;
            if (intent == null) return false;

            string header = ResolveMoveName(source, intent);
            string body   = BuildBody(intent, source);

            content = new TooltipContent
            {
                HeaderText = header,
                BodyText = body,
            };
            return true;
        }

        private static string ResolveMoveName(Unit source, EnemyIntent intent)
        {
            if (source != null && source.currentMove != null
                && source.currentMove.intents != null)
            {
                var m = source.currentMove;
                for (int i = 0; i < m.intents.Length; i++)
                {
                    if (ReferenceEquals(m.intents[i], intent))
                        return string.IsNullOrEmpty(m.name) ? GenericHeader(intent) : m.name;
                }
            }
            return GenericHeader(intent);
        }

        private static string GenericHeader(EnemyIntent intent) => intent.intentType switch
        {
            EnemyIntentType.Attack  => "Attack",
            EnemyIntentType.Guard   => "Guard",
            EnemyIntentType.Buff    => "Buff",
            EnemyIntentType.Debuff  => "Debuff",
            EnemyIntentType.Stunned => "Stunned",
            _                       => "Intent",
        };

        private static string BuildBody(EnemyIntent intent, Unit source)
        {
            switch (intent.intentType)
            {
                case EnemyIntentType.Attack:  return BuildAttackBody(intent, source);
                case EnemyIntentType.Debuff:  return BuildDebuffBody(intent, source);
                case EnemyIntentType.Buff:    return BuildBuffBody(intent, source);
                case EnemyIntentType.Guard:   return "Intends to brace.";
                case EnemyIntentType.Stunned: return "Stunned — cannot act this turn.";
                default:                      return "";
            }
        }

        private static string BuildAttackBody(EnemyIntent intent, Unit source)
        {
            ResolveTargets(intent, source, players: true, _targetScratch);
            int damage = AggregateAttackDamage(intent, source);

            string damagePart = damage > 0 ? $" for {damage} damage" : "";
            string targetPart = JoinNames(_targetScratch, source);
            if (string.IsNullOrEmpty(targetPart))
                return $"Intends to Attack{damagePart}.";
            return $"Intends to Attack {targetPart}{damagePart}.";
        }

        private static string BuildDebuffBody(EnemyIntent intent, Unit source)
        {
            ResolveTargets(intent, source, players: true, _targetScratch);
            string targetPart = JoinNames(_targetScratch, source);
            if (string.IsNullOrEmpty(targetPart))
                return "Intends to Debuff.";
            return $"Intends to Debuff {targetPart}.";
        }

        private static string BuildBuffBody(EnemyIntent intent, Unit source)
        {
            ResolveTargets(intent, source, players: false, _targetScratch);

            if (_targetScratch.Count == 1 && _targetScratch[0] == source)
                return "Intends to Buff themselves.";

            string targetPart = JoinNames(_targetScratch, source);
            if (string.IsNullOrEmpty(targetPart))
                return "Intends to Buff themselves.";
            return $"Intends to Buff {targetPart}.";
        }

        private static void ResolveTargets(
            EnemyIntent intent, Unit source, bool players, List<Unit> output)
        {
            output.Clear();
            _seen.Clear();
            if (intent == null || intent.effects == null || source == null) return;

            var mgr = CombatManager.Instance;
            if (mgr == null) return;

            var primary = LockedPrimary(source, intent, mgr.PlayerUnits);
            var selected = new List<Unit>(1);
            if (primary != null) selected.Add(primary);

            for (int i = 0; i < intent.effects.Length; i++)
            {
                var e = intent.effects[i];
                if (e == null) continue;
                if (IsPlayerSide(e.targetMode) != players) continue;

                var resolved = SkillResolver.ResolveTargets(
                    e.targetMode, source, selected,
                    mgr.PlayerUnits, mgr.EnemyUnits,
                    pickIndex: e.targetPickIndex);
                if (resolved == null) continue;

                for (int t = 0; t < resolved.Count; t++)
                {
                    var u = resolved[t];
                    if (u == null || !u.IsAlive) continue;
                    if (players && !u.isPlayerControlled) continue;
                    if (!players &&  u.isPlayerControlled) continue;
                    if (_seen.Add(u)) output.Add(u);
                }
            }
        }

        private static bool IsPlayerSide(TargetMode mode) => mode switch
        {
            TargetMode.SingleEnemy => true,
            TargetMode.AllEnemies  => true,
            TargetMode.RandomEnemy => true,
            _                      => false,
        };

        private static Unit LockedPrimary(Unit source, EnemyIntent intent, List<Unit> players)
        {
            var move = source.currentMove;
            var locked = source.lockedIntentTargets;
            if (move != null && move.intents != null && locked != null)
            {
                for (int i = 0; i < move.intents.Length && i < locked.Length; i++)
                {
                    if (!ReferenceEquals(move.intents[i], intent)) continue;
                    var t = locked[i];
                    if (t != null && t.IsAlive) return t;
                    break;
                }
            }
            return EnemyAI.SelectTargetForIntent(source, intent, players);
        }

        private static int AggregateAttackDamage(EnemyIntent intent, Unit source)
        {
            if (intent.effects == null) return 0;
            int total = 0;
            for (int i = 0; i < intent.effects.Length; i++)
            {
                var e = intent.effects[i];
                if (e == null || e.effectType != SkillEffectType.Attack) continue;
                int perHit = source != null
                    ? DamageCalculator.PreviewOutgoingDamage(e.magnitude, source, e.damageStat)
                    : e.magnitude;
                int hits = Mathf.Max(1, e.hitCount);
                total += perHit * hits;
            }
            return total;
        }

        private static string JoinNames(List<Unit> units, Unit source)
        {
            if (units.Count == 0) return "";
            if (units.Count == 1) return NameFor(units[0], source);

            _bodyBuilder.Clear();
            for (int i = 0; i < units.Count; i++)
            {
                string n = NameFor(units[i], source);
                if (i == 0) { _bodyBuilder.Append(n); continue; }

                bool last = i == units.Count - 1;
                if (last)
                {
                    if (units.Count == 2) _bodyBuilder.Append(" and ");
                    else                  _bodyBuilder.Append(", and ");
                }
                else
                {
                    _bodyBuilder.Append(", ");
                }
                _bodyBuilder.Append(n);
            }
            return _bodyBuilder.ToString();
        }

        private static string NameFor(Unit u, Unit source)
        {
            if (u == source) return "themselves";
            return !string.IsNullOrEmpty(u.unitName) ? u.unitName : "???";
        }
    }
}
