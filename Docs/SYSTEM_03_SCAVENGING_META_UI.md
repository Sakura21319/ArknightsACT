# SYSTEM 03 — Scavenging / Roguelite / Meta / UI

Updated: 2026-09-26

This file replaces the old scavenging, shell, extraction and HUD handoffs.

## 1. Economy boundary

Run-only currency:

- Originium Ingots / 源石锭

Persistent currency:

- LMD / 龙门币

Do not display Source Ingots as a permanent Home/Warehouse currency.

## 2. Scavenging inventory

Core:

- `ScavengingInventory25D`
- `ScavengingWindowUI`
- `SearchableContainer25D`
- `WorldSalvagePickup25D`

Backpack:

- real 2D grid;
- initial 4×5;
- can be expanded in-run;
- supports pickup/drop/rearrange;
- current run loot is unsecured until extraction.

Search UI:

- current item search indicator is attached to the item;
- searched/empty/unsearched states are visually distinct;
- details show description/value;
- outside gameplay input is blocked while modal search/inventory UI owns input.

## 3. Collectibles and commodities

Two gameplay meanings:

- collectibles/relics: combat/run effects;
- commodities/collection goods: mainly extraction value / selling.

Official maintained relic data source:

`Docs/RogueRelics/RogueRelicDatabase_IS1_CurrentPool.xlsx`

Do not treat historical generated XLSX/CSV variants as manual source of truth.

Runtime catalog import is handled by the RogueRelic editor bootstrap/import pipeline.

## 4. Extraction

Formal rule:

**Stage transition is not extraction.**

Successful extraction:

1. snapshot unsecured backpack;
2. deposit normal extracted goods into persistent warehouse;
3. settle/secure haul;
4. clear run-only relic effects;
5. open settlement.

Death:

- unsecured normal loot is lost;
- run-only effects are cleared;
- no warehouse deposit of unsecured loot.

Final boss completion does not by itself make loot safe.

## 5. Meta state / warehouse

Core:

- `RogueliteMetaState`
- `RogueliteGameFlowController`
- `RogueliteShellUI`

Persistent meta currently owns:

- LMD;
- warehouse item counts;
- operator progression state;
- commander-level placeholder where still used by UI.

Warehouse/trade is one combined module.

Home does not expose the old large bottom operator/module/store/intelligence strip.

## 6. Operator meta progression

Operator progression uses:

- E2 Lv1 -> Lv30 -> Lv60 -> Lv90;
- Skill7 -> M1 -> M2 -> M3.

Upgrade economy currently uses:

- LMD;
- required collection/material items.

Interfaces should stay extensible; do not add Trust/Potential/Module as hidden assumptions.

## 7. Roguelite upgrade layers

Run systems may grant:

- generic level upgrades;
- character skill upgrades;
- collectible/relic effects;
- temporary combat buffs.

They should enter the appropriate modifier layer rather than editing Definition/base values.

## 8. Combat HUD

Formal HUD owns:

- player portrait;
- HP;
- two skill rows;
- SP/skill state;
- shortcuts;
- world Ready indicator;
- brief `+X SP` feedback;
- backpack usage;
- current Source Ingots.

Rules retained:

- no large READY text inside skill rows;
- Ready is primarily world-space above player;
- SP bars use consistent width;
- Source Ingots appear only during a run;
- legacy IMGUI/duplicate HUDs remain disabled.

## 9. Shell UI

Runtime flow:

```text
Home
 -> Running
 -> ExtractionDecision
 -> Settlement
 -> Home

Home
 -> WarehouseTrade
 -> Home
```

Non-running shell states block gameplay input and hide gameplay HUD.

Current Home direction:

- minimal layout;
- clean Chernobog static background;
- Start Operation;
- Warehouse / Trade;
- Squad/Missions may remain disabled placeholders.

## 10. New-run reset

A new operation must reset run-owned state including:

- run progression;
- Source Ingots;
- unsecured inventory;
- active relics;
- run upgrades;
- temporary combat buffs;
- character run-only state;
- player HP as required by flow.

Persistent warehouse/LMD/meta progression remain.

## 11. Data maintenance

Keep:

- `Docs/RogueRelics/README.md`
- `RogueRelicDatabase_IS1_CurrentPool.xlsx`

Historical spreadsheets such as “before patch”, “all versions” and intermediate preprocess exports are not the preferred manual source. They may be retained only if still needed for tooling/debug comparison.

## 12. In-run level-up rewards

Current formal loop:

```text
enemy kill
 -> EnemyExperienceReward
 -> RogueliteRunState EXP
 -> level increased
 -> LevelUpRewardController
 -> three-choice reward
 -> LevelUpgradeInventory (RunPermanent)
```

Enemy rank contributes to EXP value: Elite and Boss kills are worth more than Normal enemies.

The reward system now has three build-defining layers in addition to ordinary stat foundations.

### A. Character skill mutations

Character-specific upgrades still go through
`CharacterSkillUpgradeDefinition -> CharacterSkillUpgradeInventory -> ICharacterSkillUpgradeApplier`,
but definitions can now be marked `Mutation` instead of ordinary tuning.

Current Chen mutation samples:

- **拔刀·回响**: after the normal 赤霄·拔刀 hit, repeat the slash after 0.18 s at 65% damage;
- **绝影·无尽追猎**: each kill during 绝影 grants one extra strike, capped at four bonus strikes
  for the cast.

These are one-copy behavior changes, not stackable percentage cards. Other operators should add
their own mutations through the same character-local applier interface rather than putting
character checks into the generic reward controller.

### B. Build tags and synergy rewards

`RunBuildTag` currently provides a small shared vocabulary: Burn, Chain, Overload, Skill, Hunt,
Risk. Existing Burn/Chain definitions infer their tags automatically, so old authored assets remain
compatible.

A reward may require tags before it is eligible. Current sample:

- **过载反应** requires Burn + Chain;
- when chain lightning resolves, it creates a 2.8 m Arts explosion for 40% of the triggering base
  damage;
- secondary proc generation prevents the reaction from recursively triggering itself.

This establishes the intended pattern: basic effects create a build identity, then rare synergy
rewards appear only after the prerequisites actually exist.

### C. Dangerous protocols

Dangerous protocols are low-weight, one-copy choices with a visible cost:

- **源石超频协议**: +25% outgoing damage, +35% SP recovery; every successful active-skill cast
  consumes 4% Max HP, clamped so the protocol itself cannot directly kill the player;
- **血债协议**: while below 35% HP, +45% outgoing damage and +45% attack speed; acquiring it also
  makes incoming damage 20% higher.

The ordinary foundation pool is still available (damage / defense / mobility / burn / chain), and
the three-choice builder continues to prefer category diversity before falling back to duplicates.
Cards are labelled as 战术强化 / 联动 / 危险协议, while character reward screens distinguish
技能特化 / 技能异变.

The current scene still owns older authored assets. New core/synergy/protocol definitions are supplied
as runtime fallbacks so existing scenes work immediately, while `PrototypeRogueliteFactory` also
authors the same definitions for future scene regeneration. Asset-authored definitions win by id.

When every finite upgrade is capped, a level-up is never silently consumed: it currently converts
to +2 Source Ingots.

Next content work should expand A/B/C across Schwarz, Skadi, Wisadel and FrostNova Winter without
adding a fourth reward architecture.
