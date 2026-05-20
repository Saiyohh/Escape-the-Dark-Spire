// IntroFlagMenu.cs
// -----------------------------------------------------------------------------
// Dev tools for the first-run intro card's PlayerPrefs gate. After dismissing
// the intro once, FirstRunIntroCard.HasSeenIntro flips to true and the card
// never appears again on this install. These menu items let you reset or
// toggle the flag for testing.
// -----------------------------------------------------------------------------
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DarkSpire
{
    public static class IntroFlagMenu
    {
        [MenuItem("Tools/DarkSpire/Dev/Reset First-Run Intro Flag")]
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(FirstRunIntroCard.PrefsKey);
            PlayerPrefs.Save();
            Debug.Log("[IntroFlagMenu] Cleared " + FirstRunIntroCard.PrefsKey +
                      ". The first-run intro will appear on the next dungeon entry.");
        }

        [MenuItem("Tools/DarkSpire/Dev/Show Intro Status")]
        public static void ShowStatus()
        {
            int v = PlayerPrefs.GetInt(FirstRunIntroCard.PrefsKey, 0);
            string state = v == 1 ? "SEEN — intro will be skipped on dungeon entry"
                                  : "NOT SEEN — intro will fire on next dungeon entry";
            EditorUtility.DisplayDialog("First-Run Intro",
                $"PlayerPrefs[{FirstRunIntroCard.PrefsKey}] = {v}\n\n{state}",
                "OK");
        }
    }
}
#endif
