# Phase 06 — Main Combat Migration to 2.5D

## Goal

Promote the approved 2.5D experiment into the main `PrototypeRun` without rewriting the higher-level combat and roguelite systems.

The new production direction is:

- real 3D map geometry and 3D collisions
- fixed low oblique orthographic camera
- 2D Spine actors presented as camera-facing billboards
- free XZ planar movement with Y reserved for jump height
- forward-facing 3D attack volumes
- enemies acquire the player only inside a forward vision cone with line of sight
- existing DamageSystem, collectibles, rewards, pause ownership and R3 route flow remain in use

## Branch

`feat/phase-06-25d-migration`

Base: `feat/phase-05-5-25d-demo`.

## Main scene generation

`ArknightsACT > Build Prototype Scene` now generates the **2.5D main roguelite scene** at:

`Assets/_Game/Scenes/PrototypeRun.unity`

The builder reuses the approved textured 3D world shell from the 2.5D demo, removes the demo-only player/enemies, and composes the production player, enemy templates, RoomLoop and R3 systems into that world.

## Player

### Movement

`PlayerMotor25D` is the production XZ motor.

- WASD / left stick: camera-relative XZ movement
- Space / gamepad south: jump
- Y is jump height only
- movement is locked while appropriate attack/skill states own the player

`IPlayerLocomotion` is now the dimension-independent contract used by combat/skills. The legacy `PlayerMotor2D` also implements it so the old side-view implementation remains a useful fallback/reference.

### Basic attack

`PlayerAttackController` keeps the existing 3-hit combo timing, events, hit-stop and collectible hooks.

- legacy scene: `Physics2D.OverlapBoxAll`
- migrated 2.5D scene: rotated `Physics.OverlapBox` in the player's latest planar movement direction

`AttackHit` is unchanged, so collectibles such as SP-on-basic-hit continue to work.

### Dash

`PlayerDashController` now supports both backends:

- Rigidbody2D velocity dash in the legacy path
- CharacterController planar dash in the 2.5D path

Dash remains an invulnerability source and retains attack dash-cancel rules.

### Skills

Ch'en Skill 1 and Skill 2 retain their gameplay definitions and cooldown APIs.

- Skill 1 uses a directional 3D overlap box in 2.5D.
- Skill 2 uses a 3D overlap sphere and planar nearest-target selection in 2.5D.
- Skill 2 remains invulnerable throughout `IsCasting`.

## Enemy AI

Production 2.5D enemies use `PrototypeEnemyCombatBrain25D`.

### Acquisition

An enemy initially acquires the player only when all conditions are true:

1. player is within the enemy's view distance
2. player is inside the enemy's forward cone
3. a 3D raycast has unobstructed line of sight

Buildings, crates, medians and other 3D colliders therefore block sight.

### Alert state

- idle cone: yellow
- alerted cone: red
- after losing line of sight longer than the configured delay, the enemy drops aggro

### Archetypes

The migrated templates preserve the R3 ordering contract:

0. Soldier — melee
1. Hound — fast melee
2. Crossbowman — ranged
3. Heavy Defender — Boss template

The Heavy Defender remains excluded from the ordinary three-template combat pool and is selected by the existing Boss tuning (`ForcedTemplateIndex = 3`).

## RoomLoop / R3

`PrototypeRoomLoopController` now supports either:

- legacy `Vector2[]` spawn points
- production `Vector3[]` XZ spawn points

The public R3 continuation API is unchanged, including `ContinueToNextRoom(CombatRoomTuning)`.

Therefore these systems remain shared:

- Normal Combat
- Emergency Combat
- Encounter
- Safe House
- Rogue Trader
- Boss
- collectible rewards
- Originium Ingots
- route depth
- action-settle-before-reward behavior
- centralized GameplayPauseService

## Camera

The production camera uses a two-level hierarchy:

```text
CameraRig25D        <- follows player at fixed 2.5D offset
└── Main Camera     <- local camera shake only
```

This separation is required because the existing camera shake service modifies local position while the rig owns world-space follow.

## Presentation

- Spine characters live under `PresentationBillboard` roots.
- Billboard roots rotate toward the camera but do not flip scale.
- Character left/right facing remains owned by `SpineCharacterPresentation2D`, preventing double flipping.
- world health bars billboard toward the camera in 2.5D
- damage numbers billboard toward the camera
- enemy vision cones are drawn on the 3D ground plane

## Local rebuild required

After pulling this branch:

1. wait for Unity compilation
2. if there is a red compiler error, fix only the first red error first
3. run `ArknightsACT > Build Prototype Scene`
4. open `Assets/_Game/Scenes/PrototypeRun.unity`
5. press Play

The generated `PrototypeRun` is now the migrated 2.5D main scene, not the old horizontal side-view scene.

## Validation checklist

### Player

- green HP bar is visible immediately
- WASD moves freely on XZ relative to the camera
- Space jumps and lands correctly
- J / LMB performs the 3-hit basic combo
- basic attacks hit only in the current planar facing direction
- K / Shift dashes in the current planar facing direction
- dash rejects incoming enemy damage during the dash
- L casts Skill 1 in front of Ch'en
- I / RMB casts Skill 2 and Ch'en remains invulnerable throughout the cast

### Enemy perception

- approaching an enemy from behind does not immediately aggro it
- entering the yellow cone with clear line of sight turns the cone red and starts pursuit
- a building/crate between player and enemy blocks initial detection
- after line of sight is lost long enough, the enemy drops alert state
- Hound moves faster than standard melee
- Crossbowman attempts to maintain range

### Combat / feedback

- enemy and player HP bars face the camera
- damage numbers face the camera
- hit-stop does not leave gameplay paused
- camera follows during movement and hit shake does not break following
- killed enemies stop counting toward the room

### R3 loop

- killing the last enemy waits for Ch'en's current attack / dash / skill to settle
- combat reward opens
- selecting a reward opens route selection
- selecting Normal/Emergency starts the next XZ combat room
- Encounter, Safe House and Trader still function
- Emergency tuning still increases enemy count/HP and enables ranged enemies early
- after 4 non-Boss combat clears the route converges to Boss
- Boss spawns the Heavy Defender template
- Boss reward and subsequent route generation continue normally

## Known migration limitations

These are intentional Phase 06 follow-ups rather than blockers for the architecture migration:

- `DamageContext.Knockback` is still `Vector2`; migrated 3D attacks currently do not apply XZ knockback.
- Ch'en's 2.5D presentation driver currently uses the generic attack/skill presentation API rather than the old exact authored clip segmentation for every Ch'en action.
- enemy 2.5D attacks currently apply combat damage without a dedicated authored attack-animation windup/recovery event.
- combat nodes still reuse one 3D arena; route nodes change rules/composition rather than loading distinct maps.
- `Build Prototype Scene` currently reuses the demo world builder as a scene-shell generator; a later cleanup can extract a shared 3D world factory.

## Validation status

The migration has been source-reviewed and wired through the repository, but Unity Editor / PlayMode has not been run by the assistant. Local Unity validation is required before this branch should be merged.
