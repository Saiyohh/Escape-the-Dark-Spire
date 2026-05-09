// DungeonEvents.cs
// -----------------------------------------------------------------------------
// Static event bus for the dungeon-exploration layer. Sibling of CombatEvents.
// HUD, Minimap, FlashMessageController, and any other dungeon UI subscribe here
// instead of holding direct refs to entities or RunContext.
//
// Entities and walkover dispatch fire the events. UI / VFX listen.
// ClearAll() is called from DungeonBootstrap.OnDestroy so listeners don't leak
// across scene reloads.
// -----------------------------------------------------------------------------
using System;

namespace DarkSpire
{
    public static class DungeonEvents
    {
        public static event Action<string> OnFlashMessage;          // text to display bottom-center
        public static event Action<int, int> OnKeyCollected;        // (total, required)
        public static event Action<int> OnGoldChanged;              // new gold amount
        public static event Action OnBossDefeated;
        public static event Action OnFloorAdvance;
        public static event Action OnPartyHpChanged;

        public static void InvokeFlashMessage(string text) => OnFlashMessage?.Invoke(text);
        public static void InvokeKeyCollected(int total, int required) => OnKeyCollected?.Invoke(total, required);
        public static void InvokeGoldChanged(int newAmount) => OnGoldChanged?.Invoke(newAmount);
        public static void InvokeBossDefeated() => OnBossDefeated?.Invoke();
        public static void InvokeFloorAdvance() => OnFloorAdvance?.Invoke();
        public static void InvokePartyHpChanged() => OnPartyHpChanged?.Invoke();

        public static void ClearAll()
        {
            OnFlashMessage = null;
            OnKeyCollected = null;
            OnGoldChanged = null;
            OnBossDefeated = null;
            OnFloorAdvance = null;
            OnPartyHpChanged = null;
        }
    }
}
