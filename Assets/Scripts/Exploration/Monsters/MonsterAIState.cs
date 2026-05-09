namespace DarkSpire
{
    // Four-state map-monster AI per the Monsters & Chase AI page.
    //
    //   Patrol -> Alert : party in detection radius + line-of-sight
    //   Alert  -> Chase : after alertDuration (0.5s by default)
    //   Chase  -> Lost  : party out of LoS for lostDuration (3s by default)
    //   Chase  -> COMBAT: monster steps onto (or party steps onto) monster tile
    //   Lost   -> Patrol: monster reaches its nearest waypoint
    //   Lost   -> Chase : party re-enters detection radius before Lost finishes
    public enum MonsterAIState
    {
        Patrol,
        Alert,
        Chase,
        Lost,
    }
}
