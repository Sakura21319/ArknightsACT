# Phase 07 — Mobile City Environment Layer

## Why blocks exist

The exploration map is intentionally assembled from independent city blocks rather than one monolithic arena. This mirrors the setting idea of mobile cities: each stage is a traversable slice of a larger moving urban platform, and later catastrophes can affect different blocks independently.

## Current runtime layers

1. `RogueliteStageMapController`
   - chooses 4 / 6 / 9 logical blocks,
   - fixes Start and Boss endpoints,
   - rolls Combat / Emergency / Shop contents.

2. `RogueliteStageRuntimeController`
   - materializes the connected 2.5D chunks,
   - spawns enemies and treasure on first entry,
   - owns Boss and stage transition.

3. `RogueliteStageEnvironmentController`
   - decorates the generated stage after assembly,
   - adds distant industrial skyline silhouettes,
   - rolls block-local terrain mechanisms,
   - is the future owner of catastrophe/environment rules.

This separation keeps combat routing independent from visual/environment experiments.

## Implemented environment prototypes

### Active Originium

ACT adaptation of active Originium terrain.

- Actors inside take true damage over time.
- Actors inside gain +30% outgoing damage.
- First prototype intentionally does not alter attack speed; playback/action-speed modifiers will be added only after action-speed ownership is centralized.

### Hole / pit

ACT adaptation of `tile_hole`.

- Enemy entering the hole is defeated.
- Player entering loses 20% maximum HP and resets to the block safe point.
- Trigger volume is deliberately shallow so the player can jump across it.
- Visual is currently a recessed/dark prototype surface; a later floor-mesh pass can create physically open holes.

### Ballista

Directional lane hazard.

- Fires periodically in one fixed direction.
- World cover/walls stop projectiles.
- Player can dodge, jump or use cover.
- First prototype treats it as hostile environmental pressure and does not damage enemies.

## Urban visual direction

Current generated background uses original simple industrial silhouettes rather than copied map geometry:

- dense dark concrete/metal masses,
- uneven building heights,
- rooftop machinery,
- antennas and orange hazard accents,
- heavy fog and low-contrast distant structures.

Reference mood: early Chernobog / main-story ruined industrial city blocks. PRTS assets and stage screenshots may be used as prototype/reference sources, but production-ready public/commercial distribution requires a content/license review.

## Shop economy

A real shop UI is now attached to Shop blocks.

- Enter Shop block and press `E`.
- Three persistent offers per stage.
- Possible stock: healing, collectible, universal level upgrade, character skill specialization, temporary damage buff.
- Skill specialization is deliberately expensive so monster chests remain its primary source.
- Reroll cost: 2 -> 4 -> 8 -> 16 Ingots.
- Stock persists when leaving/re-entering the same Shop block; only reroll changes it.

## Catastrophe foundation

`RogueliteStageEnvironmentController` already owns a catastrophe enum:

- None
- Dust Front
- Originium Fall
- Structural Collapse

Catastrophes are currently disabled by default. Planned behavior is stage-wide or block-scoped rule modification rather than simply a damage-over-time debuff.

Candidate examples:

- Dust Front: reduced sight distance, ranged-enemy accuracy/range pressure changes, stronger close encounters.
- Originium Fall: new Originium zones appear over time; high-risk high-damage routes emerge dynamically.
- Structural Collapse: warning markers followed by rubble drops; some paths/cover layouts change during the stage.

## Next map-generation pass

1. Add multiple low-rise building-shell variants to Street/Open chunks without blocking cardinal entrances.
2. Convert prototype dark pit plates into real floor openings / segmented floor geometry.
3. Add visual telegraphs and PRTS-backed presentation for environmental devices where suitable.
4. Add 3-5 layout variants per chunk theme.
5. Add first playable catastrophe (`Dust Front`) after normal environment hazards are validated.
6. Increase chunk dimensions only after traversal density is validated; current 14 x 11 dimensions are still prototype values.
