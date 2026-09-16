# Handoff — Phase 07 2.5D Exploration Roguelite

> This is the current source of truth for the active prototype after the 2.5D migration and Phase 07 exploration work.
>
> The older `Docs/HANDOFF_CHEN_2D_ACT.md` describes the earlier horizontal-2D phase and is now historical reference only. Do not use it as the current gameplay/map direction.

## 1. Current product direction

The project is now an **Arknights-inspired 2.5D ACT Roguelite** built in Unity 6000.0.23f.

Playable operator: **Ch'en / 陈**.

Core presentation direction:

- true 3D world / collision,
- fixed low-oblique orthographic camera,
- 2D Spine operators/enemies presented as billboards,
- free XZ movement with Y-axis jump/verticality,
- authored PRTS combat animation remains presentation-only; gameplay owns damage and rules.

Main scene generator:

```text
ArknightsACT > Build Prototype Scene
```

Generated scene:

```text
Assets/_Game/Scenes/PrototypeRun.unity
```

## 2. Current run structure

The legacy isolated room loop is no longer the active PrototypeRun flow.

Current exploration run:

```text
Stage 1: 2 x 2 = 4 blocks -> Boss -> next-stage entrance
Stage 2: 3 x 2 = 6 blocks -> Boss -> next-stage entrance
Stage 3: 3 x 3 = 9 blocks -> final Boss -> run complete
```

Rules:

- Start is fixed bottom-left.
- Boss / exit is fixed top-right.
- Intermediate blocks are freely explorable.
- Player does **not** need to full-clear every block before reaching Boss.
- A block spawns/activates its contents on first entry.
- Activated enemies persist after leaving the block.
- Enemies and activated monster chests can pursue across block boundaries.

Current chunk prototype dimensions are about **14 x 11 world units**. User feedback: the map currently feels a little small, but this is not the immediate priority. Increase dimensions only after block-content density is validated.

## 3. Stage architecture

### Logical map

```text
RogueliteStageMapController
```

Owns:

- Stage 1/2/3 dimensions,
- Start / Combat / Emergency / Shop / Boss block types,
- randomized Shop placement,
- emergency-combat distribution,
- treasure rolls inside blocks.

### Physical map

```text
RogueliteStageRuntimeController
```

Owns:

- runtime chunk assembly,
- first-entry encounter spawning,
- treasure spawning,
- stage-wide navigation graph,
- Boss reward/exit activation,
- `E` transition to next stage.

### Environment layer

```text
RogueliteStageEnvironmentController
```

Owns:

- mobile-city / industrial backdrop,
- block-local environmental hazards,
- future catastrophe system.

Do not fold catastrophe or terrain rules into `RogueliteStageRuntimeController`; keep environment mechanics independent from route/reward logic.

## 4. Current chunk themes

Prototype themes:

- Open tactical ground,
- Street,
- Cover Lane,
- Two-floor Facility,
- Safe Plaza / Shop,
- Boss Arena.

Each stage tries to contain at most one playable two-floor facility so the map does not become repeated buildings everywhere.

Longer-term direction:

```text
Street-A / Street-B / Street-C
Industrial-A / Industrial-B / Industrial-C
Ruins-A / Ruins-B / Ruins-C
Residential / Elevated road / Originium-polluted blocks
```

The purpose of blocks is not only procedural convenience. It matches the setting concept of **mobile-city city blocks**, and later catastrophes should be able to affect blocks independently.

## 5. Combat / controls

Desktop prototype:

```text
WASD / arrows      Move
Space              Jump
J / Left Mouse     3-hit basic combo
K / Left Shift     Dash
L                  Skill 1 — 赤霄·拔刀
I / Right Mouse    Skill 2 — 赤霄·绝影
E                  Interact / Shop / next-stage entrance
```

Current authored Ch'en mapping:

```text
Basic 1 -> first half of Attack
Basic 2 -> second half of Attack
Basic 3 -> Skill
Skill 1 -> Skill_2 + Skill_End_2
Skill 2 -> Skill_3 + Skill_End_3
```

No air attack, no plunge, no dash attack. Dash is movement / invulnerability / cancel utility only.

Recent combat fixes:

- enemy death immediately hides vision warning, disables collision, keeps the death animation briefly, then destroys the root object,
- 25D basic attack keeps the directional forward hitbox but has a small frontal point-blank safety volume so close diagonal contacts do not whiff.

## 6. Enemy perception / navigation

Enemy detection uses:

- front-facing sector,
- line of sight,
- world cover occlusion,
- height constraints.

Stage runtime creates one shared `PrototypeNavigationGraph25D` spanning all blocks. Facility chunks add ramp / second-floor nodes.

Normal enemies and activated monster chests use this graph for cross-block / multi-floor pursuit.

Monster chest behavior is intentional:

```text
normal-chest disguise
-> first hit
-> transform into Chest Seaborn
-> permanently lock player position
-> never normal-desaggro
-> may pursue across blocks
```

## 7. Progression layers

There are three intentionally separate build layers.

### A. Level / EXP upgrades

Source:

- enemy kills,
- emergency fights give higher EXP efficiency.

Current prototype cap: Lv10.

Current upgrade pool includes:

- all damage,
- physical damage,
- Arts damage,
- burn,
- chain lightning.

Do not turn level-up into another collectible source. Level upgrades are the generic combat-growth layer.

### B. Collectibles

`CollectibleInventory` remains the run-wide generic build layer.

Examples:

- all / physical / Arts / true damage,
- max HP,
- basic-hit cooldown reduction,
- skill heal,
- skill cooldown reduction.

### C. Character skill specialization

`CharacterSkillUpgradeInventory` is separate from collectibles.

Initial Ch'en specializations:

- Skill 1 range +25%,
- Skill 1 damage +20%,
- Skill 1 cooldown -15%,
- Skill 2 +2 strikes,
- Skill 2 final hit +35%,
- Skill 2 targeting radius +20%.

Character-specific effects belong here, not in generic progression code.

## 8. Reward serialization

All full-screen choices are serialized through:

```text
RewardSelectionCoordinator
```

This is important. Level-up, skill-specialization and collectible rewards must not open on top of each other.

Expected collision order is effectively serialized as:

```text
current reward finishes
-> queued level-up / skill reward / collectible reward
-> next UI
```

Do not introduce a new reward screen that bypasses this coordinator.

## 9. Current encounter rewards

Normal combat:

```text
+2 Ingots
20% chance Common+ collectible 2-choice
```

Emergency combat:

```text
more / stronger enemies
EXP x1.5
+4 Ingots
guaranteed Rare+ collectible 2-choice
```

Boss:

```text
+8 Ingots
high EXP tuning
Rare+ collectible 3-choice
```

## 10. Treasure system

PRTS prototype assets currently referenced:

```text
Normal chest   trap_065_normbox
Spike chest    trap_066_rarebox
Monster chest  enemy_2035_sybox
```

Local PRTS art is prototype-only and should remain uncommitted where configured.

### Normal chest

Low-value resource source:

- Ingots,
- heal,
- temporary all-damage buff.

### Spike chest

- does not attack,
- reflects direct damage,
- reflection cannot directly kill player,
- break reward: collectible 2-choice.

### Monster chest

- appears as normal chest before activation,
- first hit transforms it,
- permanently pursues player,
- reward: Ch'en skill specialization 2-choice, then collectible 2-choice.

## 11. Shop

Shop blocks now have a working shop UI.

Interaction:

```text
enter Shop block
-> press E
-> three offers
```

Stock can include:

- healing,
- collectible,
- universal level upgrade,
- Ch'en skill specialization,
- temporary damage buff.

Skill specialization is intentionally expensive so monster chests remain the primary source.

Reroll:

```text
2 -> 4 -> 8 -> 16 Ingots
```

Shop inventory persists within the same stage until rerolled.

## 12. Mobile-city environment mechanics

### Active Originium

Current intended ACT rule after latest user feedback:

Player:

```text
standing on tile
-> periodic true damage
-> +30% outgoing damage
-> while standing, duration refreshes
-> after leaving, buff lasts 10 seconds
```

Enemy:

```text
first exposure
-> gains +30% outgoing damage permanently for that enemy
-> leaving the tile does not remove the buff
```

Attack-speed modification is intentionally not implemented yet; centralize action-speed ownership before adding it.

### Pit / hole

Current prototype rule:

- enemy enters -> defeated,
- player enters -> lose 20% max HP and reset to block safe point,
- shallow trigger allows jumping across.

Latest generation change:

- every stage should guarantee at least one pit,
- ordinary eligible blocks have a higher pit probability,
- emergency blocks have a high pit probability,
- pit has a full four-side warning frame for readability.

Important limitation: the floor is **not physically cut open yet**. It is still a visual dark pit + trigger. Future pass should use segmented floor geometry / actual openings.

### Ballista

Current rule:

- fixed-direction periodic projectile,
- physical damage to player,
- wall / cover should stop the bolt,
- does not damage enemies in the first prototype.

Latest reliability fix:

- projectile movement uses swept SphereCast,
- additional player-segment fallback is used so CharacterController contact does not silently miss.

Latest user report before this fix: projectile was visible but dealt no damage. This latest fix still requires local verification.

## 13. Environment material fix

Latest user report: newly generated environment geometry appeared magenta/purple.

Cause: runtime material creation via `Shader.Find(...) / new Material(...)` was unreliable with the active render pipeline.

Current fix:

- reuse / clone existing verified prototype materials such as Ground / Facility / Cover / Accent / Hazard materials,
- do not invent runtime shaders for these environment objects.

This fix also still requires local Unity verification.

## 14. Mobile-city visual direction

Current procedural backdrop is original simple industrial geometry inspired by early Chernobog/mobile-city mood:

- dark industrial building masses,
- uneven skyline,
- rooftop machinery,
- antennae,
- hazard accents,
- ruined / oppressive urban scale.

Future map content requested by user:

- simple playable low-rise buildings,
- ballista mechanisms,
- active Originium floor,
- holes,
- more existing Arknights stage mechanisms where they fit ACT gameplay,
- background / block mood referencing early main-story city districts.

Do not copy full Arknights stage geometry 1:1. Use original modular layouts and use PRTS/screenshots as prototype/reference material. Review asset/content rights before public/commercial distribution.

## 15. Catastrophe direction

The block architecture intentionally supports future catastrophes.

Reserved prototype enum:

- None,
- Dust Front,
- Originium Fall,
- Structural Collapse.

Catastrophes are currently disabled by default.

Recommended first playable catastrophe: **Dust Front**.

Desired design principle: catastrophe should alter traversal / visibility / tactical decisions, not just apply a global DOT.

Example Dust Front:

```text
warning period
-> visibility falls
-> enemy/player sight rules change
-> ranged pressure changes
-> close-range encounters become less predictable
```

## 16. PRTS local workflow

When missing prototype presentations:

```text
ArknightsACT > Assets > PRTS > Download Full Prototype Pack
ArknightsACT > Assets > PRTS > 2.5 Apply High Quality Texture Settings
ArknightsACT > Assets > PRTS > 3. Build Presentation Prefabs
ArknightsACT > Assets > PRTS > 4. Validate Presentation Setup
ArknightsACT > Build Prototype Scene
```

Treasure-only assets also have a focused download entry when available.

## 17. Architecture boundaries

Keep these rules:

- generic gameplay remains character-agnostic,
- Ch'en-only skill semantics stay under `Gameplay/Characters/Chen`,
- Gameplay owns damage; Presentation observes gameplay events/state,
- PRTS URLs / IDs / local source handling stay Editor-side,
- no giant PlayerController,
- use `DamageSystem` for normal combat/environment damage where appropriate,
- full-screen reward UI goes through `RewardSelectionCoordinator`,
- map logic, physical stage runtime and environmental rules stay separated.

## 18. Current local validation state

The user locally confirmed most of the progression / treasure / exploration flow before the latest environment pass.

Latest environment feedback was:

```text
- Ballista fired but caused no damage.
- Newly added environment geometry was purple.
- Desired Active Originium behavior:
  enemy buff permanent after stepping on it;
  player buff lasts 10s after leaving.
- Pit was not visible/found.
```

Source fixes for all four were committed afterward, but **the assistant cannot run Unity Editor / PlayMode**, so these latest fixes are not yet locally confirmed.

Before adding more features, validate:

```text
[ ] No new environment geometry renders magenta/purple
[ ] Ballista visibly damages player on a clean firing lane
[ ] Cover/walls still block ballista
[ ] Player Originium buff persists ~10s after leaving
[ ] Enemy Originium buff remains after leaving
[ ] Stage contains at least one clearly visible pit
[ ] Player can jump across pit
[ ] Player pit recovery loses 20% max HP and resets safely
[ ] Enemy entering pit dies
```

If Unity compilation fails, fix the **first red compiler error** before changing design.

## 19. Recommended next implementation order

After the environment fixes above are validated:

1. Convert chunk floors to segmented floor pieces so pits are real openings.
2. Add 3-5 internal layout variants per chunk theme.
3. Add simple playable low-rise building shells without blocking cardinal connections.
4. Increase chunk size only together with additional points of interest; avoid empty running space.
5. Add proper presentation for selected environment devices / mechanisms.
6. Implement the first real catastrophe: Dust Front.
7. Revisit EXP, shop prices, chest density and emergency-combat frequency after a full 3-stage run feels stable.

## 20. Branch / merge state at handoff creation

Handoff authored from:

```text
feat/phase-07-progression-rewards
```

At the time of this document creation, `main` was an ancestor of the feature branch and the feature branch was ahead with no main-only commits. The user explicitly requested merging this current work into `main` after generating this handoff.
