# Escape the Dark Spire — Player Guide

A short, NES-manual-style guide for the current build. Use this as a script for an in-engine pause-menu help screen, an itch.io page, or an actual PDF. Each section calls out the screenshot it wants on the page.

---

## 1. The Goal

> *Image*: a wide screenshot of the dungeon map — party token visible mid-floor, the purple Stairway tile in the corner, a campsite + a chest somewhere in shot.

> *Caption*: "Escape the floor. Find the boss, defeat it, then step on the stairway."

Body copy:

```
You wake in the Dark Spire. Each floor is a procedurally
generated maze. Somewhere on it is a Boss — defeat it
and the Stairway to the next floor unlocks.
```

---

## 2. Controls — Exploration

> *Image*: a labeled keyboard diagram (W/A/S/D + Arrow Keys + E + Esc + Mouse) — pixel-art style if possible, matches the NES-manual approach in the reference.

Two columns:

| Action | Key |
|---|---|
| Move one tile | `W` `A` `S` `D` or Arrow Keys |
| Interact (chest / shrine / gate / rest) | `E` |
| Pause | `Esc` |
| Open the Icon Guide | Click the **`?`** button in the top-right of the HUD |
| Hover any icon for info | Move the mouse over a map sprite |

---

## 3. The Map — What Each Icon Means

> *Image*: a 3-column reference sheet showing each icon at 64×64 with its name. Pull straight from `Assets/ScriptableObjects/MapEntitySpriteLibrary.asset`:
> - Key, Chest, Gold Pile, Shrine
> - Campsite (rest), Stairway, Boss Gate
> - Standard Monster, Elite, Boss, Alert (`!`)

For each item, one line of body copy (same lines used by [MapTooltipCatalog.cs](../Assets/Scripts/UI/Dungeon/MapTooltipCatalog.cs) and the in-game Icon Guide modal — keep them in sync if you tweak):

- **Key** — Carry these to the boss gate. The gate needs more than one.
- **Gold Pile** — Currency. Picked up on contact — bigger wins ahead.
- **Chest** — Press `E` next to a chest to open it.
- **Shrine** — Press `E` to pray. One-time blessing or curse.
- **Boss Gate** — Locked. Carry the required keys, then press `E` to open.
- **Stairway** — Floor exit. Defeat the boss first, then step on to escape.
- **Campsite** — Press `E` to heal and revive the party. One use per camp.
- **Monster** — Walks a patrol. Spots you, alerts (`!`), then chases — fight on contact.
- **Elite Monster** — Tougher patroller with a wider detection range. Worth more.
- **Boss** — Sits in the boss room. Defeat it to unlock the stairway exit.
- **Alerted (`!`)** — A monster spotted you. After a beat it will chase.

---

## 4. Combat — Turns and Actions

> *Image*: a combat screenshot. Show the party row on the left, an enemy mid-screen, a skill card highlighted, the enemy's intent icon visible above its head.

> *Caption*: "Click a skill. Click a target. Watch it land."

Body copy:

```
Each turn:
  1. The enemy telegraphs its next move with an Intent icon
     above its head. Hover it to preview damage / target.
  2. Pick a party member, choose Attack / Skill / Item.
  3. Click End Turn when done.

Damage = your stat (POW / FIN / WIL / GUT) vs the enemy's
DEF, rolled on a d20. Crits double damage.
```

---

## 5. Intent Icons (What the Enemy Will Do)

> *Image*: the four intent icons side by side at 96×96 with labels.

| Icon | Meaning |
|---|---|
| ⚔ Attack | Will deal damage to a party member next turn. Number = expected damage. |
| 🛡 Guard | Will gain Shields. Number = Shields gained. |
| ✨ Buff | Will boost itself or an ally. |
| 💀 Debuff | Will weaken a party member. |

Tip: hover any intent icon to highlight which party member it's targeting. The red/purple aura on the danger preview is exact — it WILL hit who it says.

---

## 6. Conditions and Orbs

> *Image*: combat HUD close-up showing condition row (Strength, Vulnerable, Shields, etc.) and an orb tray on the bottom.

Body copy:

```
Conditions stack on units (Strength, Frail, Vulnerable,
Shields, etc.). Some tick down each turn; others stay
until removed.

Orbs are passive/evoke abilities specific to certain
characters. Hover to see what they do.

Both have full tooltips on hover. Read them.
```

---

## 7. Rest Tiles (Campsites)

> *Image*: dungeon screenshot with the party standing on a campsite tile, the "[E] Rest" prompt floating above.

Body copy:

```
Step onto a Campsite tile and press E:
  - Restore HP/SP — fully refill every party member
  - Revive Fallen — only the downed members come back
  - View Stats — read-only sub-panel
  - Leave — close without spending the camp

You can only use each campsite ONCE per floor.
```

---

## 8. Quick Reference Card

> *Image*: a small printable card combining the keyboard layout, the icon legend, and the intent icons. Could be a single-page printable PDF appendix.

```
   MOVE      Arrow / WASD       KEY    open boss gate
   INTERACT  E                  GATE   needs keys + E
   PAUSE     Esc                CAMP   E to rest, one use
   HELP      click ?            STAIR  boss first, then exit
   HOVER     mouse over an icon
```

---

## Production Notes (Cut Before Publishing)

These are for whoever's laying out the manual — strip them before shipping.

### Image checklist (put these into `Docs/manual-images/`)

| Slot | What to capture | Notes |
|---|---|---|
| `01-goal.png` | Dungeon-wide shot with stairway + party + a couple of features | Hide debug overlays |
| `02-controls-keyboard.png` | NES-style key diagram | Either redraw or use a public-domain pixel-key set |
| `03-icons-grid.png` | 3-column icon sheet | Pull each sprite from `MapEntitySpriteLibrary.asset` at 64px |
| `04-combat-overview.png` | Combat scene, mid-turn, with a skill card hovered | Use the CombatTest scene |
| `05-intent-icons.png` | The four intent icons + sample labels | From `IntentIconUI` prefab |
| `06-conditions-orbs.png` | Combat HUD close-up | Crop to the row of icons |
| `07-campsite.png` | Party on a rest tile, prompt visible | Take from DungeonFloor |
| `08-reference-card.png` | Combined card | Bundles 02 + 03 + 05 |

### Style references

- Voice: terse, second-person, NES-manual matter-of-fact ("He Jumps", "Moves Mario"). Avoid marketing speak.
- Typography: Kreon (the project's UI font) for body, Kreon Bold for headers.
- Page size: 8.5×11" if printing, or 1080×1920 if shipping as in-engine pages.

### Keep this in sync

When the catalog or controls change, update **both** of these:
- This guide (`Docs/PLAYER_GUIDE.md`)
- The in-game catalog ([MapTooltipCatalog.cs](../Assets/Scripts/UI/Dungeon/MapTooltipCatalog.cs))

The icon-guide modal and first-run intro card both read from the catalog, so the in-game text stays consistent if you only edit the catalog. The manual lives outside the build — touch both when copy changes.
