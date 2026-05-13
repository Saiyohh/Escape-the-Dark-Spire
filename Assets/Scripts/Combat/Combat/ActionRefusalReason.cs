namespace DarkSpire
{
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
