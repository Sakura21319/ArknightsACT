# MAIN SHELL / EXTRACTION HANDOFF — 2026-09-21

## UI direction

Selected: **Scheme B simplified tactical cards**.

Confirmed presentation rules:

- Home is intentionally minimal.
- Commander level is only a reserved external-progression display and sits at the **bottom-left** of Home.
- Squad and Missions are visible placeholders only; no functionality yet.
- Warehouse and Trading are one combined module.
- Avoid large flavor quotes / redundant descriptive copy.

## Implemented runtime flow

New core files:

- `Assets/_Game/Scripts/Gameplay/Roguelite/Routing/RogueliteGameFlowController.cs`
- `Assets/_Game/Scripts/Gameplay/Roguelite/Routing/RogueliteMetaState.cs`
- `Assets/_Game/Scripts/Gameplay/Roguelite/Routing/RogueliteShellUI.cs`
- `Assets/_Game/Editor/PrtsCurrencyIconBootstrap.cs`

Runtime states:

`Home -> Running -> ExtractionDecision -> Settlement -> Home`

Also:

`Home -> WarehouseTrade -> Home`

Non-running shell states pause gameplay and block gameplay input. Gameplay HUDs are hidden outside an active operation.

## True extraction

`RogueliteStageRuntimeController` now builds a physical `ExtractionPoint` in every stage near the starting district.

Near the extraction point:

- E opens the extraction decision screen.
- Player can continue exploring.
- Player can confirm extraction.

Important lifecycle change:

**Advancing Stage 1 -> 2 or Stage 2 -> 3 no longer calls `SecureHaul()`.**

All backpack loot remains unsecured across stage boundaries.

The final boss also no longer auto-calls `FinishRun()`; the player still has to return to an extraction point.

## Extraction settlement

On successful extraction:

1. Current unsecured backpack is snapshotted for the settlement UI.
2. Items are deposited into the persistent system warehouse.
3. `ScavengingInventory25D.SecureHaul()` completes the run settlement.
4. Active run relic effects are cleared.
5. Settlement screen opens.

Extracted goods are **not automatically sold**.

Settlement currently shows:

- recovered items,
- recovered item count,
- total estimated collection value,
- stage reached,
- explored blocks,
- combat record,
- run duration.

Death does not write unsecured loot to the warehouse and opens a failed settlement screen.

## System warehouse / trade

`RogueliteMetaState` stores:

- LMD balance,
- commander-level placeholder,
- warehouse item ID -> count.

Persistence:

`PlayerPrefs / ArknightsACT.MetaState.v1`

Development starting LMD:

`120000`

Current price rule:

- Sell: `CollectionValue` (minimum 1)
- Buy: sell price × 1.25, rounded up to the next 10

Warehouse / Trade supports:

- All / Commodity / Relic filters,
- Buy tab,
- Sell tab,
- owned quantity,
- item detail and icon,
- one-item buy,
- one-item sell.

Not yet implemented:

- taking warehouse items into a run,
- batch transactions,
- warehouse capacity,
- dynamic market pricing,
- temporary Rhodes outpost partial settlement,
- taking the current runtime-built UGUI hierarchy into authored prefabs if/when the layout stabilizes.

## UGUI shell presentation (2026-09-21)

The previous plain IMGUI presentation has been replaced by a runtime-built **UGUI shell**. `RogueliteGameFlowController.OnGUI()` now acts only as fallback; once `RogueliteShellUI` is ready, the old shell is not drawn.

Current visual rules:

- Home uses the approved generated clean Chernobog-style static 3D background from `Resources/UI/Shell/home_chernobog`; the live gameplay camera must never show through the home screen.
- The **left side only keeps Commander Level**, at bottom-left.
- Home right side only contains:
  - Start Operation
  - Warehouse / Trade
  - Squad (reserved / disabled)
  - Missions (reserved / disabled)
- The old lower horizontal strip for Operator / Module / Store / Intelligence is intentionally absent.
- Warehouse / Trade is fully opaque **gray-white**, so the 3D scene does not show behind it.
- Warehouse uses a category rail + item grid + item detail / transaction panel.
- Extraction and Settlement retain the darker tactical style and reuse real runtime item icons.
- Canvas scaler reference resolution is 1920x1080 with MatchWidthOrHeight = 0.5.
- A runtime EventSystem is created only when one is missing.

Currency presentation:

- `PrtsCurrencyIconBootstrap` imports the actual PRTS file `图标 龙门币.png` from the direct `media.prts.wiki` asset URL into `Assets/_Game/Resources/UI/Currency/lmd.png`.
- Download is asynchronous/non-blocking and only runs when the local sprite is absent/invalid (or manually refreshed from the UI menu).
- Runtime never accesses PRTS; it reads `Resources/UI/Currency/lmd` locally.
- Home / Warehouse / post-run Settlement do **not** show Originium Ingots; ingots are run-only currency and remain visible only in in-run surfaces such as Extraction Decision.
- If the LMD download/import fails, the UI keeps a lightweight fallback marker rather than breaking the shell.

Do **not** re-expand the shell by adding announcement/mail/activity sidebars or the Operator/Module/Store/Intelligence bottom strip unless the design direction is explicitly changed.

## New-run reset boundary

Starting a new operation resets:

- run-state stage / ingots / route stats / current level & EXP,
- scavenging backpack to 4x5 and backpack upgrade level to 0,
- unsecured loot,
- active relic effects,
- level-up upgrade inventory,
- character skill-specialization inventory,
- Chen runtime skill modifiers,
- player HP.

This prevents run-to-run stat leakage.

## Compatibility

Existing `PrototypeRun.unity` does not require an immediate destructive rebuild.

`RogueliteStageRuntimeController.Start()` runtime-bootstraps missing:

- `RogueliteMetaState`
- `RogueliteGameFlowController`

The editor factory also adds both components when the prototype scene is rebuilt.

## Validation

FolderBridge source smoke build/test passes with zero reported issues.

This validation does **not** replace Unity Editor script compilation / Play Mode verification.
