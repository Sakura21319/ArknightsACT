# Phase 04 — Roguelite R1 + R2

## Goal

Add the first playable Roguelite loop without coupling rewards to Ch'en or any future operator.

Current loop:

```text
combat room
-> clear enemies
-> wait for the player's current authored action to finish
-> pause
-> roll up to 3 compatible collectibles
-> choose 1
-> apply immediately
-> continue to next combat room
```

The old automatic 20% room-clear heal is disabled by default. Healing should become a meaningful build / route / rest decision rather than a guaranteed transition reward.

## Architecture decision: collectibles are operator-agnostic

Do **not** create a full collectible pool for every operator.

Public collectible definitions target generic combat concepts:

- all damage
- Physical damage
- Arts damage
- True damage
- max HP
- basic-attack hit events
- active-skill cast events
- cooldown manipulation

Every playable character declares a `PlayerCombatProfile` using `CombatFeature` flags. Reward generation filters out choices the current character cannot use.

Example: Ch'en declares BasicAttack + ActiveSkills + Dash + PhysicalDamage + ArtsDamage, so the generic True-damage collectible exists in the shared pool but is not offered to Ch'en.

Future character identity should primarily come from the character's authored moveset. If character-specific Roguelite upgrades are added later, keep them as a small separate `OperatorModule` layer and do not mix them into the global collectible pool.

Recommended content ratio:

```text
80-90% shared collectibles
10-20% archetype / mechanic modules
0 mandatory per-operator collectible pool
```

## R1 — reward gate

`PrototypeRoomLoopController` no longer has to advance automatically after `RoomCleared`.

It exposes:

```text
SetExternalContinueGate(bool)
ContinueToNextRoom()
IsWaitingForContinue
```

Room clear is now finalized only when:

```text
all enemies are dead
AND basic attack is no longer active
AND active skill casting has finished
AND dash has finished
```

This prevents reward UI from freezing a killing move halfway through and prevents skill coroutines from leaking into the next room.

`RogueliteRewardController` subscribes to `RoomCleared`, pauses gameplay, builds compatible choices, and resumes only after a successful selection.

The current reward UI intentionally uses `OnGUI` so R1 can be validated without creating Canvas/prefab art. Replace this presentation later; do not move reward rules into UI code.

## Pause ownership

`GameplayPauseService` is the only gameplay-level owner of `Time.timeScale`.

Systems pause by owner token:

```text
GameplayPauseService.Pause(owner)
GameplayPauseService.Resume(owner)
```

This allows HitStop, reward choice, future shops, events and pause menus to overlap without one system accidentally unpausing another.

`HitStopService` now uses the same pause service with realtime duration.

## R2 — generic collectible framework

### `CollectibleDefinition`

ScriptableObject data:

- stable id
- display name / description
- rarity
- required combat features
- effect type
- value
- max stack count

### `CollectibleInventory`

Lives on the player and implements `IDamageModifier`.

Responsibilities:

- acquire and stack collectibles
- modify outgoing damage by damage type
- max-HP upgrades
- subscribe to generic `PlayerAttackController.AttackHit`
- subscribe to generic `PlayerSkillController.SkillCastSucceeded`
- reduce cooldown through `IPlayerSkill.ReduceCooldown`

It must not reference `ChenSkill1`, `ChenSkill2`, or any operator name.

### Damage modifier pipeline

`Game.Combat` now exposes `IDamageModifier`.

`DamageSystem` applies:

```text
base damage
-> Source outgoing modifiers
-> Owner outgoing modifiers (if different from Source)
-> Target incoming modifiers
-> Health.TakeDamage
```

This is the future extension point for shared stats, equipment, defenses and Roguelite effects. `Game.Combat` remains unaware of Roguelite.

## Initial collectible pool

- 战术校准芯片 — all damage +8%
- 锋刃校准器 — Physical +15%
- 术式共振器 — Arts +18%
- 纯化源石核心 — True +12%
- 强化防护服 — max HP +15%
- 技力电池 — basic hit reduces all active-skill cooldowns by 0.12s
- 医疗无人机模块 — skill cast heals 2.5% max HP
- 过载技力模块 — skill cast reduces all active-skill cooldowns by 0.60s

These are prototype values, not final balance.

## Ch'en profile

```text
BasicAttack
ActiveSkills
Dash
PhysicalDamage
ArtsDamage
```

No TrueDamage flag.

## Ch'en Skill 2 invulnerability

`赤霄·绝影` is invulnerable for its entire `IsCasting` window.

This is implemented through generic `IPlayerInvulnerabilitySource`; `PlayerDamageGate` no longer has dash-specific or Ch'en-specific knowledge. Dash and Skill 2 both expose the same interface, and future skills/buffs can do the same.

## Combat readability stabilization

- player and enemy world-space HP bars
- floating damage numbers by damage type
- HP bars force an initial `Start`-phase sync after all component `Awake` calls, preventing the player bar from remaining hidden until first damage

## Local validation required

This phase was source-edited through GitHub and has not been executed in Unity by the assistant.

After pulling the branch:

```text
git checkout feat/phase-04-roguelite-r1-r2
```

Then in Unity:

```text
1. Wait for compilation.
2. Fix only the first red compiler error if one exists.
3. Existing PrototypeRun can be used for script-only validation.
4. Rebuild Prototype Scene when you want the explicit GameplayPauseService component persisted in the generated scene.
5. Play PrototypeRun.
```

Validate:

```text
[ ] Player HP bar is visible immediately on entering Play Mode.
[ ] Existing Ch'en movement / combo / dash / skills still work.
[ ] Ch'en Skill 2 rejects incoming damage for the full cast.
[ ] Killing the final enemy during Skill 2 does not open rewards until Skill 2 fully finishes.
[ ] Skill 2 never continues attacking after entering the next room.
[ ] Clearing room 1 does not auto-start room 2.
[ ] Reward overlay appears after combat action settles.
[ ] Game action is paused while reward overlay is open.
[ ] Mouse and 1/2/3 can choose rewards.
[ ] Choosing a reward closes the overlay and starts the next room.
[ ] HitStop cannot permanently freeze the next room.
[ ] Ch'en never receives the True-damage collectible.
[ ] Physical bonus increases basic attacks and physical skill hits.
[ ] Arts bonus increases the Arts portion of 赤霄·拔刀 only.
[ ] Max-HP reward increases max HP and grants the added HP immediately.
[ ] 技力电池 reduces both skill cooldowns on basic hits.
[ ] 医疗无人机模块 heals after successful skill casts.
[ ] 过载技力模块 reduces active skill cooldowns after casting.
[ ] Reward stacks stop at each definition's max stack count.
```

## Next phase

Do not jump directly to operator-specific collectibles.

Recommended R3 work:

1. Add route nodes: combat / emergency / encounter / safe house / trader / boss.
2. Move guaranteed reward frequency from every room to node reward rules.
3. Add source/origin tags to damage contexts if future collectibles need to distinguish Basic / Skill / DoT / summon damage.
4. Add a small operator-module framework only when a second playable operator exists, so the abstraction is driven by two real movesets rather than assumptions.
