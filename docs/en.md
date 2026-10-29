# FF Battle — battles with a command menu

FF Battle is a battle system in the style of classic console RPGs: the party at the bottom left, the enemies at the top, and a command menu — Attack, Items, Defend, Escape. It is the battle system of RPG Plus. The battle itself — its start, backdrop, victory and defeat screens, experience and loot — comes from Turn Battle Base; FF Battle is the way the player fights.

## The battle screen

The RPG Plus warrior meets a wolf in the village:

![The FF battle screen: the wolf's card at the top (31/31, ATK 7, DEF 2), the hero's small card at the bottom left (42/42, MP 6/6, DEF 4, one yellow action point), the Battle panel at the top left (Round 1, Enemies 1, Party 1), the speed buttons and the battle log on the right.](assets/ff-start.jpg)

- **Cards.** Each character shows health, the attack and defence (DEF is a shield that absorbs damage first), mana if the game has it, and the yellow dots of action points (AP).
- **Battle panel** — the round, how many enemies and party members are left, and whose turn it is.
- **Battle log** — every action and its result, newest at the bottom.

## A turn

1. **Pick a character** — click the card, or press **Enter**/**Space** for the first one with action points.
2. **Choose a command** with the mouse or **W/S**/arrows and **Enter**:
   - **Attack** — the character's battle actions;
   - **Items** — usable items from the inventory, 1 AP each;
   - **Defend** — the character's defence action on itself;
   - **Escape** — 70% chance; on failure the character loses 1 AP.
3. **Choose the action.** The panel next to the menu shows the action card and what it will do:

![The Hero's actions menu — Basic attack (1P), Defence (1P), Power strike (1P), Back — and the Basic attack card with the hint "Damage 12 (ATK 12 × 1,0) · cost 1 AP".](assets/ff-actions.jpg)

4. **Click the target.** "Select a target · RMB / Esc — cancel" appears and the enemy cards light up:

![The wolf's card highlighted in red and the hint "Select a target · RMB / Esc — cancel".](assets/ff-target.jpg)

When the party has spent its action points, the enemies take their turn, then a new round starts and the points are restored.

## What the log says

After one basic attack and the wolf's answer:

![The battle log: Hero: Basic attack → Wolf; Wolf's shield absorbed 2 · left 0; Basic attack: Hero → Wolf · -10 HP; The party is out of AP; Enemy turn · round 1; Wolf: Defence.](assets/ff-log.jpg)

The hero's attack is 12. The wolf's shield of 2 took the first 2 points and was used up, so the wolf lost 10 health: 31 → 21. The wolf then used Defence and raised its shield to 4 for the next blow.

## Actions

The menu lists the actions the character has (see Characters Engine). FF Battle itself carries out the basic kinds:

- `Attack=1.5` — damage of the attack times the number, absorbed first by the target's shield;
- `Defence=4` — a shield on an ally;
- `Turn=2` — stun for that many turns;
- item use.

Actions written as formulas, such as Classic RPG Rules' Power strike or Fireball, work through Battle Formulas; mana costs (`Cost=mp:4`) are spent from the character's mana.

## If something goes wrong

1. **The menu does not open.** The character has no action points left this round, or it is the enemies' turn.
2. **Items is greyed out.** There are no items in the inventory that can be used in battle.
3. **A skill does nothing.** Its values need Battle Formulas, or it is not marked as usable in battle.
