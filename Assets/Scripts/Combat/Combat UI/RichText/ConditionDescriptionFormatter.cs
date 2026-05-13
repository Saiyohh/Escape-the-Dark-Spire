using System.Globalization;
using System.Text;
using UnityEngine;

namespace DarkSpire
{
    public static class ConditionDescriptionFormatter
    {
        public static string Format(ConditionData data, int stacks = 0)
        {
            if (data == null) return string.Empty;
            string desc = data.description;
            if (string.IsNullOrEmpty(desc)) return string.Empty;
            if (desc.IndexOf('{') < 0) return desc; // fast path — no placeholders

            int perStack = ResolvePerStack(data);

            var sb = new StringBuilder(desc.Length + 8);
            int i = 0;
            while (i < desc.Length)
            {
                char ch = desc[i];
                if (ch == '{')
                {
                    int end = desc.IndexOf('}', i + 1);
                    if (end > i + 1)
                    {
                        string key = desc.Substring(i + 1, end - i - 1);
                        string value = ResolveKey(key, perStack, stacks);
                        if (value != null)
                        {
                            sb.Append(value);
                            i = end + 1;
                            continue;
                        }
                    }
                }
                sb.Append(ch);
                i++;
            }
            return sb.ToString();
        }

        // simple sensible default. If you have a condition with multiple
        // passive mods and want a specific one, author the description with
        // an indexed token in the future (e.g. {X:1}); fall back to first now.
        private static int ResolvePerStack(ConditionData data)
        {
            if (data.passiveModifiers == null) return 0;
            for (int i = 0; i < data.passiveModifiers.Length; i++)
            {
                float v = data.passiveModifiers[i].amountPerStack;
                if (Mathf.Approximately(v, 0f)) continue;
                return Mathf.RoundToInt(v);
            }
            return 0;
        }

        private static string ResolveKey(string key, int perStack, int stacks)
        {
            if (string.IsNullOrEmpty(key)) return null;
            // Use Trim so spacing tolerance is friendly: "{ X }" works.
            switch (key.Trim().ToLowerInvariant())
            {
                case "x":
                case "perstack":
                    return perStack.ToString(CultureInfo.InvariantCulture);
                case "stacks":
                    return stacks.ToString(CultureInfo.InvariantCulture);
                case "total":
                    return (perStack * stacks).ToString(CultureInfo.InvariantCulture);
                default:
                    return null; // leave "{unknown}" intact in output
            }
        }
    }
}
