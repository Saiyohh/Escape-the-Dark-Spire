namespace DarkSpire
{
    // Why a player action can't be taken right now. Surfaced via the
    // SpeechBubble system when the player clicks an unaffordable action.
    //
    // None = the action is currently legal.
    public enum ActionRefusalReason
    {
        None,
        NoAction,         // hasActedThisTurn (skill is action-cost)
        NoFreeAction,     // hasFreeActedThisTurn (skill is free-action)
        NotEnoughSP,
        NotEnoughStars,
        Immobilized,      // Stunned / any condition with preventsAction
    }

    public static class ActionRefusalMessages
    {
        // Generic fallback line. Used when the caster has no CharacterData,
        // or when the character's per-reason line is blank.
        public static string For(ActionRefusalReason r)
        {
            switch (r)
            {
                case ActionRefusalReason.NoAction:        return "No action.";
                case ActionRefusalReason.NoFreeAction:    return "No free action.";
                case ActionRefusalReason.NotEnoughSP:     return "Not enough SP.";
                case ActionRefusalReason.NotEnoughStars:  return "Not enough stars.";
                case ActionRefusalReason.Immobilized:     return "I can't move.";
                default:                                  return string.Empty;
            }
        }

        // Character-aware overload. Returns the caster's per-reason line if
        // authored (non-empty), otherwise the generic fallback. Callers in
        // the combat UI use this so each character can speak in their own
        // voice when refusing an action.
        public static string For(ActionRefusalReason r, Unit caster)
        {
            var cd = caster != null ? caster.characterData : null;
            if (cd != null)
            {
                var line = cd.GetRefusalLine(r);
                if (!string.IsNullOrWhiteSpace(line))
                    return line;
            }
            return For(r);
        }
    }
}
