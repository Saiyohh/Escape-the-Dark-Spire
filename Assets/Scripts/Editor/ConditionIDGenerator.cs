// ConditionIDGenerator.cs
// -----------------------------------------------------------------------------
// Appends a new value to the `ConditionID` enum in CombatEnums.cs.
//
// Why code-gen instead of runtime registration:
//   The ConditionID enum is the stable address space every system keys off.
//   Adding at runtime would mean numeric shifts on every domain reload and
//   would invalidate every serialized asset. A compile-time enum edit keeps
//   the existing numeric positions and lets Unity re-serialize nothing.
//
// Strategy:
//   • Locate the `enum ConditionID { ... }` block in the file by line scan.
//   • Measure indentation from the last existing entry (so inserts look
//     like the file's own style — 8 spaces in our conventions).
//   • Insert "{indent}{NewName},\n" on the line BEFORE the closing brace.
//     This places the new value at the end of the enum — critical for
//     serialization stability, since numeric indices of existing entries
//     must not change.
//   • Trigger AssetDatabase.ImportAsset so Unity recompiles immediately.
//
// Validations:
//   • Name must be a valid C# PascalCase identifier.
//   • Name must not already exist in the enum.
//   • Source file must exist at the canonical path.
//
// If any validation or file op fails, returns false with a user-friendly
// `error` message; caller shows it in a dialog. The source file is never
// partially written — File.WriteAllLines is atomic.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace DarkSpire.EditorTools
{
    public static class ConditionIDGenerator
    {
        /// <summary>Hard-coded path to the enum source file. Update if the file moves.</summary>
        public const string EnumFilePath = "Assets/Scripts/Combat/Combat/CombatEnums.cs";

        /// <summary>Name of the enum inside the file, matched verbatim.</summary>
        public const string EnumDeclaration = "enum ConditionID";

        /// <summary>
        /// Adds <paramref name="newName"/> as the last member of the ConditionID
        /// enum. On success, triggers a Unity reimport so the change recompiles
        /// immediately. Returns false with a message on validation / IO failure.
        /// </summary>
        public static bool AddID(string newName, out string error)
        {
            newName = (newName ?? "").Trim();

            if (string.IsNullOrEmpty(newName))
            {
                error = "Name cannot be empty.";
                return false;
            }
            if (!Regex.IsMatch(newName, @"^[A-Z][A-Za-z0-9]*$"))
            {
                error = "Name must be PascalCase — start with an uppercase letter, " +
                        "then letters or digits only. Example: Poisoned, FrozenStance.";
                return false;
            }
            if (System.Enum.GetNames(typeof(ConditionID)).Any(n =>
                    string.Equals(n, newName, System.StringComparison.Ordinal)))
            {
                error = $"ConditionID.{newName} already exists.";
                return false;
            }
            if (!File.Exists(EnumFilePath))
            {
                error = $"Cannot find enum source file at {EnumFilePath}.";
                return false;
            }

            // ── Scan for the enum block ───────────────────────────────────────
            var lines = File.ReadAllLines(EnumFilePath).ToList();
            int enumStart = -1;
            int enumOpenBrace = -1;
            int enumCloseBrace = -1;

            for (int i = 0; i < lines.Count; i++)
            {
                if (enumStart < 0 && lines[i].Contains(EnumDeclaration))
                {
                    enumStart = i;
                    continue;
                }
                if (enumStart >= 0 && enumOpenBrace < 0)
                {
                    if (lines[i].Contains("{")) enumOpenBrace = i;
                    continue;
                }
                if (enumOpenBrace >= 0 && lines[i].TrimStart().StartsWith("}"))
                {
                    enumCloseBrace = i;
                    break;
                }
            }

            if (enumStart < 0 || enumOpenBrace < 0 || enumCloseBrace < 0)
            {
                error = $"Could not locate `{EnumDeclaration} {{ ... }}` block in " +
                        $"{EnumFilePath}. Was the file reformatted?";
                return false;
            }

            // ── Measure indentation from the last real entry ──────────────────
            string indent = "        "; // fallback (8 spaces)
            for (int i = enumCloseBrace - 1; i > enumOpenBrace; i--)
            {
                var raw = lines[i];
                var trimmed = raw.TrimStart();
                if (trimmed.Length == 0) continue;
                if (trimmed.StartsWith("//")) continue;
                if (trimmed == "{") continue;
                int firstNonSpace = raw.Length - trimmed.Length;
                indent = new string(' ', firstNonSpace);
                break;
            }

            // ── Insert new entry right before the closing brace ───────────────
            lines.Insert(enumCloseBrace, $"{indent}{newName},");

            File.WriteAllLines(EnumFilePath, lines);
            AssetDatabase.ImportAsset(EnumFilePath);

            error = null;
            return true;
        }

        /// <summary>Convenience wrapper: adds the ID, pops a dialog on failure.</summary>
        public static bool AddIDInteractive(string newName)
        {
            if (AddID(newName, out var error))
            {
                Debug.Log($"[ConditionIDGenerator] Added ConditionID.{newName}. " +
                          "Unity is recompiling — the new value appears in dropdowns shortly.");
                return true;
            }
            EditorUtility.DisplayDialog("Couldn't add ConditionID", error, "OK");
            return false;
        }
    }
}
