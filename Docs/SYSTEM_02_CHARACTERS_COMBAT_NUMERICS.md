# SYSTEM 02 — Characters / Combat / Numeric Pipeline

Updated: 2026-09-26

This document replaces the P1-P10 numeric/combat handoff chain and individual character numeric handoffs.

## 1. Maintained characters

- Chen
- Schwarz
- Skadi
- Wisadel
- FrostNova Winter / 冬痕

Character-specific runtime belongs under:

`Assets/_Game/Scripts/Gameplay/Characters`

Generic systems must not branch on character name.

## 2. Official numeric source policy

For normal playable operators:

- initial formal state = E2 Lv1;
- supported meta checkpoints = E2 Lv1 / Lv30 / Lv60 / Lv90;
- skill tiers = Skill7 / M1 / M2 / M3;
- official stats/mastery should come through imported PRTS/local game data;
- do not reintroduce handwritten official-number fallbacks in Runtime.

Not planned:

- Trust;
- Potential;
- Module.

Project-authored ACT presentation values may remain local:

- animation startup/lock timing;
- FX offsets/scales;
- projectile visual time;
- hitboxes;
- world-space calibration.

These are not official operator numeric data.

## 3. Base progression

Core types:

- `OperatorBaseStats`
- `OperatorE2Progression`
- `OperatorProgressionController`
- `OperatorRuntimeStats`

Resolution flow:

```text
Definition E2 checkpoint
  -> OperatorProgressionController
  -> OperatorRuntimeStats base values
  -> layered combat modifiers
  -> final resolved runtime values
```

Base fields include:

- MaxHP
- ATK
- DEF
- RES
- attack interval
- basic attack range scalar
- skill range scalar

## 4. Skill mastery

Core types:

- `OperatorSkillMasterySet`
- `OperatorSkillMasteryProfile`
- `OperatorSkillMasterySnapshot`
- `IOperatorSkillMasteryTarget`

Each gameplay slot must have exact:

- Skill7
- M1
- M2
- M3

Adapters should replace tier data completely. Missing keys must not silently reuse values from the previous tier.

## 5. Modifier layer order

Formal layer order:

1. MetaProgression
2. RunPermanent
3. CollectibleEquipment
4. Temporary

Relevant types:

- `CombatStatModifierLayer`
- `CombatStatModifierBucket`
- `ILayeredCombatStatModifier`

Official skill buffs such as Skadi S3 belong to Temporary.

Run upgrades belong to RunPermanent.

Relics/collectibles belong to CollectibleEquipment.

## 6. Damage / defense / status

Formal combat supports:

- Physical
- Arts
- True
- Physical DEF
- Arts RES
- penetration / reduction
- status effects
- tags/modifiers

Do not restore historical use of generic incoming-damage multipliers as a substitute for DEF/RES.

Schwarz armor break uses the formal defense/status pipeline.

## 7. Range

Core:

- `OperatorRangePattern`
- `OperatorRangeCatalog`
- `OperatorRangeUtility`

Currently catalogued range ids include the patterns needed by the maintained roster.

Skill range is composed from:

- official base range;
- official skill range override;
- runtime global SkillRange modifier;
- per-slot Skill1/Skill2 range modifier.

Do not replace official range shape with arbitrary radius-only logic where the character already uses a range pattern.

## 8. Character summary

### Chen

Gameplay slots map to original S2/S3.

Official data consumed:

- base E2 stats;
- SP / initial SP;
- rangeId;
- `atk_scale`;
- S2 `max_target`;
- S3 `times`, `stun`.

Damage uses Runtime ATK × official scale.

### Schwarz

Gameplay slots map to original S2/S3.

Consumes:

- base E2 stats;
- SP / duration;
- `atk`;
- S3 `base_attack_time`;
- `talent@prob`;
- official range where applicable.

Armor-break base talent is imported from the E2 rank-0 talent candidate. Potential-enhanced candidates are intentionally excluded.

### Skadi

S2 / 跃浪击:

- deployment passive;
- no manual SP cast;
- duration and ATK bonus from official mastery;
- one activation per deployment/new run.

S3 / 涌潮悲歌:

- manual natural-recovery duration skill;
- official SP / initial SP / duration;
- ATK / DEF / MaxHP Temporary modifiers;
- startup may lock actions, active buff window does not permanently block basic attacks.

### Wisadel

S2:

- continuous auto-target mode;
- official `atk`, `base_attack_time`, `attack@atk_scale_ol`;
- burst count is derived from official skill semantics;
- range upgrades affect auto-target search.

S3:

- ammo state;
- official `atk`, `base_attack_time`, `attack@atk_scale_3`, `attack@trigger_time`.

Currently imported-but-not-consumed official fields may include:

- `max_cnt`
- `attack@prob`
- `sp`

P9 should continue surfacing these as warnings until intentionally implemented or waived.

### FrostNova WinterTrace

FrostNova is not represented as a normal playable-operator `char_*` source.

Dedicated source:

`Assets/_Game/Resources/Config/FrostNovaWinterTraceCombatProfile.json`

Source id:

`enemy_1510_frstar2#wintertrace`

Project progression maps enemy source endpoints into project Lv1/30/60/90 checkpoints. This is a project adaptation, not official E2.

Only Winter is maintained.

Skill mastery tiers remain flat when the source enemy has no official mastery progression; do not invent M1-M3 growth.

## 9. Numeric validation

### P9

`ArknightsACT/角色调试/数值校验器`

Checks:

- exact Lv1/30/60/90;
- exact Skill7/M1/M2/M3;
- required Blackboard;
- unknown/unconsumed fields;
- source IDs;
- SP consistency;
- likely official-number hardcodes.

### P10

`ArknightsACT/角色调试/数值自动验收`

Creates hidden temporary runtime harnesses and compares:

- official expected values;
- real RuntimeStats / Skill Adapter output;
- delta.

Target is `Fail 0`.

## 10. Current rule for future character work

When adding or modifying a character:

1. import official progression/mastery into Definition;
2. create character-local skill adapter;
3. use RuntimeStats instead of fixed damage/stat copies;
4. use modifier layers for buffs/upgrades;
5. add P9 required/known field rules;
6. add P10 runtime acceptance for character-specific final values.

## 11. Enemy runtime baseline

Enemy combat currently separates two orthogonal concepts:

- `PrototypeEnemyArchetype`: how the enemy fights (melee / fast melee / ranged);
- `EnemyRank`: how threatening/rewarding it is (Normal / Elite / Boss).

Do not create a separate AI implementation for every rank.

Current exploration behavior:

- idle Normal/Elite enemies patrol a local guard area;
- initial aggro requires vision cone + clear LOS;
- being damaged by the player immediately alerts the enemy;
- alerted enemies chase/remember the last seen position for a limited time;
- Boss rank uses wider/longer awareness and does not use ordinary local patrol.

This intentionally avoids global auto-hunt. Continuous-city exploration must preserve scouting,
route choice, first strike and disengagement.

Enemy combat numbers now use the PRTS/source-game enemy values for the concrete enemy model.
Do not derive enemy HP / ATK / DEF / RES from `PrototypeEnemyArchetype` and do not multiply those
numbers merely because an encounter is Emergency/Core or because an enemy is tagged Elite/Boss.

Current prototype source mapping:

- Soldier -> `enemy_1002_nsabr`;
- Hound -> `enemy_1000_gopro`;
- Crossbowman -> `enemy_1003_ncbow`;
- Heavy Defender -> `enemy_1006_shield`.

`EnemyOfficialStats25D` owns the current PRTS combat snapshot and applies:

- MaxHP;
- ATK;
- Physical DEF;
- Arts RES;
- movement speed;
- attack interval;
- attack radius where the source enemy uses ranged attacks.

Current numeric rule separates three layers that must not be conflated:

1. **Enemy source level**: the concrete PRTS enemy snapshot (`enemyId + enemyLevel`). The current
   custom city encounters use Level 0 unless a future exact stage mapping explicitly selects another
   enemy level.
2. **Integrated Strategies floor/difficulty scaling**: the current operation applies the
   `水月与深蓝之树` rule in `EnemyOfficialStats25D`. For the current Risk 0-5 UI, entering floor N
   scales HP/ATK by `(1 + risk%)^N`; Risk 3+ adds +10 RES to all enemies; Risk 5 adds +15% ATK/DEF
   to Boss/leader rank.
3. **Project action pacing**: official relative move speeds are preserved, then multiplied by an
   explicit `1.20x` ACT movement pacing factor requested for this real-time prototype. This is a
   project feel adjustment, not an official Arknights stat modifier.

Do not use Stage index as a shortcut for enemy Level. Rogue floor scaling and an enemy definition's
own Level are separate source-game concepts.

Emergency-combat stat bonuses are stage-specific in the source game rather than one universal
multiplier. Until custom city encounters are mapped to specific official IS stages, Emergency
difficulty should come from composition / real elite enemy IDs / reward pressure, not a fabricated
global HP/ATK multiplier.

`EnemyRank` remains orthogonal to official combat values:

- Normal / Elite / Boss may change awareness, pursuit behavior, presentation and EXP/reward value;
- Rank itself must not multiply source HP / ATK / DEF / RES / attack interval;
- an Elite slot should spawn an actual elite enemy definition. The current authored elite fallback
  is Heavy Defender rather than an ordinary Soldier/Hound with fake multipliers.

The current Boss slot still uses Heavy Defender as a placeholder presentation/source enemy. Its
base numbers are therefore Heavy Defender official values plus the applicable IS difficulty rules,
not a fabricated generic boss stat line. Replace that slot with a real boss enemy ID when a boss
roster is selected.

PRTS enemy handbook values are the base source, while individual Arknights stages may override enemy
instances. A future enemy-data importer should therefore support `enemyId + enemyLevel` first and
leave room for exact stage-instance overrides instead of baking numbers into scene objects.
