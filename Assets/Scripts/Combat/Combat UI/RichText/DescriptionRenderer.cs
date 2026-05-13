using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;

namespace DarkSpire
{
    [RequireComponent(typeof(TMP_Text))]
    [DisallowMultipleComponent]
    public class DescriptionRenderer : MonoBehaviour
    {
        private TMP_Text text;
        private DescriptionLinkHoverDispatcher dispatcher;

        private List<DescriptionToken> tokens = new();
        private Unit caster;
        public Unit HoverTarget { get; private set; }

        private readonly Dictionary<int, NumberEvaluator.Result> numberResults = new();

        private readonly Dictionary<int, KeywordGlossary.Entry> keywordEntries = new();

        private readonly StringBuilder sb = new(256);

        public TMP_Text TmpText => text;
        public IReadOnlyList<DescriptionToken> Tokens => tokens;
        public Unit Caster => caster;

        private void Awake()
        {
            text = GetComponent<TMP_Text>();
            dispatcher = GetComponent<DescriptionLinkHoverDispatcher>();
            if (dispatcher == null)
                dispatcher = gameObject.AddComponent<DescriptionLinkHoverDispatcher>();
        }

        private void OnEnable()
        {
            var ts = TargetingSystem.Instance;
            if (ts != null)
            {
                ts.OnTargetHovered += HandleTargetHovered;
                ts.OnTargetUnhovered += HandleTargetUnhovered;
            }
        }

        private void OnDisable()
        {
            var ts = TargetingSystem.Instance;
            if (ts != null)
            {
                ts.OnTargetHovered -= HandleTargetHovered;
                ts.OnTargetUnhovered -= HandleTargetUnhovered;
            }
            HoverTarget = null;
        }

        private void HandleTargetHovered(Unit unit)
        {
            HoverTarget = unit;
            Render();
        }

        private void HandleTargetUnhovered()
        {
            HoverTarget = null;
            Render();
        }

        public void SetSkill(SkillData skill, Unit caster)
        {
            SetTokens(DescriptionTokenizer.BuildSkillTokens(skill), caster);
        }

        public void SetWeapon(WeaponData weapon, Unit caster)
        {
            SetTokens(DescriptionTokenizer.BuildWeaponTokens(weapon), caster);
        }

        public void SetConditionEntry(ConditionData condition)
        {
            SetTokens(DescriptionTokenizer.BuildConditionTokens(condition), null);
        }

        public void SetPlainText(string plain)
        {
            tokens.Clear();
            if (!string.IsNullOrEmpty(plain))
                tokens.Add(DescriptionToken.Plain(plain));
            this.caster = null;
            Render();
        }

        public void SetTokens(List<DescriptionToken> newTokens, Unit caster)
        {
            tokens.Clear();
            if (newTokens != null) tokens.AddRange(newTokens);
            this.caster = caster;
            Render();
        }

        public void SetCaster(Unit caster)
        {
            this.caster = caster;
            Render();
        }

        public bool TryGetNumberResult(int tokenIndex, out NumberEvaluator.Result result) =>
            numberResults.TryGetValue(tokenIndex, out result);

        public bool TryGetKeywordEntry(int tokenIndex, out KeywordGlossary.Entry entry) =>
            keywordEntries.TryGetValue(tokenIndex, out entry);

        public bool TryGetToken(int tokenIndex, out DescriptionToken token)
        {
            if (tokenIndex < 0 || tokenIndex >= tokens.Count)
            {
                token = default;
                return false;
            }
            token = tokens[tokenIndex];
            return true;
        }

        private void Render()
        {
            if (text == null) return;

            sb.Clear();
            numberResults.Clear();
            keywordEntries.Clear();

            var ctx = EvaluationContext.ForCasterAndTarget(caster, HoverTarget);
            var glossary = KeywordGlossary.Instance;

            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                switch (t.Kind)
                {
                    case DescriptionTokenKind.Plain:
                        sb.Append(t.Text);
                        break;

                    case DescriptionTokenKind.LineBreak:
                        sb.Append('\n');
                        break;

                    case DescriptionTokenKind.Keyword:
                        AppendKeyword(i, t, glossary);
                        break;

                    case DescriptionTokenKind.ComputedNumber:
                        AppendNumber(i, t, ctx);
                        break;
                }
            }

            text.text = sb.ToString();
            text.ForceMeshUpdate();

            if (dispatcher != null)
                dispatcher.NotifyRendered();
        }

        private void AppendKeyword(int idx, DescriptionToken t, KeywordGlossary glossary)
        {
            KeywordGlossary.Entry entry = null;
            if (glossary != null)
            {
                entry = t.HasConditionKey
                    ? glossary.ResolveCondition(t.ConditionKey)
                    : glossary.Resolve(t.KeywordKey);
            }
            if (entry != null)
                keywordEntries[idx] = entry;

            string display = !string.IsNullOrEmpty(t.Text)
                ? t.Text
                : (entry != null && glossary != null
                    ? glossary.GetDisplayName(entry)
                    : (t.HasConditionKey ? t.ConditionKey.ToString() : (t.KeywordKey ?? string.Empty)));

            Color color = (glossary != null && entry != null)
                ? glossary.GetColor(entry)
                : ColorLibrary.Get("Text/KeywordPaleYellow", new Color(0.96f, 0.86f, 0.54f, 1f));

            sb.Append("<link=\"").Append(LinkIdFor(idx)).Append("\"><color=#")
              .Append(ToHex(color)).Append('>').Append(display).Append("</color></link>");
        }

        private void AppendNumber(int idx, DescriptionToken t, EvaluationContext ctx)
        {
            var result = NumberEvaluator.Evaluate(t.Number, ctx);
            numberResults[idx] = result;

            string colorHex;
            bool useColor;
            if (result.Display > result.BaseExpected)
            {
                colorHex = ToHex(ColorLibrary.Get("Text/NumberBuffed", new Color(0.49f, 0.83f, 0.49f, 1f)));
                useColor = true;
            }
            else if (result.Display < result.BaseExpected)
            {
                colorHex = ToHex(ColorLibrary.Get("Text/NumberDebuffed", new Color(0.89f, 0.42f, 0.42f, 1f)));
                useColor = true;
            }
            else
            {
                colorHex = null;
                useColor = false;
            }

            sb.Append("<link=\"").Append(LinkIdFor(idx)).Append("\">");
            if (useColor) sb.Append("<color=#").Append(colorHex).Append('>');
            sb.Append(result.Display.ToString(CultureInfo.InvariantCulture));
            if (useColor) sb.Append("</color>");
            sb.Append("</link>");
        }

        public static string LinkIdFor(int tokenIndex) => "t" + tokenIndex.ToString(CultureInfo.InvariantCulture);

        public static bool TryParseLinkId(string id, out int tokenIndex)
        {
            tokenIndex = -1;
            if (string.IsNullOrEmpty(id) || id.Length < 2 || id[0] != 't') return false;
            return int.TryParse(id.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out tokenIndex);
        }

        private static string ToHex(Color c)
        {
            int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
            int g = Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
            int b = Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
            return $"{r:X2}{g:X2}{b:X2}";
        }
    }
}
