# ArknightsACT HUD / Combat Handoff

Date: 2026-09-21  
Project: `D:\WorkSpace\ArknightsACT`  
Current focus: Formal combat HUD completed through HUD 1–10; next major work is P3 Defense / Armor formalization.

---

# 1. Current status

The formal combat HUD has replaced the old temporary skill HUD path.

Completed:

- formal UGUI combat HUD;
- player HP display;
- Chen portrait slot;
- Chen S2 / S3 original skill icons;
- dual SP bars;
- integer-only HP / SP display;
- hotkey labels;
- skill-ready world marker;
- collectible SP-gain feedback;
- backpack usage status;
- current Source Ingot count;
- legacy HUD cleanup;
- player world health-bar position tuning.

The formal HUD is intentionally compact and semi-transparent.

---

# 2. Main runtime files

## Formal HUD

`Assets/_Game/Scripts/Gameplay/Abilities/GameplayHUDController.cs`

Current SHA-256:

`6d744c2254eb42d5c6f68efdf13d2a3d53f416a3adfadb02bd5d71d6e3ed4b58`

Responsibilities:

- builds the Screen Space Overlay combat HUD;
- HP;
- Chen portrait;
- two skill rows;
- SP bars;
- integer HP / SP values;
- hotkeys;
- collectible `+X SP` feedback;
- backpack cell usage;
- current run Source Ingots;
- drives the world Ready indicator state.

Current visual rules:

- HP row is the longest top row;
- Skill 1 and Skill 2 outer panels are now both exactly `386` wide;
- both SP bars use the same fixed geometry:
  - width: `196`
  - height: `7`
  - same X position;
  - same value position;
- backpack / Source Ingot row remains shorter at `346` wide;
- SP not full: green;
- SP truly full: yellow;
- no `READY` text appears inside skill rows.

Important:

SP values are deliberately integer-only. When a skill is not actually ready, display uses floor-style behavior so e.g. `19.6 / 20` does not falsely display `20 / 20`.

---

# 3. Chen HUD assets

Local unpacked source root:

`D:\Ark\_Unpacked\chen`

The Ark folder is available as its own FolderBridge workspace.

## Chen portrait

Exact source:

`D:\Ark\_Unpacked\chen\ui_assets\avatars\char_010_chen.png`

Size:

`180 x 180`

Unity runtime target:

`Assets/_Game/Resources/UI/HUD/chen_avatar.png`

Runtime Resources key:

`UI/HUD/chen_avatar`

## Skill icons

Current gameplay Skill 1 is Chen original S2:

`赤霄·拔刀`

Exact source:

`D:\Ark\_Unpacked\chen\icons\skill_icon_skchr_chen_2.png`

Size:

`128 x 128`

Unity target:

`Assets/_Game/Resources/UI/Skills/chen_badao.png`

Resources key:

`UI/Skills/chen_badao`

Current gameplay Skill 2 is Chen original S3:

`赤霄·绝影`

Exact source:

`D:\Ark\_Unpacked\chen\icons\skill_icon_skchr_chen_3.png`

Size:

`128 x 128`

Unity target:

`Assets/_Game/Resources/UI/Skills/chen_jueying.png`

Resources key:

`UI/Skills/chen_jueying`

---

# 4. Local asset importer

File:

`Assets/_Game/Editor/PrtsCombatHudAssetBootstrap.cs`

Current SHA-256:

`89ec0f5e2e4c15b97e600250a207549922b4464291a0b06cccb45114a58f8b25`

The importer is local-only.

It does NOT:

- access PRTS over the network;
- download external assets;
- use FX frames as skill icon fallbacks.

It reads exact files from `D:\Ark\_Unpacked\chen` and copies them into the Unity Resources tree.

Unity menu:

`ArknightsACT/UI/Import Chen HUD From D Ark`

It also runs from `DidReloadScripts`.

---

# 5. Skill Ready world indicator

File:

`Assets/_Game/Scripts/Gameplay/Abilities/SkillReadyWorldIndicator.cs`

Current SHA-256:

`bd1b60425586384532469053c33f3b8beb04bd920a5ab0bf322308a237d481bb`

The old custom diamond / `!` / `!!` implementation has been removed.

## Correct Ready sprite

The correct original Ready mark is explicitly:

`D:\Ark\_Unpacked\chen\ui_assets\battle_skill_ready\sprite_skill_ready__-3542339109505237889.png`

Size:

`72 x 72`

Do NOT switch back to the generic:

`sprite_skill_ready.png`

The generic file is a different `212 x 80` asset and was previously selected incorrectly.

Runtime target remains:

`Assets/_Game/Resources/UI/HUD/BattleSkillReady/sprite_skill_ready.png`

The importer copies the exact hash-suffixed original file into that runtime target.

## Pulse layer

Source:

`sprite_skill_bg.png`

Size:

`56 x 56`

Runtime key:

`UI/HUD/BattleSkillReady/sprite_skill_bg`

Extracted animation parameters from:

`D:\Ark\_Unpacked\chen\ui_assets\battle_skill_ready\ready_animation_spec.json`

Manual-skill-ready parameters:

- from size: `47 x 47`;
- target size originally parsed as `100 x 100`;
- duration: `0.5s`;
- alpha: `1 -> 0`;
- infinite loop;
- extracted ease type: `6`.

Current presentation tuning intentionally makes the effect a little larger:

- Ready mark: `58 x 58`;
- pulse target size: `112`;
- world canvas scale: `0.0068`.

Single Ready skill uses yellow pulse.

Two Ready skills use the enhanced orange state.

The static original Ready sprite itself is rendered white so its extracted source colors remain intact; only the pulse layer is tinted.

---

# 6. Collectible SP feedback

Source event:

`CollectibleInventory.SkillPointFeedback`

File:

`Assets/_Game/Scripts/Gameplay/Roguelite/Collectibles/CollectibleInventory.cs`

Current SHA-256:

`bd8c1c71f1fba2df849819130b46c4d8fc3a6d616154681936bc66a5576e57c8`

HUD presentation is deliberately minimal.

Current text is only:

`+X SP`

Do not re-add:

- “藏品回技”;
- “普攻触发”;
- “技能触发”;
- source descriptions.

The feedback appears briefly and fades out.

---

# 7. Backpack HUD integration

File:

`Assets/_Game/Scripts/Gameplay/Roguelite/Treasure/ScavengingInventory25D.cs`

Current SHA-256:

`89979314b85a31340fd4f22cf4ce25c50d11164c4a89cfbca5f39a46528be954`

A `Changed` event is exposed for HUD refreshes.

Formal HUD displays:

`B  背包  used/capacity`

Rules:

- normal: white;
- >= 80%: yellow;
- full: red.

Changes that refresh the HUD include:

- pickup;
- drop;
- backpack upgrade;
- new run;
- death;
- extraction / secure haul.

The old always-visible FIELD RECOVERY backpack strip in `ScavengingWindowUI` has been removed.

Contextual F interaction prompts remain.

---

# 8. Source Ingot HUD integration

Source:

`RogueliteRunState.Ingots`

Refresh:

`RogueliteRunState.Changed`

The formal HUD displays current run Source Ingots on the bottom status row.

Source Ingots remain run-only.

They must not be displayed as a persistent Home / Warehouse currency.

Persistent currency remains LMD.

---

# 9. Legacy HUD cleanup

## PlayerSkillPointHUD

`Assets/_Game/Scripts/Gameplay/Abilities/PlayerSkillPointHUD.cs`

The old `OnGUI()` implementation has been removed.

The component is only retained as an inert compatibility shell for old serialized scenes.

## RogueliteProgressHUD

Its old Run Level / EXP / Source Ingot `OnGUI` rendering has been disabled.

Do not restore duplicate combat HUD rendering there.

## ScavengingWindowUI

The persistent lower-right FIELD RECOVERY / backpack status chip was removed.

Only temporary interaction prompts remain.

---

# 10. Player world health bar

File:

`Assets/_Game/Scripts/Gameplay/Presentation/WorldHealthBar2D.cs`

Current SHA-256:

`ec3c7845554cf4aef2d8a87cbdbf6c71335d0c6c798dd266c401a5413c9218f4`

Latest player-default placement:

- Y: `1.95`;
- X: `-0.14`.

Enemies remain unchanged.

Important caveat:

If the player prefab or another controller calls:

`ConfigureWorldLayout(...)`

then the serialized/runtime override wins over these defaults. If the player world health bar still appears in the old position, search all `ConfigureWorldLayout` call sites before changing the default again.

---

# 11. Current HUD layout summary

Approximate structure:

```text
[ Chen Avatar ] HP                         current / max
               [============= HP =============]

[ S2 icon ] 赤霄·拔刀                L
            SP [======= fixed 196 =======]  10 / 20

[ S3 icon ] 赤霄·绝影            I / RMB
            SP [======= fixed 196 =======]  20 / 30

B 背包 12/20                         源石锭 8
```

Rules:

- skill row 1 outer width = `386`;
- skill row 2 outer width = `386`;
- both SP backgrounds and fills are exactly the same width / height;
- bottom status row is shorter;
- HUD background is semi-transparent;
- no large READY badge inside HUD;
- Ready feedback belongs above the player.

---

# 12. Current gameplay skill mapping

`ChenSkill1.cs`

Gameplay slot:

`1`

Display:

`赤霄·拔刀`

Original Arknights icon mapped from Chen S2.

Current base SP:

- cost: `20`;
- initial: `10`;
- natural recovery: `1/sec`.

`ChenSkill2.cs`

Gameplay slot:

`2`

Display:

`赤霄·绝影`

Original Arknights icon mapped from Chen S3.

Current base SP:

- cost: `30`;
- initial: `20`;
- natural recovery: `1/sec`.

---

# 13. Validation status

Latest FolderBridge validation:

- build smoke: PASS;
- test smoke: PASS;
- issues: `0`.

Important:

FolderBridge reports this workspace as validation-only for build/test. This is not equivalent to a Unity Editor compilation or Play Mode test.

Still required locally:

- allow Unity to reload scripts;
- verify `PrtsCombatHudAssetBootstrap` copied all exact assets;
- enter Play Mode;
- verify Chen portrait is visible;
- verify S2 / S3 icons are correct;
- verify both skill outer panels are visually equal;
- verify both SP background slots are equal width and thickness;
- verify green -> yellow SP state at actual Ready boundary;
- verify Ready sprite is the specified 72x72 source;
- verify pulse scale / height above player;
- verify world health bar does not overlap Ready marker.

---

# 14. Next major task: P3 Defense / Armor formalization

P3 has NOT been implemented yet.

Required work:

1. formal Physical Defense;
2. formal Arts Resistance;
3. Armor Penetration;
4. unified DamageSystem settlement;
5. migrate collectible effects from compatibility mappings;
6. remove temporary `IncomingDamage` defense simulation;
7. prevent formal defense and legacy compatibility effects from double-applying.

Recommended migration order:

```text
DamageContext expansion
    ->
formal defensive attributes
    ->
physical formula
    ->
arts formula
    ->
penetration
    ->
single DamageSystem pipeline
    ->
collectible migration
    ->
remove compatibility mappings
    ->
full project audit
```

Critical rule:

`IncomingDamagePercent` may remain only for effects that genuinely mean “all incoming final damage +/- X%”.

Do NOT continue using it as a stand-in for “DEF +X%”.

Likewise:

- enemy DEF reduction must become a real defense reduction;
- ally DEF increase must become a real Physical Defense modifier;
- PhysicalDamagePercent must only modify actual physical damage;
- ArtsDamagePercent must only modify actual Arts damage.

Avoid a transition state where old compatibility mapping and formal Defense / RES both apply.

---

# 15. Files to inspect first when continuing

HUD:

- `Assets/_Game/Scripts/Gameplay/Abilities/GameplayHUDController.cs`
- `Assets/_Game/Scripts/Gameplay/Abilities/SkillReadyWorldIndicator.cs`
- `Assets/_Game/Editor/PrtsCombatHudAssetBootstrap.cs`
- `Assets/_Game/Scripts/Gameplay/Presentation/WorldHealthBar2D.cs`

Skills:

- `Assets/_Game/Scripts/Gameplay/Abilities/IPlayerSkill.cs`
- `Assets/_Game/Scripts/Gameplay/Abilities/PlayerSkillController.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/Chen/ChenSkill1.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/Chen/ChenSkill2.cs`

Run HUD data:

- `Assets/_Game/Scripts/Gameplay/Roguelite/Collectibles/CollectibleInventory.cs`
- `Assets/_Game/Scripts/Gameplay/Roguelite/Treasure/ScavengingInventory25D.cs`
- `Assets/_Game/Scripts/Gameplay/Roguelite/Routing/RogueliteRunState.cs`

Next combat refactor:

- `Assets/_Game/Scripts/Combat/DamageSystem.cs`
- `Assets/_Game/Scripts/Combat/DamageContext.cs`
- `Assets/_Game/Scripts/Combat/CombatEntity.cs`
- collectible stat / effect application code.

---

# 16. Do not regress these decisions

- Do not restore `PlayerSkillPointHUD.OnGUI`.
- Do not restore duplicate Run HUD rendering.
- Do not put READY text back inside skill rows.
- Do not use FX frames as skill icons.
- Do not network-download HUD assets at runtime or in the editor.
- Do not change Chen S2/S3 icon mapping without changing gameplay skill mapping.
- Do not use generic `sprite_skill_ready.png` in place of the specified hash-suffixed Ready sprite.
- Do not make the two skill outer panels different lengths.
- Do not make the two SP slot backgrounds different lengths or thicknesses.
- Do not display fractional HP / SP values.
- Do not show Source Ingots as persistent meta currency.

---

# 17. Handoff conclusion

P2 formal combat HUD is functionally complete through HUD 1–10 and is now using local unpacked Chen / battle UI assets.

The immediate practical step is one Unity Editor Play Mode visual pass. After that, the next architectural milestone should be P3 Defense / Armor rather than adding more temporary combat modifiers.
